"""Check APK integrity, ARM64-only libraries and the exact bundled LOD2 spaces."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def verify(apk):
    apk = Path(apk)
    assert apk.stat().st_size < 95_000_000, "APK must remain below 95 MB"
    source = Path(__file__).resolve().parents[1] / "TwinInspector/Assets/StreamingAssets/twins"
    spaces = json.loads((source / "spaces.json").read_text())["spaces"]
    with zipfile.ZipFile(apk) as package:
        assert package.testzip() is None, "APK ZIP integrity failure"
        names = set(package.namelist())
        assert not any(name.lower().endswith((".sentis", ".onnx", ".pt")) for name in names), "External models must not be bundled"
        assert json.loads(package.read("assets/twins/spaces.json"))["spaces"] == spaces, "Packaged space index differs from source"
        expected, demo_bytes = set(), {}
        for space in spaces:
            if not space.get("packaged"):
                continue  # transferred to the headset with push_space.py
            prefix = f"assets/twins/{space['id']}/"
            manifest = json.loads(package.read(prefix + "manifest.json"))
            assert manifest["demo"] is True and manifest["scan"] == space["scan"], space["id"]
            for tile in manifest["tiles"]:
                assert set(tile["files"]) == {"2"}, "Only LOD2 belongs in the APK"
                relative = tile["files"]["2"]
                expected.add(prefix + relative)
                assert hashlib.sha256(package.read(prefix + relative)).digest() == hashlib.sha256((source / space["id"] / relative).read_bytes()).digest(), prefix + relative
            demo_bytes[space["id"]] = sum(item.file_size for item in package.infolist() if item.filename.startswith(prefix))
            assert demo_bytes[space["id"]] <= 15_000_000, space["id"]
        assert {name for name in names if name.endswith(".glb")} == expected
        abis = {name.split("/")[1] for name in names if name.startswith("lib/") and name.endswith(".so")}
        assert abis == {"arm64-v8a"}, abis
        assert "lib/arm64-v8a/libil2cpp.so" in names
        assert "lib/arm64-v8a/libunity.so" in names
        assert "AndroidManifest.xml" in names
    digest = hashlib.sha256()
    with apk.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    result = {"status": "PASS", "apk": apk.name, "bytes": apk.stat().st_size,
              "sha256": digest.hexdigest(), "abis": sorted(abis), "demo_tiles": len(expected),
              "space_uncompressed_bytes": demo_bytes, "demo_lods": [2],
              "checks": ["ZIP CRC", "ARM64 IL2CPP", "exact source space GLB hashes", "no full-detail GLBs", "each space <=15 MB", "APK <95 MB", "no model files bundled"]}
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    verify(parser.parse_args().apk)
