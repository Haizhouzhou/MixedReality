"""Copy verified Region A LOD2 only; keep the fallback self-describing."""
import argparse
import copy
import json
from pathlib import Path
import shutil

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("destination", type=Path)
parser.add_argument("--max-bytes",type=int,default=14_000_000,help="GLB budget, leaving room below the 15 MB total cap")
args = parser.parse_args()
m = json.loads((args.source / "manifest.json").read_text())
assert m["region"]["name"] == "A"
assert 0 < args.max_bytes <= 14_000_000
# Exact seams can make LOD2 larger. Bundle complete nearby hall tiles within
# the fallback cap; never degrade boundaries or include higher LODs to fit.
def distance(tile):
    point=(20,10,1.6)
    return sum(max(tile['min'][i]-point[i],0,point[i]-tile['max'][i])**2 for i in range(3))
selected=[];size=0
for tile in sorted(m['tiles'],key=lambda t:(distance(t),t['id'])):
    if size+tile['bytes']['2'] <= args.max_bytes:
        selected.append(tile);size+=tile['bytes']['2']
assert selected, "No complete tile fits the fallback budget"
demo = copy.deepcopy(m)
demo['tiles']=copy.deepcopy(selected)
demo['demo_selection']={'source_region_tiles':len(m['tiles']),'included_tiles':len(selected),'priority_twin_point':[20,10,1.6],'glb_budget_bytes':args.max_bytes}
demo["lods"] = [{"level": 2, "max_distance_m": 80}]
demo["demo"] = True
for tile in demo["tiles"]:
    for field in ("files", "triangles", "bytes", "bounds_lod"):
        tile[field] = {"2": tile[field]["2"]}
    relative = tile["files"]["2"]
    dest = args.destination / relative
    dest.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(args.source / relative, dest)
expected={Path(t['files']['2']).as_posix() for t in demo['tiles']}
for stale in (args.destination/'tiles').glob('*/lod*.glb'):
    if stale.relative_to(args.destination).as_posix() not in expected:
        stale.unlink()
        meta=stale.with_suffix(stale.suffix+'.meta')
        if meta.exists():meta.unlink()
demo["totals"] = {"triangles_lod0": 0, "bytes_lod0": 0, "bytes": size}
args.destination.mkdir(parents=True, exist_ok=True)
(args.destination / "manifest.json").write_text(json.dumps(demo, indent=2), encoding="utf-8")
shutil.copy2(Path(__file__).parent / "markers/markers.json", args.destination / "markers.json")
(args.destination / "LICENSE.txt").write_text("Derived from LaMAR HGE NavVis data (ETH Zurich / Microsoft). CC BY-SA 4.0.\nhttps://lamar.ethz.ch/\nhttps://creativecommons.org/licenses/by-sa/4.0/\nChanges: centroid chunking, boundary-preserving quadric decimation, sampled vertex colours, fallback tile selection and axis conversion.\n", encoding="utf-8")
print(f"Packaged {len(demo['tiles'])} LOD2 tiles, {size:,} bytes")
