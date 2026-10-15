# TwinInspector development log

## 2026-10-14 — implementation plan

Read the course proposal, implementation brief, existing memory-mapped PLY reader, installed Android XR permission/camera/anchor documentation, and Google Extensions changelog and image/passthrough samples. Preserve the configured package versions and Unsafe compatibility plugin. Do not access HoloLens session directories.

1. Implement resumable bounded-memory CPU chunking, QEM LOD generation, GLB export, manifest, previews and independent verification. Process and verify Region A first.
2. Configure Unity through the CLI and Editor APIs, import interaction samples, implement the twin coordinate adapter and budgeted tile loader, and verify in the simulator.
3. Build the Swiss token/prefab system, view controls, manual/marker alignment, persistent anchors and annotations. Test coordinate round trips and persistence.
4. Build the Android APK and process Region B; document results, installation and the two device-only checks. Commit milestones on main.

Initial observations: repository has no commits; CLI cannot yet see an Editor instance from the restricted shell; adb daemon startup needs investigation. No headset result has been obtained. Google Extensions 1.4 removed its generic image provider, but retains marker and QR support via its sample's reference-image library conventions.

All device performance, passthrough, physical marker alignment and anchor relocalization results remain **unverified** until measured on a connected headset.

### Pipeline tooling milestone

- Implemented transactional PLY batches, per-cell compact NPZ, bounded QEM runs, normalized byte vertex-colour GLBs, manifests and top-down previews. Origin is `[20,0,0]`; glTFast's X reflection is corrected once by a 180° Y adapter.
- Four Python tests pass: asymmetric coordinate/handedness round trip, degenerate/duplicate removal, small real PLY end-to-end plus resume, and quadric reduction with finite colours. Independent trimesh verification passes on the synthetic integration fixture. Full Region A verification remains pending processing.
- Official Inter 4.1 archive downloaded. A4 marker sheets generated and independently decoded with OpenCV: ArUco 4×4_50 ID 0 and QR `TWIN-01`. The 150 mm physical symbol size is encoded at 300 dpi; the QR test uses camera-scale resolution.
- Design conflicts resolved in favour of the brief's legibility and contrast requirements: body text 21 mm at 1 m; ink text instead of muted text. See DESIGN.md.
- adb service works outside the restricted shell; `adb devices -l` reports **no devices**. Unity CLI reports the existing 6000.3.26f1 editor ready on port 7800 when run outside the restricted shell. No project settings changed yet.

### Real-scan processing findings

- Read all 384,836,269 source faces without loading the complete mesh. Region A retained 131,695,328 faces in 45 centroid cells; largest cell 7,249,920 faces. Original chunking/compaction run measured peak working set 977,002,496 bytes, below 8 GB.
- Pure QEM stalled on disconnected scan fragments, including at LOD0 on some tiles. Added bounded adaptive fragment welding followed by QEM; actual tolerances are recorded per tile/LOD. Caps 80/160/320 mm for LOD0/1/2. This is a documented deviation from 1 mm-only preprocessing, necessary to enforce the requested triangle targets on this scan, and needs visual inspection. No accuracy result is inferred from the tolerance.
- Stopped only our identified pipeline processes and resumed from their completed checkpoints while fixing this. The unrelated background processes and HoloLens download directories were not touched. All four Python tests pass after the fix.
- Follow-up controlled experiment identified arbitrary face-batch cuts as the main cause of the fragmentation. Complete-tile QEM on the 6,331,253-face cell produced 70,471 triangles in 78.3 seconds at 2,570,227,712 bytes peak working set, with only the original 1 mm weld; lower targets also succeeded. Switched to whole compact tiles up to 8 M faces, with spatial median splits only above that limit. Preserved the old experimental reductions outside the runtime output and restarted the affected simplification stage. Tiny boundary cells use a 64-triangle lower-LOD floor rather than dropping disconnected valid surfaces.

### Region A verified

- 45 tiles, 135 GLBs: independent trimesh verification **PASS**, including normalized uint8 COLOR_0, triangle counts, byte sizes, bounds, finite geometry, safe paths and LOD reduction.
- LOD0: **1,463,812 triangles / 29,612,000 bytes**. LOD1: **364,520 / 7,534,492 bytes**. LOD2: **86,618 / 1,895,084 bytes**. All region and per-tile budgets pass.
- Two-worker simplification observed combined peak working set **4,519,534,592 bytes**. Final maximum adaptive weld 5 mm / 80 mm / 160 mm for LOD0/1/2. Most LOD0 tiles use 1 mm. Preview visually retains hall outline, columns and the rotunda to the west; low-LOD scatter preview is sparse by construction and is not a surface-accuracy measurement.
- Five Python tests pass. Unity pre-change baseline: zero compile errors, zero console errors/warnings and zero Android OpenXR validation issues. Required Google features enabled, duplicate Google meshing disabled.

### Application implementation and automated checks

- Implemented bounded asynchronous GLB streaming, LOD2 collision surfaces, original-frame coordinate conversion, three view modes, global blend/style shaders, marker/QR alignment, two-hand yaw/translation, floor/height controls, persistent-anchor APIs with JSON fallback, and transactional annotation editing/export.
- Imported XRI Starter Assets, Hands Interaction Demo and XR Device Simulator; XR Hands visualization and Google tracking/passthrough samples remain reference assets. Only Main is in the build scene list.
- Created Inter SDF assets, shared DesignTokens/materials, a reusable PanelBase and surface AnnotationMarker. Main and prefabs are authored through Unity Editor APIs, never by editing asset YAML.
- Latest runtime and Editor compilation: **0 errors / 0 warnings**. Android OpenXR validation after render-settings changes: **0 issues**. Six Unity tests pass: asymmetric glTF handedness, aligned coordinate/normal/quaternion round trip, annotation restart/edit/delete ID behavior, and three unsafe-path rejection cases. Evidence saved under `docs/evidence/`.
- Simulator testing caught a MaterialPropertyBlock constructed during serialization and a Settings reference unloaded by NewScene. Moved the material block allocation to main-thread use, reload required assets after scene creation, and added a Settings fallback. Physical marker tracking and Android anchor restoration remain untested.
- Region B revealed small disconnected boundary remnants that cannot meet per-tile ratio targets within the bounded welding tolerance. Preserve at most 1,024 excess triangles per tile/LOD (or the existing 2% tolerance), record the exact excess, and retain hard total-region budgets. The independent verifier checks this declared exception. Partial tiles are recomputed on resume so reported welding tolerances remain accurate.

### Final simulator and visual verification

- The only enabled build scene is `Assets/_TwinInspector/Scenes/Inspector.unity`. The reusable builder and simulator helper use the same scene. Google Editor streaming auto-start is disabled so an absent headset does not interfere with local simulation; Android tracking features remain enabled.
- Final simulator run: **45 loaded tiles, 882,999 resident triangles including collision geometry, zero pending loads and zero tile failures**. The initial unaligned pose now places the viewer in the open hall, looking west in the Editor. Scanned back faces accept annotation rays, with annotation normals facing the viewer.
- Runtime smoke checks passed: all three modes, global blend, manual nudge and height lock, simulated marker/cancel, annotation create/select/retype/reload/delete, JSON and CSV export. The visual suite also verified actual LOD2 collider hits and alignment JSON save/restore. Temporary QA annotations were removed after capture.
- Inspected six running panel captures and the hall view. Corrected a clipped view-mode label, slider knob shape, incomplete visible list row, and clipped tool-strip label. The final contact sheet and hall view are in `docs/images/`; all imagery is rendered by Unity from the running implementation.
- Final Editor compilation: **0 errors / 0 warnings**. Six EditMode tests passed again in 0.54 seconds. Android OpenXR validation: **0 issues**. The CLI console history retains a resolved earlier QA raycast exception; the final live Unity console reports zero errors/warnings.
- Paused only the identified Region B worker to give the first Android build sufficient RAM. Tile checkpoints are retained. On this 16 GB machine, simultaneous full Unity recompilation and mesh reduction caused paging; run large build and reduction stages sequentially.
- Device results remain unavailable: no authorized headset is connected. No device FPS, permission-prompt, passthrough, physical marker, controller/hand interaction or persistent-anchor relocalization result is claimed.

### Android build compatibility

- The first full Vulkan/ARM64 IL2CPP compilation reached Gradle packaging. Both installed XR packages injected `android.software.xr.api.spatial`, with versions 1 and 3, into the same generated library manifest; Gradle rejected the duplicate feature. OpenXR Project Validation does not detect this package overlap.
- Added post-generation normalization of duplicate XR features, permissions and native-library declarations, preserving the highest feature version and required capabilities. The package versions and Unsafe compatibility assembly remain unchanged.
- Unity became unresponsive after the confirmed Gradle failure. Restarted only the task's Editor and restored the two AR Foundation simulation assets from its temporary build folder, preserving their contents and GUIDs. Native compiler outputs and downloaded dependencies remain cached for the retry.
- Independently inspected the Android stripped assembly: all nine JSON DTOs retain public fields and default constructors. The installed Android XR documentation confirms the runtime CPU/GPU metric names used by PerfHud.
- Recovery UI inspection could not proceed because Computer Use app approval timed out. Preserved the scene backup under `work/scene_recovery_20261015` and reopened via the Unity CLI in batch mode. The normalization fixture passes, including idempotence and retaining spatial API version 3. Fresh compilation has no errors and Android OpenXR validation remains at zero issues.

## 2026-10-15 — APK verified

- Pipeline build `build_6d7aa554b87d`: **Succeeded**, **0 errors**, 1 expected warning that the Pipeline Editor control service is disabled in player builds. The cached retry took 294.808 seconds. Final Android OpenXR validation: **0 issues**.
- APK: `Builds/TwinInspector-0.1.0.apk`, **55,348,562 bytes**. SHA-256: `58b22bb4d9828fc6d6a2c5e697d46c4eac9cc21c0bb83613a9f6a635e7d80ae6`.
- Verified ZIP integrity, ARM64-only native libraries and IL2CPP, application `com.mrtwin.inspector` version 0.1.0, GameActivity launch, min SDK 25 / target SDK 36, and required camera, scene-understanding and hand permissions. The XR packages also declare their OpenXR and eye-tracking capabilities.
- The APK contains exactly **45 Region A LOD2 GLBs**, with hashes matching their source files, and no higher-detail GLBs. The complete bundled fallback is **1,944,771 uncompressed bytes**. The build report's aggregate size includes intermediate files; it is not the APK size.
- Final `adb devices -l`: **no devices attached**. Installation, headset launch, permissions, FPS, physical marker registration, hand/controller behavior and anchor relocalization remain unverified. The exact device acceptance sequence is in README.
- Closed the verified batch-mode Editor normally to release memory. Preserved Unity's generated recovery scene outside the project under `work/scene_recovery_assets_20261015`. Resumed Region B with two workers and an enforced combined 8 GB working-set limit. Rebuilt boundary tile `x-1_y-6` from source so its actual 80 / 160 / 320 mm cleanup caps and retained fragments are recorded accurately.


## 2026-10-15 — v1.1 interaction and spatial UI

- Added `InputRouter` for active XRI ray hits and abstract Select/UI Press, with a direct screen-space mouse fallback when no HMD is tracked. Annotation placement and two-hand registration no longer depend on an active controller transform. Optional controllers use trigger confirmation.
- Authored four inspection rays alongside the hand/controller modality groups, retaining XR Hands tracking, pinch action readers and poke interactors. Rebound sample poke/system-gesture suppression to the new rays. Disabled sample locomotion and simulator defaults, stripped controller mesh renderers, and suppressed every sample beam renderer.
- Added Editor flight (RMB yaw/pitch, WASD, vertical E/Q or Space/Shift, Alt boost, wheel speed), direct mouse UGUI and annotation clicks, keys 1–4, M and Tab. The corner key hint appears only with PerfHud.
- Added two-hand pinch world-drag in Twin mode. Tracking-space samples move XR Origin with inverse translation/yaw while preserving floor height; ManualAlignment still moves TwinRoot. UI, active annotation placement and registration suppress roaming.
- Replaced the strip's panel-relative position with a separate world anchor: 0.55 m ahead, 22° below eye level, 25° heading comfort cone and 150 ms ease-out. Palm-up uses the left XR Hands palm pose. Full-height square paper/ink cells use 1 mm shared borders and Inter Medium uppercase labels with 6% tracking. Replaced sample rays with an 8 mm ink/accent reticle and paper outline.
- Actual Input System QA passes mouse clicks on all four tools, screen-ray annotation creation, keys 1–4, marker M, Tab, RMB rotation/forward flight, zero roll, vertical E and cursor unlock. A 15° look leaves the strip stationary; 50° recenters it. QA annotations are deleted afterward. Eight Unity tests and eight Python tests pass. Android OpenXR validation reports zero issues.
- Region B baseline completed: 172 tiles, 3,964,188 LOD0 triangles / 80,385,404 bytes. Its old boolean fragment allowances are interpreted as their exact bounded counts by the independent verifier; no threshold was relaxed. All 516 GLBs pass geometry/budget checks. This baseline has not been regenerated for the new seam policy.
- The independent source-edge audit of old Region A tile x2_y0 found missing boundary edges at all LODs, with maximum nearest-vertex displacement 0.365 / 0.548 / 2.330 m. v1.1 now pins all open edges at every QEM pass and removes subsequent welding. The exact boundary policy invalidates old reduction checkpoints. Region A regeneration and complete exported-edge verification passed; results follow below.


### v1.1 geometry acceptance

- Rebuilt all 45 Region A tiles with exact source open boundaries at every LOD. Totals: LOD0 **1,293,355 triangles / 29,710,628 bytes**; LOD1 **820,558 / 20,264,836**; LOD2 **815,667 / 20,169,364**. The LOD0 budget passes. Lower LOD ratios are intentionally constrained by pinned source edges; no destructive welding is used to force a target.
- Independently checked all **135 exported GLBs**: **zero missing source boundary edges**, maximum coordinate displacement **0.00000190735 m**, attributable to float32 export rounding. The proof is bound to exact GLB SHA-256 digests; stale proofs are rejected. Geometry, colour accessors, bounds, sizes and budgets pass `verify_tiles.py`.
- Activated the result at `work/twin/HGE_2022-02-06/region_a`; retained the previous dataset as `region_a_v10`. Mixed LOD neighbours share the same preserved source boundary.
- The fallback contains **32 complete nearby Region A LOD2 tiles**, **14,048,147 total bytes**, below the 15 MB limit. The complete 45-tile dataset remains external. The selection is recorded in the demo manifest.
- A Windows progress-file reader briefly blocked telemetry-file replacement. Memory-limit enforcement now precedes telemetry publication, retries transient sharing locks and survives publication errors. A scoped watchdog guarded the ongoing run; the conservative two-worker peak bound from per-worker peaks plus parent allowance is **5,434,110,976 bytes**, below 8 GB. Added a regression test for the sharing-lock retry.


### Final viewport refinements

- Visually verified the red/paper reticle on an actual collider hit and the disc/leader/label annotation on regenerated geometry. Corrected the reticle ring winding so its visible face points toward the viewer.
- Technical view previously relied on a second derivative of a reconstructed normal, which produced a flat blue silhouette in the actual capture. Added light-independent surface relief and grazing-angle edge contrast, retaining the monochrome tint and leaving Colour view unchanged.
- The first Android v1.1 build exposed an Android-only obsolete `PermissionDeniedAndDontAskAgain` subscription. Removed it; Unity uses `PermissionDenied` as the fallback, as documented in its [permission callback reference](https://docs.unity3d.com/ja/6000.0/ScriptReference/Android.PermissionCallbacks.PermissionDeniedAndDontAskAgain.html). Final build results follow below.
- With the complete new Region A active, the loader prioritizes 29 tiles at the initial view, **1,199,332 resident triangles including collision geometry**, zero load failures and no pending loads. It stays within the 1.2 M streaming budget and changes the selected tiles with the viewer.

### v1.1 final build and acceptance

- Final Android build `build_95cb8c8ceb95`: **Succeeded** in **192.020 seconds**, **0 errors**, **0 warnings from TwinInspector scripts**. The sole build warning says the Pipeline Editor control service is disabled in Player builds because no runtime control configuration is present. Post-build `recompile_status` completed without errors; Android OpenXR validation reports **0 issues**.
- Rebuilt `Builds/TwinInspector-0.1.0.apk`: **66,014,471 bytes**, SHA-256 **23e08596c75b053ca28634cab7a8bb326e3bec14d3c6d08608fa80f21ff2b189**. Verified ZIP CRC, ARM64 IL2CPP, GameActivity, required camera/scene/hand permissions and preservation of all nine JSON data types through Android stripping.
- The APK includes exactly **32 LOD2 GLBs**, each matching the regenerated source hash, no higher-detail GLBs, and **14,048,147 bytes** of uncompressed fallback data. The full 45-tile Region A remains available externally; lower LOD targets yield to exact boundary preservation.
- Acceptance evidence: **8/8 Unity tests**, **8/8 Python tests**, additional seam-proof tamper regression, actual queued mouse/keyboard Play-mode checks, all 135 Region A exported-edge checks, and seven hall/panel captures plus reticle and annotation close-ups. See `docs/evidence/` and `docs/screenshots/`.
- Final `adb devices -l` remains empty. Physical Galaxy XR pinch/poke, palm anchoring, controller pickup, world-drag, marker tracking, anchor relocalization and device frame rate require headset acceptance; no hardware result is inferred from Editor tests.
