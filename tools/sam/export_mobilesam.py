"""Reproducible CPU-only MobileSAM ONNX export and a NavVis column smoke test.

Run with work/sam-venv/Scripts/python.exe -I. Checkpoints and ONNX stay ignored.
This validates a predicted mask score, not measured segmentation accuracy.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import urllib.request

REVISION = "f706ad9c4eb7f219c00d9050e46328518ffb65d2"
SOURCE = f"https://raw.githubusercontent.com/ChaoningZhang/MobileSAM/{REVISION}"
MEAN = [123.675, 116.28, 103.53]
STD = [58.395, 57.12, 57.375]
ROOT = Path(__file__).resolve().parents[2]
IMAGE_ROOT = ROOT / "dataset/raw/HGE/sessions/navvis_2022-02-06_12.55.11/raw_data/images_undistr_center"


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def fetch(relative, destination):
    if not destination.exists() or destination.stat().st_size == 0:
        temporary = destination.with_suffix(destination.suffix + ".pending")
        with urllib.request.urlopen(f"{SOURCE}/{relative}", timeout=120) as response, temporary.open("wb") as stream:
            while block := response.read(1024 * 1024):
                stream.write(block)
        temporary.replace(destination)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--work", type=Path, default=ROOT / "work/sam")
    parser.add_argument("--image", type=Path, default=IMAGE_ROOT / "00000-cam0__center.jpg")
    parser.add_argument("--point", type=float, nargs=2, default=[260, 1100], metavar=("X", "Y"))
    args = parser.parse_args()
    image_path = args.image.resolve()
    if not image_path.is_relative_to(IMAGE_ROOT.resolve()):
        parser.error("Only the specified read-only NavVis image directory is permitted")
    args.work.mkdir(parents=True, exist_ok=True)
    checkpoint = args.work / "mobile_sam.pt"
    official = args.work / "export_onnx_model.py"
    fetch("weights/mobile_sam.pt", checkpoint)
    fetch("scripts/export_onnx_model.py", official)

    import numpy as np
    import torch
    import onnx
    import onnxruntime as ort
    from PIL import Image
    from mobile_sam import sam_model_registry

    torch.set_num_threads(2)
    torch.manual_seed(1303)
    encoder = args.work / "mobile_sam_encoder.onnx"
    decoder = args.work / "mobile_sam_decoder.onnx"
    if not encoder.exists():
        sam = sam_model_registry["vit_t"](checkpoint=str(checkpoint)).eval().cpu()
        with torch.no_grad():
            torch.onnx.export(sam.image_encoder, torch.zeros(1, 3, 1024, 1024), str(encoder),
                              opset_version=17, input_names=["image"], output_names=["image_embeddings"])
        del sam
    if not decoder.exists():
        subprocess.run([sys.executable, "-I", str(official), "--checkpoint", str(checkpoint),
                        "--model-type", "vit_t", "--output", str(decoder),
                        "--return-single-mask", "--opset", "17"], check=True)
    for path in (encoder, decoder):
        onnx.checker.check_model(str(path))
    options = ort.SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    providers = ["CPUExecutionProvider"]
    session = ort.InferenceSession(str(encoder), options, providers=providers)
    original = Image.open(image_path).convert("RGB")
    width, height = original.size
    scale = 1024 / max(width, height)
    new_width, new_height = int(width * scale + .5), int(height * scale + .5)
    resized = np.asarray(original.resize((new_width, new_height), Image.Resampling.BILINEAR), dtype=np.float32)
    # Normalize first, then zero-pad as SAM does; padded values are zero in normalized space.
    normalized = (resized - np.asarray(MEAN, dtype=np.float32)) / np.asarray(STD, dtype=np.float32)
    input_image = np.zeros((1, 3, 1024, 1024), dtype=np.float32)
    input_image[0, :, :new_height, :new_width] = normalized.transpose(2, 0, 1)
    embeddings = session.run(None, {"image": input_image})[0]
    del session
    session = ort.InferenceSession(str(decoder), options, providers=providers)
    point = [args.point[0] * new_width / width, args.point[1] * new_height / height]
    inputs = {"image_embeddings": embeddings, "point_coords": np.array([[point, [0, 0]]], dtype=np.float32),
              "point_labels": np.array([[1, -1]], dtype=np.float32),
              "mask_input": np.zeros((1, 1, 256, 256), dtype=np.float32),
              "has_mask_input": np.zeros(1, dtype=np.float32),
              "orig_im_size": np.array([height, width], dtype=np.float32)}
    values = dict(zip([o.name for o in session.get_outputs()], session.run(None, inputs)))
    pixels = int(np.count_nonzero(values["masks"] > 0))
    score = float(values["iou_predictions"].max())
    result = {"revision": REVISION, "checkpoint_url": f"{SOURCE}/weights/mobile_sam.pt",
              "checkpoint_sha256": digest(checkpoint), "official_export_sha256": digest(official),
              "encoder_sha256": digest(encoder), "decoder_sha256": digest(decoder),
              "encoder_bytes": encoder.stat().st_size, "decoder_bytes": decoder.stat().st_size,
              "image": image_path.relative_to(ROOT).as_posix(), "image_sha256": digest(image_path),
              "prompt_original_pixels": args.point, "nonempty_mask_pixels": pixels, "predicted_iou": score,
              "mean": MEAN, "std": STD, "input_size": 1024, "opset": 17,
              "validation": "PASS" if pixels > 0 and score > .5 else "FAIL",
              "note": "Predicted IoU is model confidence, not measured IoU against a ground-truth mask."}
    report = json.dumps(result, indent=2) + "\n"
    (args.work / "onnx-manifest.json").write_text(report, encoding="utf-8")
    (ROOT / "TwinInspector/docs/evidence/sam-export-v13.txt").write_text(report, encoding="utf-8")
    print(report)
    if result["validation"] != "PASS":
        raise SystemExit("Column mask validation failed")


if __name__ == "__main__":
    main()
