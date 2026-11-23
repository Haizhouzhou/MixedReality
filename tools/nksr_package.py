"""Verify NKSR results and package complete regions without spatial tile selection.

Input layout: MESHES/provenance.json (original nksr_prepare output), and
MESHES/SPACE/mesh.ply + mesh.nksr.json for each audited space. Outputs are
OUTPUT/twin and OUTPUT/packaged; existing directories are never replaced.
For the current same-app trial use --space HG_MainHall --baseline-packaged PATH:
only MainHall is reconstructed, all three native LODs are retained, and the
baseline catalog and other spaces (including absent external spaces) stay exact.
Run with the Python environment from tools/requirements-twin.txt. Source meshes
are already aligned: temporary pipeline sessions use identity alignment.
This command does not modify StreamingAssets, baseline models, or any APK.
"""
from __future__ import annotations

import argparse
import copy
from contextlib import contextmanager
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile

ROOT = Path(__file__).resolve().parents[1]
TOOLS = ROOT / "tools"
SPACE_IDS = {"HG_MainHall", "HG_E3_Hall", "HG_E7_Hall", "CAB_MainHall"}
WORLD_FRAME = "LaMAR world; right-handed; Z up; metres; source alignment already applied"
IDENTITY = "pose_graph_optimized, __absolute__, 1, 0, 0, 0, 0, 0, 0\n"


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def write_json(path, data):
    with Path(path).open("x", encoding="utf-8") as stream:
        json.dump(data, stream, indent=2, allow_nan=False)
        stream.write("\n")


def mesh_layout(path, report):
    """Bound the header read and check the fixed layout before pipeline parsing."""
    sizes = {"double": 8, "float": 4, "uchar": 1}
    header, fields, counts, element = bytearray(), {}, {}, None
    with Path(path).open("rb") as stream:
        while len(header) <= 65536:
            line = stream.readline(65537)
            require(bool(line), "Incomplete mesh PLY header")
            header.extend(line)
            parts = line.decode("ascii").split()
            if parts[:1] == ["element"]:
                require(len(parts) == 3 and parts[1] in ("vertex", "face") and parts[1] not in counts,
                        "Unexpected mesh PLY element")
                element = parts[1]
                counts[element] = int(parts[2])
            elif parts[:1] == ["property"] and element == "vertex":
                require(len(parts) == 3 and parts[1] in sizes and parts[2] not in fields,
                        "Unsupported mesh vertex property")
                fields[parts[2]] = parts[1]
            elif parts == ["end_header"]:
                break
        else:
            raise ValueError("Oversized mesh PLY header")
    require(header.startswith(b"ply\nformat binary_little_endian 1.0\n"), "Mesh must be binary little-endian PLY")
    require(b"property list uchar uint vertex_indices\n" in header, "Mesh must use uint triangle indices")
    require(all(fields.get(name) in ("float", "double") for name in ("x", "y", "z")), "Mesh must contain XYZ")
    require(all(fields.get(name) == "uchar" for name in ("red", "green", "blue")), "Mesh must contain uchar RGB")
    require(counts.get("vertex", 0) > 0 and counts.get("face", 0) > 0, "Empty reconstruction mesh")
    require(counts["vertex"] == report["vertices"] and counts["face"] == report["triangles"], "Mesh counts disagree with reconstruction report")
    expected = len(header) + counts["vertex"] * sum(sizes[t] for t in fields.values()) + counts["face"] * 13
    require(Path(path).stat().st_size == expected, "Mesh byte length does not match triangle layout")


def validate_inputs(meshes, audit_path, catalog_path, selected=None):
    meshes, audit_path = Path(meshes), Path(audit_path)
    audit = json.loads(audit_path.read_text())
    catalog = json.loads(Path(catalog_path).read_text())
    preparation_path = meshes / "provenance.json"
    preparation = json.loads(preparation_path.read_text())
    require(audit.get("schema") == "twin-nksr-source-audit/1", "Unsupported source audit")
    require(catalog.get("schema") == "twin-spaces/1", "Unsupported space catalog")
    spaces = {space["id"]: space for space in audit["spaces"]}
    prepared = {space["id"]: space for space in preparation["spaces"]}
    selected = list(SPACE_IDS) if selected is None else list(selected)
    require(bool(selected) and len(selected) == len(set(selected)) and set(selected) <= SPACE_IDS, "Invalid selected spaces")
    require(len(spaces) == len(audit["spaces"]) and set(selected) <= set(spaces), "Audit does not cover selected spaces")
    require(len(prepared) == len(preparation["spaces"]) and set(selected) <= set(prepared), "Prepared clouds do not cover selected spaces")
    require(len(catalog["spaces"]) == 4 and {s["id"] for s in catalog["spaces"]} == SPACE_IDS,
            "Catalog must name exactly the four comparison spaces")
    canonical = hashlib.sha256(json.dumps(audit, sort_keys=True).encode()).hexdigest()
    require(preparation.get("schema") == "nksr-inputs/1" and preparation.get("source_audit_canonical_json_sha256") == canonical,
            "Prepared clouds are not bound to this exact source audit")
    require(preparation.get("coordinate_frame") == "aligned LaMAR world, metres, Z up", "Unexpected prepared coordinate frame")
    halo = preparation["halo_metres"]
    require(isinstance(halo, (int, float)) and math.isfinite(halo) and halo >= 0, "Invalid preparation halo")
    preparation_hash = sha256(preparation_path)
    checked = []
    for item in catalog["spaces"]:
        ident = item["id"]
        if ident not in selected:
            continue
        space, cloud = spaces[ident], prepared[ident]
        source = audit["sources"][space["source"]]
        require(bool(re.fullmatch(r"[A-Za-z0-9_-][A-Za-z0-9_.-]*", space["scan"])) and ".." not in space["scan"],
                "Unsafe scan/session name")
        require(item["scan"] == source["scan"] == space["scan"] and item["building"] == space["source"],
                f"Catalog/source mismatch for {ident}")
        # Read-only baseline check catches an audit taken from older region definitions.
        manifest_path = Path(space["manifest_path"])
        require(sha256(manifest_path) == space["manifest_sha256"], f"Baseline manifest changed since audit: {ident}")
        baseline = json.loads(manifest_path.read_text())
        require(baseline["scan"] == space["scan"] and baseline["origin_world"] == space["origin_world"]
                and baseline.get("datum_z", 0) == space["datum_z"] and baseline["region"]["min"] == space["min"]
                and baseline["region"]["max"] == space["max"] and baseline["region"]["name"] == space["region_name"],
                f"Audit differs from baseline region/frame: {ident}")
        for key, value in space.items():
            require(cloud.get(key) == value, f"Prepared region metadata differs: {ident}/{key}")
        require(cloud["crop_min_with_halo"] == [v-halo for v in space["min"]]
                and cloud["crop_max_with_halo"] == [v+halo for v in space["max"]], f"Halo bounds mismatch: {ident}")
        prepared_source = preparation["sources"][space["source"]]
        require(prepared_source.get("zip_crc32_verified") == source["pointcloud"]["crc32_hex"], f"Unverified source CRC: {ident}")
        for key in ("scan", "alignment", "archive", "pointcloud"):
            require(prepared_source.get(key) == source[key], f"Prepared source differs from audit: {ident}/{key}")
        mesh, report_path = meshes / ident / "mesh.ply", meshes / ident / "mesh.nksr.json"
        report = json.loads(report_path.read_text())
        require(report.get("schema") == "nksr-reconstruction/1" and report.get("method") == "NKSR"
                and report.get("completed") is True, f"No completed NKSR reconstruction: {ident}")
        require(report.get("coordinate_frame") == WORLD_FRAME, f"Mesh is not in aligned world coordinates: {ident}")
        proof = report.get("preparation", {})
        require(proof.get("sha256") == preparation_hash and proof.get("space") == cloud
                and proof.get("source") == prepared_source, f"Stale/mismatched reconstruction preparation: {ident}")
        require(report["input"]["sha256"] == cloud["sha256"] and report["input"]["points"] == cloud["points"] > 0,
                f"Reconstruction used a different cloud: {ident}")
        require(sha256(mesh) == report["output"]["sha256"], f"Reconstructed mesh SHA256 mismatch: {ident}")
        mesh_layout(mesh, report["output"])
        checked.append({"space": space, "mesh": mesh.resolve(), "report": report,
                        "report_path": report_path, "report_sha256": sha256(report_path)})
    return audit, catalog, preparation, checked


def run_pipeline(session, destination, space, python, workers, target_ratio):
    region = "a" if space["id"] == "HG_MainHall" and space["min"] == [-20, -5, -1] and space["max"] == [70, 32, 8] else ",".join(
        str(v) for pair in zip(space["min"], space["max"]) for v in pair)
    command = [str(python), "-I", str(TOOLS / "twin_pipeline.py"), "chunk", "--session", str(session),
               "--out", str(destination), "--region=" + region, "--region-name", space["region_name"],
               "--origin=" + ",".join(map(str, space["origin_world"])), "--datum-z", str(space["datum_z"]),
               "--building", space["source"], "--target-ratio", str(target_ratio), "--lods", "3", "--workers", str(workers)]
    environment = os.environ.copy()
    environment.setdefault("MPLCONFIGDIR", str(destination.parent.parent / ".matplotlib-cache"))
    commands = []
    for stage in ("chunk", "simplify", "export", "manifest"):
        stage_command = list(command)
        stage_command[3] = stage
        subprocess.run(stage_command, check=True, env=environment)
        # Separate processes otherwise replace the earlier stages' peak telemetry.
        if (destination / "memory.json").is_file():
            shutil.copy2(destination / "memory.json", destination / f"memory-{stage}.json")
        commands.append(stage_command)
    for script in ("verify_seams.py", "verify_tiles.py"):
        subprocess.run([str(python), "-I", str(TOOLS / script), str(destination)], check=True)
    return commands


def package_levels(twin, destination, item, audit_path, preparation_path, levels=(2,)):
    """Copy all verified tiles at native levels, without remapping or a byte cutoff."""
    destination.mkdir(parents=True)
    manifest = json.loads((twin / "manifest.json").read_text())
    space, reconstruction = item["space"], item["report"]
    require(manifest["scan"] == space["scan"] and manifest["origin_world"] == space["origin_world"]
            and manifest["datum_z"] == space["datum_z"] and manifest["region"]["min"] == space["min"]
            and manifest["region"]["max"] == space["max"], "Tiled coordinate frame or crop changed")
    require(bool(manifest["tiles"]), "No reconstructed tiles within the requested region")
    manifest = copy.deepcopy(manifest)
    manifest["demo"] = True  # Existing app uses this flag to label built-in data.
    manifest["lods"] = [level for level in manifest["lods"] if level["level"] in levels]
    manifest["packaging"] = {"selection": "all_region_tiles", "source_region_tiles": len(manifest["tiles"]),
                             "included_tiles": len(manifest["tiles"]), "omitted_tiles": 0,
                             "native_lod_levels": list(levels), "lod_remapping": False}
    manifest["source"]["mesh"] = f"nksr/{space['id']}/mesh.ply"
    manifest["source"]["sha256_mesh"] = reconstruction["output"]["sha256"]
    manifest["reconstruction"] = {"method": "NKSR", "report_sha256": item["report_sha256"]}
    files, ids, size = {}, set(), 0
    for tile in manifest["tiles"]:
        require(tile["id"] not in ids, "Duplicate pipeline tile")
        ids.add(tile["id"])
        for key in ("files", "triangles", "bytes", "bounds_lod", "reduction"):
            tile[key] = {str(level): tile[key][str(level)] for level in levels}
        for level in levels:
            key = str(level)
            relative = tile["files"][key]
            require(relative == f"tiles/{tile['id']}/lod{level}.glb" and "/" not in tile["id"] and ".." not in relative,
                    "Unsafe pipeline tile path")
            source = twin / relative
            require(source.stat().st_size == tile["bytes"][key], "Stale tile length")
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
            files[relative] = sha256(target)
            size += target.stat().st_size
    manifest["totals"] = {"triangles_lod0": sum(t["triangles"].get("0", 0) for t in manifest["tiles"]),
                          "bytes_lod0": sum(t["bytes"].get("0", 0) for t in manifest["tiles"]), "bytes": size}
    for level in levels:
        manifest["totals"][f"triangles_lod{level}"] = sum(t["triangles"][str(level)] for t in manifest["tiles"])
        manifest["totals"][f"bytes_lod{level}"] = sum(t["bytes"][str(level)] for t in manifest["tiles"])
    write_json(destination / "manifest.json", manifest)
    seams = json.loads((twin / "seams.json").read_text())
    seams["results"] = [result for result in seams["results"] if int(result["lod"]) in levels]
    require({(r["tile"], r["lod"], r["glb_sha256"]) for r in seams["results"]}
            == {(tile["id"], str(level), files[tile["files"][str(level)]]) for tile in manifest["tiles"] for level in levels},
            "Missing/stale packaged seam proof")
    write_json(destination / "seams.json", seams)
    for source, name in ((item["report_path"], "reconstruction.json"), (audit_path, "source-audit.json"),
                         (preparation_path, "preparation.json")):
        shutil.copy2(source, destination / name)
    if space["id"] == "HG_MainHall":
        shutil.copy2(TOOLS / "markers/markers.json", destination / "markers.json")
    (destination / "LICENSE.txt").write_text(
        f"Derived from LaMAR {space['source']} NavVis data (ETH Zurich / Microsoft). CC BY-SA 4.0.\n"
        "https://lamar.ethz.ch/\nhttps://creativecommons.org/licenses/by-sa/4.0/\n"
        "Changes: NKSR surface reconstruction, vertex RGB transfer, centroid region/tile selection, "
        "boundary-preserving decimation and axis conversion. All region tiles included.\n")
    configuration = json.loads((twin / "config.json").read_text())
    cropped_faces = sum(tile["triangles"] for tile in json.loads((twin / "chunks.json").read_text()))
    region_a = space["id"] == "HG_MainHall" and space["min"] == [-20, -5, -1] and space["max"] == [70, 32, 8]
    hard_cap = 1_500_000 if region_a else 5_000_000
    proof = {"schema": "nksr-packaged/1", "method": "NKSR", "completed": True, "scan": space["scan"],
             "space": space["id"], "manifest_sha256": sha256(destination / "manifest.json"), "files": files,
             "source_audit_sha256": sha256(audit_path), "preparation_sha256": sha256(preparation_path),
             "reconstruction_report_sha256": item["report_sha256"], "input_cloud_sha256": reconstruction["input"]["sha256"],
             "reconstructed_mesh_sha256": reconstruction["output"]["sha256"],
             "identity_alignment_sha256": hashlib.sha256(IDENTITY.encode()).hexdigest(),
             "selection": manifest["packaging"], "package_lods": list(levels), "totals": manifest["totals"],
             "reduction": {"raw_reconstruction_triangles": reconstruction["output"]["triangles"],
                 "cropped_source_triangles": cropped_faces, "requested_lod0_ratio": configuration["target_ratio"],
                 "regional_lod0_hard_cap": hard_cap, "reserve_fraction": .8, "regional_lod0_target_with_reserve": int(hard_cap*.8),
                 "effective_lod0_ratio": min(configuration["target_ratio"], hard_cap*.8/max(cropped_faces, 1)),
                 "packaged_triangles_by_lod": {str(level): manifest["totals"][f"triangles_lod{level}"] for level in levels}},
             "runtime_triangle_budget": {
                 "accounting": "Each resident tile counts selected visual LOD plus LOD2 collider; loader falls back 0 to 1 to 2 before omitting a tile",
                 "shipped_default": 800000, "persisted_choices": [800000, 1200000, 500000],
                 "visual_plus_collider_by_tile": {t["id"]: {level: n+t["triangles"]["2"] for level, n in t["triangles"].items()} for t in manifest["tiles"]},
                 "tiles_that_cannot_fit_alone_at_lod2": {str(budget): [t["id"] for t in manifest["tiles"] if 2*t["triangles"]["2"] > budget]
                                                       for budget in (500000, 800000, 1200000)}},
             "crop_policy": "Exact audited bounds select triangle centroids; retained triangles may extend beyond bounds",
             "colour_policy": "NKSR mesh RGB, existing pipeline colour sampling, normalized COLOR_0 and unlit material",
             "normal_policy": "Input oriented normals used by NKSR; existing unlit GLB exporter omits normals"}
    write_json(destination / "nksr-provenance.json", proof)
    return proof


def tree_hashes(folder):
    folder = Path(folder)
    require(not folder.is_symlink() and not any(p.is_symlink() for p in folder.rglob("*")), "Baseline/output must not contain symlinks")
    return {p.relative_to(folder).as_posix(): sha256(p) for p in sorted(folder.rglob("*")) if p.is_file()}


def baseline_state(folder):
    folder = Path(folder)
    catalog = json.loads((folder / "spaces.json").read_text())
    require(catalog.get("schema") == "twin-spaces/1" and len(catalog["spaces"]) == 4
            and {s["id"] for s in catalog["spaces"]} == SPACE_IDS, "Baseline must retain the existing four-space catalog")
    require(not (folder / "nksr-trial.json").exists(), "Use the original baseline packaged tree, not an earlier trial")
    preserved = {}
    for space in catalog["spaces"]:
        path = folder / space["id"]
        require(isinstance(space["packaged"], bool), "Invalid baseline packaged flag")
        require(not space["packaged"] or path.is_dir(), "Baseline is missing a packaged space")
        if space["id"] == "HG_MainHall":
            require(space["packaged"], "Existing MainHall must already be packaged to preserve the catalog exactly")
        else:
            preserved[space["id"]] = {"packaged": space["packaged"], "present": path.is_dir(),
                                      "files": tree_hashes(path) if path.is_dir() else {}}
    return {"catalog_sha256": sha256(folder / "spaces.json"), "preserved_spaces": preserved, "files": tree_hashes(folder)}


@contextmanager
def staging_directory(output):
    with tempfile.TemporaryDirectory(prefix=".nksr-package-", dir=output) as temporary:
        stage = Path(temporary)
        try:
            yield stage
        except BaseException:
            retained = output / ("failed-"+stage.name.lstrip("."))
            require(not retained.exists(), "Failed-run retention path already exists")
            os.rename(stage, retained)
            print(f"Incomplete run retained for diagnosis (not published): {retained}", flush=True)
            raise


def package(meshes, audit_path, output, catalog_path, python=sys.executable, workers=1, target_ratio=1,
            selected=None, baseline_packaged=None, levels=None):
    meshes, audit_path, output = Path(meshes).resolve(), Path(audit_path).resolve(), Path(output).resolve()
    selected = list(SPACE_IDS) if selected is None else list(selected)
    trial = set(selected) != SPACE_IDS
    require(not trial or selected == ["HG_MainHall"], "Only the MainHall partial trial is supported")
    require((baseline_packaged is not None) == trial, "MainHall trial requires --baseline-packaged; full reconstruction does not")
    levels = sorted(levels if levels is not None else ([0, 1, 2] if trial else [2]))
    require(len(set(levels)) == len(levels) and 2 in levels and set(levels) <= {0, 1, 2}, "Choose native LOD levels including 2")
    baseline_snapshot = None
    if trial:
        baseline_packaged = Path(baseline_packaged).resolve()
        baseline_snapshot = baseline_state(baseline_packaged)
        catalog_path = baseline_packaged / "spaces.json"
    require(workers in (1, 2) and 0 < target_ratio <= 1, "Invalid pipeline settings")
    for protected in (ROOT / "work/twin", ROOT / "TwinInspector/Assets"):
        require(not output.is_relative_to(protected.resolve()), "Output cannot modify baseline or Unity assets")
    require(not any((output / child).exists() for child in ("twin", "packaged", "package-run.json")),
            "Output already contains packaging results; use a fresh output root")
    audit, catalog, preparation, checked = validate_inputs(meshes, audit_path, catalog_path, selected)
    output.mkdir(parents=True, exist_ok=True)
    run = {"schema": "nksr-package-run/1", "method": "NKSR", "spaces": [],
           "pipeline_sha256": sha256(TOOLS / "twin_pipeline.py"), "target_ratio": target_ratio,
           "reconstructed_spaces": selected, "package_lods": levels, "partial_trial": trial}
    with staging_directory(output) as stage:
        (stage / "twin").mkdir()
        if trial:
            shutil.copytree(baseline_packaged, stage / "packaged")
            os.rename(stage / "packaged/HG_MainHall", stage / "baseline-MainHall")
        else:
            (stage / "packaged").mkdir()
        proofs = {}
        for item in checked:
            space = item["space"]
            print(f"Tiling complete NKSR region: {space['id']}", flush=True)
            session = stage / "sessions" / space["id"] / space["scan"]
            (session / "proc/meshes").mkdir(parents=True)
            (session / "proc/meshes/mesh.ply").symlink_to(item["mesh"])
            (session / "proc/alignment_global.txt").write_text(IDENTITY)
            twin = stage / "twin" / space["id"]
            commands = run_pipeline(session, twin, space, python, workers, target_ratio)
            require(sha256(item["mesh"]) == item["report"]["output"]["sha256"], "Mesh changed during packaging")
            destination = stage / "packaged" / space["id"]
            proof = package_levels(twin, destination, item, audit_path, meshes / "provenance.json", levels)
            proofs[space["id"]] = proof
            if trial:
                # Surveyed/reference marker assets belong to the existing installation's frame.
                for name in ("markers.json", "markers.json.meta"):
                    existing = baseline_packaged / "HG_MainHall" / name
                    if existing.exists():
                        shutil.copy2(existing, destination / name)
                    elif (destination / name).exists():
                        (destination / name).unlink()  # Only the new staged default, never baseline data.
            subprocess.run([str(python), "-I", str(TOOLS / "verify_tiles.py"), str(destination)], check=True)
            run["spaces"].append({"id": space["id"], "manifest_sha256": proof["manifest_sha256"],
                                  "tiles": proof["selection"]["included_tiles"], "files": len(proof["files"]),
                                  "totals": proof["totals"], "reduction": proof["reduction"], "pipeline_commands": commands})
        if trial:
            current = baseline_state(baseline_packaged)
            require(current == baseline_snapshot, "Baseline packaged data changed during processing")
            staged_files = tree_hashes(stage / "packaged")
            preserved_files = {path: checksum for path, checksum in baseline_snapshot["files"].items() if not path.startswith("HG_MainHall/")}
            require({path: checksum for path, checksum in staged_files.items() if not path.startswith("HG_MainHall/")} == preserved_files,
                    "Trial changed data outside MainHall")
            trial_proof = {"schema": "nksr-trial-package/1", "completed": True, "reconstructed_spaces": ["HG_MainHall"],
                "baseline_catalog_sha256": baseline_snapshot["catalog_sha256"], "catalog_sha256": sha256(stage / "packaged/spaces.json"),
                "preserved_spaces": baseline_snapshot["preserved_spaces"], "package_lods": levels,
                "reconstructed": {"HG_MainHall": {"manifest_sha256": proofs["HG_MainHall"]["manifest_sha256"],
                    "provenance_sha256": sha256(stage / "packaged/HG_MainHall/nksr-provenance.json")}}}
            write_json(stage / "packaged/nksr-trial.json", trial_proof)
        else:
            for space in catalog["spaces"]:
                space["packaged"] = True
            write_json(stage / "packaged/spaces.json", catalog)
        # Recheck the whole provenance chain before publishing any completed dataset.
        validate_inputs(meshes, audit_path, catalog_path, selected)
        require(not any((output / child).exists() for child in ("twin", "packaged", "package-run.json")), "Output appeared during processing")
        run["completed"] = True
        write_json(stage / "package-run.json", run)
        os.rename(stage / "twin", output / "twin")
        os.rename(stage / "packaged", output / "packaged")
        os.rename(stage / "package-run.json", output / "package-run.json")
    print(f"Verified {', '.join(selected)} at native LODs {levels}: {output / 'packaged'}", flush=True)
    return run


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--meshes", type=Path, required=True)
    parser.add_argument("--audit", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, default=ROOT / "TwinInspector/Assets/StreamingAssets/twins/spaces.json")
    parser.add_argument("--space", action="append", help="For the trial: HG_MainHall; omitted retains full reconstruction mode")
    parser.add_argument("--baseline-packaged", type=Path, help="Existing app packaged tree; required for the MainHall-only trial")
    parser.add_argument("--package-lods", type=int, choices=(0, 1, 2), nargs="+", help="Native levels, including 2; MainHall trial defaults to 0 1 2")
    parser.add_argument("--python", type=Path, default=Path(sys.executable), help="Python with requirements-twin dependencies")
    parser.add_argument("--workers", type=int, choices=(1, 2), default=1)
    parser.add_argument("--target-ratio", type=float, default=1, help="LOD0 target ratio before existing regional hard cap")
    parser.add_argument("--check-only", action="store_true", help="Verify all input provenance/meshes without creating outputs")
    args = parser.parse_args()
    if args.check_only:
        if args.baseline_packaged:
            baseline_state(args.baseline_packaged)
            args.catalog = args.baseline_packaged / "spaces.json"
        validate_inputs(args.meshes, args.audit, args.catalog, args.space)
        print("Selected reconstructed mesh inputs and provenance verified.")
    else:
        package(args.meshes, args.audit, args.output, args.catalog, args.python, args.workers, args.target_ratio,
                args.space, args.baseline_packaged, args.package_lods)


if __name__ == "__main__":
    main()
