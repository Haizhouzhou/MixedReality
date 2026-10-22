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
# One-shot brief: build **TwinInspector v1.3** — change candidates, region annotations, guide, localisation, object pick

You are an autonomous senior engineer with Unity, XR, computer-vision and Python experience. You work on this Windows machine with shell access. v1.2 of **TwinInspector** (Samsung Galaxy XR, Android XR) is complete in software and committed on `main` in `E:\MixedReality`. Your job is to deliver **v1.3**: the app detects where the physical hall differs from the 2022 LiDAR twin, shows those places as candidates, lets the user confirm them as region annotations, exports them, teaches a new user the controls with a short in-app guide, and speaks English and German (plus machine-translated French and Italian). A last, lower-priority feature lets the user pick a whole object in the twin with one pinch (SAM-based).

Work end to end without waiting for a human, except for the steps marked **[HUMAN]**. Verify every step with the commands in this brief. Report faithfully in `TwinInspector/docs/DEVLOG.md`. Never invent results, never claim a device result without a connected headset, never fabricate metrics.

Read first, in this order: `doc/PROMPT_TwinInspector_v1.md` (§2 machine, §6.1 Unity CLI rules, §8 design system — all still binding), `README.md`, `TwinInspector/README.md`, `TwinInspector/docs/DEVLOG.md`, `TwinInspector/docs/DESIGN.md`, then every file under `TwinInspector/Assets/_TwinInspector/Scripts/` and `Editor/ProjectBuilder.cs`.

---

## 0. Definition of done

v1.3 is done when all of the following are true:

1. **Diff engine.** With external Region A data loaded, the app computes change candidates of three kinds — **New** (real surface with no twin surface nearby), **Missing** (twin surface with no real surface nearby in an observed area) and **Changed** (a Missing cluster paired with a New cluster within 1.0 m) — from the headset scene mesh and the twin LOD2 collision meshes. No neural network, no camera frames. Candidates are discarded and recomputed after any re-alignment, and the engine never exceeds the per-frame budgets in §9.
2. **Editor simulation.** In the Unity Editor, a deterministic simulator replaces the headset scene mesh with Region A LOD2 geometry edited by a fixture file (`tools/fixtures/reality_edits_a.json`). The automated QA run finds every planted change and reports at most 2 other visible candidates. This QA is part of the repository and runs from the Unity CLI.
3. **Review UI.** Candidates render as flat type-coloured patches with a hollow glyph. The user points at a patch or selects it from the 04 List page, sees its kind, area and distance, and either **Confirms** (creates an annotation with a region) or **Dismisses** it (hidden for the rest of the session and persisted). A **Diff on / off** control exists in the Opacity / Lens page.
4. **Region annotations.** Annotation schema `twin-annotations/2` adds an optional `region` (hull polygon, centroid, extent, area, source, and twin triangle references). v1 files still load and save. JSON and CSV export include the region. Annotations with a region draw their patch after restart.
5. **Refinement patch export.** Export also writes, for every New or Changed annotation, the scene-mesh triangles within 1.5 m of its centroid as an OBJ in the twin frame (`exports/<scan>-<stamp>-patches/<annotation id>.obj`). When no scene mesh exists (Editor without simulator), the export says so in the log and skips the file.
6. **Guide.** A six-card guide under 00 / Setup teaches the dock gesture, point-and-pinch, alignment, comparison, annotation and candidates. It opens once on first launch and from the Setup page at any time. Every card is one sentence plus one thing to try.
7. **Localisation.** Every user-visible string lives in one string table with four languages: **English and German are the primary lines** (reviewed for terminology with the glossary in §7.3), **French and Italian are machine-translated by you** with the same glossary and marked as such. The app starts in the system language when it is one of the four, else English; a Language page in Preferences switches at runtime and persists. No string overflows its control in any language (automated check). Data files, export keys, CSV headers and log lines stay English.
8. **Object pick (SAM on the twin render).** In the 03 Annotate flow, **Pick object** renders the twin in Colour style from the viewer pose, runs MobileSAM (Unity Inference Engine, on device) with the pinch point as prompt, back-projects the mask onto the twin LOD2 colliders and attaches a region to the selected annotation. This is Stage 7; if it is blocked by the package or the model, ship v1.3 without it behind `Settings.objectPickEnabled=false` and record why.
9. **Device diagnostics.** The 05 Diagnostics page gains **Depth probe** and **Camera probe**. On device they log the AR Foundation occlusion frame descriptor and the Camera2 camera list. In the Editor they log "Editor: probe unavailable".
10. **Evaluation tooling.** `tools/evaluate_annotations.py` scores an export against a ground-truth CSV (precision, recall, mean distance at 0.5 m and 1.0 m; time per annotation from `created_utc`) and writes a Markdown table. Its unit test passes.
11. **Build.** The Android APK builds with zero compile errors, zero warnings from TwinInspector scripts and zero OpenXR validation issues. Application version **1.3.0**, Android version code **3** (both set in `ProjectBuilder.Setup`, the single place), package id and filename unchanged (`Builds/TwinInspector-0.1.0.apk`). All existing 16 Unity tests still pass, all new tests pass, all Python tests pass.
12. **Docs and git.** `README.md`, `TwinInspector/README.md`, `DESIGN.md` and `DEVLOG.md` describe v1.3 with the same evidence discipline as v1.2 (`docs/evidence/*-v13.*`, `docs/screenshots/v13-*.png`). Everything is committed on `main` with clear messages. `dataset/`, `Library/`, intermediate meshes, model checkpoints (`*.pt`) and ONNX exports are never committed. The two serialized SAM models (`.sentis`, ≈40 MB total, each below 50 MB) are committed under `tools/sam/models/` and are **not** bundled in the APK: they are pushed to the device next to the tiles. GitHub rejects any file above 100 MB, so the tracked APK must stay below 95 MB; check its size before every commit.
13. **[HUMAN] device acceptance** is documented as a procedure (§13) and its results, when a headset is connected, are recorded in DEVLOG. Until then, every device-dependent item is reported as **unverified**.

Out of scope for v1.3 (do not start): automatic registration, neural reconstruction, SAM 3 or any server-side inference, appearance/colour change detection, raw passthrough camera frames beyond the Camera2 probe, Region B regeneration, Chinese or any non-Latin language, translation polishing beyond the glossary.

---

## 1. What exists (v1.2) and what you must not break

Read the code; this is the map, not a substitute.

| Area | Where | Facts you depend on |
|---|---|---|
| Boot and services | `Core/AppBootstrap.cs` | Only service locator. Requests `CAMERA`, `SCENE_UNDERSTANDING_FINE`, `HAND_TRACKING`; enables `ARCameraManager`, `ARAnchorManager`, `ARPlaneManager`, `ARTrackedImageManager`, `ARMeshManager` only after `ARSession.state >= Ready`. `Physics.queriesHitBackfaces = true` is set at line 53. Add new services here, in this pattern. |
| Assemblies | `Scripts/TwinInspector.Runtime.asmdef`, `Editor/TwinInspector.Editor.asmdef`, `Tests/Editor/TwinInspector.Tests.asmdef` | Runtime references glTFast, Unity.Collections and Newtonsoft; **Editor references Runtime, not the reverse**, and does not reference glTFast or Newtonsoft. Editor-only runtime behaviour therefore lives in Runtime under `#if UNITY_EDITOR`. Both asmdefs use explicit reference lists: a new package assembly must be added by name. |
| Coordinates | `Twin/TwinRoot.cs` | Persisted twin frame = aligned LaMAR world, metres, right-handed **Z-up**. Unity local = `SwapYZ(twin − origin)`. `TwinRoot` carries alignment only; never scales. Use `WorldToTwin`, `TwinToWorld`, `NormalToTwin`, `NormalToWorld`. `AnchorPersistence.Follow` re-poses `TwinRoot` every `LateUpdate` while following an anchor, so small root motion is normal. |
| Tiles | `Twin/TwinLoader.cs`, `Twin/TileHandle.cs` | glTFast streaming with a 1.2 M resident-triangle budget. Each loaded tile has a hidden **LOD2 collision copy** on **layer 8** (`MeshCollider`) under `LOD2 inspection surfaces`, and a visual copy under `Twin visuals`. Tile planning runs every 0.35 s and disposes tiles that fall out of range. Each tile GLB is one mesh, so (tile id, LOD 2, triangle index) is unambiguous. Largest Region A LOD2 tile: 54 089 triangles; 815 667 over 45 tiles. |
| Input | `Core/InputRouter.cs` | `BuildingMask = 1<<8`, `SurfaceMask = 1<<8 | 1<<9`. `BuildingHit` is the hand-ray hit, not constrained to the gaze. `Aim`, `Pressed`, `ConsumePress()`, `OverUI`, `Busy`. Mouse path in the Editor, XRI rays on device. |
| Scene mesh | `Editor/ProjectBuilder.cs` lines 72–97 | `ARMeshManager` on `Scene meshing` under `CameraFloorOffsetObject`, prefab `Prefabs/SceneMesh.prefab` = `MeshFilter + MeshCollider` on **layer 9**. Android XR ignores `density`, supports normals. Enabled on device after permissions. **Nothing consumes it yet.** |
| Annotations | `Annotation/*.cs` | `AnnotationStore` (JSON at `persistentDataPath/annotations/<scan>.json`; its constructor throws on `schema != "twin-annotations/1"`), `AnnotationPlacer`, `AnnotationVisuals` (hover/select by angular ranking, glyphs on **layer 10**; selecting calls `store.Select(id)` then `UI.Show("picker")`), `AnnotationMarker`, `AnnotationExporter` (builds the CSV inline). |
| Style and modes | `Twin/TwinStyle.cs` | `ViewMode {Passthrough, Twin, Overlay}`, globals `_TwinAlpha`, `_TwinTechnical`, `_TwinLensEnabled`, `_WorldToTwin`. `DatumZ` is never assigned (constant 0); do not build on it. |
| UI | `UI/InspectorUI.cs`, `UI/PanelBase.cs`, `UI/ToolStrip.cs` | Pages: `setup, mode, display, preferences, sound, roam, align, nudge, height, marker, picker, list, diagnostics`. Panel space is millimetres on a 480 × 320 panel; content from y=104; the list viewport is y=104–216 (two 56 mm rows); bottom button row at y=248 (two 208-wide at x=24/248 or three 144-wide at x=24/168/312). `display` uses y=264 for its Back button. Header meta is a 156 mm Caption rect with truncation. `PanelBase.Row/Scroll/Button/Text/Slider/Notify`. Strip cells: 0 Mode, 1 Align, 2 Annotate, 3 List. |
| Fonts | `Editor/ProjectBuilder.cs` lines 150–155 | The Inter SDF atlases are built with explicit `TryAddCharacters` strings. Any new character (`²`, umlauts, accents) must be added there or it renders as a missing glyph. |
| Design | `docs/DESIGN.md`, `UI/DesignTokens.cs` | Swiss (International Typographic Style) system: paper/ink, one accent, square controls, Inter, 8 mm rhythm; type colours Missing `#E30613`, New `#0046E6`, Changed `#F2B600`, Note `#6F6E69`. Words first, no icons-with-gradients, no head-locked UI. "Swiss" is a typographic style, not a localisation requirement. |
| Diagnostics | `Diagnostics/PerfHud.cs`, `Diagnostics/DevLog.cs` | `DevLog.Write` to `persistentDataPath/logs/`. Metrics line every 10 s. `DiagnosticsPage` draws the live readout at y=104, height 120. |
| Build | `TwinInspector/README.md` "Build, install and launch" | Unity CLI (`unity recompile`, `command eval_file`, `command build --target Android`). `tools/unity/validate.cs` = OpenXR validation. The Android build callback that merges duplicate XR manifest entries must stay. All JSON DTOs must keep public fields and default constructors (Android stripping check in evidence). |
| Tests | `Tests/Editor/*.cs` (16 tests), `tools/tests/test_twin_pipeline.py` | EditMode tests through the Unity test runner; Python tests with `-I`. |

Hard constraints carried over: Unity **6000.3.26f1** and the committed package versions; Vulkan, IL2CPP, ARM64, GameActivity; URP settings as configured; no CUDA; the `System.Runtime.CompilerServices.Unsafe` plugin stays; `dataset/` is read-only and never committed.

Two facts discovered while scoping — plan around them:

- **Submesh classification is unavailable.** `ARMeshManager.TryGetSubmeshClassifications` is compiled only `#if UNITY_6000_4_OR_NEWER`. Do not use classification. Floor and ceiling are identified geometrically (§4.3).
- **LOD2 collision meshes are not CPU-readable.** `TwinLoader.LoadTileAsync` calls `sharedMesh.UploadMeshData(true)` on collision meshes. Change that call to `UploadMeshData(false)` **for collision meshes only** (visual meshes stay non-readable) and record the memory delta in DEVLOG.

---

## 2. Machine, tools and CLI

Identical to `PROMPT_TwinInspector_v1.md` §2 and §6.1. Reminders:

- Unity CLI: `C:\Users\zackz\AppData\Local\Unity\bin\unity.exe`. Poll `console_status` after domain reloads; `recompile_status` must report zero errors before any scene work; run `tools/unity_check.ps1` after every change set.
- Add a package: `unity command package_add --identifier com.unity.ai.inference --confirm true --wait true --project-path E:\MixedReality\TwinInspector` (the newest 2.6.x that resolves for Unity 6000.3). Then read the installed package source under `Library/PackageCache/com.unity.ai.inference@*/` and **confirm the assembly name, `Worker`, `BackendType.GPUCompute`, `ScheduleIterable` and `ModelLoader` names before writing any inference code**; add the assembly to `TwinInspector.Runtime.asmdef`. Record the exact version in DEVLOG. Do not upgrade any other package.
- Python env `mr`: `D:\miniforge3\envs\mr\python.exe -I`. New dependencies go in `tools/requirements-twin.txt` (evaluation) or `tools/requirements-sam.txt` (model export: `torch` CPU wheel, `onnx`, `onnxruntime`, `timm`, `git+https://github.com/ChaoningZhang/MobileSAM.git`).
- Evidence: every verification writes a file under `TwinInspector/docs/evidence/` named `<topic>-v13.<ext>`; screenshots under `docs/screenshots/v13-*.png`, captured from the running Editor through the existing `tools/unity/capture_*.cs` pattern.
- No headset is connected unless `adb devices -l` lists an authorized device. Check it once at the start and once at the end; quote the output.

---

## 3. Architecture additions (namespace `TwinInspector`)

New folders under `Assets/_TwinInspector/Scripts/`: `Diff/`, `Pick/`. Keep classes small, `[SerializeField]` over public fields, XML doc comments on public APIs, async/await with cancellation for IO, no per-frame allocations in the hot paths (`NativeArray`, pooled `List<T>`, `RaycastCommand`, `OverlapSphereCommand`).

```
Scripts/Core/
  Strings.cs                  // static class L: Get(key), Format(key, args), Language, MissingKeys
Scripts/Diff/
  ISceneSurfaceSource.cs      // the only way the engine sees "reality"
  SceneSurfaceSource.cs       // device: wraps ARMeshManager (layer 9 meshes)
  SurfaceChunk.cs             // one readable mesh + collider + world matrix + version
  DiffEngine.cs               // MonoBehaviour service: scheduling, budgets, candidates, Selected
  DiffSampler.cs              // builds RaycastCommand/OverlapSphereCommand batches (pure C#)
  DiffClusterer.cs            // union-find over triangles, area, extent (pure C#)
  Candidate.cs                // in-memory model + session JSON DTOs
  CandidateVisuals.cs         // patches + hollow glyphs, hover/select
  RegionBuilder.cs            // hull/centroid/extent from points (pure C#)
  EditorSceneSurfaceSimulator.cs  // #if UNITY_EDITOR: Editor "reality" from LOD2 tiles + fixture edits
  DiffFixture.cs                  // #if UNITY_EDITOR: fixture DTOs + loader + validation
Scripts/Annotation/
  AnnotationRegion.cs         // schema v2 DTO
  ObjWriter.cs                // pure C# OBJ writer
Scripts/Pick/
  ObjectPicker.cs             // SAM flow: capture → encode → decode → back-project → SetRegion
  TwinCapture.cs              // renders the twin in Colour style to a 1024² RT
  SamRunner.cs                // Inference Engine workers, scheduled across frames
Resources/Strings/en.json, de.json, fr.json, it.json
tools/fixtures/reality_edits_a.json, tools/fixtures/ground_truth_example.csv
tools/evaluate_annotations.py, tools/tests/test_evaluate_annotations.py
tools/sam/export_mobilesam.py, tools/sam/README.md, tools/requirements-sam.txt
tools/sam/onnx/            (git-ignored: mobile_sam.pt, *.onnx)
tools/sam/models/mobile_sam_encoder.sentis, mobile_sam_decoder.sentis, models.json, MODELS.md
Editor/SamModelSerializer.cs    // menu: TwinInspector → Serialize SAM models (ONNX → .sentis)
```

`TwinPaths` addition: `ModelsRoot(Settings)` → `Settings.EditorModelsPath` in the Editor (default `E:/MixedReality/tools/sam/models`), else `Application.persistentDataPath/models`. `tools/push_twin.ps1` gains a `-Models` switch that pushes `tools/sam/models/*` to `/sdcard/Android/data/com.mrtwin.inspector/files/models/`.

`TwinLoader` additions: `IReadOnlyList<TileHandle> Tiles`, `int TileVersion` (incremented on every load or dispose), events `Action<TileHandle> CollisionLoaded` and `CollisionReleasing` (raised before `tile.Dispose()`), and assignment of **layer 11** to every visual tile child (edges and lens shells included) so capture cameras can isolate the twin. Name layers 8–12 in `TagManager` through `ProjectBuilder` (`Building`, `SceneMesh`, `Annotation`, `TwinVisual`, `DiffPatch`); code keeps the numeric constants.

`AnnotationExporter` additions: `static string CsvHeader` and `static string CsvLine(Annotation)` as pure functions used by `Export` and by the tests.

`InteractionPreferences` additions: `Language` (string code, default empty = follow system), `GuideSeen` (bool).

`Settings` additions (serialized, with these defaults):

| Field | Default | Meaning |
|---|---|---|
| `diffEnabled` | true | master switch; also the Diff on/off state at start |
| `diffDistanceMax` | 0.35 m | a surface counts as "present" when a twin/scene surface is within this distance along the normal |
| `diffMinAreaM2` | 0.15 | minimum cluster area |
| `diffMinExtentM` | 0.25 | minimum cluster bounding extent on its longest axis |
| `diffObservedRadiusM` | 1.0 | a twin sample counts as observed when any scene mesh lies within this radius |
| `diffPersistUpdates` | 3 | a cluster must survive this many evaluations of its chunk before it is shown |
| `diffChangedPairDistanceM` | 1.0 | Missing–New centroid distance that merges into Changed |
| `diffIgnoreFloorCeiling` | true | drop near-horizontal samples at floor or ceiling height (§4.3) |
| `diffFloorZ` | 0.0 | twin-frame floor height used by the floor/ceiling rule |
| `diffRaycastsPerFrame` | 2048 | hard cap on raycast commands scheduled per frame |
| `diffOverlapsPerFrame` | 2048 | hard cap on overlap-sphere commands scheduled per frame |
| `diffMainThreadBudgetMs` | 2.0 | engine stops scheduling work for the frame when exceeded |
| `objectPickEnabled` | true | Stage 7 feature flag |
| `objectPickFramesBudgetMs` | 4.0 | per-frame budget for scheduled inference layers |

---

## 4. Deliverable A — Diff engine

### 4.1 Reality source

`ISceneSurfaceSource` exposes `IReadOnlyList<SurfaceChunk> Chunks`, `event Action<SurfaceChunk> ChunkChanged`, `event Action<SurfaceChunk> ChunkRemoved`, and `bool Ready`. A `SurfaceChunk` holds a stable id, the `MeshFilter`, the `MeshCollider` (layer 9), a `Version` incremented on every update, cached readable `vertices`, `normals` and `triangles` in **world space at capture time** plus the capture `localToWorld` matrix, and the chunk's world `Bounds`.

`SceneSurfaceSource` (device): `ARMeshManager.meshInfosChanged` is a `UnityEvent<ARMeshInfosChangedEventArgs>` — subscribe with `meshInfosChanged.AddListener(...)`. The args carry `added`, `updated` and `removed` as `ReadOnlyList<MeshUpdateInfo>`, each with `TrackableId id` and `MeshFilter meshFilter`; the chunk id is `id.ToString()`. Request normals (`ARMeshManager.normals = true`; the manager computes them on the CPU, so the meshes are readable — still assert `mesh.isReadable` and log once). Copy vertex data once per update into the chunk and raise `ChunkChanged`. Scene meshes live in session space under `CameraFloorOffsetObject`; the engine always converts through world space and never assumes identity.

`EditorSceneSurfaceSimulator` (`Scripts/Diff/`, whole file inside `#if UNITY_EDITOR`, created by `EditorDesktopDriver`): loads the LOD2 GLB of every Region A tile from `Settings.EditorDataPath` (or the packaged demo when absent) with glTFast into objects on layer 9 with `MeshCollider`, applies the fixture edits (§4.6), splits the result into chunks of at most 20 000 triangles, and publishes them through the same interface. It is deterministic: same input, same chunks, same versions. It never exists in a player build.

### 4.2 Twin source

The twin side is the set of loaded LOD2 collision meshes (`TileHandle.Collision`, layer 8, readable after §1). On `CollisionLoaded`, the engine caches the tile's vertices, triangle centroids, area-weighted normals and triangles in tile-local space plus the tile's `localToWorld`; on `CollisionReleasing` it drops the cache and every candidate that references the tile.

### 4.3 Algorithm

All distances in metres, all thresholds from `Settings`. One ray per sample is enough: `Physics.queriesHitBackfaces` is already global and the batched query sets `hitBackfaces = true`, so a single ray from `sample − normal × diffDistanceMax` along `+normal` with distance `2 × diffDistanceMax` finds a surface on either side.

**Floor/ceiling rule** (applied to every sample when `diffIgnoreFloorCeiling`): drop the sample when its twin-frame normal is within 20° of ±Z **and** (`|z − diffFloorZ| < 0.15` **or** `z > diffFloorZ + 2.4`). Walls and columns at any height are kept.

**New candidates** (per scene chunk, on `ChunkChanged`):
1. Samples = chunk vertices. Skip a sample when it is outside the union of loaded twin tile bounds expanded by `diffDistanceMax` (unknown twin coverage), or when the floor/ceiling rule drops it.
2. One `RaycastCommand` per remaining sample against `BuildingMask` (layer 8), built as `new RaycastCommand(origin, direction, new QueryParameters(layerMask, false, QueryTriggerInteraction.Ignore, true), distance)`. A sample is **unexplained** when the ray does not hit.
3. Triangles whose three vertices are unexplained are clustered by shared edges (`DiffClusterer`, union-find). Clusters with area ≥ `diffMinAreaM2` and longest extent ≥ `diffMinExtentM` become **New** candidates with centroid, PCA normal, hull (§5.1), area and the list of (chunk id, triangle index). New candidates are stored in **world space** (they belong to session-space scene chunks) and converted to the twin frame only at Confirm.
4. Persistence: a candidate is **stable** once a cluster with centroid within 0.5 m has been produced in `diffPersistUpdates` consecutive evaluations of that chunk, or after 5 s if the chunk stops updating. Only stable candidates are visible.

**Missing candidates** (per loaded twin tile; the next tile starts when the previous evaluation completes, never on a timer):
1. Samples = tile LOD2 **triangle centroids only**. Apply the floor/ceiling rule.
2. Observed test in a job: one `OverlapSphereCommand` per sample (radius `diffObservedRadiusM`, layer 9, `maxHits = 1`), scheduled with `OverlapSphereCommand.ScheduleBatch`, capped by `diffOverlapsPerFrame`. Unobserved samples are skipped (unknown, not missing).
3. One `RaycastCommand` per observed sample against **layer 9**, as above. A sample is **unexplained** when the ray does not hit.
4. A triangle is unexplained when its centroid sample is unexplained. Cluster adjacent unexplained triangles by the tile's edge adjacency; same area/extent gates → **Missing** candidates referencing (tile id, LOD 2, triangle index), stored in **tile-local space** (twin frame after `SwapYZ` + origin).
5. Persistence as for New.

**Changed candidates:** after each evaluation, pair every stable Missing candidate with the nearest stable New candidate whose centroid is within `diffChangedPairDistanceM`; the pair becomes one **Changed** candidate whose region is the Missing hull and whose `paired_new` references the New candidate. Pairs are one-to-one, greedy by distance. Pairing **hides** the two members (they stay in the live set, flagged `hiddenByPair`); every count, limit and QA assertion in this brief is over **visible** candidates. Dismissing or confirming a Changed candidate also removes its two members.

**Alignment and modes — one rule:** the engine pauses and **discards all candidates** (then recomputes from scratch) when any of these holds: `ManualAlignment.Active` or `Dragging`; `MarkerAlignment.AwaitingConfirmation`; `HandLocomotion.Moving`; `ViewMode.Twin`; or `TwinRoot` moved by more than 1 cm or 0.5° since the last evaluation (small anchor corrections below that threshold are ignored; a saved-alignment restore or marker confirmation exceeds it and triggers the discard). Work resumes 1.0 s after the last trigger clears.

### 4.4 Scheduling, consistency and budgets

- One `ChunkChanged` or one twin tile per frame at most; the rest queue. The queue is **coalesced by chunk id** (latest version wins), so continuous mesh updates cannot grow it without bound.
- Every batch is stamped at schedule time with the chunk id + version (New), or tile id + `TwinLoader.TileVersion` (Missing), plus the set of loaded tile ids. When the batch completes, the engine checks the stamps: a changed chunk version, a disposed tile, or a changed loaded-tile set discards the batch. This closes the race with `TwinLoader`, which disposes tiles every 0.35 s.
- Batched queries complete the **next** frame; never `Complete()` in the frame they were scheduled. Hits are reduced on the main thread to a `NativeArray<byte>` of explained/unexplained flags (`RaycastHit.collider` is main-thread only); clustering and hull building then run on a worker thread (`Task.Run`) over those flags and the cached geometry copies, never touching Unity objects; results are applied on the main thread.
- `triangleIndex`: add an EditMode test that a batched hit on a `MeshCollider` populates `RaycastHit.triangleIndex`. If it does not on this Unity version, resolve triangles for accepted clusters only with a bounded number of main-thread `Physics.Raycast` calls and record the fallback.
- A `Stopwatch` guards main-thread time; when `diffMainThreadBudgetMs` is exceeded the engine yields until the next frame. Log every 10 s: `Diff chunks=<n> tiles=<n> samples=<n> rays=<n> overlaps=<n> visible_new=<n> visible_missing=<n> visible_changed=<n> main_ms=<avg> job_ms=<avg>`.
- Memory: at most 200 visible candidates (drop the smallest-area ones beyond that), patch meshes at most 50 000 triangles total.

### 4.5 Candidate model, selection and session persistence

`Candidate { string id; CandidateKind kind (New|Missing|Changed); Space space (World|TwinLocal); float[] centroid; float[] normal; float[][] hull; float area_m2; float[] extent_m; float confidence (0–1 = min(1, area / (4 × diffMinAreaM2)) × persistence ratio); string chunk_or_tile; int chunk_or_tile_version; int[] triangles; string paired_new; bool stable; bool hiddenByPair; string first_seen_utc; }` with `CentroidTwin()` / `HullTwin()` helpers that convert through `TwinRoot` at call time.

`DiffEngine.Selected` (a `Candidate` or null) and `DiffEngine.PendingType` (an `AnnotationType`) drive the picker's candidate mode (§4.8). Selecting a candidate clears `AnnotationStore.Selected`; selecting an annotation clears `DiffEngine.Selected`.

Dismissals are persisted at `persistentDataPath/diff/<scan>-dismissed.json` (schema `twin-dismissed/1`: list of `{ kind, centroid_twin, radius_m = max(0.5, extent/2), dismissed_utc }`). A new candidate whose twin-frame centroid falls inside a dismissed sphere of the same kind is suppressed. Confirmed candidates are removed from the live set and suppressed by the created annotation's region for the rest of the session.

### 4.6 Fixture for the Editor simulator — `tools/fixtures/reality_edits_a.json`

```json
{"schema": "twin-reality-edits/1", "scan": "navvis_2022-02-06_12.55.11", "region": "A",
 "edits": [
  {"id": "e1", "kind": "remove_sphere", "center_twin": [0, 0, 0], "radius_m": 0.9, "expect": "missing"},
  {"id": "e2", "kind": "add_box", "center_twin": [0, 0, 0.45], "size_m": [0.9, 0.9, 0.9], "yaw_deg": 15, "expect": "new"},
  {"id": "e3", "kind": "move_object", "from_twin": [0, 0, 0], "to_twin": [0, 0, 0], "radius_m": 0.6, "size_m": [0.8, 0.8, 0.8], "expect": "changed"},
  {"id": "e4", "kind": "jitter", "sigma_m": 0.02, "expect": "none"}
 ]}
```

You choose the coordinates. Rules the fixture loader enforces (it fails loudly otherwise): `remove_sphere` and `move_object.from_twin` must intersect at least 50 twin triangles that survive the floor/ceiling rule (a column base, a wall foot, a counter); `add_box` and `move_object.to_twin` sit on the hall floor inside the loaded tiles, at least 3 m apart from each other and from any removal; `move_object` removes a sphere of `radius_m` at `from_twin` and adds the box at `to_twin`, 0.7 m away. `jitter` adds Gaussian noise to all scene vertices (seeded, seed 1303) to model scanner noise. Record the chosen coordinates in DEVLOG and keep them inside the 32-tile demo set so the packaged fallback can run the same fixture.

### 4.7 Visuals

- **Patch**: a mesh built from the candidate's triangles (twin tile triangles for Missing/Changed, scene chunk triangles for New), rendered with a new unlit `DiffPatch.shader` (URP, SPI-safe): flat type colour at 35 % alpha, depth test on, depth write off, 1 mm screen-expanded ink outline on the hull edges using the ink segment technique from `ArchitecturalEdges`. Patches live on layer 12, under `TwinRoot` (Missing/Changed) or under the scene chunk object (New), so they follow alignment and session space respectively. Hidden when Diff is off, in `ViewMode.Passthrough`, and when the Lens hides the point (`TwinStyle.PlacementVisible`).
- **Glyph**: reuse `AnnotationMarker` with a `candidate` variant: paper disc, 1 mm type-colour ring, the type symbol, label `?? [+] NEW · 0.8 m²` (index `??` until confirmed). Add `_Candidate` (0/1) to `Annotation.shader` next to the existing `_Kind` and `_Selected` to draw the hollow ring. Add `²` to both `TryAddCharacters` strings in `ProjectBuilder`. Same angular sizing rules (≥ 1.5°, 2° selection target).
- **Hover/select**: extend `AnnotationVisuals` ranking to include candidate glyphs. Selecting a candidate sets `DiffEngine.Selected`, clears the annotation selection and calls `UI.Show("picker")`. Accepted actions produce `MicroFeedback.Confirm`.

### 4.8 UI (Swiss rules apply; reuse `PanelBase`; all strings through `L`)

- **Opacity / Lens page (`display`)**: the page is full (opacity text y=104, slider y=128, lens text y=184, slider y=208, Back y=264 full width). Replace the full-width Back with three 144-wide buttons at y=264: **Diff on** (x=24) and **Diff off** (x=168) as a two-cell segmented control (the current state drawn selected), and **Back** (x=312). State in `DiffEngine.Visible`, initialised from `Settings.diffEnabled`. Header meta: `LENS ACTIVE · DIFF ON` style status, at most 18 characters.
- **List page (`list`)**: rows = annotations first, then visible candidates, each candidate row `?? [+] NEW   3.2 m` / `candidate · 0.8 m²`. Header meta `05 ITEMS · 02 CAND` (never longer than 18 characters). Selecting a candidate row: `LookAt` + `DiffEngine.Selected = candidate` + `Show("picker")`. Bottom row unchanged.
- **Picker page in candidate mode** (rendered when `DiffEngine.Selected != null`): header `03 / Annotate`, heading `Candidate`, meta `NEW · 0.8 m² · 3.2 m`; the four type buttons set `DiffEngine.PendingType` (pre-selected = detected kind) instead of calling `Retype`; bottom row **Dismiss** (x=24, 208) and **Confirm** (x=248, 208, primary). Confirm creates the annotation with the pending type and `region.source = "diff"`, selects it, clears `DiffEngine.Selected` and returns to `list`; Dismiss persists the dismissal, clears the selection and returns to `list`.
- **Picker page for a fresh annotation** (one created in this session, tracked in a `HashSet<string>` on `InspectorUI`): when `objectPickEnabled` and both `.sentis` files exist under `TwinPaths.ModelsRoot` (checked once at startup, logged), the bottom row becomes **Pick object** (x=24, 144) · **List** (x=168, 144) · **Done** (x=312, 144, primary). Otherwise the row stays List · Done.
- **Setup page (`setup`)**: the bottom-right button becomes **Guide** (`Diagnostics` moves to Preferences; its own page layout is in §9).
- **Preferences page (`preferences`)**: today it has the hand buttons at y=104, a two-line help text at y=168–224 and the bottom row at y=248. Replace the help text with a row of two buttons at y=168: **Diagnostics** (x=24, 208) and **Language** (x=248, 208); keep one uppercase Label-size line at y=224 (`EITHER HAND CAN SUMMON AND OPERATE`, from the table); the two-line explanation moves into guide cards 1 and 2. Bottom row unchanged (`Feedback sound`, `Recenter / back`).
- Toasts: `Candidate confirmed · Annotation 08`, `Candidate dismissed`, `Diff paused during alignment`, `Processing object…`, `Object picked · 1.9 m²`, `No object found`.

---

## 5. Deliverable B — Region annotations and export v2

### 5.1 Region geometry (`RegionBuilder`, pure C#)

Input: a set of 3D points in one frame (hit points or triangle vertices) and the triangle list. Output: PCA plane (centroid + normal), the **convex hull** of the points projected onto that plane (2-D monotone chain), simplified to at most 64 vertices by dropping the vertex with the smallest triangle area until the limit holds, lifted back to 3-D on the plane; `area_m2` = triangle area sum of the source triangles (not the hull area); `extent_m` = size of the oriented bounding box in the plane basis plus thickness along the normal. The builder is frame-agnostic; callers convert to the twin frame before persisting.

### 5.2 Schema `twin-annotations/2` (Appendix A)

- `AnnotationDocument.schema` becomes `"twin-annotations/2"` on save. Loading accepts `/1` and `/2`; a `/1` file is upgraded in memory and written back as `/2` at the first save. Any other schema still throws. The `AnnotationStore` constructor check `document.schema != "twin-annotations/1"` changes to accept both; `AnnotationExporter` writes `/2`; the existing test `AnnotationEditsSurviveRestartWithoutReusingDeletedIds` must pass unchanged.
- `Annotation.region` (nullable `AnnotationRegion`): `kind` (`"patch"`), `source` (`"diff" | "sam" | "manual"`), `hull_twin` (float[][]), `centroid_twin`, `normal_twin`, `extent_m`, `area_m2`, `triangles` (optional `{ "tile": "x1_y0", "lod": 2, "indices": [..] }` for twin-side regions; null for scene-side regions), `paired_new_hull_twin` (optional, Changed only).
- `AnnotationStore.Add(...)` gets an overload with `AnnotationType type` and `AnnotationRegion region`; `SetRegion(string id, AnnotationRegion region)` replaces or clears a region; existing callers unchanged. `AnnotationMarker` draws the patch for an annotation with a region (same `DiffPatch` material, type colour, 25 % alpha, outline), hidden with the glyph when the Lens hides the anchor.
- DTO rules: `[Serializable]`, public fields, default constructor, arrays where the existing DTOs use arrays. Count the DTOs after the build and update the Android stripping check (`docs/evidence/android-json-metadata-v13.txt`).

### 5.3 Export

- JSON: the whole document (`/2`).
- CSV: `CsvHeader` = existing columns, then `region_kind,region_source,area_m2,centroid_x,centroid_y,centroid_z,extent_x,extent_y,extent_z,hull_points` where `hull_points` is `x y z;x y z;…` (space inside, semicolon between, quoted). Empty cells for annotations without a region. `CsvLine(Annotation)` is the pure function the test calls.
- Patches (§0.5): `ObjWriter` produces one OBJ per New/Changed annotation, vertices in the twin frame (`v x y z`, Z-up, metres), faces 1-based, a header comment with scan, annotation id and `exported_utc`. Scene triangles are selected by centroid within 1.5 m of the annotation centroid; cap 200 000 triangles per file.
- Toast after export: `JSON, CSV and 03 patches saved in Exports`.

---

## 6. Deliverable F — Guide

A six-card sequence on the inspection panel (pages `guide`, index 0–5 in `InspectorUI`), reached from **Setup → Guide** and opened automatically once per install (`InteractionPreferences.GuideSeen`) after the first `setup` page renders with `app.Ready`.

| Card | Heading | Body (one sentence) | Try it |
|---|---|---|---|
| 1 | Summon the dock | Raise a palm briefly and the tool dock appears in front of you. | Lower the hand, raise it again. |
| 2 | Point and pinch | Point with your hand and pinch to select; near the panel you can also poke. | Pinch the Next button. |
| 3 | Align the twin | Look at the measured marker, or drag the building with one hand; height stays locked. | Open 02 Align after the guide. |
| 4 | Compare | Reality, Twin and Overlay show the same place; the opacity slider blends them. | Try 01 Mode. |
| 5 | Annotate | Pinch a surface and choose Missing, New, Changed or Note; List exports everything. | Try 03 Annotate. |
| 6 | Candidates | Coloured patches mark differences found automatically; confirm or dismiss them in List. | Open 04 List. |

Layout: header `00 / Setup`, heading = card heading, meta `GUIDE 1 / 6`; body text at y=104 (width 432, Body size, at most two lines, auto-size floor 16); "Try it" line in Label style at y=184; bottom row **Skip** (x=24, 144) · **Back** (x=168, 144, disabled on card 1) · **Next** (x=312, 144, primary; `Done` on card 6). Skip and Done set `GuideSeen` and return to `setup`. In the Editor, the body of cards 1 and 2 keeps the headset wording and the meta adds `· EDITOR: SEE DIAGNOSTICS`. All text comes from the string table. Screenshots `v13-guide-1.png` … `v13-guide-6.png` in English and German.

---

## 7. Deliverable G — Localisation

### 7.1 String table

`Resources/Strings/<code>.json`, flat `{"key": "text"}` with stable keys named by location (`strip.mode`, `page.setup.heading`, `button.back`, `toast.candidateConfirmed`), placeholders `{0}`, `{1}`. `L.Get(key)` returns the current language's text, falls back to English, and records the key in `L.MissingKeys`; `L.Format(key, args)` applies `string.Format` with `CultureInfo.InvariantCulture`. The table is loaded once with Newtonsoft into a `Dictionary<string, string>`; switching languages reloads and re-renders the current page. At startup in the Editor, `DevLog` lists missing keys per language; the strings QA (§7.5) fails on any missing key.

What is **not** localised: annotation type keys in data (`missing`, `new`, …), JSON field names, CSV headers, manifest and fixture files, DevLog lines, evidence files, the language names on the Language page (always native: `English · Deutsch · Français · Italiano`), proper nouns (TwinInspector, Galaxy XR, Overlay as a mode name is translated per glossary).

### 7.2 Language selection

Default = `Application.systemLanguage` mapped (`German → de`, `French → fr`, `Italian → it`, anything else → `en`). `InteractionPreferences.Language` overrides it when set. **Language page** (`language`, from Preferences): four-cell segmented control at y=104 (each 108 mm wide, labels in native names), current language selected; `Back` at y=248 full width. Selecting a cell persists, reloads the table and re-renders immediately.

### 7.3 Glossary (mandatory terms; English and German are the reviewed lines)

| English | Deutsch | Français (machine) | Italiano (machine) |
|---|---|---|---|
| Twin | Zwilling | Jumeau | Gemello |
| Reality | Realität | Réalité | Realtà |
| Overlay | Überlagerung | Superposition | Sovrapposizione |
| Mode | Modus | Mode | Modalità |
| Align / Alignment | Ausrichten / Ausrichtung | Aligner / Alignement | Allineare / Allineamento |
| Annotate / Annotation | Markieren / Markierung | Annoter / Annotation | Annotare / Annotazione |
| List | Liste | Liste | Elenco |
| Missing | Fehlt | Manquant | Mancante |
| New | Neu | Nouveau | Nuovo |
| Changed | Geändert | Modifié | Modificato |
| Note | Notiz | Note | Nota |
| Candidate | Kandidat | Candidat | Candidato |
| Lens | Lupe | Loupe | Lente |
| Diff | Abweichungen | Écarts | Differenze |
| Marker | Marker | Marqueur | Marcatore |
| Roam | Umhergehen | Se déplacer | Spostarsi |
| Setup | Einrichtung | Configuration | Configurazione |
| Preferences | Einstellungen | Préférences | Preferenze |
| Diagnostics | Diagnose | Diagnostic | Diagnostica |
| Guide | Anleitung | Guide | Guida |
| Export | Exportieren | Exporter | Esportare |
| Confirm / Dismiss | Bestätigen / Verwerfen | Confirmer / Ignorer | Confermare / Ignorare |
| Back / Next / Skip / Done | Zurück / Weiter / Überspringen / Fertig | Retour / Suivant / Passer / Terminé | Indietro / Avanti / Salta / Fatto |
| Pick object | Objekt wählen | Choisir l'objet | Scegli oggetto |
| Save alignment | Ausrichtung speichern | Enregistrer l'alignement | Salva allineamento |

You translate every other string yourself, consistently with this glossary, in the register of a professional tool (German: Sie-form or imperative without pronoun, as the English uses imperatives). Mark `fr.json` and `it.json` with a top-level key `"_note": "machine translated, unreviewed"` and say so in README and DEVLOG. Do not spend time polishing them.

### 7.4 Fit rules (layout is fixed; text adapts)

- `PanelBase.Button` and `Row` labels use TMP auto-size with max = the current size and min = 16; strip cell labels use min 12. Nothing wraps inside a button.
- Soft limits you check in the table: strip cell label ≤ 11 characters; 208 mm button ≤ 18 characters; 144 mm button ≤ 12 characters; header meta ≤ 18 characters; body lines ≤ 2 at Body size. German strings that exceed a limit are shortened (abbreviations with a period are acceptable in buttons, never in body text).
- Characters: add to both `TryAddCharacters` strings in `ProjectBuilder` every character used by the four tables (generate the set from the JSON files at build time of the atlases: `ÄÖÜäöüßÉÈÊËéèêëÀÂàâÇçÎÏîïÔôÙÛùûŒœÌìÒòÁáÍíÓóÚú’²°·`), then rebuild the SDF assets through the existing menu and commit them.

### 7.5 Strings QA

`tools/unity/strings_qa_v13.cs`: for each of the four languages, switch, render every page (including `guide` 1–6, `language`, candidate-mode picker with a fixture candidate, `list` with candidates) and assert for every `TMP_Text` in the panel and strip: `!isTextOverflowing`, `textInfo.lineCount` ≤ allowed, and no "Character with Unicode value … not found" console message; write `docs/evidence/strings-v13.txt` (per language: pages rendered, overflow count = 0, missing keys = 0) and capture `v13-de-mode.png`, `v13-de-list.png`, `v13-fr-mode.png`, `v13-it-mode.png`.

EditMode tests `StringsTests.cs`: the four JSON files have identical key sets; `L.Get` falls back to English and records the key; `L.Format` uses invariant culture (`0.8 m²` never becomes `0,8`); the system-language mapping; no string in any table exceeds its slot limit (slot limits encoded in a small map by key prefix).

---

## 8. Deliverable C — Object pick (Stage 7)

### 8.1 Models

`tools/sam/export_mobilesam.py` (run in a fresh venv from `tools/requirements-sam.txt`, CPU only):
1. Fetch `weights/mobile_sam.pt` from the MobileSAM repository tree (it ships in the repo, not as a release asset); record URL/commit and SHA-256 in `tools/sam/README.md`; the `.pt` file is git-ignored.
2. Export the **image encoder**: `torch.onnx.export(sam.image_encoder, torch.zeros(1,3,1024,1024), "mobile_sam_encoder.onnx", opset_version=17, input_names=["image"], output_names=["image_embeddings"])`. Input is RGB float32 in 0–255 after resize-longest-side-to-1024 and zero padding bottom/right, normalised inside the app with mean `[123.675, 116.28, 103.53]` and std `[58.395, 57.12, 57.375]` — written once to `Models/models.json`, read by the app and by `MODELS.md`.
3. Export the **prompt encoder + mask decoder** with the repository script: `python scripts/export_onnx_model.py --checkpoint weights/mobile_sam.pt --model-type vit_t --output mobile_sam_decoder.onnx --return-single-mask --opset 17`. Inputs: `image_embeddings (1,256,64,64)`, `point_coords (1,N,2)`, `point_labels (1,N)`, `mask_input (1,1,256,256)`, `has_mask_input (1)`, `orig_im_size (2)`. Outputs: `masks`, `iou_predictions`, `low_res_masks (1,1,256,256)`.
4. Validate both with `onnxruntime` on one image from `dataset/raw/HGE/sessions/navvis_2022-02-06_12.55.11/raw_data/images_undistr_center/` (read-only): the mask for a point on a column must be non-empty and the IoU prediction > 0.5. Save the check as `docs/evidence/sam-export-v13.txt` with both SHA-256 digests.
5. Serialize for runtime loading: place the ONNX files temporarily under `Assets/_TwinInspector/ModelsImport/` so the Inference Engine imports them, run the new Editor menu **TwinInspector → Serialize SAM models** (`SamModelSerializer`: load each model asset, write `tools/sam/models/<name>.sentis` with the package's model writer — confirm the `ModelLoader`/`ModelWriter` names in the installed package source), then delete `ModelsImport/` so nothing ends up in the APK. At runtime the app loads the `.sentis` files from `TwinPaths.ModelsRoot` with the package's path-based loader. Commit only `tools/sam/models/` (≈ 40 MB, each file below 50 MB) plus `models.json` and `MODELS.md`.

If the Inference Engine fails to import the decoder's dynamic `Resize` (`orig_im_size`), use `low_res_masks` only and upsample 256² → 1024² on the CPU with bilinear interpolation; record the choice.

### 8.2 Flow (`ObjectPicker`)

1. **Pick object** is an action on the currently selected fresh annotation (§4.8); it attaches a region with `SetRegion` and keeps the annotation's type. `AnnotationPlacer` stays inactive; `ObjectPicker` consumes the next pinch on a visible twin surface itself (`InputRouter.BuildingHit` + `ConsumePress`). Prompt point = that hit.
2. `TwinCapture`: a temporary non-stereo `Camera` (`stereoTargetEye = None`, no additional camera data, disabled between captures) placed at the viewer position and **oriented toward the hit point** (look-at, up = world up), `fieldOfView = viewer.fieldOfView`, `aspect = 1`, `cullingMask = 1<<11`, `clearFlags = SolidColor` paper-2, target a 1024 × 1024 RGB `RenderTexture`. Temporarily set `_TwinAlpha = 1`, `_TwinTechnical = 0`, `_TwinLensEnabled = 0`; render with `RenderPipeline.SubmitRenderRequest(capture, new UniversalRenderPipeline.SingleCameraRequest { destination = rt })`; restore the globals in the same frame. Keep the capture camera's pose until back-projection is done. Read back with `AsyncGPUReadback` into the encoder input tensor. If `WorldToViewportPoint(hit)` is outside [0,1]² or behind the camera, abort with `No object found`.
3. `SamRunner`: encoder worker on the GPU compute backend (fall back to CPU when compute shaders are unavailable), scheduled across frames with the iterable scheduling API under the `objectPickFramesBudgetMs` budget; decoder in one frame. Prompt: `point_coords = [[px, py], [0, 0]]`, `point_labels = [1, -1]`, `mask_input` zeros, `has_mask_input = 0`, `orig_im_size = [1024, 1024]`, where `(px, py)` is the prompt in pixel coordinates with origin top-left (`px = u × 1024`, `py = (1 − v) × 1024`).
4. Mask → region: threshold logits > 0; take a 64 × 64 sample grid over the mask bounding box; for every grid cell inside the mask, one `RaycastCommand` from the capture camera through the pixel against layer 8 (distance 80); discard hits whose distance differs from the prompt hit distance by more than `max(0.5 m, 25 %)` (background bleed); `RegionBuilder` on the remaining hit points and their (tile, triangle) references; convert to the twin frame.
5. `SetRegion(selected.id, region)` with `source = "sam"`, toast `Object picked · 1.9 m²`. Fewer than 12 accepted hits → toast `No object found`, the point annotation is unchanged.
6. Budget: capture ≤ 1 frame; encoder total ≤ 3 s on device (estimate — measure and record), decoder ≤ 1 frame; workers created on the first press and disposed on destroy; peak additional memory ≤ 400 MB (log `Profiler.GetTotalAllocatedMemoryLong` before/after). Cancel when the page closes or the user moves more than 1 m.

### 8.3 Editor QA

`tools/unity/pick_qa_v13.cs`: in Play mode with Region A external data, create a point annotation on a column and one on a wall section at known twin coordinates (fixture-chosen), run Pick object on each; assert the region's hull contains the prompt hit, `area_m2` between 0.1 and 20, and the region normal within 30° of the surface normal at the hit; write `docs/evidence/pick-v13.txt` with timings.

---

## 9. Deliverable D — Device diagnostics (log only)

- **Depth probe**: `AROcclusionManager` carries `[RequireComponent(typeof(Camera))]`, so add it to the camera object, disabled at build time, enabled only while the probe runs on device (`SCENE_UNDERSTANDING_FINE` is already requested; the Android XR occlusion feature is already enabled in the OpenXR settings). Subscribe to `frameReceived` once; for each `ARExternalTexture` in `externalTextures` log `propertyId` (and which known AR Foundation occlusion property name it matches — read `ARShaderOcclusion` for the names), `texture.dimension`, width, height, `(texture as Texture2DArray)?.depth`, `texture.graphicsFormat`; log `TryGetFovs`, `TryGetPoses`, `TryGetNearFarPlanes`; then disable the manager. Also log `descriptor.environmentDepthImageSupported` and one `TryAcquireEnvironmentDepthCpuImage` (dimensions, format), disposing the image. v1.3 does not consume depth.
- **Camera probe**: through `AndroidJavaObject` only (no Java plugin): `android.hardware.camera2.CameraManager.getCameraIdList()`, and for each id `getCameraCharacteristics`: `LENS_FACING`, `SENSOR_INFO_PIXEL_ARRAY_SIZE`, `SENSOR_INFO_ACTIVE_ARRAY_SIZE`, availability of `LENS_INTRINSIC_CALIBRATION`, `LENS_POSE_TRANSLATION`, `LENS_POSE_ROTATION`, `LENS_POSE_REFERENCE`, and `REQUEST_AVAILABLE_CAPABILITIES`. Never open a camera. Catch and log `SecurityException`.
- **Diagnostics page layout**: the readout stays at y=104, height 120; bottom row of three 144-wide buttons at y=248: **Depth probe** (x=24), **Camera probe** (x=168), **Back** (x=312). Probe results go to `DevLog` in full and as one status line appended to the readout (truncated to the rect). In the Editor both log `Editor: probe unavailable`.

---

## 10. Deliverable E — Evaluation tooling

`tools/evaluate_annotations.py <export.json> <ground_truth.csv> [--out docs/evidence/evaluation-v13.md] [--radius 0.5 1.0]`

- Ground truth CSV columns: `id,type,x_twin,y_twin,z_twin,note` (team-measured real changes; ship `tools/fixtures/ground_truth_example.csv` with the fixture's planted changes as an example).
- Matching: greedy nearest within radius, one-to-one, type-agnostic and type-aware both reported. Metrics per radius: precision, recall, F1, mean and max distance of matches, count by type; plus median time between consecutive `created_utc` values and total session span.
- Output: Markdown table, printed and written to `--out`. Pure standard library + numpy; `-I` safe; unit test `tools/tests/test_evaluate_annotations.py` with a synthetic export (3 true positives, 1 false positive, 1 miss) asserting exact precision/recall values.

---

## 11. Performance rules

- Rendering budgets unchanged: ≤ 1.5 M triangles per frame, ≤ 300 draw calls, 90 Hz target / 72 Hz fallback, SPI multiview, no post-processing. Patches add at most 50 000 triangles and one material.
- Diff engine: ≤ `diffMainThreadBudgetMs` (2 ms) main thread per frame averaged over 10 s, ≤ `diffRaycastsPerFrame` ray commands and ≤ `diffOverlapsPerFrame` overlap commands per frame, all physics queries in jobs, no synchronous `Physics.*` loops on the main thread in the engine (the bounded `triangleIndex` fallback of §4.4 is the only exception).
- Object pick: ≤ 4 ms per frame of scheduled inference; never block the frame for the encoder; a visible "Processing object…" state; cancellation when the page closes or the user moves away.
- Measure in the Editor with the simulator and record numbers; mark device numbers **unverified** until a headset run.

---

## 12. Tests and QA scripts (all must pass before each commit)

EditMode tests, `Tests/Editor/DiffTests.cs`, `RegionTests.cs`, `SchemaV2Tests.cs`, `PickTests.cs`, `StringsTests.cs`, `GuideTests.cs`:

1. Union-find clustering splits two disjoint triangle islands and merges edge-connected ones; area and extent gates applied.
2. Hull builder: a square patch with 2 cm jitter yields a 4-vertex hull within 3 cm; PCA normal within 2° of +Z; 64-vertex cap holds on a 500-point circle.
3. Sampler builds exactly one ray per sample with the correct origin, direction, distance and `hitBackfaces = true`; the floor/ceiling rule drops a horizontal floor sample at `diffFloorZ`, drops a ceiling sample at 2.6 m, keeps a horizontal sample at 1.0 m and keeps a wall sample at 3.0 m.
4. Persistence: a cluster appearing twice is not stable; three times is; a dismissal sphere suppresses a same-kind candidate at 0.3 m and not a different kind.
5. Changed pairing is one-to-one, greedy by distance, within the threshold only; members become hidden, visible counts reflect it; dismissing the Changed candidate removes both members.
6. Batch stamps: a batch scheduled against tile version n is discarded when the tile version is n+1 at completion; a coalesced queue keeps only the latest chunk version.
7. `RaycastHit.triangleIndex` is populated for a batched hit on a `MeshCollider` (or the fallback is exercised).
8. Schema: a `/1` document loads, saves as `/2`, round-trips region fields with asymmetric coordinates; `/3` throws; `CsvLine` for a region has the expected column count and `hull_points` formatting; `CsvLine` for a point annotation has the same column count with empty region cells.
9. Region persists through `TwinRoot` yaw + translation: twin-frame hull identical after `WorldToTwin(TwinToWorld(p))`.
10. Pick geometry: viewport-to-pixel conversion (top-left origin), out-of-frustum abort, background-bleed filter, the 12-hit minimum.
11. OBJ writer: vertex and face count, 1-based indices, header fields.
12. Strings: identical key sets across the four tables; English fallback records the key; invariant formatting; system-language mapping; slot-limit map holds for every key.
13. Guide: six cards, index clamps, `GuideSeen` set only by Skip or Done.

Play-mode QA through the Unity CLI (`tools/unity/*.cs`, follow `desktop_v12_qa.cs`):
- `diff_qa_v13.cs`: external Region A + simulator + fixture; wait until the engine reports idle; assert ≥ 1 visible Missing within 0.5 m of e1; exactly one visible Changed within 0.5 m of e3.from whose paired New is within 0.5 m of e3.to, with both members hidden; ≥ 1 visible New within 0.5 m of e2; ≤ 2 other visible candidates; confirm e2 through the UI path (`list` → candidate row → `Confirm`) and assert the annotation has a region with `source = "diff"`; dismiss e1 and assert suppression after a forced re-evaluation; write `docs/evidence/diff-v13.txt` with counts and timings; capture `v13-candidates.png`, `v13-picker-candidate.png`, `v13-list-candidates.png`, `v13-diff-toggle.png`. The script takes a flag `assertPatches`: off in Stage 4, on in Stage 5, when it also exports and asserts that the patch OBJ for e2 exists.
- `pick_qa_v13.cs` (§8.3).
- `strings_qa_v13.cs` (§7.5).
- `guide_qa_v13.cs`: first-launch opens the guide, Next × 5 reaches Done, Done sets `GuideSeen`, Setup → Guide reopens it; screenshots in English and German.
- Existing `desktop_v12_qa.cs`, `spatial_v12_qa.cs` and `single_hand_v12.cs` still pass unchanged.

Python: `tools/tests/test_twin_pipeline.py` (unchanged) and `test_evaluate_annotations.py`.

---

## 13. [HUMAN] device acceptance (procedure to document; run when a headset is connected)

1. `adb devices -l`; install with `adb install -r`; push Region A with `tools/push_twin.ps1 -Region region_a` and the models with `tools/push_twin.ps1 -Models`; launch; grant the three permissions. The guide opens; go through it once; note any card that is unclear.
2. Switch the language to Deutsch in Preferences; check the strip, Mode and List pages; switch back.
3. Align with the marker or by hand; save. Open Diagnostics; run **Depth probe** and **Camera probe**; pull the log; paste the descriptor lines into DEVLOG.
4. Walk the hall for 2 minutes in Overlay with Diff on. Record: time to first visible candidate, candidate counts by kind, FPS/CPU/GPU from the metrics line, and whether the scene mesh appears at all (log line `Diff chunks=`).
5. Stand at a known change (a moved piece of furniture): confirm the candidate, retype, export, `adb pull`, open the CSV and the OBJ patch on the laptop.
6. Pick object on a column; record the encoder time and whether the region is plausible.
7. Record everything in DEVLOG under "Device acceptance v1.3" with the device model, build id and date. Failures are results too.

---

## 14. Working method and order

1. Read everything in §0's list. Write the plan for v1.3 at the top of a new DEVLOG section, then execute it in this order; commit after each numbered stage with a descriptive message.
2. **Stage 1 — foundations:** readable collision meshes, layer 11/12 assignment, `TwinLoader.Tiles` + `TileVersion` + events, `Settings` fields, `InteractionPreferences` fields, the string table with `en.json` and `L` (every existing user-visible string moved into it now, so every later stage writes through `L`), schema v2 + `RegionBuilder` + `ObjWriter` + `CsvHeader/CsvLine`; tests 2, 8, 9, 11, 12 (English only). Export v2 works for point annotations (empty region cells). Version bump in `ProjectBuilder.Setup`.
3. **Stage 2 — reality source:** `ISceneSurfaceSource`, `SceneSurfaceSource`, the Editor simulator + fixture + `DiffFixture` validation.
4. **Stage 3 — engine:** sampler, clusterer, persistence, pairing, stamps and coalescing, scheduling, dismissals; tests 1, 3, 4, 5, 6, 7; log line; Editor numbers in DEVLOG.
5. **Stage 4 — visuals and UI:** patches, glyph variant and `²`, hover/select, list rows, candidate picker mode, Diff on/off, Setup/Preferences button moves; `diff_qa_v13.cs` without `assertPatches`; screenshots.
6. **Stage 5 — export and evaluation:** patch OBJ export, `evaluate_annotations.py` + test, ground-truth example; `diff_qa_v13.cs` with `assertPatches`. Build an interim APK, verify as in v1.2 (`verify_apk.py`, metadata, DTO stripping check), record.
7. **Stage 6 — guide and localisation:** guide pages, `de.json` (reviewed against the glossary), `fr.json` and `it.json` (machine translated, marked), Language page, atlas characters and SDF rebuild, fit rules, `strings_qa_v13.cs`, `guide_qa_v13.cs`, test 13 and the three remaining languages in test 12.
8. **Stage 7 — object pick:** package (verify names first, §2), export script, models, `TwinCapture`, `SamRunner`, `ObjectPicker`, UI button, `pick_qa_v13.cs`, test 10. If blocked after a bounded effort (one working day of agent time), set `objectPickEnabled = false`, hide the button, record the exact blocker and continue.
9. **Stage 8 — diagnostics probes** (§9), small and isolated.
10. **Stage 9 — final build, docs, APK, commit:** rebuild the APK; `docs/evidence/{compile,tests,build,apk,android-metadata,android-json-metadata}-v13.*`; README (English, with a one-line note that German is reviewed and French/Italian are machine translated), DEVLOG, DESIGN (type auto-size floor, character set, glossary pointer) updates; final `adb devices -l` quoted; commit.
11. After every Unity change: `recompile_status` zero errors, `tools/unity_check.ps1` clean, console free of new errors. After every Python change: the tests with `-I`. Never hand-edit scene/prefab YAML while the Editor is open; author through `ProjectBuilder` and Editor APIs as before.
12. When something is impossible on this machine (no headset, package import failure, model export failure), finish everything that does not need it, write exact **[HUMAN]** instructions, and continue. Do not fabricate.

---

## Appendix A — `annotations.json` schema `twin-annotations/2`

```json
{"schema": "twin-annotations/2", "scan": "navvis_2022-02-06_12.55.11", "exported_utc": "...", "next_index": 9,
 "items": [
  {"id": "a-000008", "index": 8, "type": "new", "label": "New",
   "position_twin": [23.10, 11.42, 0.46], "normal_twin": [0, 0, 1],
   "created_utc": "2026-11-05T14:03:21.0000000Z",
   "device_pose_twin": {"position": [21.0, 10.0, 1.6], "rotation_quat_xyzw": [0, 0.7, 0, 0.7]},
   "screenshot": "", "comment": "",
   "region": {"kind": "patch", "source": "diff",
              "hull_twin": [[22.6, 10.9, 0.0], [23.6, 10.9, 0.0], [23.6, 11.9, 0.0], [22.6, 11.9, 0.0]],
              "centroid_twin": [23.1, 11.4, 0.46], "normal_twin": [0, 0, 1],
              "extent_m": [0.92, 0.91, 0.88], "area_m2": 3.1,
              "triangles": null, "paired_new_hull_twin": null}}
 ]}
```

Twin-side regions (Missing, Changed, SAM) fill `triangles` with `{"tile": "x1_y0", "lod": 2, "indices": [..]}`. Point annotations keep `"region": null`. The `label` field stays the English type word; the UI shows the localised word from the table.

## Appendix B — `exports/<scan>-<stamp>.csv` columns

`id,index,type,x_twin,y_twin,z_twin,nx,ny,nz,created_utc,label,comment,region_kind,region_source,area_m2,centroid_x,centroid_y,centroid_z,extent_x,extent_y,extent_z,hull_points`

## Appendix C — `diff/<scan>-dismissed.json`

```json
{"schema": "twin-dismissed/1", "scan": "navvis_2022-02-06_12.55.11",
 "items": [{"kind": "missing", "centroid_twin": [12.4, 3.1, 1.5], "radius_m": 0.6, "dismissed_utc": "..."}]}
```

## Appendix D — `Resources/Strings/en.json` (excerpt; the full table is yours to write)

```json
{"strip.mode": "MODE", "strip.align": "ALIGN", "strip.annotate": "ANNOTATE", "strip.list": "LIST",
 "page.setup.label": "00 / Setup", "page.setup.heading": "Inspect the building",
 "button.back": "Back", "button.next": "Next", "button.skip": "Skip", "button.done": "Done",
 "button.confirm": "Confirm", "button.dismiss": "Dismiss", "button.pickObject": "Pick object",
 "button.diffOn": "Diff on", "button.diffOff": "Diff off",
 "type.missing": "Missing", "type.new": "New", "type.changed": "Changed", "type.note": "Note",
 "toast.candidateConfirmed": "Candidate confirmed · Annotation {0}", "toast.objectPicked": "Object picked · {0} m²",
 "guide.1.heading": "Summon the dock", "guide.1.body": "Raise a palm briefly and the tool dock appears in front of you.", "guide.1.try": "Lower the hand, raise it again."}
```

## Appendix E — Models folder

`tools/sam/models/MODELS.md` records: source repository and commit, checkpoint SHA-256, export command lines, opset, input/output names and shapes, serialization step, normalisation constants, licence (MobileSAM: Apache-2.0), and the Inference Engine version used. `models.json` next to the `.sentis` files holds the constants the app reads and the SHA-256 of each model file; the app logs a mismatch and disables Pick object.

## Appendix F — Reference links (read when needed)

- AR Foundation 6.6 meshing and occlusion: `TwinInspector/Library/PackageCache/com.unity.xr.arfoundation@*/Documentation~` and the Runtime sources named in §4.1 and §9.
- Android XR package feature notes: `TwinInspector/Library/PackageCache/com.unity.xr.androidxr-openxr@*/Documentation~/features/{meshing,occlusion,camera}.md`.
- URP render requests: `Library/PackageCache/com.unity.render-pipelines.universal@*/Runtime/UniversalRenderPipeline.cs` (`SingleCameraRequest`).
- Unity Inference Engine manual: https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html
- MobileSAM: https://github.com/ChaoningZhang/MobileSAM
- Unity `RaycastCommand`, `OverlapSphereCommand` and `QueryParameters`: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RaycastCommand.html
