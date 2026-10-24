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

### Shared APK for teammate testing

- At the owner's request, added the verified `Builds/TwinInspector-0.1.0.apk` to the private GitHub repository and linked it from the README. Its 66,014,471 bytes are below GitHub's 100 MiB Git-file limit. This is an explicit exception to the original build-output exclusion; all other build outputs and external data remain ignored.
- The binary and SHA-256 are unchanged from the accepted v1.1 build above. Android package version remains 0.1.0; application source is commit `55d82ce`. Repository access is required to download it. Physical headset verification remains outstanding.

### Panel docking improvement

- Settings and all tool pages now sit centered 24 mm above the four-cell strip at the same depth. They share a world anchor and the 150 ms comfort-cone recenter, so the panel stays close to the buttons without becoming a head-locked HUD.
- Palm mode moves only the compact strip. The full panel keeps its world pose; opening a page from the palm summons a reachable world panel, and lowering the palm restores the shared layout. Annotation placement still hides the panel. Feedback toasts appear above the panel and do not cover the tool buttons.
- Compilation passes with **0 errors and 0 warnings**. Actual queued mouse/keyboard checks pass tool-page clicks, the opacity slider, annotation placement, flight and shortcuts. Layout checks confirm the 24 mm gap on Setup, Mode, Align, List and the annotation picker, no motion within 15°, and shared recenter after a 50° turn.
- Simulated palm transitions through the production spatial update confirm that the full panel does not follow hand motion, opening a page recenters it, and returning to world mode restores the gap. Physical palm tracking has not been re-tested for this patch. Refreshed screenshots include `docs/screenshots/panel-docked.png`; detailed evidence is in `docs/evidence/panel-dock.txt` and `desktop-v11.txt`.
- Android build `build_62e2c7ae021f` from application source `5dc7fd7` **succeeded** in **608.283 seconds**, with **0 errors** and only the existing Pipeline Editor-service warning. Post-build compilation is clear and Android OpenXR validation reports **0 issues**. The stripped Android assembly contains the new docking methods and retains all nine JSON data types.
- Updated shared APK: **66,012,611 bytes**, SHA-256 **82b1fcb4efe99a5268388bb246c8facdafbc0ac3a6987dbb03bd5aaf64602fde**. ZIP integrity, ARM64 IL2CPP and all 32 bundled LOD2 hashes pass; fallback size remains 14,048,147 bytes. Install over the existing app with `adb install -r` to retain saved data.

## 2026-10-15 — v1.2 architectural inspection and accessible spatial controls

- Kept the existing scene, packages, Android identity, loading budgets, coordinate frame and saved-data schemas. Application version is now **1.2.0** (Android version code 2); the tracked delivery filename remains `Builds/TwinInspector-0.1.0.apk`.
- Technical rendering caches adjacency, area-weighted normals and bounded concavity per loaded mesh/LOD. Shared edge quads expand in stereo screen space to a roughly one-pixel Ink core with an anti-aliased footprint; coplanar noise and unpaired chunk-cut edges are omitted. Measured vertices are unchanged. Original-frame 0.5 m contours and the existing datum grid share the alignment-aware coordinate matrix. Colour rendering remains available. No scan processing was rerun.
- Added an optional Overlay Lens with 0.5–5 m radius, 20% boundary falloff and 150 ms hold/fade. Its solid inner region writes depth; a shared non-depth-writing shell handles the soft boundary. Outside geometry is clipped in fill, edges, contours and grid. UI capture preserves the last center. Near/far screenshots demonstrate different apparent footprints of the same physical radius.
- The numbered Swiss strip and pages share the existing 24 mm dock. Either hand can summon it with a qualified, re-armed palm gesture and then operate it. Preferences persist handedness, sound and volume. Active, hover, press and disabled states are distinct; geometric type icons retain their identity under selection. Bundled Inter digit advances and required symbols are retained during player builds.
- One adaptive position/quaternion filter drives each actual XRI ray; the existing continuous input readers feed ownership and the 0.4/0.3, 150 ms pre-pinch lifecycle. Single-hand fixed-reference alignment, transition-safe two-hand alignment, one-hand Roam controls and automatic return to the physical comparison frame share the existing router. Roaming suspends anchor following without changing saved registration.
- Annotation anchors and persisted normals stay fixed. Separate billboard glyphs meet 1.5° at 10/20/30 m, independent selection targets meet 2°, and labels use their own text scale. Picking uses stable angular ranking and building occlusion; enlarged colliders are unnecessary. The reticle scales to 0.35° and contracts continuously. Cached, zero-ended procedural hover/confirmation clips accompany accepted actions, with complete visual feedback when muted.
- **16/16 Unity tests pass**, including coordinate consistency, angular sizing, crease discrimination, pinch cancellation/re-arm and single-hand ownership. Full production-XRI synthetic workflows pass independently for left hand, right hand, left controller and right controller: summon, preferences, comparison, Lens, alignment/save/reload, simulated marker confirmation, roaming/return, creation/retyping, list drag/selection, deletion and export. These fixtures supply tracked poses/continuous values; they do not establish physical tracking.
- Actual Editor captures are in `docs/screenshots/v12-*.png`. Lens UI hold/no-hit fade, live alignment and LOD movement checks pass. The external view settles at 29 tiles / 1,199,332 source-plus-collider triangles; the packaged demo loads all 32 tiles / 1,133,706 triangles with zero failures. Packaged-demo mode switching, annotation save/reload and JSON/CSV export pass. The rendering cache adds edge geometry without changing the existing streaming budget; hardware rendering cost remains unmeasured.
- Final rendering checks compare the same camera at 100% and 75% render scale. Stroke width uses output dimensions, with explicit XR eye-texture scale compensation. A synthetic constant-height datum plane stays lightly shaded; it does not turn into a filled contour. All four shaders report zero compilation messages. The final unattended desktop run passes flight, mouse UI, direct annotation placement, shortcuts and the 24 mm dock checks; a fresh packaged-demo run reports zero Editor console errors/warnings.
- Selected and hovered annotation labels have explicit canvas priority over ordinary labels. Controlled overlap captures verify that emphasis stays readable while the geometric type identity and physical anchor remain unchanged.
- Final Android build `build_c4587f016d7d` **succeeded** in **258.404 seconds**, with **0 errors** and the existing warning that the Pipeline Editor control service has no player runtime configuration. Post-build compilation reports **0 errors / 0 warnings**; Android OpenXR project validation reports **0 issues**. The APK retains `com.mrtwin.inspector`, GameActivity, ARM64 IL2CPP and all nine JSON data types after stripping.
- Delivery APK: **66,366,495 bytes**, SHA-256 **bf4e2daafa8afc507413752684df6590251254ec5eac60e537de70de593e6030**. ZIP integrity and exact hashes of all 32 packaged LOD2 tiles pass; fallback data remains **14,048,147 bytes**. Production dataset settings are restored and verified in the APK. Application source, generated assets, evidence and APK are recorded in commit `7cd4f90`.
- Final `adb devices -l` is empty. Installation/launch, physical left/right hand and controller tracking, near poke, both-eye output, runtime OpenXR logs, marker/anchor relocalization and headset performance remain **unverified**. Neither the 72 FPS minimum / 90 FPS target nor the 300-draw-call budget is claimed from Editor measurements.

### Repository documentation

- Added an illustrated repository README with the inspection workflow, Editor controls, APK installation, release checksum and verification status. Existing Editor captures show the dock, Colour/Technical views, Lens and annotation controls. The project README remains the detailed build and data guide.
- The documentation update leaves the verified v1.2 application binary unchanged.

## v1.3 autonomous implementation — 2026-10-22

Plan: (1) geometry/schema/localisation foundations; (2) device and simulated reality sources; (3) bounded geometry diff engine; (4) candidate patches and review UI; (5) refinement export and evaluation; (6) guide and four languages; (7) bounded MobileSAM integration; (8) diagnostic probes; (9) complete validation, Android build and publication.

Baseline: main 80b0ca3, implementation branch v1.3, brief committed e68ddfb. Python mr imports numpy/trimesh. Unity 6000.3.26f1 started by this run. `adb devices -l` returned only `List of devices attached`; all device behavior remains unverified. The authoritative numbered stages in PROMPT_TwinInspector_v1.3.md govern gates for features implemented so far; Android builds at Stages 5 and 9. Fixture separation is interpreted between independent edits (the moved object's own removal/addition are necessarily 0.7 m apart).

Stage 1: collision meshes stay CPU-readable; lifecycle notifications track collision-set changes. Optional annotation regions preserve the original frame and IDs, legacy files upgrade on save. PCA/convex-hull geometry, invariant CSV and OBJ writers, English resource strings and persistent preferences added. Multi-tile regions may include optional `additional_triangles`; the required primary triangle reference is unchanged. Memory measurement and validation follow below.
Stage 1 validation: 22/22 EditMode tests pass (16 original + 6 new), zero current compile errors/warnings, zero OpenXR Android issues. Initial test-assembly Newtonsoft reference error was repaired with an explicit precompiled reference; CLI history retains that resolved error, while live console groundTruth is zero. Evidence: tests-stage1-v13.json and compile-stage1-v13.txt.

Stage 2: AR Foundation 6.6 meshInfosChanged source and deterministic Editor source implemented. Missing GLB normals are generated before snapshotting. Real Region A produces 69 chunks and 815,253 triangles after fixture edits; every chunk is readable, on layer 9, and at most 20,000 triangles. Loaded collision CPU positions/normals/indices require at least 15,264,348 additional bytes for 29 loaded tiles (excludes overhead/other channels); total Unity allocated memory at this checkpoint 802,172,710 bytes. Fixture: e1 (9.98,8,0.6), e2 (15,10,0.45), e3 from (9.98,15,0.4) to (10.68,15,0.4), noise sigma .02 seed 1303. Validation checks retained-triangle intersections and coverage; see source-v13.txt. The coalescing contract test exposed Unity's fake-null collider semantics; replaced ?? with Unity's explicit existence test. Pure sampler, cluster and candidate contract helpers were added alongside these tests in preparation for Stage 3.

Stage 3: bounded RaycastCommand/OverlapSphereCommand work with later-frame completion, immutable worker clustering, queue coalescing, version/generation checks, geometric floor/ceiling filtering, persistence, one-to-one Changed pairing and persisted dismissal spheres. A real QA run found a streaming-boundary ambiguity: expanded loaded AABBs also cover parts of unloaded neighbor tiles. Samples in the expanded bounds of an unloaded tile are now conservatively unknown. This additional coverage guard removes false New candidates without using simulator IDs or fixture labels. Initial removal on a thick column crossed tile boundaries and fragmented; final fixture uses e1 (12,0,.6), e3 (34,19,.4) -> (33.3,19,.4), retaining e2 (15,10,.45). The fixture validator still requires >=50 intersected triangles. Initial run before the guard is retained in the work history; final metrics and counts are in diff-engine-v13.txt. All planted edits found; Changed members hidden and target within .5m. Batched triangleIndex verified in a real MeshCollider query; no synchronous fallback is required.
Stage 4: candidate meshes, hollow glyphs, combined angular selection, confirmation/dismissal and Diff controls passed real Input System mouse clicks through the production UI. Confirmation round-trips its region; dismissal remains suppressed after a complete re-evaluation. Global patch geometry is capped at 50,000 triangles. Screenshots v13-candidates, v13-picker-candidate, v13-list-candidates and v13-diff-toggle captured from Play mode; the candidate picker image was visually inspected. diff-v13.txt records assertPatches=False. Guide/language destinations are completed in Stage 6; device gestures remain unverified.

Stage 5: asynchronous export snapshots the current scene mesh and writes nearby New/Changed triangles as bounded, twin-frame OBJ patches alongside JSON and CSV. The repeated UI QA now passes with assertPatches=True and verifies the confirmed New patch contains vertices and faces. The standard-library evaluation tool reports type-aware and type-agnostic one-to-one metrics; 11 Python tests pass. The example evaluates one confirmed New annotation against three fixture targets (precision 1, recall 1/3, error 0.097 m); it is not a full-detection benchmark. Narrow linker preservation keeps all 13 JSON DTOs in the stripped Android assembly.
Stage 5 interim Android build build_83952a875be2 succeeded in 934.227 seconds: zero errors, only the existing Pipeline editor-service configuration warning. APK 66,813,063 bytes, SHA-256 e05a3aa87853ccf7503618d24910209876e2d03ab45a1fb03218aa491144cde1; ARM64 IL2CPP, version 1.3.0 / code 3 and all 32 bundled LOD2 tile hashes verified. The interim binary is retained locally until the Stage 9 final build replaces it. This checkpoint includes guide work prepared during compilation; final evidence will cover the complete release tree.

Stage 6: six-card first-launch/reopenable guide, persistent runtime language selection, semantic resource keys and generated atlas character coverage. English and German terminology reviewed by the implementation agent against the brief glossary; no human language review is claimed. French and Italian are machine translated, unreviewed. All four languages render 20 page/card states with zero overflow, missing keys or missing glyph warnings. Initial checks caught three 24 mm slider labels too tall and two German guide sentences using three lines; corrected to 19-point labels and shorter full sentences. Button/row auto-size minimum 16, strip minimum 12. Guide first launch, five Next actions, Done, reopen and Skip pass in English and German; screenshots visually inspected. All 30 Unity tests pass, current compile errors/warnings and Android OpenXR issues are zero. A transient Windows file mapping lock on old setup.txt affected only evidence writing after assets were saved; the builder now writes setup-v13.txt and rerun succeeded. Diff can safely release its completed work on disable for isolated UI inspection. Physical tracking remains unverified.

Stage 7: installed Unity Inference Engine/Sentis 2.6.1 (assembly Unity.InferenceEngine), with its required app-ui 2.2.4 dependency; no existing package version changed. Package 2.6.0 first reported missing Hub authentication, but official 2.6.1 installation succeeded. CPU-only pinned MobileSAM export passed ONNX checking and ONNX Runtime validation on the permitted NavVis column image (nonempty mask, predicted IoU 0.979965; this is confidence, not measured accuracy). Serialized encoder 27,899,484 bytes and decoder 20,517,020 bytes load successfully; SHA-256 manifest, provenance and Apache license are committed externally. Temporary ONNX import assets were removed through AssetDatabase before building. The full dynamic decoder imports; low_res_masks is sampled bilinearly, with no Resize workaround.

SAM's explicit prototype path captures a colour-only twin render, schedules the encoder iterably, decodes, back-projects accepted mask samples and preserves the annotation type. Initial coarse-triangle expansion and adjoining angled surfaces caused a 36–48 degree normal error. Regions now fit accepted grid samples, reject >0.5m discontinuities and additionally reject normals >30 degrees from the prompt (two-sided sign-independent). This intentionally yields a conservative visible surface patch, not a guarantee of complete semantic-object coverage. A verified vertical column and wall now pass area, normal and prompt-containment tests (0.5413 / 4.2603 m2, 0 / 5.42 degrees); page-close cancellation and external-model integrity pass. The first proposed wall fixture hit a sloping surface and was replaced with a measured vertical wall, with an explicit vertical-normal assertion. A teardown regression found while stopping Play mode was fixed by guarding destroyed roots in DiffEngine.RebuildVisible.

**Stage 7 release gate: SAM disabled by default (`objectPickEnabled=false`).** The package's first GPU dispatch blocked the Editor for 30,070.55 ms (33,127.6 ms total); an earlier cold attempt hit the 45-second timeout. Subsequent functional runs were 468.7 / 659.0 ms total, but encoder scheduling peaks 4.88 / 23.77 ms and decoder scheduling 10.15 / 38.23 ms still fail the strict frame budget. ScheduleIterable cannot preempt one layer/driver compilation. The bounded integration effort therefore uses the brief's optional-feature fallback: retain tested code, models and re-enable instructions, hide the action in the release, and continue all other delivery work. Sampled Unity allocations do not include complete GPU-driver memory, so the 400 MB target is not claimed. Device performance is unverified. URP 17.3 warns when setting built-in stereoTargetEye; the capture now uses its source-verified standalone SingleCameraRequest path without additional camera data or an XR pass, avoiding that unsupported setter. Evidence: sam-export, sam-serialize, sam-performance, pick and pick-cancel-v13.

Stage 8: added a disabled AROcclusionManager to the viewer through ProjectBuilder and an opt-in DeviceProbes service. Depth subscribes for one frame, logs texture property ID/name, dimensions/array depth/format, descriptor support, FOVs, poses, near/far and a disposed CPU-image attempt, then disables the manager; no-frame timeout is eight seconds. Camera2 enumerates IDs and characteristics without opening a camera; AndroidJavaException/SecurityException is logged. Diagnostics retains its 120 mm readout with a short localized status and three 144 mm actions. Both production UI buttons return exactly "Editor: probe unavailable" and leave occlusion disabled. Physical descriptor contents and camera access remain unverified. Tests-stage7-v13 also covers the combined compiled probe code: 32/32 pass, current compile errors/warnings and Android OpenXR issues zero.

Stage 9 regression: the unchanged single-hand workflow exposed a list interaction race after two successful modalities: a background candidate update destroyed the ScrollRect during a pinch drag. InspectorUI now coalesces background diff refreshes until the pointer leaves and the held input ends, with a 0.5-second idle margin, and preserves the scroll offset. Explicit page actions still render immediately. A production Input System regression verifies hovered and held controls survive, refresh resumes after release, and the scroll position persists (`list-refresh-v13.txt`). The original v1.2 input scripts remain unchanged. Current localisation coverage is 21 page/card states in each of four languages, all zero overflow/missing-key counts, with zero missing-glyph warnings.

The final Android metadata audit initially included SamModels' compiler-generated async state machine among nested DTO classes and incorrectly required a parameterless class constructor on that struct. The verifier now excludes types marked CompilerGeneratedAttribute while continuing to check every declared JSON DTO, public field and default constructor. This fixes the audit scope without changing runtime code or relaxing preservation checks for data contracts.


### v1.3 final verification — 2026-10-23 (Europe/Paris)

Final Android build `build_0defc2fd1512`: **Succeeded**, 1431.269 seconds, 0 errors. **354 build warnings**: 353 from the installed Inference Engine shader sources (320 ConvGeneric unsupported-variant warnings plus 33 pixel-shader warnings), and one existing Pipeline Editor control-service warning. Basename-only ConvGeneric messages were checked against the installed package source; no warning originates in TwinInspector scripts. Full messages and the classification are retained in build-v13.json / build-warnings-v13.json. These package warnings are not suppressed or claimed harmless; SAM remains disabled and device evaluation is outstanding. C# compilation is clean, Android OpenXR validation has zero issues, and all 32 Unity / 11 Python tests pass. The current console reports zero errors and 34 warning entries; historical CLI entries from repaired development errors are not current errors.

APK: `TwinInspector/Builds/TwinInspector-0.1.0.apk`, **77,210,373 bytes**, SHA-256 **3d8ddab95b8afadd32b939e8595b0bf6e0a4e0e75416931e0b29384342297824**, version **1.3.0**, code **3**, package `com.mrtwin.inspector`. ZIP integrity, ARM64 IL2CPP, exact hashes of all 32 packaged LOD2 tiles and all 14 JSON DTOs after stripping pass. The serialized SAM weights are external, with no temporary model imports in Assets and no model files in the APK. The filename remains unchanged for deployment-script compatibility.

The table distinguishes implementation evidence from physical acceptance. “done” does not establish headset behavior or device performance.

| Brief §0 | Status | Evidence under `docs/evidence/` |
|---|---|---|
| 1 Geometry diff engine | done; device behavior unverified | diff-engine-v13.txt; tests-v13.json |
| 2 Deterministic Editor reality | done | source-v13.txt; diff-engine-v13.txt |
| 3 Candidate review, confirm, dismiss, toggle | done | diff-v13.txt; list-refresh-v13.txt |
| 4 Persistent region annotations / v1 compatibility | done | tests-v13.json; diff-v13.txt |
| 5 Scene-mesh OBJ refinement exports | done | diff-v13.txt (assertPatches=True) |
| 6 Six-card guide | done | guide-v13.txt |
| 7 Four languages and layout | done | strings-v13.txt; tests-v13.json |
| 8 SAM object picker | blocked (model-runtime dispatch exceeds frame budget); release flag false | sam-performance-v13.txt; pick-v13.txt; pick-cancel-v13.txt; release-config-v13.txt |
| 9 Diagnostic probes | done; device outputs unverified | probes-v13.txt; build-v13.json |
| 10 Evaluation tooling | done | python-tests-v13.txt; evaluation-v13.md |
| 11 Android build and regression checks | done | compile-v13.json; tests-v13.json; build-v13.json; apk-v13.json; android-metadata-v13.txt; android-json-metadata-v13.txt; desktop/spatial/single-hand-v13.txt |
| 12 Documentation and git delivery | done at release publication below | README.md; ../README.md; DESIGN.md; this log; publication record |
| 13 Physical headset acceptance | unverified (device) | device-v13.txt; ../README.md → Device acceptance run |

Measured Editor results: **0.611 ms average diff main-thread time**, maxima **2,048 rays and 2,048 overlaps per frame** over the final 180-frame sample. All three independent fixture edits found: **2 Missing + 1 New + 1 Changed**, plus **1 other** visible candidate. Paired Changed members are hidden and the New member lies within 0.5 m of its planted target. These are fixture results, not surveyed real-world precision. The saved evaluation example uses one confirmed annotation and must not be read as the engine's overall recall.

Strings: **en 0, de 0, fr 0, it 0** overflows; each language covers 21 page/card states, with zero missing keys and zero missing-glyph warnings. English/German terminology is agent-reviewed against the brief glossary; French/Italian are machine-translated and unreviewed. All four unchanged synthetic XRI workflows pass (left/right hand, left/right controller), as do desktop and spatial regressions. The final background-list-refresh regression passes.

SAM functional prototype: column **468.7 ms**, wall **659.0 ms** warm total time; areas **0.5413 / 4.2603 m²**, normal errors **0 / 5.42°**. Cold total **33,127.6 ms**, including a **30,070.55 ms** single scheduling stall; warm encoder scheduling peaks **4.88 / 23.77 ms**, decoder **10.15 / 38.23 ms**. These violate the 4 ms scheduling target, so `objectPickEnabled=false` is the release default and the button is hidden. Sampled Unity allocations exclude GPU-driver allocations; neither the 400 MB memory target nor device latency is verified.

Stage commits: 1 `3e7bce1`; 2 `e4c659e`; 3 `9671119`; 4 `13129e2`; 5 `4ecc386`; 6 `0adb34f`; 7 `ee2c24d`; 8 `cea8868`; 9 `bf5818d`. Release tag: `v1.3.0`. `gh` is not installed, so the authorized local-merge publication path was used; no pull request was created.

Final `adb devices -l` output:

```text
List of devices attached
```

Open [HUMAN] items, in execution order:

1. **Device acceptance v1.3:** connect/authorize Samsung Galaxy XR, record device model/runtime/build ID/date, install with `adb install -r`, transfer Region A and optionally the external models, launch and grant camera, scene-understanding and hand-tracking permissions. Complete/reopen the guide, inspect German Mode/List/strip labels, then switch back. See the project README for exact install/push/pull commands.
2. Align with a surveyed marker or manually and save. Run Depth probe and Camera probe, pull logs and paste the actual descriptors into this log. Verify physical hands/controllers, near poke, both-eye rendering and anchor relocalization; synthetic inputs do not establish these.
3. Walk the hall for two minutes in Overlay with Diff on. Record whether `Diff chunks=` shows a scene mesh, first-candidate latency, per-kind counts, false candidates, FPS/CPU/GPU and draw calls. The 72 FPS minimum / 90 FPS target and 300 draw calls remain targets.
4. Confirm/retype a known physical change, restart to check its region, dismiss/recompute another, export/pull and inspect JSON/CSV/OBJ on the laptop. Replace the Editor fixture ground truth with surveyed physical changes for evaluation. Timestamp intervals are a proxy, not actual task duration.
5. **Optional SAM evaluation, after the ordinary flow passes:** use an explicitly enabled evaluation build and transfer hash-validated models, select a column and measure cold/warm latency, memory and cancellation. Transferring models alone does not enable the release flag. Do not enable it by default until device scheduling and memory gates pass.
6. Record failures as well as successes under “Device acceptance v1.3”. Human French/Italian language review is still open; those translations remain marked unreviewed.

Implementation decisions and reasons:

- Conservative unloaded-tile coverage exclusion treats ambiguous streamed boundaries as unknown and prevents false New patches without reading fixture labels.
- Optional `additional_triangles` preserves the primary region reference while representing clusters crossing tile boundaries.
- The moved object's own endpoints are exempt from the 3 m independent-edit separation because the required pairing radius is 1 m.
- SAM uses bilinear low-resolution decoder-mask sampling, actual accepted back-projection samples, a 0.5 m discontinuity limit and a 30° normal-coherence guard; this avoids coarse-triangle overreach and yields a conservative visible surface patch rather than guaranteed full-object coverage.
- URP's standalone SingleCameraRequest path replaces the unsupported built-in stereoTargetEye setter, retaining the twin-only colour capture without the obsolete-API warning.
- The package/model runtime fails the frame budget, so the brief's bounded optional-feature fallback is used: code/models retained, SAM disabled, geometric comparison enabled.
- Background diff refreshes wait for idle UI input and preserve scroll position because rebuilding a live ScrollRect interrupts real interaction.
- Deployment is standalone local processing after installation/data transfer; the workstation prepares data and retrieves exports. Continuous PC or server inference is not part of the application architecture, and hardware support/performance remains subject to the above acceptance run.

### Publication record

Stage 9 commit **bf5818d1ce6be3769162b3124ef36220a389b9bd** was pushed to `origin/v1.3`, fast-forwarded into `main`, and published with annotated tag **v1.3.0**. The remote tag object is `7c903982b7d7a850fd482de1d91fbb31ceda5f74`; its peeled commit and `origin/main` both resolved to the Stage 9 commit immediately after publication. GitHub accepted the 73.63 MiB APK with its standard advisory about files above 50 MiB; it is below the project's 95 MB limit. PR: none (`gh` unavailable; local-merge route authorized).

Quoted `git log origin/main --oneline -3`, captured immediately after the release push and before this documentation-only audit commit:

```text
bf5818d v1.3 Stage 9 — verified Android release, interaction regression and acceptance docs
cea8868 v1.3 Stage 8 — bounded depth and Camera2 metadata probes
ee2c24d v1.3 Stage 7 — validated MobileSAM prototype with guarded release fallback
```

This follow-up records the publication proof without changing the tagged application or APK. The final build's Unity-authored preload entries reference the existing XR General Settings and OpenXR Package Settings assets; they are retained. No model asset is preloaded or bundled.

### First Galaxy XR test — 2026-10-24 (Europe/Paris)

The user connected a Samsung Galaxy XR (SM-I610, Android 14 / API 34, build I610UEU2AZF3) and chose to test display and operation in a different room from the scanned hall. The verified release APK updated the existing installation from 0.1.0 / code 1 to 1.3.0 / code 3, preserving application data. Camera, scene-understanding and hand-tracking permissions were already granted. The existing annotations and alignment directories were empty when backed up.

The initial Region A transfer failed with ADB `secure_mkdirs: Operation not permitted` while creating nested tile directories. Explicit `adb shell mkdir -p` succeeded and a single-file transfer verified the workaround. `tools/push_twin.ps1` now creates all tile directories through the shell before the existing sync transfer. The retry transferred 134 files and skipped the already tested file. All 135 device GLBs match local SHA-256 values; the active manifest also matches the locally staged manifest. Versioned paths and manifest-last activation remain intact.

After restart, the app logged `Manifest A: 45 tiles, demo=False` and `Ready: Region A · external data`. The user confirmed seeing the Guide or numbered 00–05 menu. This establishes startup and visible UI only; hand interaction, both-eye correctness, annotation persistence and exports still require observation. The user was directed to Twin and Colour for the next step. No runtime code or APK changed in this test.

The next user report confirms the hall is visible and pinch clicks work after following the Twin / Colour instructions. A Note creation/list check was then requested. ADB disconnected before its saved file could be retrieved; the last device listing was empty. This does not establish whether the user unplugged USB or another connection issue occurred, nor does it yet establish continued untethered operation. An earlier local log contains an export completion at 08:33:40 (`patches=0`), but the exported files have not been retrieved or validated.

Five initial samples between 08:31:28 and 08:32:09 local time report 43.7, 46.0, 39.9, 38.0 and 40.1 FPS, with reported GPU times 33.88–47.36 ms and 15–32 resident tiles. These are initial operation/loading samples, not a controlled steady-view benchmark, and do not meet the 72 FPS minimum. Refresh requests repeatedly alternate between 72 and 90 Hz as pending loads change; requested refresh is not measured FPS. `Diff chunks=0` in this interval provides no evidence of physical scene-mesh comparison. The unrelated room also prevents a valid new/old-scene accuracy evaluation. SAM remains disabled and external models were not transferred.

Evidence: `docs/evidence/device-first-run-v13.txt`; full session logs, empty-save backups and per-file device hashes are retained locally under `work/device-tests/2026-10-24-v13-082718`. Earlier release-time statements that no device was connected describe that release snapshot, not this subsequent test.

## v1.4 — inspection spaces, Setup entry and Chinese — 2026-10-24 (Europe/Paris)

Developed on macOS with Unity 6000.3.26f1 (Hongrui Wu).

- **Spaces.** `StreamingAssets/twins/spaces.json` lists the inspectable spaces; the former `twin_demo` is now `twins/HG_MainHall`. One id names every copy of a space's data: `twins/<id>` (packaged LOD2), `work/twin/<id>` (pipeline output), `files/twin/<id>` on the headset, and its annotation, alignment and dismissal files. The app asks for a space after tracking is ready; Skip opens `Settings.defaultSpace`. Spaces cut from one scan keep separate storage and are aligned separately.
- **New spaces.** `CAB_MainHall` comes from the cropped `entrance_hall.ply` of CAB `navvis_2022-06-25_11.14.36` (8 tiles, LOD0 295,462 triangles, `datum_z` −4.8 m from the dominant horizontal surface). `HG_E3_Hall` (`--region 16,69,-33,4,-1,8`) and `HG_E7_Hall` (`--region 16,69,16,52,-1,8`) come from HG `navvis_2022-02-06_12.55.11` (28 tiles each; LOD0 861,418 and 679,807 triangles). All passed `verify_seams.py` and `verify_tiles.py`. Cropped meshes have many open boundaries, so pinned borders keep LOD1/LOD2 close to LOD0.
- **APK budget.** HG and CAB are packaged as LOD2 (the dev build measured 89.5 MB). E3/E7 LOD2 would add about 20 MB and exceed the 95 MB limit, so they are transferred with the new cross-platform `tools/push_space.py` and appear greyed out until then. Full data for all four spaces is attached to release `data-2026-10-24`.
- **Pipeline.** `twin_pipeline.py` gained `--mesh`, `--region auto`, `--region-name`, `--origin auto`, `--building` and `--datum-z`; defaults keep existing HG outputs' fingerprints. `package_demo.py` handles any building; `verify_apk.py` checks every packaged space.
- **Setup entry.** A gear cell left of the four tools opens 00 Setup from any view: Space (switch after confirmation: per-space services are torn down and rebuilt), Preferences (Language), Guide, Close. Setup no longer repeats Manual alignment and View modes.
- **Languages.** First run is English (no longer the system language); 中文 joins en/de/fr/it. Chinese is machine translated and unreviewed. Glyphs come from Noto Sans SC subsets (`tools/subset_cjk_font.py`, OFL) added at runtime as fallbacks of the Inter fonts and removed on quit.
- **Editor.** `Settings.editorDataPath` is now `work/twin` relative to the repository (was a workstation `E:` path); the Editor loads `work/twin/<id>` when present. SAM models moved to `files/models` on the device.

Device evidence (Galaxy XR SM-I610, headset clock set to 2026-06-12): CAB Main Hall loaded its 8 packaged tiles and mostly ran at 90 FPS; HG Main Hall loaded full Region A (45 tiles) after the backed-up data was restored; switching HG Main Hall → HG E3 Hall tore down and restarted services but exposed a NullReferenceException when an unpackaged space looked for packaged markers. After the fix, HG E3 Hall loaded its transferred data. The rebuild after the first headset test required uninstalling (different signing key); app data was backed up and restored, and the one CAB annotation was migrated to `CAB_MainHall.json`.

Not verified for v1.4: Unity EditMode tests (not run; new `SpacesTests` and string tests compile), HG E7 Hall and Chinese rendering on the device (reported working by the user, no log evidence), repeated space switching, and APK verification of the final 1.4.0 build.
