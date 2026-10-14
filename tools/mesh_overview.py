"""Overview of NavVis meshes: floor heights and top-down maps per floor.

Reads the vertices of each proc/meshes/mesh.ply with a memmap (the files are
too large to load), samples them, moves them into the common frame with
proc/alignment_global.txt, and writes a height histogram and one top-down
image per height slab.

usage: python mesh_overview.py <sessions_dir> <out_dir> [--samples N]
"""
import argparse
from pathlib import Path

import numpy as np
import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt


def read_ply_layout(path):
    """Return header length, vertex count, face count and vertex dtype."""
    types = {"double": "f8", "float": "f4", "uchar": "u1", "uint": "u4", "int": "i4"}
    fields, n_vert, n_face, elem = [], 0, 0, None
    with open(path, "rb") as f:
        while True:
            line = f.readline().decode("ascii").strip()
            if line.startswith("element"):
                _, elem, n = line.split()
                if elem == "vertex":
                    n_vert = int(n)
                elif elem == "face":
                    n_face = int(n)
            elif line.startswith("property") and elem == "vertex":
                _, t, name = line.split()
                fields.append((name, types[t]))
            elif line == "end_header":
                return f.tell(), n_vert, n_face, np.dtype(fields)


def read_alignment(path):
    """Return (R, t) of the pose_graph_optimized line: p_world = R p + t."""
    for line in open(path):
        if line.startswith("pose_graph_optimized"):
            qw, qx, qy, qz, tx, ty, tz = map(float, line.split(",")[2:9])
            break
    R = np.array([
        [1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy - qz * qw), 2 * (qx * qz + qy * qw)],
        [2 * (qx * qy + qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz - qx * qw)],
        [2 * (qx * qz - qy * qw), 2 * (qy * qz + qx * qw), 1 - 2 * (qx * qx + qy * qy)],
    ])
    return R, np.array([tx, ty, tz])


def sample_session(session, n_samples):
    ply = session / "proc" / "meshes" / "mesh.ply"
    offset, n_vert, n_face, dtype = read_ply_layout(ply)
    verts = np.memmap(ply, dtype=dtype, mode="r", offset=offset, shape=(n_vert,))
    step = max(1, n_vert // n_samples)
    v = np.asarray(verts[::step])
    xyz = np.stack([v["x"], v["y"], v["z"]], axis=1)
    rgb = np.stack([v["red"], v["green"], v["blue"]], axis=1)
    R, t = read_alignment(session / "proc" / "alignment_global.txt")
    return xyz @ R.T + t, rgb, n_vert, n_face


def find_floors(z, bin_size=0.05, min_gap=2.0):
    """Floor levels are the strongest peaks of the height histogram."""
    hist, edges = np.histogram(z, bins=np.arange(z.min(), z.max() + bin_size, bin_size))
    centers = (edges[:-1] + edges[1:]) / 2
    order = np.argsort(hist)[::-1]
    floors = []
    for i in order:
        if hist[i] < hist[order[0]] * 0.05:
            break
        if all(abs(centers[i] - f) > min_gap for f in floors):
            floors.append(centers[i])
    return sorted(floors), hist, centers


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("sessions_dir", type=Path)
    ap.add_argument("out_dir", type=Path)
    ap.add_argument("--samples", type=int, default=2_000_000)
    args = ap.parse_args()
    args.out_dir.mkdir(parents=True, exist_ok=True)

    all_xyz, all_rgb, all_id = [], [], []
    sessions = sorted(p for p in args.sessions_dir.glob("navvis_*") if p.is_dir())
    for i, s in enumerate(sessions):
        xyz, rgb, nv, nf = sample_session(s, args.samples)
        lo, hi = xyz.min(0), xyz.max(0)
        print(f"{s.name}: {nv:,} verts {nf:,} faces  "
              f"x[{lo[0]:.1f},{hi[0]:.1f}] y[{lo[1]:.1f},{hi[1]:.1f}] z[{lo[2]:.1f},{hi[2]:.1f}]")
        all_xyz.append(xyz); all_rgb.append(rgb); all_id.append(np.full(len(xyz), i))
    xyz, rgb, sid = np.concatenate(all_xyz), np.concatenate(all_rgb), np.concatenate(all_id)

    floors, hist, centers = find_floors(xyz[:, 2])
    print("floor levels (z):", ", ".join(f"{f:.2f}" for f in floors))
    plt.figure(figsize=(6, 8))
    plt.barh(centers, hist, height=0.05)
    for f in floors:
        plt.axhline(f, color="r", lw=0.5)
    plt.xlabel("vertex count"); plt.ylabel("z [m]"); plt.title("height histogram")
    plt.savefig(args.out_dir / "height_histogram.png", dpi=120); plt.close()

    # one top-down image per floor: vertices from floor level to 2.5 m above it
    x0, y0 = xyz[:, 0].min(), xyz[:, 1].min()
    for k, f in enumerate(floors):
        m = (xyz[:, 2] > f - 0.2) & (xyz[:, 2] < f + 2.5)
        if m.sum() < 1000:
            continue
        fig, ax = plt.subplots(figsize=(14, 14))
        ax.scatter(xyz[m, 0], xyz[m, 1], c=rgb[m] / 255.0, s=0.05, linewidths=0)
        ax.set_aspect("equal"); ax.grid(True, lw=0.3)
        ax.set_xlabel("x [m]"); ax.set_ylabel("y [m]")
        ax.set_title(f"floor {k}: z = {f:.2f} m  ({m.sum():,} sampled verts)")
        fig.savefig(args.out_dir / f"floor_{k}_z{f:+.1f}.png", dpi=110); plt.close(fig)
        # which sessions cover this floor
        ids, counts = np.unique(sid[m], return_counts=True)
        cover = ", ".join(f"{sessions[i].name}:{c}" for i, c in zip(ids, counts))
        print(f"floor {k} z={f:.2f}: {cover}")


if __name__ == "__main__":
    main()
