"""Compare twin annotation exports to measured points; creation intervals are not task durations."""
from __future__ import annotations

import argparse
import csv
import json
import math
import statistics
from collections import Counter
from datetime import datetime
from pathlib import Path


def point(item):
    return tuple(float(x) for x in item["position_twin"])


def match(items, truth, radius, type_aware):
    if not math.isfinite(radius) or radius <= 0:
        raise ValueError("Radius must be finite and positive")
    edges = []
    for i, a in enumerate(items):
        for j, b in enumerate(truth):
            if type_aware and a["type"].lower() != b["type"].lower():
                continue
            distance = math.dist(point(a), point(b))
            if distance <= radius:
                edges.append((distance, i, j))
    used_items, used_truth, distances = set(), set(), []
    for distance, i, j in sorted(edges):
        if i in used_items or j in used_truth:
            continue
        used_items.add(i)
        used_truth.add(j)
        distances.append(distance)
    tp = len(distances)
    precision = tp / len(items) if items else 0.0
    recall = tp / len(truth) if truth else 0.0
    return dict(tp=tp, fp=len(items)-tp, fn=len(truth)-tp,
                precision=precision, recall=recall,
                f1=2*precision*recall/(precision+recall) if precision+recall else 0.0,
                mean_distance=statistics.mean(distances) if distances else None,
                max_distance=max(distances) if distances else None)


def intervals(items):
    times = sorted(datetime.fromisoformat(x["created_utc"].replace("Z", "+00:00"))
                   for x in items if x.get("created_utc"))
    if len(times) < 2:
        return None, None
    return statistics.median((b-a).total_seconds() for a, b in zip(times, times[1:])), (times[-1]-times[0]).total_seconds()


def report(items, truth, radii=(0.5, 1.0)):
    lines = ["# Annotation evaluation", "",
             "Greedy nearest one-to-one matching in the original Z-up twin frame.", "",
             "| Radius (m) | Matching | TP | FP | FN | Precision | Recall | F1 | Mean distance (m) | Max distance (m) |",
             "|---:|:---|---:|---:|---:|---:|---:|---:|---:|---:|"]
    def value(x): return "n/a" if x is None else f"{x:.3f}"
    for radius in radii:
        for aware in (False, True):
            r = match(items, truth, radius, aware)
            lines.append(f"| {radius:g} | {'Type-aware' if aware else 'Type-agnostic'} | {r['tp']} | {r['fp']} | {r['fn']} | {r['precision']:.3f} | {r['recall']:.3f} | {r['f1']:.3f} | {value(r['mean_distance'])} | {value(r['max_distance'])} |")
    lines += ["", "| Type | Export count | Ground truth count |", "|:---|---:|---:|"]
    counts, targets = Counter(x["type"].lower() for x in items), Counter(x["type"].lower() for x in truth)
    for kind in sorted(counts.keys() | targets.keys()):
        lines.append(f"| {kind} | {counts[kind]} | {targets[kind]} |")
    median, span = intervals(items)
    lines += ["", f"Median interval between creation timestamps: {value(median)} s.",
              f"Session span between earliest and latest creation: {value(span)} s.",
              "These intervals include pauses and travel; they do not measure active annotation time."]
    return "\n".join(lines)+"\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("export", type=Path)
    parser.add_argument("ground_truth", type=Path)
    parser.add_argument("--out", type=Path)
    parser.add_argument("--radius", type=float, nargs="+", default=[0.5, 1.0])
    args = parser.parse_args()
    document = json.loads(args.export.read_text(encoding="utf-8-sig"))
    if document.get("schema") not in ("twin-annotations/1", "twin-annotations/2"):
        parser.error("Unsupported annotation schema")
    with args.ground_truth.open(encoding="utf-8-sig", newline="") as stream:
        truth = [dict(row, position_twin=[float(row[f"{axis}_twin"]) for axis in "xyz"])
                 for row in csv.DictReader(stream)]
    for row in document["items"] + truth:
        if len(point(row)) != 3 or not all(math.isfinite(x) for x in point(row)):
            parser.error("Coordinates must be three finite numbers")
    text = report(document["items"], truth, args.radius)
    print(text, end="")
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")


if __name__ == "__main__":
    main()
