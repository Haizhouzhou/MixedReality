"""Check that UI strings and slider readouts fit their text boxes, using the real font advance widths.

usage: python -I tools/check_text_fit.py [--margin 0.95]

Box sizes mirror the layouts in InspectorUI.cs / PanelBase.cs (button text area = button width - 24,
body 21 px with auto-size down to 16, labels 12 px, titles 24 px semibold). Inter is measured first and
Noto Sans SC is the fallback for CJK glyphs, as at runtime. Run it after editing Strings/*.json or a layout.
"""
import argparse
import json
from pathlib import Path

from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "TwinInspector/Assets/_TwinInspector"
LANGUAGES = ("en", "zh", "de", "fr", "it")


def advance(path):
    font = TTFont(path)
    cmap, metrics, em = font.getBestCmap(), font["hmtx"], font["head"].unitsPerEm
    return lambda ch: metrics[cmap[ord(ch)]][0] / em if ord(ch) in cmap else None


INTER = {w: advance(APP / f"Fonts/Inter-{n}.otf") for w, n in (("regular", "Regular"), ("medium", "Medium"), ("semibold", "SemiBold"))}
NOTO = {w: advance(APP / f"Resources/Fonts/NotoSansSC-{n}.otf") for w, n in (("regular", "Regular"), ("medium", "Medium"), ("semibold", "Bold"))}


def width(text, size, weight="regular"):
    total = 0
    for ch in text:
        a = INTER[weight](ch)
        a = NOTO[weight](ch) if a is None else a
        if a is None:
            raise SystemExit(f"No glyph for {ch!r} in {text!r}: rerun tools/subset_cjk_font.py")
        total += a
    return total * size


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--margin", type=float, default=.95, help="fraction of each box the text may use")
    args = parser.parse_args()
    strings = {c: json.loads((APP / f"Resources/Strings/{c}.json").read_text(encoding="utf-8-sig")) for c in LANGUAGES}
    spaces = [s["name"] for s in json.loads((ROOT / "TwinInspector/Assets/StreamingAssets/twins/spaces.json").read_text())["spaces"]]
    problems, checked = [], 0

    def check(name, text, box, size, weight="regular", shrink_to=None):
        nonlocal checked
        checked += 1
        limit = box * args.margin
        if width(text, size, weight) <= limit or (shrink_to and width(text, shrink_to, weight) <= limit):
            return
        problems.append(f"{name}: {text!r} needs {width(text, shrink_to or size, weight):.0f}px, box {box}px")

    for text in ("-30 cm", "-20 cm", "90 cm", "60 cm", "-45°", "45°"):
        check("Menu position readout", text, 96, 21)
    for text in ("100 %", "5.0 m", "0.5 m"):
        check("opacity/lens/volume readout", text, 72, 21)
    for c, t in strings.items():
        for key in ("text.dockDistance", "text.dockSideways", "text.dockHeight"):
            check(f"{c} {key}", t[key], 104, 16)
        check(f"{c} text.dockAutoHide", t["text.dockAutoHide"], 432, 12)
        for key in ("button.palmOn", "button.palmOff", "button.reset", "button.back", "button.guide", "button.diagnostics", "button.close",
                    "button.sceneProbe", "button.depthProbe", "button.cameraProbe"):
            check(f"{c} {key}", t[key], 120, 21, shrink_to=16)
        for key in ("button.menuPosition", "button.language", "button.feedbackSound", "button.preferences", "button.cancel", "button.switch", "button.skip",
                    ):
            check(f"{c} {key}", t[key], 184, 21, shrink_to=16)
        for code in LANGUAGES:
            check(f"{c} language.{code}", t["language." + code], 120, 21, shrink_to=16)
        for name in spaces:
            check(f"{c} button.space0", t["button.space0"].replace("{0}", name), 184, 21, shrink_to=16)
            check(f"{c} space button", name, 184, 21, shrink_to=16)
            check(f"{c} page.space.switchTo0", t["page.space.switchTo0"].replace("{0}", name), 432, 24, "semibold")
            check(f"{c} space hint", t["text.skipOpens0"].replace("{0}", name) + " · " + t["text.greyNoDataOnHeadset"], 432, 12)
        check(f"{c} probe.sceneRunning", t["probe.sceneRunning"].replace("{0}", "30").replace("{1}", "999"), 432, 16)
        check(f"{c} probe.sceneDone", t["probe.sceneDone"].replace("{0}", "999").replace("{1:F1}", "9999.9"), 432, 16)
        # Auto align: buttons, the 18 px two-line readout (one line each), 16 px tips (two lines), the Align page label.
        for key in ("button.autoAlign", "button.useMarker", "button.fineAdjustment", "button.heightFloor"):
            check(f"{c} {key}", t[key], 184, 21, shrink_to=16)
        for key in ("button.rescan", "button.resetScan", "button.alignNow"):
            check(f"{c} {key}", t[key], 120, 21, shrink_to=16)
        check(f"{c} toast.scanReset", t["toast.scanReset"], 432, 21, shrink_to=16)
        check(f"{c} button.saveAlignment", t["button.saveAlignment"], 408, 21, shrink_to=16)
        check(f"{c} button.autoAlign heading", t["button.autoAlign"], 432, 24, "semibold")
        check(f"{c} text.dragTwinHint", t["text.dragTwinHint"], 432, 12)
        coverage = t["text.autoAlign.coverage"].replace("{0:F1}", "999.9").replace("{1:F0}", "6").replace("{2:F1}", "999.9").replace("{3:F0}", "2")
        result = t["text.autoAlign.result"].replace("{0:F0}", "100").replace("{1:F1}", "4.0")
        review = t["text.autoAlign.reviewTip"].replace("{0:F2}", "99.99").replace("{1:F1}", "180.0")
        cross = t["text.autoAlign.cross"].replace("{0:F1}", "99.9").replace("{1:F1}", "1.5")
        for key, text in (("coverage", coverage), ("result", result), ("cross", cross)):
            check(f"{c} text.autoAlign.{key}", text, 432, 18)
        for key in ("matching", "reliable", "uncertain", "slides"):
            check(f"{c} text.autoAlign.{key}", t["text.autoAlign." + key], 432, 18)
        # Failures stand alone in the readout, and the tips have their own box: two lines each, with slack for word wrap.
        for key in ("failedWalls", "failedPoints", "failedMatch", "failedError"):
            check(f"{c} text.autoAlign.{key} (two lines)", t["text.autoAlign." + key], 2 * 432 * .85, 18)
        for key, text in (("tip", t["text.autoAlign.tip"]), ("noMesh", t["text.autoAlign.noMesh"]), ("reviewTip", review)):
            check(f"{c} text.autoAlign.{key} (two lines)", text, 2 * 432 * .85, 16)
        # Toasts: one line, 432 px, body size auto-shrinking to 16.
        check(f"{c} toast.sceneProbeStarted", t["toast.sceneProbeStarted"], 432, 21, shrink_to=16)
        check(f"{c} probe.sceneDone toast", t["probe.sceneDone"].replace("{0}", "999").replace("{1:F1}", "9999.9"), 432, 21, shrink_to=16)
        check(f"{c} meta.0D2Of1D2Ready", t["meta.0D2Of1D2Ready"].replace("{0:D2}", "04").replace("{1:D2}", "04"), 156, 10)
        for key in ("page.space.heading", "button.menuPosition"):
            check(f"{c} {key} heading", t[key], 432, 24, "semibold")
    print(f"checked {checked} strings at {args.margin:.0%} of their boxes")
    if problems:
        raise SystemExit("\n".join(problems))
    print("all fit")


if __name__ == "__main__":
    main()
