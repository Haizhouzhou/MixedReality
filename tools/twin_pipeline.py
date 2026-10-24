"""Bounded-memory, restartable NavVis -> vertex-coloured glTF tile pipeline.

Run with the mr Python and -I. Raw inputs are opened read-only. Outputs are
transactional; a config fingerprint prevents mixing incompatible runs.
"""
from __future__ import annotations

import argparse
import gc
import hashlib
import importlib.util
import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor
import time
import threading
from datetime import datetime, timezone

import numpy as np

# Explicit sibling import also works under Python -I.
_spec = importlib.util.spec_from_file_location("mesh_overview", Path(__file__).with_name("mesh_overview.py"))
_overview = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_overview)
read_ply_layout, read_alignment = _overview.read_ply_layout, _overview.read_alignment

VERSION = "twin_pipeline 0.1"
BOUNDARY_POLICY = "exact-open-boundary-v1"
REGIONS = {"a": [-20, 70, -5, 32, -1, 8], "b": [-40, 90, -80, 60, -1, 8]}
DEFAULT_MESH = "proc/meshes/mesh.ply"
FACE_DTYPE = np.dtype([("count", "u1"), ("indices", "<u4", (3,))])
ORIGIN = np.array([20., 0., 0.])
AXIS = "twin(x,y,z)-origin -> gltf(x,z,-y); glTFast reflects X; Unity adapter yaw=180deg -> local(x,z,y); inverse local(x,z,y)+origin"


def log(message):
    print(f"[{time.strftime('%H:%M:%S')}] {message}", flush=True)


def atomic_json(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(data, indent=2, allow_nan=False), encoding="utf-8")
    # Windows readers (including progress inspection) can briefly deny rename.
    for attempt in range(10):
        try:
            os.replace(tmp, path)
            break
        except PermissionError:
            if attempt == 9:
                raise
            time.sleep(.05)


def atomic_npz(path, **arrays):
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp")
    with tmp.open("wb") as f:
        np.savez(f, **arrays)
    os.replace(tmp, path)


def twin_to_gltf(points, origin=ORIGIN):
    p = np.asarray(points) - origin
    return np.stack((p[..., 0], p[..., 2], -p[..., 1]), axis=-1)


def gltf_to_twin(points, origin=ORIGIN):
    p = np.asarray(points)
    return np.stack((p[..., 0], -p[..., 2], p[..., 1]), axis=-1) + origin


def aligned_bounds(ply, offset, nv, dtype, rotation, translation):
    """Whole-metre box around every aligned vertex, for --region auto."""
    lo, hi = np.full(3, np.inf), np.full(3, -np.inf)
    for start in range(0, nv, 5_000_000):
        p, _ = vertices_at(ply, offset, nv, dtype, np.arange(start, min(nv, start + 5_000_000)), rotation, translation)
        lo, hi = np.minimum(lo, p.min(0)), np.maximum(hi, p.max(0))
    lo, hi = np.floor(lo), np.ceil(hi)
    return [float(v) for pair in zip(lo, hi) for v in pair]


def config(args):
    ply = args.session / args.mesh
    offset, nv, nf, dtype = read_ply_layout(ply)
    with ply.open("rb") as f:
        header = f.read(offset)
    if b"format binary_little_endian 1.0" not in header or b"property list uchar uint vertex_indices" not in header:
        raise ValueError("Expected little-endian triangle PLY with uchar/uint face list")
    if ply.stat().st_size != offset + nv * dtype.itemsize + nf * FACE_DTYPE.itemsize:
        raise ValueError("PLY byte length does not match fixed triangle layout")
    region = REGIONS.get(args.region.lower())
    if region is None and args.region.lower() == "auto":
        region = aligned_bounds(ply, offset, nv, dtype, *read_alignment(args.session / "proc/alignment_global.txt"))
    elif region is None:
        region = list(map(float, args.region.split(",")))
    if len(region) != 6 or any(region[i] >= region[i + 1] for i in (0, 2, 4)):
        raise ValueError("Region must be xmin,xmax,ymin,ymax,zmin,zmax")
    if args.origin == "auto":
        # Nearest 10 m grid point to the region centre; keeps the building's height frame.
        origin = [round((region[0] + region[1]) / 20) * 10., round((region[2] + region[3]) / 20) * 10., 0.]
    else:
        origin = list(map(float, args.origin.split(",")))
    if len(origin) != 3:
        raise ValueError("Origin must be x,y,z or auto")
    args.origin_world = np.array(origin)
    data = dict(scan=args.session.name, source=str(ply.resolve()), source_bytes=ply.stat().st_size,
                source_mtime_ns=ply.stat().st_mtime_ns,
                sha256_header=hashlib.sha256(header).hexdigest(),
                sha256_alignment=hashlib.sha256((args.session / "proc/alignment_global.txt").read_bytes()).hexdigest(),
                region=region, region_name=args.region_name or args.region.upper(), chunk=args.chunk,
                target_ratio=args.target_ratio, lods=args.lods, batch_faces=args.batch_faces,
                simplify_batch_faces=args.simplify_batch_faces, origin_world=origin, version=VERSION)
    # Non-default sources only, so existing HGE outputs keep their fingerprint.
    if args.mesh != DEFAULT_MESH:
        data["mesh"] = args.mesh
    if args.building != "HGE":
        data["building"] = args.building
    args.out.mkdir(parents=True, exist_ok=True)
    cpath = args.out / "config.json"
    if cpath.exists() and json.loads(cpath.read_text()) != data:
        raise ValueError("Output config differs: use another --out directory (never mix stale stages)")
    if not cpath.exists():
        atomic_json(cpath, data)
    return data, ply, offset, nv, nf, dtype


def vertices_at(ply, offset, nv, dtype, ids, rotation, translation):
    # Close each mapping after use so already-read source pages do not accumulate.
    mapping = np.memmap(ply, dtype=dtype, mode="r", offset=offset, shape=(nv,))
    sampled = mapping[ids]
    del mapping
    p = np.column_stack([sampled[k] for k in ("x", "y", "z")])
    c = np.column_stack([sampled[k] for k in ("red", "green", "blue")])
    return (p @ rotation.T + translation).astype("<f4"), c


def chunk(args, cfg):
    data, ply, offset, nv, nf, dtype = cfg
    R, t = read_alignment(args.session / "proc/alignment_global.txt")
    region = data["region"]
    lo, hi = np.array(region)[[0, 2, 4]], np.array(region)[[1, 3, 5]]
    batches = args.out / "batches"
    batches.mkdir(exist_ok=True)
    for start in range(0, nf, args.batch_faces):
        name = f"b{start:012d}"
        dest = batches / name
        if (dest / "done.json").exists() and not args.force:
            continue
        temp = batches / (name + ".partial")
        temp.mkdir(exist_ok=True)
        # Only this pipeline's incomplete output can be replaced; never source data.
        for stale in temp.glob("*.npy"):
            stale.unlink()
        count = min(args.batch_faces, nf - start)
        fm = np.memmap(ply, dtype=FACE_DTYPE, mode="r", offset=offset + nv * dtype.itemsize + start * 13, shape=(count,))
        if np.any(fm["count"] != 3):
            raise ValueError(f"Non-triangle face in batch {start}")
        faces = fm["indices"].copy()
        del fm
        if faces.max() >= nv:
            raise ValueError("Out-of-range source face index")
        ids, inverse = np.unique(faces, return_inverse=True)
        p, _ = vertices_at(ply, offset, nv, dtype, ids, R, t)
        centroid = p[inverse.reshape(-1, 3)].mean(axis=1)
        keep = np.isfinite(centroid).all(axis=1) & (centroid >= lo).all(axis=1) & (centroid <= hi).all(axis=1)
        faces, centroid = faces[keep], centroid[keep]
        cells = np.floor(centroid[:, :2] / args.chunk).astype(np.int32)
        keys = cells[:, 0].astype(np.int64) * 100000 + cells[:, 1]
        order = np.argsort(keys, kind="stable")
        keys = keys[order]
        boundaries = np.r_[0, np.flatnonzero(np.diff(keys)) + 1, len(keys)]
        counts = {}
        for a, b in zip(boundaries[:-1], boundaries[1:]):
            if a == b:
                continue
            x, y = cells[order[a]]
            tile = f"x{x}_y{y}"
            np.save(temp / (tile + ".npy"), faces[order[a:b]], allow_pickle=False)
            counts[tile] = int(b - a)
        atomic_json(temp / "done.json", counts)
        if dest.exists():
            shutil.rmtree(dest)
        temp.rename(dest)
        log(f"chunk faces {start + count:,}/{nf:,}; retained {len(faces):,}")
        del faces, centroid, p, ids, inverse, cells, keys, order
    sources = {}
    for batch in sorted(batches.glob("b*")):
        if not (batch / "done.json").exists() or batch.suffix == ".partial":
            continue
        for tile, count in json.loads((batch / "done.json").read_text()).items():
            sources.setdefault(tile, []).append((batch / (tile + ".npy"), count))
    records = []
    for tile, shards in sorted(sources.items()):
        dest = args.out / "chunks" / (tile + ".npz")
        meta_path = dest.with_suffix(".json")
        if dest.exists() and meta_path.exists() and not args.force:
            records.append(json.loads(meta_path.read_text()))
            continue
        count = sum(n for _, n in shards)
        # mmap the merge buffer; unique needs bounded additional RAM for one tile.
        bufpath = args.out / "merge_faces.tmp"
        faces = np.memmap(bufpath, mode="w+", dtype="<u4", shape=(count, 3))
        pos = 0
        for source, n in shards:
            faces[pos:pos+n] = np.load(source, mmap_mode="r")
            pos += n
        faces.flush()
        if count > 35_000_000:
            raise MemoryError(f"Tile {tile} too large for 8GB envelope: reduce --chunk (new output)")
        ids, inverse = np.unique(faces, return_inverse=True)
        local = inverse.reshape(-1, 3).astype("<u4")
        del inverse, faces
        bufpath.unlink()
        p, colors = vertices_at(ply, offset, nv, dtype, ids, R, t)
        atomic_npz(dest, positions=p, colors=colors, faces=local)
        record = dict(id=tile, triangles=count, vertices=len(p), min=p.min(0).tolist(), max=p.max(0).tolist())
        atomic_json(meta_path, record)
        records.append(record)
        log(f"compact {tile}: {count:,} triangles / {len(p):,} vertices")
        del ids, local, p, colors
        gc.collect()
    atomic_json(args.out / "chunks.json", records)


def clean_mesh(p, c, f, weld=True, weld_m=.001):
    """Weld at 1 mm, discard duplicate/zero-area faces and unused vertices."""
    if weld:
        _, selected, inverse = np.unique(np.round(p / weld_m).astype(np.int64), axis=0, return_index=True, return_inverse=True)
        p, c, f = p[selected], c[selected], inverse[f]
    valid = (f[:, 0] != f[:, 1]) & (f[:, 1] != f[:, 2]) & (f[:, 0] != f[:, 2])
    f = f[valid]
    area = np.cross(p[f[:, 1]] - p[f[:, 0]], p[f[:, 2]] - p[f[:, 0]])
    f = f[np.sum(area * area, axis=1) > 1e-16]
    _, unique = np.unique(np.sort(f, axis=1), axis=0, return_index=True)
    f = f[np.sort(unique)]
    ids, inverse = np.unique(f, return_inverse=True)
    return p[ids].astype("<f4"), c[ids].astype("u1"), inverse.reshape(-1, 3).astype("<u4")


def decimate(p, c, f, target):
    import fast_simplification
    from scipy.spatial import cKDTree
    # Centroid chunks share a zig-zag edge, not necessarily vertices on x/y % 10.
    # Do not quantize or weld their coordinates independently. Pin every open
    # boundary, including intermediate spatial partitions, at every LOD.
    p, c, f = clean_mesh(p, c, f, weld=False)
    if len(f) > target:
        q, g = fast_simplification.simplify(p, f, target_count=max(4, target), agg=7.0, preserve_border=True)
        # Sample up to one million original colours, with an exact nearest query.
        step = max(1, math.ceil(len(p) / 1_000_000))
        tree = cKDTree(p[::step])
        _, ix = tree.query(q, workers=1)
        p, c, f = q, c[::step][ix], g
    return clean_mesh(p, c, f, weld=False)


def spatial_parts(p, f, limit):
    """Split only unusually large tiles, along a spatial median, not face order.

    Arbitrary face runs cut millions of internal edges and prevent QEM from
    reaching low targets. Spatial cuts retain each surface's connectivity.
    """
    if len(f) <= limit:
        yield f
        return
    extent = np.ptp(p, axis=0)
    axis = int(np.argmax(extent))
    centres = (p[f[:, 0], axis] + p[f[:, 1], axis] + p[f[:, 2], axis]) / 3
    order = np.argsort(centres)
    half = len(order) // 2
    for indices in (order[:half], order[half:]):
        subset = f[indices]
        ids, inverse = np.unique(subset, return_inverse=True)
        # Determine each child's actual spatial extent for further splits.
        for local in spatial_parts(p[ids], inverse.reshape(-1, 3), limit):
            yield ids[local]


def simplify(args, cfg):
    records = json.loads((args.out / "chunks.json").read_text())
    total = sum(r["triangles"] for r in records)
    cap = 1_500_000 if args.region.lower() == "a" else 5_000_000
    # Reserve room for irreducible pinned edges in tiny boundary tiles.
    ratio = min(args.target_ratio, cap * .80 / max(total, 1))
    def current(outputs, metadata):
        if args.force or not all(p.exists() for p in outputs) or not metadata.exists():
            return False
        records = json.loads(metadata.read_text())
        return all(records.get(str(i), {}).get("boundary_policy") == BOUNDARY_POLICY for i in range(args.lods))
    if args.workers > 1 and not args.tile:
        def run_tile(record):
            tile = record["id"]
            outputs = [args.out / "simplified" / tile / f"lod{i}.npz" for i in range(args.lods)]
            if current(outputs, outputs[0].parent / "reduction.json"):
                return
            command = [sys.executable, "-I", str(Path(__file__).resolve()), "simplify",
                       "--session", str(args.session), "--region", args.region,
                       "--mesh", args.mesh, "--origin", args.origin, "--building", args.building,
                       "--out", str(args.out), "--chunk", str(args.chunk),
                       "--target-ratio", str(args.target_ratio), "--lods", str(args.lods),
                       "--batch-faces", str(args.batch_faces), "--simplify-batch-faces", str(args.simplify_batch_faces),
                       "--tile", tile, "--workers", "1"]
            if args.force:
                command.append("--force")
            if args.region_name:
                command += ["--region-name", args.region_name]
            subprocess.run(command, check=True)
        with ThreadPoolExecutor(max_workers=args.workers) as pool:
            list(pool.map(run_tile, records))
        return
    for record in records:
        tile = record["id"]
        if args.tile and args.tile != tile:
            continue
        outputs = [args.out / "simplified" / tile / f"lod{i}.npz" for i in range(args.lods)]
        reduction_meta = args.out / "simplified" / tile / "reduction.json"
        if current(outputs, reduction_meta):
            continue
        target = max(4, int(record["triangles"] * ratio))
        # A partial tile is rebuilt from its source chunk. Reusing reduced LOD0
        # would lose its cleanup history and could compound decimation on resume.
        with np.load(args.out / "chunks" / (tile + ".npz")) as data:
            p, c, f = data["positions"], data["colors"], data["faces"]
        pieces = []
        # Compact tiles fit comfortably within the measured RAM envelope.
        # Only oversized tiles need spatial (connectivity-preserving) splits.
        for part in spatial_parts(p, f, args.simplify_batch_faces):
            ids, inv = np.unique(part, return_inverse=True)
            part_target = max(4, math.ceil(target * len(part) / len(f)))
            pieces.append(decimate(p[ids], c[ids], inv.reshape(-1, 3), part_target))
        del p, c, f
        offset, indices = 0, []
        for points, _, faces in pieces:
            indices.append(faces + offset)
            offset += len(points)
        p = np.concatenate([q[0] for q in pieces])
        c = np.concatenate([q[1] for q in pieces])
        f = np.concatenate(indices)
        del pieces, indices
        p, c, f = decimate(p, c, f, target)
        reductions = {}
        for level, out in enumerate(outputs):
            used_weld = .001
            level_target = target
            original = p, c, f
            if level:
                multiplier = .25 if level == 1 else .06 * (.25 ** (level - 2))
                # Tiny boundary slivers may contain too few connected faces
                # for a further legal collapse. Preserve up to 64 triangles.
                level_target = max(64, int(target * multiplier))
                p, c, f = decimate(*original, level_target)
            # Pinned boundaries take priority over an individual LOD ratio.
            # Never fall back to destructive welding to meet a target. The
            # independent seam check and hard regional budget remain gates.
            reductions[str(level)] = {"triangles": len(f), "fragment_weld_m": 0,
                                      "boundary_policy": BOUNDARY_POLICY,
                                      "target_triangles": level_target, "preserved_fragment_excess": max(0, len(f) - level_target)}
            atomic_npz(out, positions=p, colors=c, faces=f)
            log(f"simplify {tile} LOD{level}: {len(f):,} (source {record['triangles']:,})")
        atomic_json(reduction_meta, reductions)
        del p, c, f
        gc.collect()


def write_glb(path, points, colors, faces, origin=ORIGIN):
    """Minimal glTF 2.0: float positions, normalized u8 COLOR_0, u32 indices."""
    p = twin_to_gltf(points, origin).astype("<f4")
    c = np.column_stack((colors, np.full(len(colors), 255, dtype="u1"))).astype("u1")
    f = np.asarray(faces, dtype="<u4")
    blobs = [p.tobytes(), c.tobytes(), f.tobytes()]
    offsets = [0, len(blobs[0]), len(blobs[0]) + len(blobs[1])]
    binary = b"".join(blobs)
    doc = {"asset": {"version": "2.0", "generator": VERSION}, "scene": 0,
           "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0}],
           "extensionsUsed": ["KHR_materials_unlit"],
           "materials": [{"name": "Twin vertex colour", "doubleSided": True,
                          "pbrMetallicRoughness": {"metallicFactor": 0, "roughnessFactor": 1},
                          "extensions": {"KHR_materials_unlit": {}}}],
           "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "COLOR_0": 1}, "indices": 2, "material": 0, "mode": 4}]}],
           "buffers": [{"byteLength": len(binary)}],
           "bufferViews": [{"buffer": 0, "byteOffset": offsets[i], "byteLength": len(blob), "target": 34963 if i == 2 else 34962} for i, blob in enumerate(blobs)],
           "accessors": [{"bufferView": 0, "componentType": 5126, "count": len(p), "type": "VEC3", "min": p.min(0).tolist(), "max": p.max(0).tolist()},
                         {"bufferView": 1, "componentType": 5121, "normalized": True, "count": len(c), "type": "VEC4"},
                         {"bufferView": 2, "componentType": 5125, "count": f.size, "type": "SCALAR"}]}
    js = json.dumps(doc, separators=(",", ":"), allow_nan=False).encode()
    js += b" " * (-len(js) % 4)
    binary += b"\0" * (-len(binary) % 4)
    content = struct.pack("<4sII", b"glTF", 2, 12 + 8 + len(js) + 8 + len(binary))
    content += struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(binary), 0x004E4942) + binary
    path.parent.mkdir(parents=True, exist_ok=True)
    tmp = path.with_suffix(".tmp")
    tmp.write_bytes(content)
    os.replace(tmp, path)


def export(args, cfg):
    records = json.loads((args.out / "chunks.json").read_text())
    for record in records:
        for level in range(args.lods):
            dest = args.out / "tiles" / record["id"] / f"lod{level}.glb"
            source = args.out / "simplified" / record["id"] / f"lod{level}.npz"
            if dest.exists() and dest.stat().st_mtime_ns >= source.stat().st_mtime_ns and not args.force:
                continue
            with np.load(args.out / "simplified" / record["id"] / f"lod{level}.npz") as mesh:
                write_glb(dest, mesh["positions"], mesh["colors"], mesh["faces"], args.origin_world)
            if dest.stat().st_size > 30_000_000:
                raise ValueError(f"Tile exceeds 30 MB: {dest}")
        log(f"export {record['id']}")


def manifest(args, cfg):
    data = cfg[0]
    tiles = []
    for record in json.loads((args.out / "chunks.json").read_text()):
        tile = {"id": record["id"], "min": record["min"], "max": record["max"], "files": {}, "triangles": {}, "bytes": {}, "bounds_lod": {}, "reduction": json.loads((args.out / "simplified" / record["id"] / "reduction.json").read_text())}
        for level in range(args.lods):
            key = str(level)
            rel = f"tiles/{tile['id']}/lod{level}.glb"
            with np.load(args.out / "simplified" / tile["id"] / f"lod{level}.npz") as mesh:
                p = mesh["positions"]
                tile["triangles"][key] = len(mesh["faces"])
                tile["bounds_lod"][key] = {"min": p.min(0).tolist(), "max": p.max(0).tolist()}
            tile["files"][key], tile["bytes"][key] = rel, (args.out / rel).stat().st_size
        tiles.append(tile)
    total_tris = sum(t["triangles"]["0"] for t in tiles)
    total_lod0_bytes = sum(t["bytes"]["0"] for t in tiles)
    a = args.region.lower() == "a"
    if total_tris > (1_500_000 if a else 5_000_000) or total_lod0_bytes > (150_000_000 if a else 450_000_000):
        raise ValueError("Region LOD0 budget exceeded")
    region = data["region"]
    m = {"schema": "twin-manifest/1", "scan": data["scan"],
         "source": {"mesh": f"raw/{args.building}/sessions/{data['scan']}/{args.mesh}", "sha256_header": data["sha256_header"], "sha256_alignment": data["sha256_alignment"], "bytes": data["source_bytes"]},
         "frame": {"up": "z", "units": "m", "alignment": "pose_graph_optimized", "gltf_axis_convention": AXIS},
         "origin_world": args.origin_world.tolist(), "datum_z": args.datum_z, "region": {"name": data["region_name"], "min": [region[i] for i in (0, 2, 4)], "max": [region[i] for i in (1, 3, 5)]},
         "chunk_size_m": args.chunk, "lods": [{"level": i, "max_distance_m": [12, 30, 80][min(i, 2)]} for i in range(args.lods)],
         "tiles": tiles, "totals": {"triangles_lod0": total_tris, "bytes_lod0": total_lod0_bytes, "bytes": sum(sum(t["bytes"].values()) for t in tiles)},
         "generated_utc": datetime.now(timezone.utc).isoformat(), "tool_version": VERSION,
         "attribution": f"LaMAR (ETH Zurich / Microsoft), {args.building} NavVis; derived decimated vertex-coloured geometry, CC BY-SA 4.0"}
    atomic_json(args.out / "manifest.json", m)
    log(f"manifest: {len(tiles)} tiles; {total_tris:,} LOD0 triangles; {total_lod0_bytes/1e6:.2f} MB")


def preview(args, cfg):
    import matplotlib.pyplot as plt
    m = json.loads((args.out / "manifest.json").read_text())
    lines = [VERSION, f"Scan: {m['scan']} | region {m['region']['name']}", f"Axis: {AXIS}", "Centroid clipping: triangles may extend beyond region/cell edges.", "Colour sampled from original vertices after QEM; no textures."]
    for level in range(args.lods):
        fig, ax = plt.subplots(figsize=(12, 9), facecolor="#F2F1EC")
        ax.set_facecolor("#F2F1EC")
        triangles, size = 0, 0
        for tile in m["tiles"]:
            with np.load(args.out / "simplified" / tile["id"] / f"lod{level}.npz") as mesh:
                p, c = mesh["positions"], mesh["colors"]
                keep = (p[:, 2] >= args.datum_z - .2) & (p[:, 2] <= args.datum_z + 2.5)
                p, c = p[keep], c[keep]
                step = max(1, len(p) // 12000)
                ax.scatter(p[::step, 0], p[::step, 1], c=c[::step] / 255., s=.2, linewidths=0)
            triangles += tile["triangles"][str(level)]
            size += tile["bytes"][str(level)]
        ax.set_aspect("equal")
        ax.set_xlabel("Twin X / metres (west is negative)")
        ax.set_ylabel("Twin Y / metres")
        ax.set_title(f"{args.building} / REGION {m['region']['name']} / LOD {level}   {triangles:,} triangles")
        fig.tight_layout()
        fig.savefig(args.out / f"preview_lod{level}.png", dpi=160)
        plt.close(fig)
        lines.append(f"LOD{level}: {triangles:,} triangles, {size:,} bytes")
        tolerance = max(t["reduction"][str(level)]["fragment_weld_m"] for t in m["tiles"])
        lines.append(f"  Maximum fragment cleanup weld: {tolerance:g} m (quantization cell width; not an accuracy guarantee)")
    (args.out / "report.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    log("\n".join(lines))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("stage", choices=["chunk", "simplify", "export", "manifest", "preview", "all"])
    parser.add_argument("--session", type=Path, required=True)
    parser.add_argument("--region", default="a", help="a, b, auto (bounds of the whole mesh) or xmin,xmax,ymin,ymax,zmin,zmax")
    parser.add_argument("--region-name", help="Display name stored in the manifest; defaults to the uppercased --region")
    parser.add_argument("--mesh", default=DEFAULT_MESH, help="Mesh PLY relative to --session, e.g. a cropped proc/meshes/entrance_hall.ply")
    parser.add_argument("--origin", default=",".join(f"{v:g}" for v in ORIGIN), help="Tile origin x,y,z in metres, or auto")
    parser.add_argument("--datum-z", type=float, default=0., help="Main floor height in twin metres; used for the preview slab and stored in the manifest")
    parser.add_argument("--building", default="HGE", help="LaMAR building code used in source paths and attribution")
    parser.add_argument("--chunk", type=float, default=10)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--target-ratio", type=float, default=.013)
    parser.add_argument("--lods", type=int, default=3)
    parser.add_argument("--batch-faces", type=int, default=500_000)
    parser.add_argument("--simplify-batch-faces", type=int, default=8_000_000)
    parser.add_argument("--workers", type=int, choices=(1, 2), default=1)
    parser.add_argument("--tile", help=argparse.SUPPRESS)
    parser.add_argument("--force", action="store_true")
    args = parser.parse_args()
    if args.chunk <= 0 or not 0 < args.target_ratio <= 1 or not 1 <= args.lods <= 3 or args.batch_faces < 1 or args.simplify_batch_faces < 1:
        parser.error("Invalid chunk, ratio, LOD count or batch size")
    cfg = config(args)
    import psutil
    process = psutil.Process()
    stopped = threading.Event()
    def monitor():
        peak = 0
        combined_peak = 0
        while not stopped.is_set():
            mem = process.memory_info()
            peak = max(peak, getattr(mem, "peak_wset", mem.rss))
            combined = mem.rss
            for child in process.children(recursive=True):
                try:
                    combined += child.memory_info().rss
                except psutil.NoSuchProcess:
                    pass
            combined_peak = max(combined_peak, combined)
            filename = f"memory_{args.tile}.json" if args.tile else "memory.json"
            if max(peak, combined_peak) > 8_000_000_000:
                log("ERROR: exceeded 8GB memory envelope; checkpoints retained")
                for child in process.children(recursive=True):
                    child.terminate()
                os._exit(8)
            try:
                atomic_json(args.out / filename, {"pid": os.getpid(), "peak_working_set_bytes": peak, "observed_peak_combined_bytes": combined_peak, "limit_bytes": 8_000_000_000, "checked_unix": time.time()})
            except OSError as error:
                # Telemetry publication must never stop limit enforcement.
                log(f"Memory telemetry temporarily unavailable: {error}")
            stopped.wait(2)
    sampler = threading.Thread(target=monitor, daemon=True)
    sampler.start()
    try:
        for name in (["chunk", "simplify", "export", "manifest", "preview"] if args.stage == "all" else [args.stage]):
            log(f"begin {name}")
            globals()[name](args, cfg)
    finally:
        stopped.set()
        sampler.join(timeout=3)
    log("complete")


if __name__ == "__main__":
    main()
