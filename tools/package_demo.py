"""Copy verified Region A LOD2 only; keep the fallback self-describing."""
import argparse
import copy
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
args = parser.parse_args()
m = json.loads((args.source / "manifest.json").read_text())
assert m["region"]["name"] == "A"
size = sum(t["bytes"]["2"] for t in m["tiles"])
assert size < 15_000_000, "Fallback exceeds 15 MB"
demo = copy.deepcopy(m)
demo["lods"] = [{"level": 2, "max_distance_m": 80}]
demo["demo"] = True
for tile in demo["tiles"]:
    for field in ("files", "triangles", "bytes", "bounds_lod"):
        tile[field] = {"2": tile[field]["2"]}
    relative = tile["files"]["2"]
    dest = args.destination / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.source / relative, dest)
demo["totals"] = {"triangles_lod0": 0, "bytes_lod0": 0, "bytes": size}
args.destination.mkdir(parents=True, exist_ok=True)
(args.destination / "manifest.json").write_text(json.dumps(demo, indent=2), encoding="utf-8")
shutil.copy2(Path(__file__).parent / "markers/markers.json", args.destination / "markers.json")
(args.destination / "LICENSE.txt").write_text("Derived from LaMAR HGE NavVis data (ETH Zurich / Microsoft). CC BY-SA 4.0.\nhttps://lamar.ethz.ch/\nhttps://creativecommons.org/licenses/by-sa/4.0/\nChanges: spatial clipping, 1 mm welding, quadric decimation, sampled vertex colours and axis conversion.\n", encoding="utf-8")
print(f"Packaged {len(demo['tiles'])} LOD2 tiles, {size:,} bytes")
