"""Independently validate every manifest GLB with trimesh and raw glTF metadata."""
import argparse
import json
from pathlib import Path
import struct

import numpy as np
import trimesh


def verify(root):
    root = Path(root)
    m = json.loads((root / "manifest.json").read_text())
    assert m["schema"] == "twin-manifest/1"
    origin = np.array(m["origin_world"])
    total, size, seen = 0, 0, set()
    print("TILE               LOD   TRIANGLES      BYTES  COLOUR  BOUNDS")
    for tile in m["tiles"]:
        assert tile["id"] not in seen, "Duplicate tile"
        seen.add(tile["id"])
        if "0" in tile["triangles"]:
            for level, ratio in (("1", .27), ("2", .07)):
                if level in tile["triangles"]:
                    reduction = tile.get("reduction", {}).get(level, {})
                    excess = reduction.get("preserved_fragment_excess", 0)
                    assert 0 <= excess <= max(1024, reduction.get("target_triangles", 0) * .02)
                    assert tile["triangles"][level] <= max(64, tile["triangles"]["0"] * ratio + 4) + excess, f"LOD reduction insufficient: {tile['id']} LOD{level}"
        for level, relative in tile["files"].items():
            path = (root / relative).resolve()
            assert path.is_relative_to(root.resolve()), "Path escapes output"
            data = path.read_bytes()
            magic, version, length = struct.unpack_from("<4sII", data)
            assert (magic, version, length) == (b"glTF", 2, len(data))
            jslen, kind = struct.unpack_from("<II", data, 12)
            assert kind == 0x4E4F534A
            doc = json.loads(data[20:20 + jslen])
            primitive = doc["meshes"][0]["primitives"][0]
            color = doc["accessors"][primitive["attributes"]["COLOR_0"]]
            assert color["componentType"] == 5121 and color["normalized"] is True
            scene = trimesh.load(path, force="scene", process=False)
            mesh = scene.to_geometry()
            assert len(mesh.faces) == tile["triangles"][level], f"Count mismatch: {path}"
            assert len(data) == tile["bytes"][level] and len(data) <= 30_000_000
            assert np.isfinite(mesh.vertices).all()
            assert mesh.faces.max() < len(mesh.vertices)
            # trimesh may expose COLOR_0 through TextureVisuals when a material
            # is present. Validate the original accessor rather than its wrapper.
            assert color["count"] == len(mesh.vertices)
            # Independent inverse of exporter; do not import production transform.
            p = np.column_stack((mesh.vertices[:, 0], -mesh.vertices[:, 2], mesh.vertices[:, 1])) + origin
            bounds = tile["bounds_lod"][level]
            np.testing.assert_allclose(p.min(0), bounds["min"], atol=2e-5)
            np.testing.assert_allclose(p.max(0), bounds["max"], atol=2e-5)
            assert (p >= np.array(tile["min"]) - .5).all() and (p <= np.array(tile["max"]) + .5).all(), "QEM escaped source bounds"
            if level == "0":
                total += len(mesh.faces)
                size += len(data)
            print(f"{tile['id']:18} {level:>3} {len(mesh.faces):11,d} {len(data):10,d}      OK      OK")
    assert total == m["totals"]["triangles_lod0"]
    is_a = m["region"]["name"] == "A"
    assert total <= (1_500_000 if is_a else 5_000_000)
    assert size <= (150_000_000 if is_a else 450_000_000)
    print(f"PASS: {len(seen)} tiles; LOD0 {total:,} triangles / {size:,} bytes")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("out", type=Path)
    verify(parser.parse_args().out)
