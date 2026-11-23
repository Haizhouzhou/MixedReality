"""Simulated-device tests: identity guards and manifest-last, data-preserving update."""
from argparse import Namespace
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest import mock
import zipfile

TOOLS = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("install", TOOLS / "install_nksr_mainhall.py")
install = importlib.util.module_from_spec(spec)
spec.loader.exec_module(install)


def hash_bytes(data):
    return hashlib.sha256(data).hexdigest()


def fixture(root):
    packaged = root / "packaged"
    main = packaged / install.SPACE
    main.mkdir(parents=True)
    manifest = {"schema": "twin-manifest/1", "scan": "scan1", "frame": {"up": "z", "units": "m"},
                "origin_world": [20, 0, 0], "datum_z": 0,
                "region": {"name": "A", "min": [-20, -5, -1], "max": [70, 32, 8]},
                "tiles": [{"id": "x0_y0", "files": {str(n): f"tiles/x0_y0/lod{n}.glb" for n in range(3)}}]}
    proof = {"schema": "nksr-packaged/1", "method": "NKSR", "completed": True, "files": {}}
    for n, rel in manifest["tiles"][0]["files"].items():
        path = main / rel
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(b"new coloured mesh LOD" + n.encode())
        proof["files"][rel] = install.digest(path)
    (main / "manifest.json").write_text(json.dumps(manifest))
    proof["manifest_sha256"] = install.digest(main / "manifest.json")
    (main / "nksr-provenance.json").write_text(json.dumps(proof))
    old_apk, new_apk = root / "old.apk", root / "new.apk"
    for path in (old_apk, new_apk):
        with zipfile.ZipFile(path, "w") as z:
            z.writestr("assets/twins/spaces.json", '{"spaces":[]}')
            z.writestr("assets/twins/CAB_MainHall/manifest.json", "baseline CAB")
            z.writestr("assets/twins/HG_MainHall/manifest.json", json.dumps(manifest))
            z.writestr("assets/twins/HG_MainHall/tiles/x0_y0/lod2.glb", path.name)
    args = Namespace(packaged=packaged, apk=new_apk, serial="test-device", android_player=root,
                     max_apk_bytes=1_500_000_000, max_space_bytes=1_400_000_000,
                     record=root / "backup", adb=root / "never-run-adb", apply=False)
    data = {install.ACTIVE: json.dumps(manifest).encode(),
            install.PREFIX + "markers.json": b"original markers",
            install.PREFIX + "versions/old/tiles/x0_y0/lod2.glb": b"old mesh",
            "twin/HG_E3_Hall/manifest.json": b"existing E3",
            "twin/HG_E7_Hall/manifest.json": b"existing E7",
            "twin/CAB_MainHall/manifest.json": b"existing CAB",
            "annotations/scan1.json": b"user annotations", "settings.json": b"user settings",
            "models/model.onnx": b"existing external model"}
    return args, old_apk, data


class FakeDevice:
    def __init__(self, old_apk, data):
        self.apk = old_apk
        self.data = copy.deepcopy(data)
        self.actions = []
        self.original = {"/data/app/original/base.apk": "a" * 64}
        self.corrupt_push = False

    def inventory(self):
        return {p: hash_bytes(value) for p, value in self.data.items()}

    def package_snapshot(self, package):
        if package == install.ORIGINAL:
            return dict(self.original)
        return {"/data/app/current/base.apk": install.digest(self.apk)}

    def relative(self, path):
        assert path.startswith(install.REMOTE + "/"), path
        return path[len(install.REMOTE) + 1:]

    def hash(self, path):
        if path == "/data/app/current/base.apk":
            return install.digest(self.apk)
        return hash_bytes(self.data[self.relative(path)])

    def run(self, *args):
        self.actions.append(args)
        if args[0] == "get-state":
            return b"device\n"
        if args[0] == "pull":
            remote, local = args[1], Path(args[2])
            if remote == "/data/app/current/base.apk":
                shutil.copy2(self.apk, local)
            elif remote == install.REMOTE:
                local.mkdir()
                for relative, value in self.data.items():
                    p = local / relative
                    p.parent.mkdir(parents=True, exist_ok=True)
                    p.write_bytes(value)
            else:
                raise AssertionError(args)
            return b"pulled"
        if args[:2] == ("install", "-r"):
            self.apk = Path(args[2])
            return b"Success"
        if args[0] == "push":
            data = Path(args[1]).read_bytes()
            if self.corrupt_push and args[2].endswith(".glb"):
                data += b"corruption"
            self.data[self.relative(args[2])] = data
            return b"pushed"
        raise AssertionError(args)

    def shell(self, *args):
        self.actions.append(("shell", *args))
        if args[:2] in (("am", "force-stop"), ("mkdir", "-p")):
            return b""
        if args[0] in ("cp", "mv"):
            self.data[self.relative(args[2])] = self.data[self.relative(args[1])]
            if args[0] == "mv":
                del self.data[self.relative(args[1])]
            return b""
        raise AssertionError(args)


class InstallerTests(unittest.TestCase):
    def patches(self, fake, signer_mismatch=False):
        def metadata(path, player):
            return {"package": install.PACKAGE, "label": "twininspector2", "version_code": 7,
                    "signer_sha256": "mismatch" if signer_mismatch and Path(path).name == "installed-base.apk" else "a" * 64}
        return (mock.patch.object(install, "Device", return_value=fake),
                mock.patch.object(install, "android_metadata", side_effect=metadata),
                mock.patch.object(install.verifier, "validate_nksr_trial"),
                mock.patch.object(install.verifier, "verify"),
                mock.patch.object(install, "private_preferences", return_value={"available": False}))

    def call(self, args, fake, mismatch=False):
        from contextlib import ExitStack
        with ExitStack() as stack:
            for patch in self.patches(fake, mismatch):
                stack.enter_context(patch)
            return install.deploy(args)

    def test_default_check_only_backs_up_without_device_mutation(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            fake = FakeDevice(old, data)
            report = self.call(args, fake)
            self.assertFalse(report["applied"])
            self.assertEqual(fake.data, data)
            self.assertEqual(fake.apk, old)
            self.assertTrue(all(a[0] in ("get-state", "pull") for a in fake.actions))
            self.assertEqual((args.record / "files/annotations/scan1.json").read_bytes(), b"user annotations")

    def test_signer_mismatch_prevents_force_stop_install_or_push(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            args.apply = True
            fake = FakeDevice(old, data)
            with self.assertRaisesRegex(RuntimeError, "Signer mismatch"):
                self.call(args, fake, mismatch=True)
            self.assertEqual(fake.data, data)
            self.assertTrue(all(a[0] in ("get-state", "pull") for a in fake.actions))

    def test_apply_preserves_every_old_file_and_activates_all_lods_last(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            args.apply = True
            fake = FakeDevice(old, data)
            report = self.call(args, fake)
            self.assertTrue(report["applied"])
            self.assertEqual(report["status"], "complete")
            for path, value in data.items():
                if path != install.ACTIVE:
                    self.assertEqual(fake.data[path], value)
            runtime = json.loads(fake.data[install.ACTIVE])
            self.assertEqual(set(runtime["tiles"][0]["files"]), {"0", "1", "2"})
            for relative in runtime["tiles"][0]["files"].values():
                self.assertTrue(relative.startswith("versions/"))
                self.assertIn(install.PREFIX + relative, fake.data)
            self.assertEqual(fake.actions[-1][0:2], ("shell", "mv"))
            install_commands = [a for a in fake.actions if a[0] == "install"]
            self.assertEqual(install_commands, [("install", "-r", str(args.apk.resolve()))])
            self.assertNotIn("uninstall", repr(fake.actions))
            self.assertEqual(fake.original, report["original_package_apks_before"])
            self.assertIn(data[install.ACTIVE], [v for k, v in fake.data.items() if "manifest.before-nksr-" in k])

    def test_corrupt_tile_never_activates_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            args.apply = True
            fake = FakeDevice(old, data)
            fake.corrupt_push = True
            with self.assertRaisesRegex(RuntimeError, "Transferred asset hash mismatch"):
                self.call(args, fake)
            self.assertEqual(fake.data[install.ACTIVE], data[install.ACTIVE])
            self.assertFalse(any(a[:2] == ("shell", "mv") for a in fake.actions))

    def test_other_packaged_space_changes_block_update(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            args.apply = True
            with zipfile.ZipFile(args.apk, "a") as z:
                z.writestr("assets/twins/CAB_MainHall/extra.glb", b"unrequested change")
            fake = FakeDevice(old, data)
            with self.assertRaisesRegex(RuntimeError, "Other spaces"):
                self.call(args, fake)
            self.assertEqual(fake.data, data)
            self.assertTrue(all(a[0] in ("get-state", "pull") for a in fake.actions))

    def test_changed_world_origin_blocks_update_before_app_is_stopped(self):
        with tempfile.TemporaryDirectory() as temp:
            args, old, data = fixture(Path(temp))
            args.apply = True
            manifest_path = args.packaged / install.SPACE / "manifest.json"
            manifest = json.loads(manifest_path.read_text())
            manifest["origin_world"] = [100, 0, 0]
            manifest_path.write_text(json.dumps(manifest))
            proof_path = manifest_path.with_name("nksr-provenance.json")
            proof = json.loads(proof_path.read_text())
            proof["manifest_sha256"] = install.digest(manifest_path)
            proof_path.write_text(json.dumps(proof))
            fake = FakeDevice(old, data)
            with self.assertRaisesRegex(RuntimeError, "world frame"):
                self.call(args, fake)
            self.assertEqual(fake.data, data)
            self.assertTrue(all(a[0] in ("get-state", "pull") for a in fake.actions))

    def test_unsafe_paths_and_protected_annotations_fail_closed(self):
        for value in ("../annotations/a.json", "/absolute", "a/../b", "a\\b", "a\nb", "a//b"):
            with self.subTest(value=value), self.assertRaises(RuntimeError):
                install.safe_relative(value)
        with self.assertRaisesRegex(RuntimeError, "Protected file"):
            install.preserved({"annotations/a.json": "old"}, {"annotations/a.json": "new"})
        with self.assertRaisesRegex(RuntimeError, "Unexpected new file"):
            install.preserved({}, {"settings.json": "new"})


if __name__ == "__main__":
    unittest.main()
