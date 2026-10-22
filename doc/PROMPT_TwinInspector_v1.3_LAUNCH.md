# TwinInspector v1.3 — autonomous build run

You are an autonomous senior engineer (Unity 6, Android XR / OpenXR, C#, computer vision, Python) working in the Git repository `E:\MixedReality` on this Windows 11 machine. The repository holds **TwinInspector**, a Samsung Galaxy XR mixed-reality app that overlays a LiDAR digital twin on the real building and records where they differ. Version 1.2 is complete and committed on `main`. Your mission is to implement **version 1.3** exactly as specified in the brief `doc/PROMPT_TwinInspector_v1.3.md`, verify it, document it, and publish the result to GitHub.

The brief is the specification. This message is the operating procedure. Where they disagree, the brief wins; where the brief is silent, choose the option that keeps the existing architecture and record the decision in `TwinInspector/docs/DEVLOG.md`.

## 1. Start-up (do all of this before writing any code)

1. Read `doc/PROMPT_TwinInspector_v1.3.md` completely, then the files it lists in its first paragraph (v1 brief sections, both READMEs, DEVLOG, DESIGN, every script under `TwinInspector/Assets/_TwinInspector/Scripts/` and `Editor/ProjectBuilder.cs`). Do not skim. If the brief file is missing, stop and say so.
2. Confirm the environment and quote the outputs in DEVLOG: `git status --short` (must be clean apart from the brief files), `git remote -v` (expects `origin git@github.com:Haizhouzhou/MixedReality.git`), `git log --oneline -3`, `adb devices -l`, `C:\Users\zackz\AppData\Local\Unity\bin\unity.exe status --project-path E:\MixedReality\TwinInspector --format json`, and `D:\miniforge3\envs\mr\python.exe -I -c "import numpy, trimesh; print('ok')"`.
3. Create the work branch: `git checkout -b v1.3` from `main`. If the v1.3 brief files under `doc/` (`PROMPT_TwinInspector_v1.3.md`, `PROMPT_TwinInspector_v1.3_LAUNCH.md`, `PASTE_TwinInspector_v1.3.md`) are untracked, commit them as the first commit: `v1.3: add implementation brief`.
4. Write your execution plan as a new DEVLOG section `## <date> — v1.3 plan`, one line per stage of the brief's §14, then start Stage 1.

## 2. Git and GitHub rules (non-negotiable)

- Work on branch `v1.3`. Commit at the end of every stage with the message `v1.3 Stage N — <what changed>`; push after every stage: `git push -u origin v1.3`. Pushing often is the backup; never leave a finished stage unpushed.
- Never force-push, never rewrite published history, never commit `dataset/`, `TwinInspector/Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `work/twin/`, `*.pt` or `*.onnx`. The tracked APK `TwinInspector/Builds/TwinInspector-0.1.0.apk` is the one allowed build artefact.
- GitHub rejects files above 100 MB. Before every commit run a size check on staged files (PowerShell: `git diff --cached --name-only | % { if (Test-Path $_) { Get-Item $_ } } | ? Length -gt 50MB | select FullName, Length`). Anything above 95 MB must not be committed: stop, fix the packaging (the brief keeps the SAM models outside the APK for exactly this reason), then continue.
- Final publication (brief §14 Stage 9): when every gate in §3 below is green, merge `v1.3` into `main` with a fast-forward or a merge commit (`git checkout main && git merge v1.3`), tag `v1.3.0`, and push both: `git push origin main --tags`. If the `gh` CLI is installed and authenticated, open a pull request from `v1.3` to `main` with the final report as its body before merging; otherwise merge locally. Confirm with `git log origin/main --oneline -3` and quote it.
- If a push fails (SSH key, network), record the exact error in DEVLOG, keep committing locally, and report it in the final summary. Do not change SSH or Git credentials.

## 3. Verification gates (all must hold at the end of each stage; quote results in DEVLOG)

- `unity recompile_status`: 0 errors, 0 warnings from TwinInspector scripts; `tools/unity_check.ps1` clean (OpenXR validation 0 issues, no new console errors).
- Unity EditMode tests: all existing 16 plus every new test green (`unity command` test runner as in v1.2; save `docs/evidence/tests-v13.json`).
- Python: `D:\miniforge3\envs\mr\python.exe -I tools\tests\test_twin_pipeline.py` and `test_evaluate_annotations.py` green.
- The Play-mode QA scripts named in the brief §12 for the stages completed so far (`diff_qa_v13.cs`, `pick_qa_v13.cs`, `strings_qa_v13.cs`, `guide_qa_v13.cs`, plus the unchanged v1.2 QA scripts) pass and write their evidence files.
- Android build: 0 errors, 0 OpenXR issues; APK verified with `tools/verify_apk.py` and the metadata/DTO stripping checks exactly as v1.2 did; APK size below 95 MB; SHA-256 recorded.
- DEVLOG updated for the stage, with numbers, not adjectives.

## 4. Working rules

- Follow the brief's stage order (§14). Do not start a later stage before the current one passes its gates. Stage 7 (object pick) has a bounded effort of one working day of agent time; if it is blocked, disable it behind `Settings.objectPickEnabled = false` as the brief says, record the exact blocker, and continue.
- Never fabricate a result. No headset is connected unless `adb devices -l` lists one; every device-dependent claim (frame rate, scene mesh quality, hand tracking, probes) is reported as **unverified** with the exact [HUMAN] procedure from the brief §13.
- Keep the architecture: service locator in `AppBootstrap`, coordinate conventions in `TwinRoot`, Swiss design system from `docs/DESIGN.md` and v1 brief §8, all user-visible strings through the string table, no YAML hand-edits while the Editor is open, assets authored through `ProjectBuilder` and Editor APIs.
- Run heavy operations sequentially (16 GB RAM): never a Unity build and a mesh job at the same time. Long commands go to the background with logs under `work/`.
- Do not stop, kill or modify any process you did not start. Do not read, modify or delete anything under `dataset/`. Network use is limited to the Unity package registry, GitHub, and the MobileSAM checkpoint download named in the brief.
- When something is impossible on this machine, finish everything that does not depend on it, write the exact [HUMAN] steps, and continue. Do not wait for a human.

## 5. Final report (last thing you write, also appended to DEVLOG)

1. A table of the brief's §0 items 1–13 with status `done` / `unverified (device)` / `blocked (reason)`, and the evidence file for each.
2. Commit hashes per stage, the tag, the pull request URL if any, and the quoted `git log origin/main --oneline -3`.
3. APK path, size in bytes, SHA-256, application version and version code.
4. Measured Editor numbers: diff engine main-thread ms, candidates found by the fixture QA, string QA overflow counts per language, object-pick timings if built.
5. Every open [HUMAN] item, in the order the team should do them, starting with the device acceptance run.
6. Decisions you made where the brief was silent, with one line of reasoning each.

The complete brief follows below the line. If the file `doc/PROMPT_TwinInspector_v1.3.md` also exists in the repository, treat the file as authoritative and this copy as a convenience.

---
