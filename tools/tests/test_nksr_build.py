"""APK provenance/budget rejection and reversible build staging; synthetic data only."""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("build_nksr_apk", Path(__file__).resolve().parents[1] / "build_nksr_apk.py")
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)
verify = build.verifier


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def fixture(root):
    source = root / "packaged"
    spaces = []
    for ident in sorted(verify.NKSR_SPACES):
        space = {"id": ident, "scan": "synthetic_scan", "packaged": True}
        spaces.append(space)
        folder = source / ident
        tile = folder / "tiles/test/lod2.glb"
        tile.parent.mkdir(parents=True)
        tile.write_bytes(b"synthetic GLB hash fixture")
        selection = {"selection": "all_region_tiles", "source_region_tiles": 1, "included_tiles": 1, "omitted_tiles": 0}
        for name in ("reconstruction", "preparation", "source-audit"):
            save(folder / (name + ".json"), {"fixture": name})
        save(folder / "reconstruction.json", {"schema": "nksr-reconstruction/1", "method": "NKSR", "completed": True,
                                             "input": {"sha256": "b" * 64}, "output": {"sha256": "a" * 64},
                                             "preparation": {"sha256": verify.sha256(folder / "preparation.json")}})
        report_hash = verify.sha256(folder / "reconstruction.json")
        manifest = {"scan": space["scan"], "demo": True, "packaging": selection,
                    "source": {"sha256_mesh": "a" * 64},
                    "reconstruction": {"method": "NKSR", "report_sha256": report_hash},
                    "tiles": [{"files": {"2": "tiles/test/lod2.glb"}}]}
        save(folder / "manifest.json", manifest)
        proof = {"schema": "nksr-packaged/1", "method": "NKSR", "completed": True,
                 "space": ident, "scan": space["scan"], "manifest_sha256": verify.sha256(folder / "manifest.json"),
                 "files": {"tiles/test/lod2.glb": verify.sha256(tile)}, "selection": selection,
                 "reconstruction_report_sha256": report_hash,
                 "reconstructed_mesh_sha256": "a" * 64, "input_cloud_sha256": "b" * 64,
                 "preparation_sha256": verify.sha256(folder / "preparation.json"),
                 "source_audit_sha256": verify.sha256(folder / "source-audit.json")}
        save(folder / "nksr-provenance.json", proof)
    save(source / "spaces.json", {"schema": "twin-spaces/1", "spaces": spaces})
    apk = root / "synthetic.apk"
    with zipfile.ZipFile(apk, "w") as z:
        for p in source.rglob("*"):
            if p.is_file():
                z.write(p, "assets/twins/" + p.relative_to(source).as_posix())
        z.writestr("AndroidManifest.xml", b"synthetic manifest; identity checked separately with aapt")
        z.writestr("lib/arm64-v8a/libil2cpp.so", b"test")
        z.writestr("lib/arm64-v8a/libunity.so", b"test")
    return source, apk


def trial_fixture(root):
    source, apk = fixture(root)
    catalog = json.loads((source / "spaces.json").read_text())
    for space in catalog["spaces"]:
        if space["id"] in ("HG_E3_Hall", "HG_E7_Hall"):
            space["packaged"] = False
            shutil.rmtree(source / space["id"])
    save(source / "spaces.json", catalog)
    baseline = root / "baseline"
    shutil.copytree(source, baseline)
    folder = source / "HG_MainHall"
    manifest = json.loads((folder / "manifest.json").read_text())
    proof = json.loads((folder / "nksr-provenance.json").read_text())
    for level in ("0", "1"):
        relative = f"tiles/test/lod{level}.glb"
        (folder / relative).write_bytes(b"synthetic detail level " + level.encode())
        manifest["tiles"][0]["files"][level] = relative
        proof["files"][relative] = verify.sha256(folder / relative)
    save(folder / "manifest.json", manifest)
    proof["manifest_sha256"] = verify.sha256(folder / "manifest.json")
    save(folder / "nksr-provenance.json", proof)
    trial = {"schema": "nksr-trial-package/1", "completed": True, "reconstructed_spaces": ["HG_MainHall"],
             "catalog_sha256": verify.sha256(source / "spaces.json"),
             "baseline_catalog_sha256": verify.sha256(baseline / "spaces.json"),
             "preserved_spaces": {space["id"]: {"packaged": space["packaged"],
                                  "present": (baseline / space["id"]).exists(),
                                  "files": verify.file_tree(baseline / space["id"])}
                                  for space in catalog["spaces"] if space["id"] != "HG_MainHall"},
             "reconstructed": {"HG_MainHall": {"manifest_sha256": proof["manifest_sha256"],
                                                "provenance_sha256": verify.sha256(folder / "nksr-provenance.json")}}}
    save(source / "nksr-trial.json", trial)
    with zipfile.ZipFile(apk, "w") as z:
        for p in source.rglob("*"):
            if p.is_file():
                z.write(p, "assets/twins/" + p.relative_to(source).as_posix())
        for name in ("AndroidManifest.xml", "lib/arm64-v8a/libil2cpp.so", "lib/arm64-v8a/libunity.so"):
            z.writestr(name, b"synthetic APK inspection fixture")
    return source, apk, baseline


class NksrBuildTests(unittest.TestCase):
    def test_mainhall_trial_keeps_native_lods_and_unchanged_other_spaces(self):
        with tempfile.TemporaryDirectory() as temp, contextlib.redirect_stdout(io.StringIO()):
            source, apk, baseline = trial_fixture(Path(temp))
            report = verify.verify(apk, source, nksr_mainhall=True, baseline_source=baseline)
            self.assertEqual(report["demo_lods"], [0, 1, 2])
            self.assertEqual(report["bundled_glbs"], 4)
            self.assertEqual(report["demo_tiles"], 2)
            with self.assertRaises(AssertionError):
                verify.validate_nksr_source(source)
            (source / "HG_MainHall/tiles/test/lod0.glb").write_bytes(b"stale detailed mesh")
            with self.assertRaises(AssertionError):
                verify.validate_nksr_trial(source, baseline)

    def test_mainhall_trial_rejects_other_space_change_or_missing_baseline(self):
        for change in ("preserved_file", "new_unpacked_folder", "baseline_file"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as temp:
                source, _, baseline = trial_fixture(Path(temp))
                if change == "preserved_file":
                    (source / "CAB_MainHall/tiles/test/lod2.glb").write_bytes(b"changed CAB")
                elif change == "new_unpacked_folder":
                    (source / "HG_E7_Hall").mkdir()
                else:
                    (baseline / "CAB_MainHall/tiles/test/lod2.glb").write_bytes(b"different baseline")
                with self.assertRaises(AssertionError):
                    verify.validate_nksr_trial(source, baseline)

    def test_explicit_budgets_and_saved_source(self):
        with tempfile.TemporaryDirectory() as temp, contextlib.redirect_stdout(io.StringIO()):
            source, apk = fixture(Path(temp))
            report = verify.verify(apk, source, require_nksr=True)
            self.assertEqual(report["max_apk_bytes"], 95_000_000)
            self.assertEqual(report["max_space_bytes"], 15_000_000)
            with self.assertRaisesRegex(AssertionError, "APK must remain"):
                verify.verify(apk, source, max_apk_bytes=apk.stat().st_size)
            with self.assertRaises(AssertionError):
                verify.verify(apk, source, max_space_bytes=10)
            verify.verify(apk, source, max_apk_bytes=apk.stat().st_size + 1, max_space_bytes=100_000, require_nksr=True)

    def test_stale_or_incomplete_nksr_never_passes(self):
        for corruption in ("method", "completed", "tile", "extra_tile", "manifest", "omitted", "report", "unpackaged"):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory() as temp:
                source, _ = fixture(Path(temp))
                folder = source / "HG_MainHall"
                proof_path = folder / "nksr-provenance.json"
                proof = json.loads(proof_path.read_text())
                if corruption == "method":
                    proof["method"] = "Poisson"
                elif corruption == "completed":
                    proof["completed"] = False
                elif corruption == "tile":
                    (folder / "tiles/test/lod2.glb").write_bytes(b"stale")
                elif corruption == "extra_tile":
                    (folder / "old.glb").write_bytes(b"old")
                elif corruption == "report":
                    (folder / "reconstruction.json").write_text("{}")
                elif corruption == "unpackaged":
                    catalog = json.loads((source / "spaces.json").read_text())
                    catalog["spaces"][0]["packaged"] = False
                    save(source / "spaces.json", catalog)
                else:
                    manifest = json.loads((folder / "manifest.json").read_text())
                    manifest["packaging"]["omitted_tiles"] = 1
                    save(folder / "manifest.json", manifest)
                    if corruption == "omitted":
                        proof["manifest_sha256"] = verify.sha256(folder / "manifest.json")
                        proof["selection"] = manifest["packaging"]
                save(proof_path, proof)
                with self.assertRaises(AssertionError):
                    verify.validate_nksr_source(source)

    def test_staging_restores_exact_baseline_bytes_and_meta(self):
        for partial in (False, True):
            with self.subTest(partial=partial), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                project = root / "project"
                twins = project / "Assets/StreamingAssets/twins"
                settings = project / "ProjectSettings"
                twins.mkdir(parents=True)
                settings.mkdir()
                (twins / "original.glb").write_bytes(b"original mesh\x00\xff")
                (twins / "original.glb.meta").write_bytes(b"original-guid\r\n")
                (settings / "ProjectSettings.asset").write_bytes(b"original identity\r\n")
                twins.with_suffix(".meta").write_bytes(b"folder-guid\r\n")
                before = build.tree_hashes(project)
                source, _ = fixture(root)
                staging = build.Staging(source, project, root / "recovery")
                staging.prepare()
                if partial:
                    record = json.loads(staging.path.read_text())
                    # Simulate termination after backing up a directory but before its replacement.
                    entry = record["directories"][0]
                    os.replace(entry["target"], entry["backup"])
                else:
                    staging.activate()
                    (settings / "ProjectSettings.asset").write_bytes(b"temporary NKSR identity")
                    (settings / "new.asset").write_bytes(b"Unity generated")
                    twins.with_suffix(".meta").write_bytes(b"temporary-guid")
                build.restore(staging.path)
                self.assertEqual(build.tree_hashes(project), before)
                build.restore(staging.path)  # Recovery remains safe to repeat.
                self.assertEqual(build.tree_hashes(project), before)


if __name__ == "__main__":
    unittest.main()
