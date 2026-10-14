"""Generate deterministic, detector-verified A4 ArUco and QR print sheets."""
from pathlib import Path
import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont
import qrcode

out = Path(__file__).parent / "markers"
out.mkdir(exist_ok=True)
dpi = 300
mm = lambda value: round(value * dpi / 25.4)
dictionary = cv2.aruco.getPredefinedDictionary(cv2.aruco.DICT_4X4_50)
marker = cv2.aruco.generateImageMarker(dictionary, 0, mm(150))
font_path = Path(__file__).parents[1] / "work/fonts/Inter-Regular.otf"
font = ImageFont.truetype(str(font_path), 38) if font_path.exists() else ImageFont.load_default(size=38)


def sheet(symbol, name, title, symbol_mm=150):
    page = Image.new("RGB", (mm(210), mm(297)), "white")
    symbol = symbol.resize((mm(symbol_mm), mm(symbol_mm)), Image.Resampling.NEAREST)
    page.paste(symbol, ((page.width - symbol.width) // 2, mm(65)))
    draw = ImageDraw.Draw(page)
    draw.text((mm(24), mm(22)), "TWININSPECTOR / REGISTRATION", fill="#111111", font=font)
    draw.text((mm(24), mm(40)), title, fill="#111111", font=font)
    draw.text((mm(24), mm(240)), "Print A4 at 100% / actual size", fill="#111111", font=font)
    draw.text((mm(24), mm(252)), "Check symbol width: 150 mm", fill="#111111", font=font)
    draw.line((mm(24), mm(275), mm(124), mm(275)), fill="#111111", width=3)
    draw.text((mm(24), mm(280)), "Scale check: 100 mm", fill="#111111", font=font)
    page.save(out / name, dpi=(dpi, dpi))
    return np.asarray(page)


page = sheet(Image.fromarray(marker), "marker_00.png", "ArUco 4x4_50 / ID 0")
corners, ids, _ = cv2.aruco.ArucoDetector(dictionary).detectMarkers(page)
assert ids is not None and ids.flatten().tolist() == [0], "Generated marker must decode as ID 0"
qr = qrcode.QRCode(version=1, error_correction=qrcode.constants.ERROR_CORRECT_M, box_size=20, border=0)
qr.add_data("TWIN-01")
qr.make(fit=True)
page = sheet(qr.make_image().convert("RGB"), "qr_twin_01.png", "QR / TWIN-01")
# Detector's default scan window assumes camera-size frames, not a 3508px
# print raster. Test the exported sheet at a realistic camera resolution.
decoded, _, _ = cv2.QRCodeDetector().detectAndDecode(cv2.resize(page, None, fx=.5, fy=.5))
assert decoded == "TWIN-01", "Generated QR must round-trip"
print("PASS: ArUco ID 0 and QR TWIN-01 decoded; 150 mm symbols on A4 at 300 dpi")
