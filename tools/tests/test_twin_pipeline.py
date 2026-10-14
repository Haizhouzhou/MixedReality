"""Real small PLY integration tests plus asymmetric coordinate fixtures."""
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("pipeline", ROOT / "twin_pipeline.py")
pipeline = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pipeline)


class PipelineTests(unittest.TestCase):
    def test_asymmetric_axes_and_gltfast(self):
        twin = np.array([[-17.2, 6.5, 1.7], [68.2, -3.7, 7.1]])
        gltf = pipeline.twin_to_gltf(twin)
        np.testing.assert_allclose(gltf[0], [-37.2, 1.7, -6.5])
        np.testing.assert_allclose(pipeline.gltf_to_twin(gltf), twin)
        imported = gltf * [-1, 1, 1]  # glTFast reflects X
        adapted = imported * [-1, 1, -1]  # yaw 180
        np.testing.assert_allclose(adapted[0], [-37.2, 1.7, 6.5])
        self.assertLess(adapted[0, 0], 0, "West landmark must remain west")

    def test_clean_duplicates_degenerates(self):
        p = np.array([[0, 0, 0], [1, 0, 0], [0, 1, 0], [0.0001, 0, 0], [8, 8, 8]], dtype=np.float32)
        c = np.full((5, 3), 128, dtype=np.uint8)
        f = np.array([[0, 1, 2], [2, 1, 3], [0, 0, 1]])
        q, _, g = pipeline.clean_mesh(p, c, f)
        self.assertEqual(len(g), 1)
        self.assertEqual(len(q), 3)

    def test_restart_and_glb_roundtrip(self):
        scratch = ROOT.parent / "work/tmp"
        scratch.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=scratch) as temp:
            base = Path(temp)
            session = base / "scan"
            (session / "proc/meshes").mkdir(parents=True)
            (session / "proc/alignment_global.txt").write_text("pose_graph_optimized, world, 1,0,0,0,2,3,0\n")
            dt = np.dtype([(n, "<f8") for n in ("x", "y", "z")] + [(n, "u1") for n in ("red", "green", "blue")])
            p = np.zeros(4, dtype=dt)
            p["x"], p["y"], p["z"] = [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]
            p["red"] = [255, 0, 0, 255]
            p["green"] = [0, 255, 0, 255]
            p["blue"] = [0, 0, 255, 255]
            f = np.zeros(4, dtype=pipeline.FACE_DTYPE)
            f["count"] = 3
            f["indices"] = [[0, 1, 2], [0, 3, 1], [1, 3, 2], [2, 3, 0]]
            header = "ply\nformat binary_little_endian 1.0\nelement vertex 4\nproperty double x\nproperty double y\nproperty double z\nproperty uchar red\nproperty uchar green\nproperty uchar blue\nelement face 4\nproperty list uchar uint vertex_indices\nend_header\n"
            (session / "proc/meshes/mesh.ply").write_bytes(header.encode() + p.tobytes() + f.tobytes())
            out = base / "output"
            cmd = [sys.executable, "-I", str(ROOT / "twin_pipeline.py"), "all", "--session", str(session), "--out", str(out), "--target-ratio", "1", "--batch-faces", "2", "--workers", "2"]
            subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL)
            tile = out / "tiles/x0_y0/lod0.glb"
            before = tile.stat().st_mtime_ns
            subprocess.run(cmd, check=True, stdout=subprocess.DEVNULL)
            self.assertEqual(before, tile.stat().st_mtime_ns)
            subprocess.run([sys.executable, "-I", str(ROOT / "verify_tiles.py"), str(out)], check=True)
            m = json.loads((out / "manifest.json").read_text())
            self.assertEqual(m["tiles"][0]["min"], [2, 3, 0])

    def test_quadric_reduction_preserves_finite_colors(self):
        import trimesh
        sphere = trimesh.creation.icosphere(subdivisions=3)
        p = np.asarray(sphere.vertices, dtype=np.float32)
        c = np.clip((p + 1) * 127, 0, 255).astype(np.uint8)
        q, colors, faces = pipeline.decimate(p, c, sphere.faces, 120)
        self.assertLessEqual(len(faces), 120)
        self.assertTrue(np.isfinite(q).all())
        self.assertEqual(colors.dtype, np.uint8)
        self.assertEqual(len(q), len(colors))

    def test_spatial_partitions_retain_every_source_face(self):
        import trimesh
        mesh = trimesh.creation.icosphere(subdivisions=2)
        parts = list(pipeline.spatial_parts(mesh.vertices, mesh.faces, 100))
        self.assertTrue(all(len(part) <= 100 for part in parts))
        restored = sorted(tuple(row) for part in parts for row in part)
        self.assertEqual(restored, sorted(tuple(row) for row in mesh.faces))


if __name__ == "__main__":
    unittest.main()
