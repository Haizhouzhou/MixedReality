import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("evaluation", Path(__file__).parents[1]/"evaluate_annotations.py")
evaluation = importlib.util.module_from_spec(spec)
spec.loader.exec_module(evaluation)


class EvaluationTests(unittest.TestCase):
    def test_three_true_one_false_one_miss(self):
        truth = [dict(type="missing", position_twin=[x, 0, 0]) for x in (0, 3, 6, 9)]
        items = [dict(type="missing", position_twin=[x, 0, 0]) for x in (0.1, 3.1, 6.1, 20)]
        for aware in (False, True):
            result = evaluation.match(items, truth, .5, aware)
            self.assertEqual((result["tp"], result["fp"], result["fn"]), (3, 1, 1))
            self.assertEqual(result["precision"], .75)
            self.assertEqual(result["recall"], .75)
            self.assertEqual(result["f1"], .75)
            self.assertAlmostEqual(result["mean_distance"], .1)

    def test_one_to_one_type_and_interval_semantics(self):
        truth = [dict(type="missing", position_twin=[0, 0, 0])]
        items = [dict(type="new", position_twin=[0, 0, 0], created_utc="2026-10-22T12:00:00Z"),
                 dict(type="new", position_twin=[.1, 0, 0], created_utc="2026-10-22T12:01:00Z")]
        self.assertEqual(evaluation.match(items, truth, .5, False)["tp"], 1)
        self.assertEqual(evaluation.match(items, truth, .5, True)["tp"], 0)
        self.assertEqual(evaluation.intervals(items), (60, 60))
        self.assertIn("do not measure active annotation time", evaluation.report(items, truth))

    def test_empty_and_invalid_radius(self):
        self.assertEqual(evaluation.match([], [], .5, True)["f1"], 0)
        with self.assertRaises(ValueError): evaluation.match([], [], -1, False)


if __name__ == "__main__":
    unittest.main()
