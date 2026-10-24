"""Transfer a space's full data to the headset (macOS, Linux or Windows).

usage: python -I tools/push_space.py <space id> [--source DIR] [--adb PATH] [--serial ID]

<space id> is its id in twins/spaces.json, e.g. HG_E3_Hall. The data is read from
work/twin/<space id> (the pipeline output) unless --source points elsewhere, and goes to
files/twin/<space id> on the headset; a markers.json next to the manifest is sent too.
Same safety rules as push_twin.ps1: tiles go into a manifest-hash revision folder,
and the manifest is activated last, so an interrupted transfer keeps the previous data.
"""
import argparse
import glob
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

SAFE = re.compile(r"^[A-Za-z0-9_.-]+$")


def find_adb():
    found = shutil.which("adb")
    if found:
        return found
    # Unity Hub's Android module on macOS.
    hub = sorted(glob.glob("/Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"))
    return hub[-1] if hub else "adb"


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("space")
    parser.add_argument("--source", type=Path, help="pipeline output or unzipped release folder; default work/twin/<space id>")
    parser.add_argument("--adb", default=None)
    parser.add_argument("--serial", default="")
    parser.add_argument("--app", default="com.mrtwin.inspector")
    args = parser.parse_args()
    if not SAFE.match(args.space) or not re.match(r"^[A-Za-z0-9_.]+$", args.app):
        parser.error("Unsafe space or application id")
    args.source = args.source or Path(__file__).resolve().parents[1] / "work" / "twin" / args.space
    markers = args.source / "markers.json"
    manifest_path = args.source / "manifest.json"
    if not manifest_path.is_file():
        raise SystemExit(f"No manifest.json in {args.source}: run the pipeline with --out work/twin/{args.space} or unzip its release asset into work/twin")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for tile in manifest["tiles"]:
        for relative in tile["files"].values():
            if ".." in relative or relative.startswith("/") or not (args.source / relative).is_file():
                raise SystemExit(f"Missing or unsafe tile file: {relative}")
    adb = [args.adb or find_adb()] + (["-s", args.serial] if args.serial else [])

    def run(*command):
        result = subprocess.run(adb + list(command), capture_output=True, text=True)
        if result.returncode != 0:
            raise SystemExit(f"adb {' '.join(command)} failed: {result.stderr.strip() or result.stdout.strip()}")
        return result.stdout + result.stderr  # adb push reports its summary on either stream

    if run("get-state").strip() != "device":
        raise SystemExit("Connect one authorized headset, or pass --serial.")
    destination = f"/sdcard/Android/data/{args.app}/files/twin/{args.space}"
    revision = hashlib.sha256(manifest_path.read_bytes()).hexdigest()[:16]
    version = f"{destination}/versions/{revision}"
    tiles = args.source / "tiles"
    # Galaxy XR allows shell mkdir here but rejects ADB sync's recursive secure_mkdirs.
    directories = [f"{version}/tiles"] + [f"{version}/tiles/{d.relative_to(tiles).as_posix()}" for d in sorted(tiles.rglob("*")) if d.is_dir()]
    if not all(SAFE.match(d.rsplit("/", 1)[1]) for d in directories):
        raise SystemExit("Unsafe tile directory name")
    run("shell", "mkdir", "-p", *directories)
    print((run("push", "--sync", str(tiles), f"{version}/").strip().splitlines() or ["tiles pushed"])[-1])
    if markers.is_file():
        run("push", str(markers), f"{destination}/markers.json")
    for tile in manifest["tiles"]:
        tile["files"] = {lod: f"versions/{revision}/{relative}" for lod, relative in tile["files"].items()}
    with tempfile.TemporaryDirectory() as temp:
        staged = Path(temp) / "manifest.json"
        staged.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        run("push", str(staged), f"{destination}/manifest.json.pending")
    run("shell", "mv", f"{destination}/manifest.json.pending", f"{destination}/manifest.json")
    print(f"Pushed {args.space}: {len(manifest['tiles'])} tiles, revision {revision}. Restart TwinInspector or reopen the space list.")


if __name__ == "__main__":
    main()
