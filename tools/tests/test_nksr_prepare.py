"""Small synthetic clouds exercise streaming integrity, alignment, and atomic crops."""
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import zlib

import numpy as np

spec = importlib.util.spec_from_file_location("nksr_prepare", Path(__file__).resolve().parents[1] / "nksr_prepare.py")
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)

RAW_DTYPE = np.dtype([(n, "<f4") for n in ("x", "y", "z")]
                    + [(n, "u1") for n in ("red", "green", "blue")]
                    + [(n, "<f4") for n in ("nx", "ny", "nz", "curvature")])


def fixture():
    points = np.zeros(5, dtype=RAW_DTYPE)
    points["x"] = [1, 2, 3, 1, 20]
    points["y"] = [2, 3, 4, 2, 20]
    points["z"] = [3, 4, 5, 3, 20]
    points["nx"] = [2, 0, 0, 0, 1]
    points["ny"] = [0, 3, 0, 0, 0]
    points["nz"] = [0, 0, -4, 0, 0]
    points["red"], points["green"], points["blue"] = [255, 12, 1, 4, 0], [3, 254, 5, 6, 0], [4, 7, 253, 8, 0]
    properties = "".join(f"property {'uchar' if dt == '|u1' else 'float'} {name}\n"
                         for name, dt in RAW_DTYPE.descr)
    header = ("ply\nformat binary_little_endian 1.0\ncomment synthetic test\n"
              f"element vertex {len(points)}\n{properties}end_header\n").encode()
    raw = header + points.tobytes()
    compressor = zlib.compressobj(wbits=-15)
    packed = compressor.compress(raw) + compressor.flush()
    alignment_text = "pose_graph_optimized, __absolute__, 0.7071067811865476, 0, 0, 0.7071067811865476, 10, 20, 30\n"
    source = {
        "scan": "synthetic", "archive": {"url": "https://example.invalid/test.zip", "bytes": 1000 + len(packed), "etag": '"synthetic"'},
        "pointcloud": {"member": "test.ply", "zip_method": 8, "compressed_range": [100, 99 + len(packed)],
                       "compressed_bytes": len(packed), "uncompressed_bytes": len(raw),
                       "crc32_hex": f"{zlib.crc32(raw):08x}", "vertex_count": len(points),
                       "header_bytes": len(header), "vertex_stride_bytes": 31,
                       "vertex_fields": [{"name": name, "dtype": dt} for name, dt in RAW_DTYPE.descr]},
        "alignment": {"text": alignment_text, "sha256": hashlib.sha256(alignment_text.encode()).hexdigest(),
                      "rotation_row_major": [0, -1, 0, 1, 0, 0, 0, 0, 1], "translation": [10, 20, 30]},
    }
    spaces = [
        {"id": "FIRST", "source": "TEST", "min": [7.9, 20.9, 32.9], "max": [8.1, 21.1, 33.1], "origin_world": [100, 100, 100]},
        {"id": "SECOND", "source": "TEST", "min": [5.9, 21.9, 33.9], "max": [7.1, 23.1, 35.1], "datum_z": -4.8},
    ]
    return {"sources": {"TEST": source}, "spaces": spaces}, raw, packed


def output_points(path):
    data = path.read_bytes()
    end = data.index(b"end_header\n") + len(b"end_header\n")
    count_line = next(line for line in data[:end].splitlines() if line.startswith(b"element vertex"))
    count = int(count_line.split()[2])
    points = np.frombuffer(data[end:], dtype=prepare.OUTPUT_DTYPE)
    assert len(points) == count
    return points


class Response(io.BytesIO):
    def __init__(self, data, source):
        super().__init__(data)
        self.status = 206
        start, end = source["pointcloud"]["compressed_range"]
        self.headers = {"Content-Range": f"bytes {start}-{end}/{source['archive']['bytes']}",
                        "Content-Length": str(len(data)), "ETag": source["archive"]["etag"]}


class PrepareTests(unittest.TestCase):
    def test_local_crops_rotate_normals_preserve_rgb_and_world_coordinates(self):
        config, raw, _ = fixture()
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            path = base / "raw.ply"
            path.write_bytes(raw)
            report = prepare.prepare(config, base / "out", local_paths={"TEST": path}, block_bytes=1024)
            first = output_points(base / "out/FIRST.ply")
            second = output_points(base / "out/SECOND.ply")
            self.assertEqual((len(first), len(second)), (1, 2))
            np.testing.assert_allclose([first[n][0] for n in ("x", "y", "z")], [8, 21, 33])
            np.testing.assert_allclose([first[n][0] for n in ("nx", "ny", "nz")], [0, 1, 0], atol=1e-7)
            np.testing.assert_allclose([second[n][0] for n in ("nx", "ny", "nz")], [-1, 0, 0], atol=1e-7)
            np.testing.assert_allclose([second[n][1] for n in ("nx", "ny", "nz")], [0, 0, -1], atol=1e-7)
            self.assertEqual([int(first[n][0]) for n in ("red", "green", "blue")], [255, 3, 4])
            self.assertEqual(report["sources"]["TEST"]["invalid_position_or_normal_points"], 1)
            for space in report["spaces"]:
                self.assertEqual(space["sha256"], hashlib.sha256((base / "out" / space["ply"]).read_bytes()).hexdigest())
            self.assertEqual(json.loads((base / "out/provenance.json").read_text()), report)
            with self.assertRaises(FileExistsError):
                prepare.prepare(config, base / "out", local_paths={"TEST": path})

    def test_http_range_path_and_recorded_halo(self):
        config, _, packed = fixture()
        source = config["sources"]["TEST"]
        response = Response(packed, source)
        with tempfile.TemporaryDirectory() as temp, mock.patch.object(prepare.urllib.request, "urlopen", return_value=response) as opener:
            report = prepare.prepare(config, Path(temp) / "out", halo=1.1, block_bytes=1024)
            self.assertEqual([s["points"] for s in report["spaces"]], [2, 3])
            self.assertEqual(report["halo_metres"], 1.1)
            self.assertEqual(report["spaces"][0]["crop_min_with_halo"], [6.800000000000001, 19.799999999999997, 31.799999999999997])
            request = opener.call_args.args[0]
            self.assertEqual(request.get_header("Range"), f"bytes={source['pointcloud']['compressed_range'][0]}-{source['pointcloud']['compressed_range'][1]}")
            self.assertEqual(request.get_header("If-range"), '"synthetic"')

    def test_streaming_small_chunks_and_high_compression_stay_bounded(self):
        config, raw, packed = fixture()
        metadata = config["sources"]["TEST"]["pointcloud"]
        streamed = prepare.checked_chunks(io.BytesIO(packed), metadata, 1, compressed=True)
        blocks = list(prepare.vertex_blocks(streamed, metadata))
        self.assertEqual(sum(map(len, blocks)), 5)
        self.assertEqual(b"".join(p.tobytes() for p in blocks), raw[metadata["header_bytes"]:])
        raw = b"a" * 1_000_000
        compressor = zlib.compressobj(wbits=-15)
        packed = compressor.compress(raw) + compressor.flush()
        metadata = {"compressed_bytes": len(packed), "uncompressed_bytes": len(raw), "crc32_hex": f"{zlib.crc32(raw):08x}"}
        chunks = list(prepare.checked_chunks(io.BytesIO(packed), metadata, 1024, compressed=True))
        self.assertLessEqual(max(map(len, chunks)), 1024)
        self.assertEqual(b"".join(chunks), raw)

    def test_rejects_truncation_trailing_bytes_incomplete_deflate_and_crc(self):
        config, raw, packed = fixture()
        metadata = config["sources"]["TEST"]["pointcloud"]
        for data, compressed in [(packed[:-2], True), (packed + b"x", True), (raw[:-1], False), (raw + b"x", False)]:
            with self.subTest(compressed=compressed, size=len(data)), self.assertRaises(ValueError):
                list(prepare.checked_chunks(io.BytesIO(data), metadata, 7, compressed))
        for changed in [dict(metadata, compressed_bytes=len(packed)-1), dict(metadata, crc32_hex="00000000"), dict(metadata, uncompressed_bytes=len(raw)+1)]:
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                list(prepare.checked_chunks(io.BytesIO(packed[:-1] if changed["compressed_bytes"] < len(packed) else packed), changed, 7, True))

    def test_bad_final_crc_never_publishes_partial_crops(self):
        config, raw, _ = fixture()
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            raw_path = base / "raw.ply"
            raw_path.write_bytes(raw[:-1] + bytes([raw[-1] ^ 1]))
            with self.assertRaisesRegex(ValueError, "CRC32"):
                prepare.prepare(config, base / "out", local_paths={"TEST": raw_path}, block_bytes=1024)
            self.assertFalse((base / "out").exists())
            self.assertEqual(list(base.glob(".nksr-prepare-*")), [])

    def test_rejects_mismatched_http_metadata(self):
        config, _, packed = fixture()
        source = config["sources"]["TEST"]
        prepare.validate_http(Response(packed, source), source)
        for key, value in [("status", 200), ("Content-Range", "bytes 1-3/4"), ("Content-Length", "1"), ("ETag", '"changed"'), ("Content-Encoding", "gzip")]:
            response = Response(packed, source)
            if key == "status":
                response.status = value
            else:
                response.headers[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                prepare.validate_http(response, source)

    def test_rejects_wrong_alignment_and_wrong_ply_layout(self):
        config, raw, _ = fixture()
        source = config["sources"]["TEST"]
        for field, value in [("text", "wrong"), ("translation", [0, 0, 0]), ("rotation_row_major", list(np.eye(3).flat))]:
            bad = copy.deepcopy(source)
            bad["alignment"][field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                prepare.alignment(bad)
        metadata = source["pointcloud"]
        for changed_raw in [raw.replace(b"float nx", b"float xx"), raw.replace(b"vertex 5", b"vertex 6")]:
            with self.assertRaises(ValueError):
                list(prepare.vertex_blocks([changed_raw], metadata))


if __name__ == "__main__":
    unittest.main()
