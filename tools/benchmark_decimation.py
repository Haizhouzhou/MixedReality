"""Compare complete-tile QEM with bounded face-run QEM on a known tile."""
import importlib.util
from pathlib import Path
import time
import numpy as np
import psutil

spec = importlib.util.spec_from_file_location("pipeline", Path(__file__).with_name("twin_pipeline.py"))
p = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p)
root = Path(__file__).parents[1] / "work/twin/HG_MainHall"
tile = "x-1_y0"
start = time.perf_counter()
with np.load(root / "chunks" / (tile + ".npz")) as data:
    xyz, rgb, faces = data["positions"], data["colors"], data["faces"]
print(f"Loaded {len(faces):,} faces", flush=True)
points, colors, triangles = p.decimate(xyz, rgb, faces, 70670)
print(f"Full-tile QEM: {len(triangles):,} faces; seconds={time.perf_counter()-start:.1f}; peak={psutil.Process().memory_info().peak_wset:,}", flush=True)
p.atomic_npz(root / "benchmark_full_tile.npz", positions=points, colors=colors, faces=triangles)
