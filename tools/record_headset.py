"""Record what the headset wearer sees (passthrough plus the app) to a video on this computer.

usage: python -I tools/record_headset.py [--minutes N] [--size WxH] [--out DIR] [--adb PATH] [--serial ID]

Put the headset on first: a sleeping display records nothing (adb reports INVALID_LAYER_STACK).
adb screenrecord stops every 3 minutes, so longer recordings are made of 3-minute parts that
are pulled one after another into work/recordings/<timestamp>-NN.mp4. Ctrl+C ends the
recording and still keeps the part in progress.
"""
import argparse
import glob
import shutil
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PART_SECONDS = 180  # the screenrecord maximum
DEVICE_DIR = "/sdcard/Download"


def find_adb():
    found = shutil.which("adb")
    if found:
        return found
    hub = sorted(glob.glob("/Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"))
    return hub[-1] if hub else "adb"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--minutes", type=float, default=3, help="total length (default 3)")
    # The display is 7104 x 3840; the encoder needs a smaller size of the same shape.
    parser.add_argument("--size", default="1776x960")
    parser.add_argument("--bit-rate", type=int, default=12_000_000)
    parser.add_argument("--out", type=Path, default=ROOT / "work/recordings")
    parser.add_argument("--adb", default=None)
    parser.add_argument("--serial", default=None)
    args = parser.parse_args()
    adb = [args.adb or find_adb()] + (["-s", args.serial] if args.serial else [])

    awake = subprocess.run(adb + ["shell", "dumpsys", "power"], capture_output=True, text=True).stdout
    if "mWakefulness=Awake" not in awake:
        raise SystemExit("The headset display is asleep: put the headset on, then run this again.")
    args.out.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    remaining = args.minutes * 60
    part = 0
    try:
        while remaining > 1:
            part += 1
            seconds = int(min(PART_SECONDS, remaining))
            device_file = f"{DEVICE_DIR}/twininspector-{stamp}-{part:02d}.mp4"
            print(f"Recording part {part} ({seconds} s) ... Ctrl+C to stop", flush=True)
            started = time.time()
            recorder = subprocess.Popen(adb + ["shell", "screenrecord", "--size", args.size, "--bit-rate", str(args.bit_rate),
                                               "--time-limit", str(seconds), device_file])
            try:
                recorder.wait()
            except KeyboardInterrupt:
                # Stopping screenrecord on the device (not just adb) finalises the file so it plays.
                subprocess.run(adb + ["shell", "pkill", "-INT", "screenrecord"], capture_output=True)
                recorder.wait()
                remaining = 0
            else:
                remaining -= time.time() - started
            time.sleep(1)  # let the encoder close the file
            target = args.out / f"{stamp}-{part:02d}.mp4"
            subprocess.run(adb + ["pull", device_file, str(target)], capture_output=True)
            subprocess.run(adb + ["shell", "rm", "-f", device_file], capture_output=True)
            if target.exists() and target.stat().st_size > 0:
                print(f"Saved {target} ({target.stat().st_size / 1e6:.1f} MB)")
            else:
                print(f"Part {part} produced no video (was the headset taken off?)", file=sys.stderr)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
