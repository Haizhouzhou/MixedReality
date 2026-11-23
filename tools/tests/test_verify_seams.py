"""Boundary preservation must not depend on a KDTree tie between coincident vertices."""
import importlib.util
from pathlib import Path
import unittest

import numpy as np

spec = importlib.util.spec_from_file_location("seams", Path(__file__).resolve().parents[1] / "verify_seams.py")
seams = importlib.util.module_from_spec(spec)
spec.loader.exec_module(seams)


class SeamMatchingTests(unittest.TestCase):
    def test_coincident_endpoint_indices_preserve_real_geometric_edge(self):
        points = np.array([[0., 0, 0], [0, 0, 0], [1, 0, 0], [1, 0, 0], [0, 1, 0], [0, -1, 0]])
        faces = np.array([[0, 3, 4], [1, 2, 5]])
        error, missing, resolved = seams.boundary_matches(points, faces, points[[0, 2]], np.array([[0, 1]]))
        self.assertEqual(error, 0)
        self.assertEqual(missing, 0)
        self.assertEqual(resolved, 1)

    def test_matching_vertices_do_not_hide_a_genuinely_missing_edge(self):
        points = np.array([[0., 0, 0], [0, 0, 0], [1, 0, 0], [1, 0, 0], [0, 1, 0], [0, -1, 0]])
        faces = np.array([[0, 4, 5], [2, 4, 5]])
        error, missing, resolved = seams.boundary_matches(points, faces, points[[0, 2]], np.array([[0, 1]]))
        self.assertEqual((error, missing, resolved), (0, 1, 0))

    def test_candidate_outside_existing_tolerance_cannot_repair_edge(self):
        boundary = np.array([[0., 0, 0], [1, 0, 0]])
        points = np.array([[0., 0, 0], [0, 0, 3e-5], [1, 0, 0], [1, 0, 3e-5], [0, 1, 0]])
        faces = np.array([[1, 3, 4]])
        error, missing, resolved = seams.boundary_matches(points, faces, boundary, np.array([[0, 1]]))
        self.assertEqual((error, missing, resolved), (0, 1, 0))

    def test_displacement_still_reports_out_of_tolerance_geometry(self):
        boundary = np.array([[0., 0, 0], [1, 0, 0]])
        points = np.array([[0., 0, 3e-5], [1, 0, 3e-5], [0, 1, 0]])
        error, _, _ = seams.boundary_matches(points, np.array([[0, 1, 2]]), boundary, np.array([[0, 1]]))
        self.assertGreater(error, 2e-5)

    def test_displacement_includes_the_actual_candidate_edge_endpoints(self):
        boundary = np.array([[0., 0, 0], [1, 0, 0]])
        points = np.array([[0., 0, 0], [0, 0, 1e-5], [1, 0, 0], [1, 0, 1e-5], [0, 1, 0]])
        error, missing, resolved = seams.boundary_matches(points, np.array([[1, 3, 4]]), boundary, np.array([[0, 1]]))
        self.assertEqual((missing, resolved), (0, 1))
        self.assertAlmostEqual(error, 1e-5)


if __name__ == "__main__":
    unittest.main()
