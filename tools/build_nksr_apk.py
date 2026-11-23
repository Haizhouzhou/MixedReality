"""Build a twininspector2 MainHall-only NKSR update, restoring baseline assets/settings.

python tools/build_nksr_apk.py --packaged work/nksr-comparison/export/packaged
python tools/build_nksr_apk.py --recover work/build-records/nksr-stage-.../recovery.json

Run with Unity closed. No device commands are used. Signing comes from existing
TWIN2_* environment variables, or the ignored local signing JSON. Values are
never printed. SIGINT/SIGTERM restore assets; after a hard interruption use the
recorded --recover command before opening Unity or starting another build.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / "TwinInspector"
BUILD = ROOT / "work/build-records/twininspector2-nksr-mainhall"
APK = BUILD / "twininspector2-nksr-mainhall.apk"
APPLICATION_ID = "com.mrtwin.inspector2"
LABEL = "twininspector2"
SIGNING_KEYS = ("TWIN2_KEYSTORE", "TWIN2_KEYSTORE_PASS", "TWIN2_KEY_ALIAS", "TWIN2_KEY_PASS")
_spec = importlib.util.spec_from_file_location("verify_apk", Path(__file__).with_name("verify_apk.py"))
verifier = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(verifier)


def write_json(path, data):
    path = Path(path)
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(data, indent=2, allow_nan=False) + "\n")
    os.replace(temporary, path)


def tree_hashes(root):
    root = Path(root)
    return {p.relative_to(root).as_posix(): verifier.sha256(p) for p in sorted(root.rglob("*")) if p.is_file()}


def restore(record_path):
    """Idempotent recovery; original directories are renamed, never deleted."""
    record_path = Path(record_path).resolve()
    record = json.loads(record_path.read_text())
    if record.get("schema") != "nksr-build-recovery/1":
        raise ValueError("Unsupported recovery manifest")
    failures = []
    for entry in reversed(record["directories"]):
        try:
            target, backup = Path(entry["target"]), Path(entry["backup"])
            if backup.exists():
                if tree_hashes(backup) != entry["original_sha256"]:
                    raise RuntimeError("Original backup changed; retained for inspection: " + str(backup))
                if target.exists():
                    displaced = record_path.parent / ("generated-" + target.name)
                    if displaced.exists():
                        raise RuntimeError("Recovery destination already exists: " + str(displaced))
                    os.replace(target, displaced)
                os.replace(backup, target)
            if not target.is_dir() or tree_hashes(target) != entry["original_sha256"]:
                raise RuntimeError("Baseline restoration could not be verified: " + str(target))
        except (OSError, RuntimeError) as error:
            # A damaged backup must not prevent restoring other intact directories.
            failures.append(str(error))
    meta = record["outer_meta"]
    if meta["existed"]:
        original = Path(meta["backup"])
        if verifier.sha256(original) != meta["sha256"]:
            raise RuntimeError("Original twins.meta backup changed")
        shutil.copy2(original, meta["target"])
    elif Path(meta["target"]).exists():
        os.replace(meta["target"], record_path.parent / "generated-twins.meta")
    if failures:
        raise RuntimeError("; ".join(failures))
    record["restored"] = True
    write_json(record_path, record)
    return record


class Staging:
    def __init__(self, packaged, project, records):
        self.packaged, self.project, self.records = Path(packaged), Path(project), Path(records)
        self.path = self.records / "recovery.json"

    def prepare(self):
        self.records.mkdir(parents=True, exist_ok=False)
        targets = (self.project / "Assets/StreamingAssets/twins", self.project / "ProjectSettings")
        if not all(p.is_dir() and not p.is_symlink() for p in targets):
            raise ValueError("Expected real baseline twins and ProjectSettings directories")
        directories = []
        for target in targets:
            directories.append({"target": str(target), "backup": str(self.records / ("original-" + target.name)),
                                "original_sha256": tree_hashes(target)})
        outer_meta = targets[0].with_suffix(".meta")
        meta = {"target": str(outer_meta), "existed": outer_meta.exists()}
        if outer_meta.exists():
            backup = self.records / "original-twins.meta"
            shutil.copy2(outer_meta, backup)
            meta.update(backup=str(backup), sha256=verifier.sha256(backup))
        write_json(self.path, {"schema": "nksr-build-recovery/1", "restored": False,
                              "directories": directories, "outer_meta": meta,
                              "packaged_source": str(self.packaged),
                              "recovery_command": f"python3 {Path(__file__).resolve()} --recover {self.path}"})
        shutil.copytree(self.packaged, self.records / "staged-twins")
        shutil.copytree(targets[1], self.records / "staged-ProjectSettings")

    def activate(self):
        record = json.loads(self.path.read_text())
        for entry in record["directories"]:
            target = Path(entry["target"])
            if tree_hashes(target) != entry["original_sha256"]:
                raise RuntimeError("Baseline changed during staging preparation")
            os.replace(target, entry["backup"])
            os.replace(self.records / ("staged-" + target.name), target)


def require_unity_closed():
    result = subprocess.run(["pgrep", "-x", "Unity"], capture_output=True)
    if result.returncode == 0:
        raise RuntimeError("Close the Unity Editor before staging or restoring datasets")


def signing_environment(config):
    environment = os.environ.copy()
    saved = json.loads(Path(config).read_text()) if Path(config).exists() else {}
    for key in SIGNING_KEYS:
        if not environment.get(key) and saved.get(key):
            environment[key] = str(saved[key])
        if not environment.get(key):
            raise ValueError("Missing signing environment field: " + key)
    if not Path(environment["TWIN2_KEYSTORE"]).is_file():
        raise FileNotFoundError("Configured twininspector2 keystore is unavailable")
    return environment


def android_tools(unity, player=None):
    player = Path(player) if player else Path(unity).parents[3] / "PlaybackEngines/AndroidPlayer"
    versions = sorted((player / "SDK/build-tools").glob("*"), key=lambda p: tuple(int(n) for n in re.findall(r"\d+", p.name)))
    if not versions:
        raise FileNotFoundError("Unity Android SDK build tools unavailable")
    result = {"aapt": versions[-1] / "aapt", "apksigner": versions[-1] / "apksigner",
              "keytool": player / "OpenJDK/bin/keytool", "java_home": player / "OpenJDK"}
    if not all(result[name].is_file() for name in ("aapt", "apksigner", "keytool")):
        raise FileNotFoundError("Android metadata/signing tools unavailable")
    return result


def check_android(apk, sdk, environment):
    env = environment.copy()
    env["JAVA_HOME"] = str(sdk["java_home"])
    badging = subprocess.check_output([str(sdk["aapt"]), "dump", "badging", str(apk)], text=True, env=env)
    if not re.search(r"^package: name='" + re.escape(APPLICATION_ID) + r"'", badging, re.M):
        raise ValueError("APK package identity is not the existing twininspector2 application")
    if "application-label:'" + LABEL + "'" not in badging:
        raise ValueError("APK label is not twininspector2")
    certificate = subprocess.check_output([str(sdk["keytool"]), "-exportcert", "-keystore", env["TWIN2_KEYSTORE"],
                                          "-alias", env["TWIN2_KEY_ALIAS"], "-storepass:env", "TWIN2_KEYSTORE_PASS"], env=env)
    expected = hashlib.sha256(certificate).hexdigest()
    verified = subprocess.check_output([str(sdk["apksigner"]), "verify", "--print-certs", str(apk)], text=True, env=env)
    signers = re.findall(r"Signer #\d+ certificate SHA-256 digest: ([0-9a-fA-F]+)", verified)
    if signers != [expected]:
        raise ValueError("APK signer differs from the configured twininspector2 keystore")
    return {"application_id": APPLICATION_ID, "label": LABEL, "signer_sha256": expected}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--packaged", type=Path)
    parser.add_argument("--unity", type=Path, default=Path("/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity"))
    parser.add_argument("--android-player", type=Path)
    parser.add_argument("--signing-config", type=Path, default=ROOT / "work/signing/twininspector2-signing.json")
    parser.add_argument("--max-apk-bytes", type=int, default=250_000_000, help="Explicit comparison budget; baseline verifier still defaults to 95MB")
    parser.add_argument("--max-space-bytes", type=int, default=100_000_000)
    parser.add_argument("--check-only", action="store_true")
    parser.add_argument("--recover", type=Path)
    args = parser.parse_args()
    require_unity_closed()
    if args.recover:
        restore(args.recover)
        print("Original data and settings restored and verified.")
        return
    for recovery in (ROOT / "work/build-records").glob("nksr-stage-*/recovery.json"):
        if not json.loads(recovery.read_text()).get("restored"):
            raise RuntimeError("An interrupted staging transaction needs recovery first: "
                               + f"python3 {Path(__file__).resolve()} --recover {recovery}")
    if args.packaged is None:
        parser.error("--packaged is required unless recovering")
    if args.max_apk_bytes <= 0 or args.max_space_bytes <= 0:
        parser.error("APK and space budgets must be positive")
    packaged = args.packaged.resolve()
    verifier.validate_nksr_trial(packaged, verifier.DEFAULT_SOURCE)
    if any(p.is_symlink() for p in packaged.rglob("*")):
        raise ValueError("Packaged inputs must contain only self-contained regular files")
    if packaged.is_relative_to(PROJECT.resolve()):
        raise ValueError("Packaged source must be outside the Unity project")
    if not args.unity.is_file():
        raise FileNotFoundError("Configured Unity Editor is unavailable")
    environment = signing_environment(args.signing_config)
    sdk = android_tools(args.unity, args.android_player)
    if args.check_only:
        print("MainHall NKSR trial, unchanged baseline spaces, Unity, Android SDK and signing configuration are ready.")
        return
    if APK.exists():
        raise FileExistsError("Preserve or move the existing trial APK before building: " + str(APK))
    name = "nksr-stage-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S") + "-" + str(os.getpid())
    stage = Staging(packaged, PROJECT, ROOT / "work/build-records" / name)
    stage.prepare()
    print("Recovery manifest: " + str(stage.path), flush=True)
    BUILD.mkdir(parents=True, exist_ok=True)
    log = BUILD / "build.log"
    child = None
    def terminate(signum, frame):
        raise SystemExit(128 + signum)
    signal.signal(signal.SIGTERM, terminate)
    try:
        stage.activate()
        command = [str(args.unity), "-batchmode", "-quit", "-projectPath", str(PROJECT), "-buildTarget", "Android",
                   "-executeMethod", "TwinInspector.Editor.StandaloneBuild.BuildNksrMainHall", "-logFile", str(log)]
        print("Building twininspector2 HG MainHall NKSR update; Unity log: " + str(log), flush=True)
        child = subprocess.Popen(command, cwd=PROJECT, env=environment)
        status = child.wait()
        if status != 0:
            raise RuntimeError("Unity NKSR build failed; inspect " + str(log))
    finally:
        if child is not None and child.poll() is None:
            child.terminate()
            try:
                child.wait(timeout=30)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait()
        restore(stage.path)
        print("Baseline StreamingAssets and ProjectSettings restored exactly.", flush=True)
    verification = verifier.verify(APK, packaged, args.max_apk_bytes, args.max_space_bytes,
                                   nksr_mainhall=True, baseline_source=verifier.DEFAULT_SOURCE)
    verification.update(check_android(APK, sdk, environment))
    verification.update(recovery_manifest=str(stage.path), baseline_restored=True, installed=False,
                        replaced_spaces=["HG_MainHall"], external_mainhall_update_required=True)
    write_json(BUILD / "verification.json", verification)
    print("Verified twininspector2 update APK (not installed): " + str(APK))


if __name__ == "__main__":
    main()
