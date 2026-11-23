"""Stream verified NavVis point clouds into aligned, oriented RGB crops for NKSR.

Only NumPy is required. No reconstruction or model replacement is performed.
Example (downloads about 10.94 GB for both sources):
  python tools/nksr_prepare.py --config work/nksr-comparison/source-audit.json \
    --out work/nksr-comparison/inputs --halo 0.3
Use --source HGE to prepare just the three HG spaces. A complete existing raw
PLY can replace its download with --local-ply HGE=/path/pointcloud.ply.
"""
from __future__ import annotations

import argparse
from contextlib import closing
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile
import time
import urllib.request
import zlib

import numpy as np

PLY_TYPES = {"float": "<f4", "float32": "<f4", "uchar": "|u1", "uint8": "|u1"}
OUTPUT_DTYPE = np.dtype([(n, "<f4") for n in ("x", "y", "z", "nx", "ny", "nz")]
                        + [(n, "u1") for n in ("red", "green", "blue")])


def alignment(source):
    """Verify original alignment text, then derive both position and normal rotation."""
    a = source["alignment"]
    text = a["text"]
    if hashlib.sha256(text.encode("utf-8")).hexdigest() != a["sha256"]:
        raise ValueError("Alignment text does not match its SHA-256")
    rows = [line.split(",") for line in text.splitlines()
            if line.split(",")[0].strip() == "pose_graph_optimized"]
    if len(rows) != 1 or len(rows[0]) < 9 or rows[0][1].strip() != "__absolute__":
        raise ValueError("Expected one pose_graph_optimized alignment")
    qw, qx, qy, qz, tx, ty, tz = map(float, rows[0][2:9])
    q = np.array([qw, qx, qy, qz])
    if not np.isfinite(q).all() or not np.isclose(np.linalg.norm(q), 1, atol=1e-5):
        raise ValueError("Invalid alignment quaternion")
    rotation = np.array([
        [1-2*(qy*qy+qz*qz), 2*(qx*qy-qz*qw), 2*(qx*qz+qy*qw)],
        [2*(qx*qy+qz*qw), 1-2*(qx*qx+qz*qz), 2*(qy*qz-qx*qw)],
        [2*(qx*qz-qy*qw), 2*(qy*qz+qx*qw), 1-2*(qx*qx+qy*qy)],
    ])
    translation = np.array([tx, ty, tz])
    if not np.isfinite(translation).all():
        raise ValueError("Invalid alignment translation")
    if not np.allclose(rotation, np.array(a["rotation_row_major"]).reshape(3, 3), atol=1e-8, rtol=0):
        raise ValueError("Embedded rotation disagrees with original alignment")
    if not np.allclose(translation, a["translation"], atol=1e-8, rtol=0):
        raise ValueError("Embedded translation disagrees with original alignment")
    return rotation, translation


def checked_chunks(raw, metadata, block_bytes, compressed, progress_label=None):
    """Yield bounded byte chunks; verify complete member size and CRC before EOF."""
    expected_input = metadata["compressed_bytes" if compressed else "uncompressed_bytes"]
    expected_output = metadata["uncompressed_bytes"]
    inflater = zlib.decompressobj(-15) if compressed else None
    consumed = produced = crc = 0
    last_progress = time.monotonic()
    while consumed < expected_input:
        data = raw.read(min(block_bytes, expected_input - consumed))
        if not data:
            raise ValueError("Truncated source member")
        consumed += len(data)
        pending = data
        while pending:
            if inflater:
                chunk = inflater.decompress(pending, block_bytes)
                pending = inflater.unconsumed_tail
                if inflater.unused_data:
                    raise ValueError("Extra data after DEFLATE stream")
            else:
                chunk, pending = pending, b""
            produced += len(chunk)
            if produced > expected_output:
                raise ValueError("Uncompressed source exceeds declared length")
            crc = zlib.crc32(chunk, crc)
            if chunk:
                yield chunk
        if progress_label and time.monotonic() - last_progress >= 10:
            print(f"  {progress_label}: {consumed / 2**20:,.1f}/{expected_input / 2**20:,.1f} MiB read"
                  f" ({100 * consumed / expected_input:.1f}%), {produced / 2**20:,.1f} MiB decompressed", flush=True)
            last_progress = time.monotonic()
    if raw.read(1):
        raise ValueError("Source contains bytes beyond declared member length")
    if inflater and not inflater.eof:
        raise ValueError("Incomplete DEFLATE stream")
    if produced != expected_output or f"{crc & 0xffffffff:08x}" != metadata["crc32_hex"].lower():
        raise ValueError("Source size or ZIP CRC32 mismatch")


def validate_http(response, source):
    archive, member = source["archive"], source["pointcloud"]
    start, end = member["compressed_range"]
    expected = f"bytes {start}-{end}/{archive['bytes']}"
    if response.status != 206 or response.headers.get("Content-Range") != expected:
        raise ValueError("Server did not return the exact requested archive range")
    if int(response.headers.get("Content-Length", -1)) != member["compressed_bytes"]:
        raise ValueError("HTTP range length mismatch")
    if response.headers.get("Content-Encoding", "identity") != "identity":
        raise ValueError("Unexpected HTTP content encoding")
    if archive.get("etag") and response.headers.get("ETag") != archive["etag"]:
        raise ValueError("Archive ETag changed; re-audit its ZIP directory before proceeding")


def source_chunks(source, local_path, block_bytes):
    member = source["pointcloud"]
    if member["zip_method"] != 8:
        raise ValueError("Only audited raw-DEFLATE ZIP members are supported")
    if local_path:
        with Path(local_path).open("rb") as stream:
            yield from checked_chunks(stream, member, block_bytes, compressed=False, progress_label=source["scan"])
        return
    start, end = member["compressed_range"]
    if end - start + 1 != member["compressed_bytes"]:
        raise ValueError("Invalid archive range in source config")
    headers = {"Range": f"bytes={start}-{end}", "Accept-Encoding": "identity",
               "User-Agent": "TwinInspector-NKSR-Prepare/1"}
    if source["archive"].get("etag"):
        headers["If-Range"] = source["archive"]["etag"]
    request = urllib.request.Request(source["archive"]["url"], headers=headers)
    # Uses the interpreter's normal trusted CA store; TLS verification is never disabled.
    with urllib.request.urlopen(request, timeout=120) as response:
        validate_http(response, source)
        yield from checked_chunks(response, member, block_bytes, compressed=True, progress_label=source["scan"])


def vertex_blocks(chunks, metadata):
    """Parse only the audited vertex-only PLY layout; reject extra/truncated records."""
    iterator = iter(chunks)
    header = b""
    for chunk in iterator:
        header += chunk
        end = header.find(b"end_header\n")
        if end >= 0:
            end += len(b"end_header\n")
            body = header[end:]
            header = header[:end]
            break
        if len(header) > 65536:
            raise ValueError("PLY header too large or missing")
    else:
        raise ValueError("No complete PLY header")
    lines = header.decode("ascii").splitlines()
    if lines[0] != "ply" or "format binary_little_endian 1.0" not in lines:
        raise ValueError("Expected binary little-endian PLY")
    fields, count, element = [], None, None
    for line in lines:
        parts = line.split()
        if parts[:1] == ["element"]:
            element = parts[1]
            if element != "vertex":
                raise ValueError("Raw cloud must contain only the vertex element")
            if count is not None:
                raise ValueError("Duplicate vertex element")
            count = int(parts[2])
        elif parts[:1] == ["property"]:
            if element != "vertex" or len(parts) != 3 or parts[1] not in PLY_TYPES:
                raise ValueError("Unsupported PLY property")
            fields.append((parts[2], PLY_TYPES[parts[1]]))
    expected_fields = [(f["name"], f["dtype"]) for f in metadata["vertex_fields"]]
    dtype = np.dtype(fields)
    if (count != metadata["vertex_count"] or len(header) != metadata["header_bytes"]
            or dtype != np.dtype(expected_fields) or dtype.itemsize != metadata["vertex_stride_bytes"]):
        raise ValueError("PLY header does not match the audited source")
    if len(header) + count * dtype.itemsize != metadata["uncompressed_bytes"]:
        raise ValueError("Inconsistent audited source size")
    remaining = count
    while True:
        n = len(body) // dtype.itemsize
        if n > remaining:
            raise ValueError("Extra vertices in raw cloud")
        if n:
            size = n * dtype.itemsize
            yield np.frombuffer(body[:size], dtype=dtype)
            body = body[size:]
            remaining -= n
        try:
            body += next(iterator)
        except StopIteration:
            break
    if body or remaining:
        raise ValueError("Incomplete PLY vertices")


def output_header(count):
    return ("ply\nformat binary_little_endian 1.0\n"
            "comment Aligned RGB oriented cloud for NKSR; coordinates in metres\n"
            f"element vertex {count:020d}\n"
            "property float x\nproperty float y\nproperty float z\n"
            "property float nx\nproperty float ny\nproperty float nz\n"
            "property uchar red\nproperty uchar green\nproperty uchar blue\nend_header\n").encode("ascii")


def prepare(config, output, selected=None, local_paths=None, halo=0, block_bytes=4*1024*1024):
    output = Path(output)
    if output.exists():
        raise FileExistsError(f"Refusing to replace existing output: {output}")
    if not np.isfinite(halo) or halo < 0 or not 1024 <= block_bytes <= 64*1024*1024:
        raise ValueError("Invalid halo or block size")
    selected = list(config["sources"]) if selected is None else list(selected)
    if not selected or len(set(selected)) != len(selected):
        raise ValueError("Select one or more distinct sources")
    if set(selected) - set(config["sources"]):
        raise ValueError("Unknown source key")
    local_paths = local_paths or {}
    if set(local_paths) - set(selected):
        raise ValueError("Local PLY supplied for an unselected source")
    spaces = [s for s in config["spaces"] if s["source"] in selected]
    if len({s["id"] for s in spaces}) != len(spaces):
        raise ValueError("Duplicate space ID")
    for space in spaces:
        if not re.fullmatch(r"[A-Za-z0-9_-]+", space["id"]):
            raise ValueError("Unsafe space ID")
        lo, hi = np.asarray(space["min"], float), np.asarray(space["max"], float)
        if lo.shape != (3,) or hi.shape != (3,) or not np.isfinite([lo, hi]).all() or not (hi > lo).all():
            raise ValueError("Invalid crop bounds")
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {"schema": "nksr-inputs/1", "coordinate_frame": "aligned LaMAR world, metres, Z up",
              "halo_metres": halo, "reconstruction_performed": False,
              "normal_policy": "Preserve source orientation; rotate by alignment, normalize; discard nonfinite/zero normals",
              "source_audit_canonical_json_sha256": hashlib.sha256(json.dumps(config, sort_keys=True).encode()).hexdigest(),
              "sources": {}, "spaces": []}
    with tempfile.TemporaryDirectory(prefix=".nksr-prepare-", dir=output.parent) as temporary:
        stage = Path(temporary)
        for source_name in selected:
            source = config["sources"][source_name]
            rotation, translation = alignment(source)
            chosen = [s for s in spaces if s["source"] == source_name]
            if not chosen:
                raise ValueError(f"No spaces for source {source_name}")
            handles, counts = {}, {s["id"]: 0 for s in chosen}
            seen = invalid = 0
            last_progress = time.monotonic()
            print(f"Preparing {source_name}: {source['scan']}", flush=True)
            try:
                for space in chosen:
                    handle = (stage / (space["id"] + ".ply")).open("w+b")
                    handles[space["id"]] = handle
                    handle.write(output_header(0))
                chunks = source_chunks(source, local_paths.get(source_name), block_bytes)
                with closing(chunks):
                    for vertices in vertex_blocks(chunks, source["pointcloud"]):
                        xyz = np.column_stack([vertices[k] for k in ("x", "y", "z")]).astype(np.float64)
                        normals = np.column_stack([vertices[k] for k in ("nx", "ny", "nz")]).astype(np.float64)
                        lengths = np.linalg.norm(normals, axis=1)
                        valid = np.isfinite(xyz).all(1) & np.isfinite(normals).all(1) & (lengths > 1e-8)
                        invalid += int((~valid).sum())
                        points = xyz @ rotation.T + translation
                        transformed_normals = normals @ rotation.T
                        for space in chosen:
                            keep = valid & (points >= np.asarray(space["min"])-halo).all(1) & (points <= np.asarray(space["max"])+halo).all(1)
                            count = int(keep.sum())
                            if not count:
                                continue
                            result = np.empty(count, dtype=OUTPUT_DTYPE)
                            for i, name in enumerate(("x", "y", "z")):
                                result[name] = points[keep, i]
                            for i, name in enumerate(("nx", "ny", "nz")):
                                result[name] = transformed_normals[keep, i] / lengths[keep]
                            for name in ("red", "green", "blue"):
                                result[name] = vertices[name][keep]
                            handles[space["id"]].write(result.tobytes())
                            counts[space["id"]] += count
                        previous = seen
                        seen += len(vertices)
                        if seen // 10_000_000 != previous // 10_000_000 or time.monotonic() - last_progress >= 10:
                            print(f"  {seen:,} source points read; crops {counts}", flush=True)
                            last_progress = time.monotonic()
                for space in chosen:
                    ident = space["id"]
                    if not counts[ident]:
                        raise ValueError(f"No valid points in {ident}; refusing empty output")
                    handle = handles[ident]
                    handle.seek(0)
                    handle.write(output_header(counts[ident]))
                    handle.flush()
                    os.fsync(handle.fileno())
            finally:
                for handle in handles.values():
                    handle.close()
            report["sources"][source_name] = {"scan": source["scan"], "input_points": seen,
                "invalid_position_or_normal_points": invalid, "zip_crc32_verified": source["pointcloud"]["crc32_hex"],
                "alignment_sha256": source["alignment"]["sha256"], "alignment": source["alignment"],
                "input_mode": "local raw PLY" if source_name in local_paths else "verified HTTP ZIP range",
                "archive": source["archive"],
                "pointcloud": source["pointcloud"]}
            for space in chosen:
                path = stage / (space["id"] + ".ply")
                digest = hashlib.sha256()
                with path.open("rb") as stream:
                    for data in iter(lambda: stream.read(block_bytes), b""):
                        digest.update(data)
                report["spaces"].append({**space, "ply": path.name, "points": counts[space["id"]],
                    "sha256": digest.hexdigest(), "bytes": path.stat().st_size,
                    "crop_min_with_halo": (np.asarray(space["min"])-halo).tolist(),
                    "crop_max_with_halo": (np.asarray(space["max"])+halo).tolist()})
        (stage / "provenance.json").write_text(json.dumps(report, indent=2, allow_nan=False)+"\n")
        if output.exists():
            raise FileExistsError(f"Output appeared during processing: {output}")
        os.rename(stage, output)
    print(f"Verified inputs saved to {output}", flush=True)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--source", action="append", help="source key, repeat to select several")
    parser.add_argument("--local-ply", action="append", default=[], metavar="SOURCE=PATH")
    parser.add_argument("--halo", type=float, default=0, help="extra metres around each exact region")
    parser.add_argument("--block-mib", type=int, default=4)
    args = parser.parse_args()
    local = {}
    for entry in args.local_ply:
        key, sep, path = entry.partition("=")
        if not sep or not key or not path or key in local:
            parser.error("--local-ply requires distinct SOURCE=PATH entries")
        local[key] = path
    prepare(json.loads(args.config.read_text()), args.out, args.source, local,
            args.halo, args.block_mib*1024*1024)


if __name__ == "__main__":
    main()
