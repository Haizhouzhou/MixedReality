"""Subset Noto Sans SC to the characters the UI can show; rerun after editing any Strings/*.json.

usage: python -I tools/subset_cjk_font.py <dir with NotoSansSC-{Regular,Medium,Bold}.otf>

Source: https://github.com/notofonts/noto-cjk (Sans/SubsetOTF/SC), SIL Open Font License 1.1.
The subsets are runtime fallbacks for the Inter UI fonts; see CjkFonts.cs.
"""
import argparse
import json
from pathlib import Path

from fontTools import subset

ROOT = Path(__file__).resolve().parents[1]
STRINGS = ROOT / "TwinInspector/Assets/_TwinInspector/Resources/Strings"
SPACES = ROOT / "TwinInspector/Assets/StreamingAssets/twins/spaces.json"
DEST = ROOT / "TwinInspector/Assets/_TwinInspector/Resources/Fonts"
WEIGHTS = ("Regular", "Medium", "Bold")


def characters():
    text = set(chr(c) for c in range(0x20, 0x7F))
    for table in STRINGS.glob("*.json"):
        if table.stem == "slots":
            continue
        for value in json.loads(table.read_text(encoding="utf-8-sig")).values():
            text.update(value)
    for space in json.loads(SPACES.read_text(encoding="utf-8"))["spaces"]:
        text.update(space["name"])
    text.discard("\n")
    return "".join(sorted(text))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    args = parser.parse_args()
    text = characters()
    DEST.mkdir(parents=True, exist_ok=True)
    options = subset.Options()
    options.name_IDs = ["*"]
    options.notdef_outline = True
    for weight in WEIGHTS:
        font = subset.load_font(str(args.source / f"NotoSansSC-{weight}.otf"), options)
        subsetter = subset.Subsetter(options)
        subsetter.populate(text=text)
        subsetter.subset(font)
        out = DEST / f"NotoSansSC-{weight}.otf"
        subset.save_font(font, str(out), options)
        print(f"{out.relative_to(ROOT)}: {len(text)} characters, {out.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()
