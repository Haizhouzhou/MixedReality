"""CPU-only input integrity and coordinate/colour interchange checks for NKSR."""
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest

import numpy as np

TOOLS = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("reconstruct", TOOLS / "nksr_reconstruct.py")
reconstruct = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reconstruct)


def cloud(path):
    dtype = np.dtype([(n, "<f8") for n in ("x", "y", "z", "nx", "ny", "nz")]
                     + [(n, "u1") for n in ("red", "green", "blue")])
    points = np.zeros(4, dtype=dtype)
    points["x"] = [1000000.01, 1000000.02, 1000001.01, 1000000.01]
    points["y"] = [-2000000.02, -2000000.01, -2000000.02, -1999999.02]
    points["z"] = [-4.0, -4.0, -4.0, -3.0]
    points["nx"], points["ny"], points["nz"] = [0, 0, 2, 0], [0, 0, 0, -3], [4, 2, 0, 0]
    points["red"], points["green"], points["blue"] = [255, 23, 0, 7], [0, 17, 255, 8], [1, 13, 0, 255]
    header = ("ply\nformat binary_little_endian 1.0\nelement vertex 4\n" +
              "".join(f"property {'uchar' if name in ('red', 'green', 'blue') else 'double'} {name}\n"
                      for name in dtype.names) + "end_header\n").encode()
    path.write_bytes(header + points.tobytes())
    return points, header


class ReconstructTests(unittest.TestCase):
    def test_world_centring_preserves_small_details_normal_orientation_and_rgb(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "TEST.ply"
            source, _ = cloud(path)
            data = reconstruct.read_cloud(path)
            stats = reconstruct.inspect_cloud(data)
            xyz, normal, rgb, centre, sample = reconstruct.load_samples(data, stats, 0, 0, 0)
            restored = (xyz * 2).astype(np.float64) / 2 + centre
            np.testing.assert_allclose(restored, np.column_stack([source[n] for n in ("x", "y", "z")]), atol=1e-7, rtol=0)
            self.assertGreater(np.linalg.norm(xyz[0] - xyz[1]), .01)
            np.testing.assert_allclose(normal, [[0, 0, 1], [0, 0, 1], [1, 0, 0], [0, -1, 0]])
            np.testing.assert_array_equal(rgb, [[255, 0, 1], [23, 17, 13], [0, 255, 0], [7, 8, 255]])
            self.assertEqual(sample["points_after_voxel"], 4)

    def test_grid_sampling_keeps_first_record_and_distinguishes_negative_positions(self):
        xyz = np.array([[-.01, 1, 4], [-.009, 1.001, 4.001], [.02, 1, 4], [-.01, 1, 4.03]])
        np.testing.assert_array_equal(reconstruct.voxel_representatives(xyz, .025), [0, 2, 3])
        np.testing.assert_array_equal(reconstruct.voxel_representatives(xyz, 0), [0, 1, 2, 3])
        with self.assertRaisesRegex(ValueError, "int64"):
            reconstruct.voxel_representatives(xyz, 1e-10)

    def test_stratified_cap_is_bounded_reproducible_sorted_and_covers_input(self):
        indices = reconstruct.stratified_indices(10003, 601, 7)
        self.assertEqual(len(indices), 601)
        self.assertTrue(np.all(np.diff(indices) > 0))
        self.assertGreaterEqual(indices[0], 0)
        self.assertLess(indices[-1], 10003)
        lo = np.floor(np.arange(601) * 10003 / 601).astype(int)
        hi = np.floor((np.arange(601) + 1) * 10003 / 601).astype(int)
        self.assertTrue(np.all((indices >= lo) & (indices < hi)))
        np.testing.assert_array_equal(indices, reconstruct.stratified_indices(10003, 601, 7))
        self.assertFalse(np.array_equal(indices, reconstruct.stratified_indices(10003, 601, 8)))
        np.testing.assert_array_equal(reconstruct.stratified_indices(3, 10, 0), [0, 1, 2])

    def test_rejects_truncated_cloud_and_missing_colour_or_normals(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "TEST.ply"
            _, _ = cloud(path)
            original = path.read_bytes()
            for raw in (original[:-1], original + b"x", original.replace(b"double nx", b"double xx"),
                        original.replace(b"uchar red", b"uchar hue"), original.replace(b"vertex 4", b"vertex 0")):
                path.write_bytes(raw)
                with self.assertRaises(ValueError):
                    reconstruct.read_cloud(path)

    def test_validates_all_records_even_those_omitted_by_sampling(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "TEST.ply"
            points, header = cloud(path)
            for field, value in (("x", np.nan), ("nz", 0)):
                bad = points.copy()
                bad[field][0] = value
                path.write_bytes(header + bad.tobytes())
                with self.assertRaises(ValueError):
                    reconstruct.inspect_cloud(reconstruct.read_cloud(path))

    def test_provenance_requires_matching_content_and_point_count(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path, provenance = root / "TEST.ply", root / "provenance.json"
            cloud(path)
            info = {"sha256": reconstruct.sha256(path), "points": 4}
            report = {"spaces": [{"id": "TEST", "source": "SRC", "ply": "TEST.ply", **info}],
                      "sources": {"SRC": {"scan": "test", "alignment_sha256": "example"}}}
            provenance.write_text(json.dumps(report))
            result = reconstruct.preparation_record(provenance, path, info)
            self.assertEqual(result["space"]["id"], "TEST")
            for bad in ({**info, "sha256": "bad"}, {**info, "points": 3}):
                with self.assertRaisesRegex(ValueError, "hash and point count"):
                    reconstruct.preparation_record(provenance, path, bad)

    def test_mesh_binary_layout_matches_existing_pipeline_and_colour_bytes(self):
        # Exercise the actual pipeline reader, including its fixed 13-byte triangle layout.
        sys.path.insert(0, str(TOOLS))
        try:
            import mesh_overview
            import twin_pipeline
        finally:
            sys.path.pop(0)
        xyz = np.array([[-17.2, 6.5, -4.7], [68.2, -3.7, 7.1], [0, 1, 2]], dtype=np.float64)
        rgb = np.array([[255, 1, 2], [3, 254, 5], [6, 7, 253]], dtype=np.uint8)
        faces = np.array([[0, 1, 2]], dtype=np.int64)
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "mesh.ply"
            reconstruct.write_mesh(path, xyz, faces, rgb)
            offset, nv, nf, dtype = mesh_overview.read_ply_layout(path)
            vertices = np.memmap(path, dtype=dtype, mode="r", offset=offset, shape=(nv,))
            triangles = np.memmap(path, dtype=twin_pipeline.FACE_DTYPE, mode="r",
                                  offset=offset + nv * dtype.itemsize, shape=(nf,))
            np.testing.assert_array_equal(np.column_stack([vertices[n] for n in ("x", "y", "z")]), xyz)
            np.testing.assert_array_equal(np.column_stack([vertices[n] for n in ("red", "green", "blue")]), rgb)
            np.testing.assert_array_equal(triangles["indices"], faces)
            np.testing.assert_array_equal(triangles["count"], [3])
            self.assertEqual(path.stat().st_size, offset + 3 * 27 + 13)
            with self.assertRaises(FileExistsError):
                reconstruct.write_mesh(path, xyz, faces, rgb)

    def test_mesh_validation_rejects_broken_outputs(self):
        xyz = np.eye(3)
        rgb = np.zeros((3, 3), dtype=np.uint8)
        report = reconstruct.mesh_statistics(xyz, np.array([[0, 1, 2]]), rgb)
        self.assertEqual(report["degenerate_triangles"], 0)
        for faces in (np.array([[0, 1, 3]]), np.array([[0, 0, 1]]), np.array([[0., 1, 2]])):
            with self.assertRaises(ValueError):
                reconstruct.mesh_statistics(xyz, faces, rgb)

    def test_validate_only_emits_no_mesh_and_cannot_claim_reconstruction(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path, output = root / "TEST.ply", root / "result/mesh.ply"
            cloud(path)
            args = reconstruct.checked_args(["--input", str(path), "--output", str(output), "--validate-only"])
            with contextlib.redirect_stdout(io.StringIO()):
                result = reconstruct.run(args)
            self.assertFalse(result["completed"])
            self.assertEqual(result["schema"], "nksr-input-validation/1")
            self.assertFalse(output.parent.exists())

    def test_invalid_parameters_are_rejected_before_processing(self):
        for arguments in (["--voxel-size", "nan"], ["--voxel-size", "0"], ["--chunk-size", "-1"],
                          ["--overlap", "0"], ["--point-voxel", "-1"], ["--max-input-points", "-1"],
                          ["--max-eval-points", "0"], ["--seed", "-1"], ["--mise-iter", "4"]):
            with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                reconstruct.checked_args(["--input", "x", "--output", "y", *arguments])


if __name__ == "__main__":
    unittest.main()
