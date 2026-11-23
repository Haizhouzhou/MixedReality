"""Check APK integrity, ARM64-only libraries and the exact bundled LOD2 spaces."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

DEFAULT_SOURCE = Path(__file__).resolve().parents[1] / "TwinInspector/Assets/StreamingAssets/twins"
NKSR_SPACES = {"HG_MainHall", "HG_E3_Hall", "HG_E7_Hall", "CAB_MainHall"}


def sha256(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def validate_nksr_source(source, required_spaces=None, allow_full_lods=False):
    """Check completed NKSR packaging proofs for the requested spaces."""
    source = Path(source).resolve()
    required_spaces = NKSR_SPACES if required_spaces is None else set(required_spaces)
    catalog = json.loads((source / "spaces.json").read_text())
    spaces = catalog["spaces"]
    assert catalog["schema"] == "twin-spaces/1"
    assert len(spaces) == 4 and {s["id"] for s in spaces} == NKSR_SPACES
    assert required_spaces and required_spaces <= NKSR_SPACES
    assert all(s.get("packaged") is True for s in spaces if s["id"] in required_spaces), "Requested NKSR spaces must be packaged"
    for space in spaces:
        if space["id"] not in required_spaces:
            continue
        folder = source / space["id"]
        manifest = json.loads((folder / "manifest.json").read_text())
        proof = json.loads((folder / "nksr-provenance.json").read_text())
        assert proof["schema"] == "nksr-packaged/1" and proof["method"] == "NKSR" and proof["completed"] is True
        assert proof["space"] == space["id"] and proof["scan"] == manifest["scan"] == space["scan"]
        assert proof["manifest_sha256"] == sha256(folder / "manifest.json"), "Stale NKSR manifest proof"
        selection = manifest["packaging"]
        assert selection["selection"] == "all_region_tiles" and selection["omitted_tiles"] == 0
        assert selection["source_region_tiles"] == selection["included_tiles"] == len(manifest["tiles"]) > 0
        assert proof["selection"] == selection, "NKSR packaging coverage differs from proof"
        assert manifest["reconstruction"]["method"] == "NKSR"
        assert manifest["reconstruction"]["report_sha256"] == proof["reconstruction_report_sha256"]
        assert sha256(folder / "reconstruction.json") == proof["reconstruction_report_sha256"]
        assert sha256(folder / "preparation.json") == proof["preparation_sha256"]
        assert sha256(folder / "source-audit.json") == proof["source_audit_sha256"]
        reconstruction = json.loads((folder / "reconstruction.json").read_text())
        assert reconstruction["schema"] == "nksr-reconstruction/1" and reconstruction["method"] == "NKSR" and reconstruction["completed"] is True
        assert reconstruction["output"]["sha256"] == proof["reconstructed_mesh_sha256"] == manifest["source"]["sha256_mesh"]
        assert reconstruction["input"]["sha256"] == proof["input_cloud_sha256"]
        assert reconstruction["preparation"]["sha256"] == proof["preparation_sha256"]
        expected = set()
        for tile in manifest["tiles"]:
            levels = set(tile["files"])
            assert levels == {"2"} or allow_full_lods and "2" in levels and levels <= {"0", "1", "2"}
            for relative in tile["files"].values():
                path = (folder / relative).resolve()
                assert path.is_relative_to(folder.resolve()) and relative.endswith(".glb"), "Unsafe NKSR tile path"
                assert relative not in expected, "Duplicate NKSR tile path"
                expected.add(relative)
                assert sha256(path) == proof["files"][relative], "Stale NKSR GLB proof"
        assert set(proof["files"]) == expected
        assert {p.relative_to(folder).as_posix() for p in folder.rglob("*.glb")} == expected, "Unlisted NKSR GLBs"
    expected_glbs = set()
    for space in spaces:
        if space.get("packaged"):
            for tile in json.loads((source / space["id"] / "manifest.json").read_text())["tiles"]:
                expected_glbs.update(space["id"] + "/" + relative for relative in tile["files"].values())
    assert {p.relative_to(source).as_posix() for p in source.rglob("*.glb")} == expected_glbs, "Unlisted space GLBs"
    return spaces


def file_tree(folder):
    folder = Path(folder)
    return {p.relative_to(folder).as_posix(): sha256(p) for p in folder.rglob("*") if p.is_file()}


def validate_nksr_trial(source, baseline_source=None):
    """Exactly HG MainHall changes; the catalog and other baseline space files do not."""
    source = Path(source)
    spaces = validate_nksr_source(source, {"HG_MainHall"}, allow_full_lods=True)
    proof = json.loads((source / "nksr-trial.json").read_text())
    assert proof["schema"] == "nksr-trial-package/1" and proof["completed"] is True
    assert proof["reconstructed_spaces"] == ["HG_MainHall"]
    assert proof["catalog_sha256"] == proof["baseline_catalog_sha256"] == sha256(source / "spaces.json")
    assert set(proof["preserved_spaces"]) == NKSR_SPACES - {"HG_MainHall"}
    assert set(proof["reconstructed"]) == {"HG_MainHall"}
    for name, digest in (("manifest.json", "manifest_sha256"), ("nksr-provenance.json", "provenance_sha256")):
        assert sha256(source / "HG_MainHall" / name) == proof["reconstructed"]["HG_MainHall"][digest]
    for space in spaces:
        if space["id"] == "HG_MainHall":
            continue
        preserved = proof["preserved_spaces"][space["id"]]
        folder = source / space["id"]
        assert preserved["packaged"] == space["packaged"] and preserved["present"] == folder.exists()
        assert preserved["files"] == file_tree(folder), "A baseline space changed in the MainHall trial"
        if baseline_source is not None:
            baseline = Path(baseline_source) / space["id"]
            assert preserved["present"] == baseline.exists() and preserved["files"] == file_tree(baseline), "Trial does not preserve the selected baseline"
    if baseline_source is not None:
        assert sha256(Path(baseline_source) / "spaces.json") == proof["baseline_catalog_sha256"]
    return spaces


def verify(apk, source=DEFAULT_SOURCE, max_apk_bytes=95_000_000, max_space_bytes=15_000_000, require_nksr=False,
           nksr_mainhall=False, baseline_source=None):
    apk = Path(apk)
    assert max_apk_bytes > 0 and max_space_bytes > 0, "Budgets must be positive"
    assert apk.stat().st_size < max_apk_bytes, f"APK must remain below {max_apk_bytes:,} bytes"
    source = Path(source)
    spaces = json.loads((source / "spaces.json").read_text())["spaces"]
    assert not (require_nksr and nksr_mainhall), "Choose a full NKSR or MainHall-only proof"
    if nksr_mainhall:
        validate_nksr_trial(source, baseline_source)
    elif require_nksr:
        validate_nksr_source(source)
    with zipfile.ZipFile(apk) as package:
        assert package.testzip() is None, "APK ZIP integrity failure"
        names = set(package.namelist())
        assert not any(name.lower().endswith((".sentis", ".onnx", ".pt")) for name in names), "External models must not be bundled"
        assert json.loads(package.read("assets/twins/spaces.json"))["spaces"] == spaces, "Packaged space index differs from source"
        if nksr_mainhall:
            assert package.read("assets/twins/nksr-trial.json") == (source / "nksr-trial.json").read_bytes()
            assert package.read("assets/twins/spaces.json") == (source / "spaces.json").read_bytes()
            trial = json.loads((source / "nksr-trial.json").read_text())
            for ident, preserved in trial["preserved_spaces"].items():
                prefix = f"assets/twins/{ident}/"
                # Unity excludes .meta files from StreamingAssets in Android builds.
                expected_files = {prefix + relative: digest for relative, digest in preserved["files"].items()
                                  if not relative.endswith(".meta")}
                actual_files = {item.filename for item in package.infolist() if not item.is_dir() and item.filename.startswith(prefix)}
                assert actual_files == set(expected_files), "APK changed a preserved baseline space's files"
                for filename, digest in expected_files.items():
                    assert hashlib.sha256(package.read(filename)).hexdigest() == digest, "APK changed a preserved baseline space's bytes"
        expected, demo_bytes, bundled_lods, tile_count = set(), {}, set(), 0
        for space in spaces:
            if not space.get("packaged"):
                continue  # transferred to the headset with push_space.py
            prefix = f"assets/twins/{space['id']}/"
            manifest_bytes = package.read(prefix + "manifest.json")
            assert manifest_bytes == (source / space["id"] / "manifest.json").read_bytes(), "Packaged manifest differs from source"
            manifest = json.loads(manifest_bytes)
            assert manifest["demo"] is True and manifest["scan"] == space["scan"], space["id"]
            if require_nksr or nksr_mainhall and space["id"] == "HG_MainHall":
                for name in ("nksr-provenance.json", "reconstruction.json", "preparation.json", "source-audit.json"):
                    assert package.read(prefix + name) == (source / space["id"] / name).read_bytes(), "APK NKSR provenance differs from source"
            for tile in manifest["tiles"]:
                levels = set(tile["files"])
                assert levels == {"2"} or nksr_mainhall and space["id"] == "HG_MainHall" and "2" in levels and levels <= {"0", "1", "2"}, "Unexpected packaged LOD set"
                tile_count += 1
                bundled_lods.update(map(int, levels))
                for relative in tile["files"].values():
                    expected.add(prefix + relative)
                    assert hashlib.sha256(package.read(prefix + relative)).digest() == hashlib.sha256((source / space["id"] / relative).read_bytes()).digest(), prefix + relative
            demo_bytes[space["id"]] = sum(item.file_size for item in package.infolist() if item.filename.startswith(prefix))
            assert demo_bytes[space["id"]] <= max_space_bytes, space["id"]
        assert {name for name in names if name.endswith(".glb")} == expected
        abis = {name.split("/")[1] for name in names if name.startswith("lib/") and name.endswith(".so")}
        assert abis == {"arm64-v8a"}, abis
        assert "lib/arm64-v8a/libil2cpp.so" in names
        assert "lib/arm64-v8a/libunity.so" in names
        assert "AndroidManifest.xml" in names
    result = {"status": "PASS", "apk": apk.name, "bytes": apk.stat().st_size,
              "sha256": sha256(apk), "abis": sorted(abis), "demo_tiles": tile_count, "bundled_glbs": len(expected),
              "space_uncompressed_bytes": demo_bytes, "demo_lods": sorted(bundled_lods),
              "source": str(source.resolve()), "max_apk_bytes": max_apk_bytes, "max_space_bytes": max_space_bytes,
              "nksr_required": require_nksr,
              "nksr_mainhall": nksr_mainhall,
              "checks": ["ZIP CRC", "ARM64 IL2CPP", "exact source manifests and GLB hashes", "only declared LOD GLBs" if nksr_mainhall else "no full-detail GLBs",
                         f"each space <={max_space_bytes:,} bytes", f"APK <{max_apk_bytes:,} bytes", "no model files bundled"]}
    if require_nksr:
        result["checks"].append("all four completed NKSR packaging proofs and full region tile coverage")
    if nksr_mainhall:
        result["checks"].append("HG MainHall NKSR proof and full tile coverage; other three spaces and catalog unchanged")
    print(json.dumps(result, indent=2))
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE, help="Exact packaged dataset root, even after restoring Unity assets")
    parser.add_argument("--max-apk-bytes", type=int, default=95_000_000)
    parser.add_argument("--max-space-bytes", type=int, default=15_000_000)
    parser.add_argument("--require-nksr", action="store_true", help="Require all four complete NKSR spaces and bound provenance")
    parser.add_argument("--nksr-mainhall", action="store_true", help="Require exactly the MainHall NKSR trial and preserve the other spaces")
    parser.add_argument("--baseline-source", type=Path, help="Compare preserved trial files with this original packaged tree")
    args = parser.parse_args()
    verify(args.apk, args.source, args.max_apk_bytes, args.max_space_bytes, args.require_nksr, args.nksr_mainhall, args.baseline_source)
