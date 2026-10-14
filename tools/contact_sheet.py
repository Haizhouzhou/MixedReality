"""Assemble unmodified Unity panel captures into a documented QA contact sheet."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
images = root / "TwinInspector/docs/images"
font = ImageFont.truetype(str(root / "TwinInspector/Assets/_TwinInspector/Fonts/Inter-Medium.otf"), 26)
sheet = Image.new("RGB", (1536, 1800), "#F2F1EC")
draw = ImageDraw.Draw(sheet)
draw.text((32, 24), "TWININSPECTOR / EDITOR SIMULATOR / UI VERIFICATION", font=font, fill="#111111")
for i, (name, label) in enumerate([
    ("setup", "01 / Setup"), ("mode", "02 / Compare"),
    ("align", "03 / Alignment"), ("picker", "04 / Annotation type"),
    ("list", "05 / Annotation list"), ("diagnostics", "06 / Diagnostics — Editor only")
]):
    x, y = 24 + i % 2 * 756, 90 + i // 2 * 560
    draw.text((x + 12, y), label, font=font, fill="#111111")
    with Image.open(images / f"{name}.png") as source:
        panel = source.convert("RGB")
        panel.thumbnail((732, 510), Image.Resampling.LANCZOS)
        sheet.paste(panel, (x, y + 42))
draw.text((36, 1770), "Actual running Unity captures. No headset performance or tracking claim.", font=font, fill="#111111")
sheet.save(images / "contact-sheet.png")
print(images / "contact-sheet.png")
