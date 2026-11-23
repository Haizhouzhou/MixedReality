"""Check, back up, and optionally update only twininspector2 / HG_MainHall.

Default is a read-only device preflight with a fresh local backup. --apply is
required for installation/transfer. No uninstall, clear-data, permission change,
or mutation of com.mrtwin.inspector is implemented. The app is kept stopped
after an applied update so the user can choose when to inspect it.

  python tools/install_nksr_mainhall.py --apk NEW.apk --packaged PACKAGED \
      --serial DEVICE [--apply]

Every invocation uses a new backup directory. In apply mode, the existing app
is stopped only after APK identity/signature/content checks pass. An interrupted
transfer keeps the previous external manifest; all previous revisions remain.
"""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import shlex
import subprocess
import tarfile
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = "com.mrtwin.inspector2"
ORIGINAL = "com.mrtwin.inspector"
SPACE = "HG_MainHall"
REMOTE = f"/sdcard/Android/data/{PACKAGE}/files"
PREFIX = f"twin/{SPACE}/"
ACTIVE = PREFIX + "manifest.json"
PLAYER = Path("/Applications/Unity/Hub/Editor/6000.3.26f1/PlaybackEngines/AndroidPlayer")
_spec = importlib.util.spec_from_file_location("verify_apk", Path(__file__).with_name("verify_apk.py"))
verifier = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(verifier)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def digest(path):
    return verifier.sha256(Path(path))


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n")


def safe_relative(value):
    require(isinstance(value, str) and value and "\\" not in value and
            all(ord(c) >= 32 and ord(c) != 127 for c in value), "Unsafe data filename")
    p = PurePosixPath(value)
    require(not p.is_absolute() and p.as_posix() == value and
            all(part not in (".", "..") for part in p.parts), "Unsafe relative path")
    return value


def command(arguments, env=None):
    return subprocess.run([str(a) for a in arguments], check=True, capture_output=True, env=env).stdout


class Device:
    def __init__(self, executable, serial):
        self.prefix = [str(executable), "-s", serial]

    def run(self, *args):
        return command(self.prefix + list(args))

    def shell(self, *args):
        # ADB shell accepts one complete remote command; quote every path explicitly.
        return self.run("shell", "-T", shlex.join([str(a) for a in args]))

    def hash(self, remote):
        line = self.shell("sha256sum", remote).decode().strip()
        value = line.split(None, 1)[0]
        require(bool(re.fullmatch(r"[0-9a-f]{64}", value)), "Invalid remote SHA256 response")
        return value

    def inventory(self):
        links = self.shell("find", REMOTE, "-type", "l", "-print0")
        require(not links, "External data contains symlinks; inspect before a complete backup")
        paths = self.shell("find", REMOTE, "-type", "f", "-print0").decode().split("\0")
        result, full_paths = {}, []
        for path in filter(None, paths):
            require(path.startswith(REMOTE + "/"), "Unexpected path in device inventory")
            relative = safe_relative(path[len(REMOTE) + 1:])
            require(relative not in result, "Duplicate device file")
            result[relative] = None
            full_paths.append(path)
        for start in range(0, len(full_paths), 40):
            batch = full_paths[start:start + 40]
            lines = self.shell("sha256sum", *batch).decode().splitlines()
            require(len(lines) == len(batch), "Incomplete device hash inventory")
            for line in lines:
                value, path = line.split(None, 1)
                require(path in batch and re.fullmatch(r"[0-9a-f]{64}", value), "Invalid device hash inventory")
                result[path[len(REMOTE) + 1:]] = value
        require(all(result.values()), "Missing file in device hash inventory")
        return result

    def package_snapshot(self, package):
        output = self.shell("pm", "path", package).decode().strip()
        paths = []
        for line in output.splitlines():
            require(line.startswith("package:"), "Unexpected Android package path")
            path = line[len("package:"):]
            require(path.startswith("/data/app/") and "\n" not in path, "Unexpected installed APK path")
            paths.append(path)
        return {path: self.hash(path) for path in paths}


def android_metadata(apk, player):
    versions = sorted((player / "SDK/build-tools").glob("*"),
                      key=lambda p: tuple(int(x) for x in re.findall(r"\d+", p.name)))
    require(bool(versions), "Android build tools were not found")
    env = os.environ.copy()
    env["JAVA_HOME"] = str(player / "OpenJDK")
    badging = command([versions[-1] / "aapt", "dump", "badging", apk], env).decode()
    found = re.search(r"^package: name='([^']+)' versionCode='(\d+)'", badging, re.M)
    label = re.search(r"^application-label:'([^']+)'", badging, re.M)
    require(found is not None and label is not None, "APK identity/label could not be read")
    cert = command([versions[-1] / "apksigner", "verify", "--print-certs", apk], env).decode()
    signers = re.findall(r"Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]{64})", cert)
    require(len(signers) == 1, "Expected one verified APK signer")
    return {"package": found[1], "version_code": int(found[2]), "label": label[1],
            "signer_sha256": signers[0].lower()}


def asset_hashes(apk, mainhall=False):
    """APK baseline assets other than MainHall, including the exact space catalog."""
    with zipfile.ZipFile(apk) as z:
        require(len(z.namelist()) == len(set(z.namelist())), "Duplicate ZIP members in APK")
        return {n: hashlib.sha256(z.read(n)).hexdigest() for n in z.namelist()
                if not n.endswith("/") and n.startswith("assets/twins/")
                and (mainhall or not n.startswith("assets/twins/HG_MainHall/"))
                and n != "assets/twins/nksr-trial.json"}


def frame(manifest):
    return {"schema": manifest["schema"], "scan": manifest["scan"], "frame": manifest["frame"],
            "origin_world": manifest["origin_world"], "datum_z": manifest.get("datum_z", 0),
            "region": manifest["region"]}


def preserved(before, after):
    for path, value in before.items():
        if path != ACTIVE:
            require(after.get(path) == value, "Protected file changed or vanished: " + path)
    require(all(p in before or p.startswith(PREFIX) for p in after),
            "Unexpected new file outside HG_MainHall")


def private_preferences(device, archive=None):
    """Read-only optional backup; never enables debugging or changes permissions."""
    result = subprocess.run(device.prefix + ["exec-out", "run-as", PACKAGE,
                            "tar", "-cf", "-", "shared_prefs"], capture_output=True)
    if result.returncode:
        return {"available": False, "reason": "run-as/shared_prefs unavailable; no permissions changed"}
    try:
        files = {}
        with tarfile.open(fileobj=io.BytesIO(result.stdout), mode="r:") as tar:
            for item in tar.getmembers():
                if item.isfile():
                    safe_relative(item.name)
                    require(item.name.startswith("shared_prefs/"), "Unexpected private backup entry")
                    files[item.name] = hashlib.sha256(tar.extractfile(item).read()).hexdigest()
        if archive:
            archive.write_bytes(result.stdout)
            archive.chmod(0o600)
        return {"available": True, "files": files}
    except (tarfile.TarError, RuntimeError):
        return {"available": False, "reason": "Private backup could not be verified; no permissions changed"}


def deploy(args):
    # All local content checks happen before any command capable of changing the device.
    packaged, apk = args.packaged.resolve(), args.apk.resolve()
    verifier.validate_nksr_trial(packaged)
    verifier.verify(apk, packaged, args.max_apk_bytes, args.max_space_bytes, nksr_mainhall=True)
    new = android_metadata(apk, args.android_player)
    require(new["package"] == PACKAGE and new["label"] == "twininspector2",
            "Only package com.mrtwin.inspector2 / label twininspector2 can be updated")
    apk_hash = digest(apk)
    source = packaged / SPACE
    manifest = json.loads((source / "manifest.json").read_text())
    proof = json.loads((source / "nksr-provenance.json").read_text())
    revision = digest(source / "manifest.json")
    require(proof["manifest_sha256"] == revision, "Stale MainHall proof")
    expected_tiles = {safe_relative(p) for tile in manifest["tiles"] for p in tile["files"].values()}
    require(expected_tiles == set(proof["files"]), "Tile proof differs from all manifest LOD paths")
    for relative in expected_tiles:
        require(relative.startswith("tiles/") and relative.endswith(".glb") and ":" not in relative and ".." not in relative and
                digest(source / relative) == proof["files"][relative], "Invalid tile proof")

    record_dir = args.record or ROOT / "work/device-backups" / (
        datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ") + "-nksr-mainhall-" + uuid.uuid4().hex[:8])
    record_dir.mkdir(parents=True, exist_ok=False, mode=0o700)
    device = Device(args.adb, args.serial)
    require(device.run("get-state").decode().strip() == "device", "Headset is not authorized/connected")
    installed = device.package_snapshot(PACKAGE)
    require(len(installed) == 1 and next(iter(installed)).endswith("/base.apk"),
            "Expected an existing single-APK twininspector2 installation; no fresh install is allowed")
    installed_path, installed_hash = next(iter(installed.items()))
    pulled = record_dir / "installed-base.apk"
    device.run("pull", installed_path, str(pulled))
    require(digest(pulled) == installed_hash == device.hash(installed_path), "Installed APK changed during pull")
    old = android_metadata(pulled, args.android_player)
    require(old["package"] == PACKAGE and old["label"] == "twininspector2", "Installed application identity differs")
    require(old["signer_sha256"] == new["signer_sha256"], "Signer mismatch: update blocked; app was not changed")
    require(new["version_code"] >= old["version_code"], "Version downgrade is blocked")
    require(asset_hashes(pulled) == asset_hashes(apk), "Other spaces or space catalog differ from installed APK")
    with zipfile.ZipFile(pulled) as z:
        old_manifest = json.loads(z.read("assets/twins/HG_MainHall/manifest.json"))
    require(frame(old_manifest) == frame(manifest), "MainHall world frame, crop, origin or datum changed")
    original = device.package_snapshot(ORIGINAL)
    if args.apply:
        device.shell("am", "force-stop", PACKAGE)

    print("Backing up and hashing all twininspector2 external files...", flush=True)
    before = device.inventory()
    device.run("pull", REMOTE, str(record_dir / "files"))
    local = {p.relative_to(record_dir / "files").as_posix(): digest(p)
             for p in (record_dir / "files").rglob("*") if p.is_file()}
    require(local == before == device.inventory(), "External backup is incomplete or changed during capture")
    if ACTIVE in before:
        external_manifest = json.loads((record_dir / "files" / ACTIVE).read_text())
        require(frame(external_manifest) == frame(manifest), "Active external MainHall frame differs")
    private = private_preferences(device, record_dir / "private-shared-prefs.tar")
    record = {"schema": "nksr-mainhall-install/1", "status": "checked", "applied": False,
              "serial": args.serial, "package": PACKAGE, "label": new["label"],
              "apk_sha256": apk_hash, "new_apk": new, "installed_apk_before": old,
              "installed_apk_sha256_before": installed_hash, "original_package_apks_before": original,
              "external_files_before": before, "private_preferences_before": private,
              "backup": str(record_dir.resolve()), "revision": revision,
              "protected_policy": "Every existing file except HG_MainHall/manifest.json must remain byte-identical"}
    report_path = record_dir / "deployment.json"
    write_json(report_path, record)
    if not args.apply:
        print(f"CHECKED. Matching signer and verified backup: {record_dir}. No device files changed.")
        return record

    require(device.package_snapshot(PACKAGE) == installed and digest(apk) == apk_hash,
            "Application changed after preflight; refusing install")
    require(device.inventory() == before, "Data changed after backup; refusing install")
    print("Installing the same package with data-preserving adb install -r...", flush=True)
    device.run("install", "-r", str(apk))
    record["status"] = "apk_installed_manifest_not_activated"
    write_json(report_path, record)
    require(set(device.package_snapshot(PACKAGE).values()) == {apk_hash}, "Installed APK hash differs")
    require(device.package_snapshot(ORIGINAL) == original, "Original inspector package unexpectedly changed")
    require(device.inventory() == before, "External data changed during install")

    remote_space = REMOTE + "/" + PREFIX.rstrip("/")
    version_prefix = "versions/" + revision + "/"
    remote_version = remote_space + "/" + version_prefix.rstrip("/")
    runtime = copy.deepcopy(manifest)
    for tile in runtime["tiles"]:
        tile["files"] = {lod: version_prefix + relative for lod, relative in tile["files"].items()}
    staged = record_dir / "activation"
    staged.mkdir()
    runtime_path = staged / "runtime-manifest.json"
    write_json(runtime_path, runtime)
    runtime_hash = digest(runtime_path)
    receipt = {"schema": "nksr-external-mainhall/1", "package": PACKAGE, "space": SPACE,
               "packaged_manifest_sha256": revision, "packaged_proof_sha256": digest(source / "nksr-provenance.json"),
               "active_manifest_sha256": runtime_hash,
               "files_relative_to_space": {version_prefix + p: h for p, h in proof["files"].items()},
               "apk_sha256": apk_hash, "frame": frame(manifest)}
    receipt_name = "external-deployment-" + apk_hash + ".json"
    receipt_path = staged / receipt_name
    write_json(receipt_path, receipt)
    transfers = {relative: source / relative for relative in sorted(expected_tiles)}
    transfers.update({"packaged-manifest.json": source / "manifest.json",
                      "packaged-nksr-provenance.json": source / "nksr-provenance.json",
                      "runtime-manifest.json": runtime_path, receipt_name: receipt_path})
    for name in ("reconstruction.json", "preparation.json", "source-audit.json", "seams.json", "LICENSE.txt"):
        if (source / name).is_file():
            transfers[name] = source / name
    staged_hashes = {}
    for relative, path in transfers.items():
        remote_file = remote_version + "/" + safe_relative(relative)
        key = PREFIX + version_prefix + relative
        expected_hash = digest(path)
        if relative in proof["files"]:
            require(expected_hash == proof["files"][relative], "Source tile changed after preflight")
        staged_hashes[key] = expected_hash
        if key in before:
            require(before[key] == expected_hash, "Existing revision file differs; never overwriting: " + key)
        else:
            device.shell("mkdir", "-p", str(PurePosixPath(remote_file).parent))
            device.run("push", str(path), remote_file)
        require(device.hash(remote_file) == expected_hash, "Transferred asset hash mismatch: " + relative)

    # Protect every old revision and unrelated file before the one allowed activation rename.
    transferred = device.inventory()
    preserved(before, transferred)
    require(all(transferred.get(p) == h for p, h in staged_hashes.items()), "Staged revision changed before activation")
    pending = remote_space + "/manifest.pending-nksr-" + uuid.uuid4().hex
    device.run("push", str(runtime_path), pending)
    require(device.hash(pending) == runtime_hash, "Pending manifest hash mismatch")
    if ACTIVE in before:
        require(device.hash(remote_space + "/manifest.json") == before[ACTIVE], "Active manifest changed")
        saved_manifest = remote_space + "/manifest.before-nksr-" + uuid.uuid4().hex + ".json"
        device.shell("cp", remote_space + "/manifest.json", saved_manifest)
        require(device.hash(saved_manifest) == before[ACTIVE], "Old on-device manifest backup differs")
        record["previous_manifest_remote_backup"] = saved_manifest
    record["active_manifest_sha256"] = runtime_hash
    record["status"] = "assets_verified_activation_pending"
    write_json(report_path, record)
    device.shell("mv", pending, remote_space + "/manifest.json")
    require(device.hash(remote_space + "/manifest.json") == runtime_hash, "Active manifest verification failed")
    after = device.inventory()
    preserved(before, after)
    require(all(after.get(p) == h for p, h in staged_hashes.items()), "Staged revision changed after activation")
    require(device.package_snapshot(ORIGINAL) == original, "Original inspector package unexpectedly changed")
    private_after = private_preferences(device)
    if private["available"] and private_after["available"]:
        require(private["files"] == private_after["files"], "Private preference contents changed")
    record.update(status="complete", applied=True, external_files_after=after,
                  private_preferences_after=private_after,
                  private_preferences_verified=private["available"] and private_after["available"])
    write_json(report_path, record)
    print(f"Updated only twininspector2/HG_MainHall. Old files retained; backup/report: {record_dir}")
    return record


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--packaged", type=Path, required=True)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--adb", type=Path, default=PLAYER / "SDK/platform-tools/adb")
    parser.add_argument("--android-player", type=Path, default=PLAYER)
    parser.add_argument("--record", type=Path, help="New backup/report directory; must not already exist")
    parser.add_argument("--max-apk-bytes", type=int, default=1_500_000_000)
    parser.add_argument("--max-space-bytes", type=int, default=1_400_000_000)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--check-only", action="store_true", help="Default: fresh backup and checks, no device mutation")
    mode.add_argument("--apply", action="store_true", help="Explicitly perform verified install-r and MainHall activation")
    deploy(parser.parse_args())


if __name__ == "__main__":
    main()
