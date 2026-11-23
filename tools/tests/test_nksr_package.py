"""Synthetic full-region integration and provenance rejection for NKSR packaging."""
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock
import numpy as np

spec = importlib.util.spec_from_file_location("nksr_package", Path(__file__).resolve().parents[1] / "nksr_package.py")
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


def save(path, value):
    path.write_text(json.dumps(value, indent=2) + "\n")


def fixture(base):
    meshes = base / "meshes"
    meshes.mkdir()
    header = ("ply\nformat binary_little_endian 1.0\nelement vertex 6\n"
              "property double x\nproperty double y\nproperty double z\n"
              "property uchar red\nproperty uchar green\nproperty uchar blue\n"
              "element face 2\nproperty list uchar uint vertex_indices\nend_header\n").encode()
    # Two separated source tiles, both required in the final bundle.
    vertices = b"".join(struct.pack("<dddBBB", *p) for p in [
        (1, 1, 1, 255, 0, 4), (2, 1, 1, 0, 255, 5), (1, 2, 1, 0, 3, 255),
        (21, 1, 1, 255, 0, 4), (22, 1, 1, 0, 255, 5), (21, 2, 1, 0, 3, 255)])
    raw = header + vertices + struct.pack("<BIIIBIII", 3, 0, 1, 2, 3, 3, 4, 5)
    audit = {"schema": "twin-nksr-source-audit/1", "sources": {}, "spaces": []}
    for name in ("HGE", "CAB"):
        audit["sources"][name] = {"scan": "synthetic_" + name, "archive": {"url": "https://example.invalid/" + name},
                                  "pointcloud": {"crc32_hex": "12345678"}, "alignment": {"sha256": "synthetic"}}
    catalog = {"schema": "twin-spaces/1", "spaces": []}
    for ident in sorted(package.SPACE_IDS):
        source = "CAB" if ident.startswith("CAB") else "HGE"
        baseline = base / (ident + "-baseline.json")
        space = {"id": ident, "source": source, "scan": audit["sources"][source]["scan"],
                 "min": [0, 0, 0], "max": [30, 30, 3], "origin_world": [10, -20, 0], "datum_z": 0,
                 "region_name": ident, "manifest_path": str(baseline)}
        save(baseline, {"scan": space["scan"], "origin_world": space["origin_world"], "datum_z": 0,
                        "region": {"min": space["min"], "max": space["max"], "name": ident}})
        space["manifest_sha256"] = package.sha256(baseline)
        audit["spaces"].append(space)
        catalog["spaces"].append({"id": ident, "name": ident, "scan": space["scan"], "building": source, "packaged": False})
    audit_path, catalog_path = base / "audit.json", base / "catalog.json"
    save(audit_path, audit)
    save(catalog_path, catalog)
    preparation = {"schema": "nksr-inputs/1", "coordinate_frame": "aligned LaMAR world, metres, Z up", "halo_metres": .3,
                   "source_audit_canonical_json_sha256": hashlib.sha256(json.dumps(audit, sort_keys=True).encode()).hexdigest(),
                   "sources": {}, "spaces": []}
    for name, source in audit["sources"].items():
        preparation["sources"][name] = dict(source, zip_crc32_verified="12345678")
    for space in audit["spaces"]:
        cloud = dict(space, crop_min_with_halo=[v-.3 for v in space["min"]], crop_max_with_halo=[v+.3 for v in space["max"]],
                     sha256="a" * 64, points=6)
        preparation["spaces"].append(cloud)
    save(meshes / "provenance.json", preparation)
    for space, cloud in zip(audit["spaces"], preparation["spaces"]):
        folder = meshes / space["id"]
        folder.mkdir()
        (folder / "mesh.ply").write_bytes(raw)
        report = {"schema": "nksr-reconstruction/1", "method": "NKSR", "completed": True,
                  "coordinate_frame": package.WORLD_FRAME, "input": {"sha256": cloud["sha256"], "points": 6},
                  "output": {"sha256": hashlib.sha256(raw).hexdigest(), "vertices": 6, "triangles": 2},
                  "preparation": {"sha256": package.sha256(meshes / "provenance.json"), "space": cloud,
                                  "source": preparation["sources"][space["source"]]}}
        save(folder / "mesh.nksr.json", report)
    return meshes, audit_path, catalog_path


class PackageTests(unittest.TestCase):
    @unittest.skipUnless(importlib.util.find_spec("trimesh") and importlib.util.find_spec("fast_simplification"), "Requires geometry environment")
    def test_mainhall_trial_preserves_baseline_absent_spaces_and_native_lods(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            meshes, audit_path, catalog_path = fixture(base)
            audit = json.loads(audit_path.read_text())
            space = next(s for s in audit["spaces"] if s["id"] == "HG_MainHall")
            space.update(min=[-20, -5, -1], max=[70, 32, 8], origin_world=[20, 0, 0], region_name="A")
            save(Path(space["manifest_path"]), {"scan": space["scan"], "origin_world": space["origin_world"], "datum_z": 0,
                "region": {"name": "A", "min": space["min"], "max": space["max"]}})
            space["manifest_sha256"] = package.sha256(space["manifest_path"])
            audit["spaces"], audit["sources"] = [space], {"HGE": audit["sources"]["HGE"]}
            save(audit_path, audit)
            preparation_path = meshes / "provenance.json"
            preparation = json.loads(preparation_path.read_text())
            cloud = next(s for s in preparation["spaces"] if s["id"] == "HG_MainHall")
            cloud.update(space)
            cloud.update(crop_min_with_halo=[v-.3 for v in space["min"]], crop_max_with_halo=[v+.3 for v in space["max"]])
            preparation["spaces"], preparation["sources"] = [cloud], {"HGE": preparation["sources"]["HGE"]}
            preparation["source_audit_canonical_json_sha256"] = hashlib.sha256(json.dumps(audit, sort_keys=True).encode()).hexdigest()
            save(preparation_path, preparation)
            report_path = meshes / "HG_MainHall/mesh.nksr.json"
            report = json.loads(report_path.read_text())
            report["preparation"].update(sha256=package.sha256(preparation_path), space=cloud)
            save(report_path, report)
            for ident in package.SPACE_IDS - {"HG_MainHall"}:
                (meshes / ident / "mesh.ply").unlink()
                (meshes / ident / "mesh.nksr.json").unlink()

            baseline = base / "baseline-packaged"
            (baseline / "HG_MainHall").mkdir(parents=True)
            (baseline / "CAB_MainHall/tiles/old").mkdir(parents=True)
            catalog = json.loads(catalog_path.read_text())
            for entry in catalog["spaces"]:
                entry["packaged"] = entry["id"] in ("HG_MainHall", "CAB_MainHall")
            save(baseline / "spaces.json", catalog)
            (baseline / "HG_MainHall.meta").write_text("original folder metadata")
            (baseline / "HG_MainHall/markers.json").write_text('{"original installation marker": 123}')
            (baseline / "HG_MainHall/markers.json.meta").write_text("original marker metadata")
            (baseline / "HG_MainHall/original-model.txt").write_text("old MainHall geometry")
            pipeline_spec = importlib.util.spec_from_file_location("trial_test_pipeline", package.TOOLS / "twin_pipeline.py")
            pipeline = importlib.util.module_from_spec(pipeline_spec)
            pipeline_spec.loader.exec_module(pipeline)
            cab_glb = baseline / "CAB_MainHall/tiles/old/lod2.glb"
            pipeline.write_glb(cab_glb, np.array([[0., 0, 0], [1, 0, 0], [0, 1, 0]]), np.full((3, 3), 123, dtype="u1"), [[0, 1, 2]])
            save(baseline / "CAB_MainHall/manifest.json", {"scan": "synthetic_CAB", "tiles": [{"files": {"2": "tiles/old/lod2.glb"}}]})
            cab_glb.with_suffix(".glb.meta").write_text("original CAB GLB metadata")
            (baseline / "CAB_MainHall/LICENSE.txt").write_text("original CAB licence")
            before = package.tree_hashes(baseline)

            output = base / "trial"
            run = package.package(meshes, audit_path, output, catalog_path, python=sys.executable,
                                  selected=["HG_MainHall"], baseline_packaged=baseline)
            self.assertEqual(run["reconstructed_spaces"], ["HG_MainHall"])
            self.assertEqual(run["package_lods"], [0, 1, 2])
            self.assertEqual(len(run["spaces"]), 1)
            self.assertEqual(package.tree_hashes(baseline), before)
            packaged = output / "packaged"
            self.assertEqual((packaged / "spaces.json").read_bytes(), (baseline / "spaces.json").read_bytes())
            self.assertEqual(package.tree_hashes(packaged / "CAB_MainHall"), package.tree_hashes(baseline / "CAB_MainHall"))
            for ident in ("HG_E3_Hall", "HG_E7_Hall"):
                self.assertFalse((packaged / ident).exists())
            self.assertEqual((packaged / "HG_MainHall/markers.json").read_bytes(), (baseline / "HG_MainHall/markers.json").read_bytes())
            self.assertEqual((packaged / "HG_MainHall.meta").read_bytes(), (baseline / "HG_MainHall.meta").read_bytes())
            manifest = json.loads((packaged / "HG_MainHall/manifest.json").read_text())
            proof = json.loads((packaged / "HG_MainHall/nksr-provenance.json").read_text())
            self.assertEqual(len(manifest["tiles"]), 2)
            self.assertTrue(all(set(tile["files"]) == {"0", "1", "2"} for tile in manifest["tiles"]))
            self.assertEqual(len(proof["files"]), 6)
            self.assertFalse(proof["selection"]["lod_remapping"])
            self.assertEqual(proof["reduction"]["regional_lod0_hard_cap"], 1500000)
            self.assertEqual(proof["reduction"]["regional_lod0_target_with_reserve"], 1200000)
            self.assertEqual(proof["reduction"]["raw_reconstruction_triangles"], 2)
            self.assertEqual(proof["reduction"]["packaged_triangles_by_lod"], {"0": 2, "1": 2, "2": 2})
            verifier_spec = importlib.util.spec_from_file_location("verify_trial_package_test", package.TOOLS / "verify_apk.py")
            verifier = importlib.util.module_from_spec(verifier_spec)
            verifier_spec.loader.exec_module(verifier)
            verifier.validate_nksr_trial(packaged, baseline)

    def test_refuses_false_method_stale_mesh_cloud_or_audit(self):
        for corruption in ("method", "mesh", "cloud", "audit", "baseline", "frame", "completed", "preparation"):
            with self.subTest(corruption=corruption), tempfile.TemporaryDirectory() as temp:
                base = Path(temp)
                meshes, audit, catalog = fixture(base)
                package.validate_inputs(meshes, audit, catalog)
                report_path = meshes / "HG_MainHall/mesh.nksr.json"
                report = json.loads(report_path.read_text())
                if corruption == "method":
                    report["method"] = "Poisson"
                elif corruption == "cloud":
                    report["input"]["sha256"] = "b" * 64
                elif corruption == "frame":
                    report["coordinate_frame"] = "centred"
                elif corruption == "completed":
                    report["completed"] = False
                elif corruption == "preparation":
                    report["preparation"]["sha256"] = "c" * 64
                elif corruption == "mesh":
                    with (meshes / "HG_MainHall/mesh.ply").open("ab") as stream:
                        stream.write(b"bad")
                elif corruption == "audit":
                    data = json.loads(audit.read_text())
                    data["spaces"][0]["datum_z"] = 1
                    save(audit, data)
                elif corruption == "baseline":
                    (base / "HG_MainHall-baseline.json").write_text("changed")
                save(report_path, report)
                with self.assertRaises(ValueError):
                    package.validate_inputs(meshes, audit, catalog)

    def test_failed_pipeline_publishes_no_outputs_and_baselines_unchanged(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            meshes, audit, catalog = fixture(base)
            before = {path: path.read_bytes() for path in base.glob("*-baseline.json")}
            with mock.patch.object(package, "run_pipeline", side_effect=RuntimeError("synthetic failure")):
                with self.assertRaisesRegex(RuntimeError, "synthetic failure"):
                    package.package(meshes, audit, base / "out", catalog)
            self.assertFalse((base / "out/twin").exists())
            self.assertFalse((base / "out/packaged").exists())
            retained = list((base / "out").glob("failed-nksr-package-*"))
            self.assertEqual(len(retained), 1)
            self.assertTrue((retained[0] / "sessions").is_dir())
            self.assertEqual(before, {path: path.read_bytes() for path in before})

    def test_identity_command_uses_original_crop_origin_and_datum(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            _, audit, _ = fixture(base)
            space = json.loads(audit.read_text())["spaces"][0]
            with mock.patch.object(package.subprocess, "run") as run:
                commands = package.run_pipeline(base / "session", base / "twin", space, "python", 1, 1)
            command = commands[0]
            self.assertEqual([command[3] for command in commands], ["chunk", "simplify", "export", "manifest"])
            self.assertIn("--region=0,30,0,30,0,3", command)
            self.assertIn("--origin=10,-20,0", command)
            self.assertEqual(command[command.index("--datum-z") + 1], "0")
            self.assertEqual(command[command.index("--target-ratio") + 1], "1")
            self.assertEqual(run.call_count, 6)

    @unittest.skipUnless(importlib.util.find_spec("trimesh") and importlib.util.find_spec("fast_simplification"), "Requires geometry environment")
    def test_four_space_real_pipeline_preserves_all_tiles_rgb_and_frames(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            meshes, audit, catalog = fixture(base)
            output = base / "out"
            result = package.package(meshes, audit, output, catalog, python=sys.executable)
            self.assertTrue(result["completed"])
            verifier_spec = importlib.util.spec_from_file_location("verify_apk_nksr_test", package.TOOLS / "verify_apk.py")
            verifier = importlib.util.module_from_spec(verifier_spec)
            verifier_spec.loader.exec_module(verifier)
            verifier.validate_nksr_source(output / "packaged")
            bundled = json.loads((output / "packaged/spaces.json").read_text())
            self.assertTrue(all(space["packaged"] for space in bundled["spaces"]))
            for ident in package.SPACE_IDS:
                manifest_path = output / "packaged" / ident / "manifest.json"
                manifest = json.loads(manifest_path.read_text())
                proof = json.loads((manifest_path.parent / "nksr-provenance.json").read_text())
                self.assertEqual({tile["id"] for tile in manifest["tiles"]}, {"x0_y0", "x2_y0"})
                self.assertEqual(manifest["origin_world"], [10, -20, 0])
                self.assertEqual(manifest["packaging"]["omitted_tiles"], 0)
                self.assertEqual(proof["manifest_sha256"], package.sha256(manifest_path))
                self.assertEqual(len(proof["files"]), 2)
                for tile in manifest["tiles"]:
                    self.assertEqual(set(tile["files"]), {"2"})
                    raw = (manifest_path.parent / tile["files"]["2"]).read_bytes()
                    jslen = struct.unpack_from("<I", raw, 12)[0]
                    document = json.loads(raw[20:20+jslen])
                    self.assertIn("COLOR_0", document["meshes"][0]["primitives"][0]["attributes"])
                    self.assertIn("KHR_materials_unlit", document["extensionsUsed"])
                    colour = document["accessors"][document["meshes"][0]["primitives"][0]["attributes"]["COLOR_0"]]
                    view = document["bufferViews"][colour["bufferView"]]
                    start = 28 + jslen + view.get("byteOffset", 0) + colour.get("byteOffset", 0)
                    rgb = {tuple(raw[start + 4*i:start + 4*i + 3]) for i in range(colour["count"])}
                    self.assertEqual(rgb, {(255, 0, 4), (0, 255, 5), (0, 3, 255)})
                    expected_x = 1 if tile["id"] == "x0_y0" else 21
                    self.assertEqual(tile["bounds_lod"]["2"]["min"], [expected_x, 1, 1])
                self.assertEqual(len(list(manifest_path.parent.glob("tiles/*/*.glb"))), 2)
            with self.assertRaisesRegex(ValueError, "already contains"):
                package.package(meshes, audit, output, catalog)


if __name__ == "__main__":
    unittest.main()
