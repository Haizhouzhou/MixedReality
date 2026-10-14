# One-shot brief: build **TwinInspector v1** for Samsung Galaxy XR

You are an autonomous senior engineer with Unity, XR, computer-vision and Python experience. You work on this Windows machine with shell access. Your job is to deliver the first runnable version of **TwinInspector**, a mixed-reality app for the **Samsung Galaxy XR** headset (Android XR). The app shows a LiDAR digital twin of a building floor on top of the real environment, lets the user align it, compare it with reality, and annotate where the twin no longer matches reality.

Work end to end without waiting for a human, except for the two device-only steps marked **[HUMAN]**. Verify every step. Report faithfully. Do not invent results.

---

## 0. Definition of done

v1 is done when all of the following are true:

1. `tools/twin_pipeline.py` turns the NavVis mesh of scan `navvis_2022-02-06_12.55.11` into chunked, simplified, LOD'd GLB tiles plus a manifest, for the **main hall region first** and then for the **whole indoor floor**, within the budgets in §5.
2. The Unity project `E:\MixedReality\TwinInspector` builds an Android APK with `unity build` (or the Pipeline build command) with zero compile errors and zero OpenXR validation issues.
3. On the headset the app: starts, asks for the required permissions, shows passthrough, loads the twin tiles, and runs at **≥ 72 fps (target 90)** while the hall tiles are loaded.
4. The user can align the twin with (a) a printed marker/QR code, (b) manual two-hand adjustment, and the alignment persists across app restarts (persistent anchor + JSON fallback).
5. Three view modes work and switch instantly: **Passthrough only**, **Twin only**, **Overlay** with a blend slider (0–100 %).
6. The user can create, select, retype, delete and export annotations (types: *Missing*, *New*, *Changed*, *Note*). Annotations are stored in twin coordinates, survive restarts, and export as JSON.
7. The whole UI follows the **Swiss design system in §8**. No default Unity UI skin, no "AI-generated" look (see §8.6).
8. The app also runs in the Unity Editor with the XRI **XR Device Simulator** (mouse/keyboard) so it can be tested without the headset.
9. `README.md` in the Unity project explains setup, data push, build, install and use. `docs/DEVLOG.md` lists what was done, what was verified on device, and what is open.
10. All work is committed to git on `main` in `E:\MixedReality` with clear messages. `dataset/` and Unity `Library/` are never committed.

---

## 1. Project context

- Course project "Inspection and Refinement of Mesh Digital Twins" (ETH Zurich Mixed Reality course, 13 weeks, 4 students). Proposal: `E:\MixedReality\doc\MR_Proposal_Inspection and Refinement of Mesh Digital Twins.tex`. Read it first.
- Work packages: WP1 twin reconstruction and XR-friendly representation; WP2 registration between twin and headset; WP3 MR visualization (passthrough / twin-only / overlay); WP4 discrepancy annotation. **v1 = WP1-lite (decimation only, no neural reconstruction) + WP2-v1 (marker + manual + persistent anchors, no automatic registration) + WP3 full + WP4 core.**
- Dataset: **LaMAR** (ETH Zurich, CC BY-SA 4.0). Scene **HGE** (ETH main building, floor E). Ground-truth poses in LaMAR were produced with SuperPoint/SuperGlue. Credit LaMAR in the README.
- Decisions already taken (do not re-open): Unity 6.3 LTS, URP, OpenXR + Unity "OpenXR: Android XR" package + Google "Android XR Extensions", runtime glTF loading with glTFast, marker-based initial alignment instead of GPS (the headset has no GNSS), vertex-coloured decimated mesh for v1 (texture baking is a stretch goal).

---

## 2. Machine and tool environment (exact, verified)

| Item | Value |
|---|---|
| OS | Windows 11 Pro 10.0.26200, PowerShell 7 and Git Bash available, WSL2 Ubuntu 24.04 available (GPU visible in WSL) |
| CPU / RAM | Intel i5-9300H (4 cores), **16 GB RAM** → never load a full NavVis mesh into memory |
| GPU | NVIDIA GTX 1650, **4 GB VRAM**, driver 532. **Do not** try NKSR, fVDB or other CUDA reconstruction (needs Ampere+). CPU-only mesh processing. |
| Unity Editor | `E:\Unity\Editor\6000.3.26f1\Editor\Unity.exe` (Unity 6.3 LTS, Student licence, modules: Android, Android SDK & NDK, OpenJDK) |
| adb | `E:\Unity\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` |
| Unity CLI | `C:\Users\zackz\AppData\Local\Unity\bin\unity.exe` (v1.0.0-beta.13, signed in). It is on the user PATH; if your shell does not see it, call it by full path. |
| Python | conda env **`mr`**: `D:\miniforge3\envs\mr\python.exe` (Python 3.11) with `numpy scipy pillow matplotlib trimesh fast-simplification` installed via pip. Install more with `D:\miniforge3\envs\mr\python.exe -m pip install <pkg>`. Run scripts with `-I`. |
| Git | repo root `E:\MixedReality`, branch `main`, no remote yet. Root `.gitignore` contains `dataset/`. The Unity project has the standard Unity `.gitignore`. |
| Downloads | A background script is still downloading HoloLens raw sessions into `dataset/raw/HGE/sessions/hl_*`. **Do not stop it, do not read or modify `dataset/raw/HGE/sessions/hl_*`, do not delete anything under `dataset/`.** |

---

## 3. Repository and data layout (exact)

```
E:\MixedReality\
├─ .gitignore                       # dataset/
├─ doc\                             # proposal (.tex), this brief
├─ tools\mesh_overview.py           # existing: memmap PLY reader + floor histogram + top-down maps (reuse its read_ply_layout / read_alignment)
├─ work\overview\                   # floor_*.png top-down maps, floor_E_regions.png (region boxes drawn)
├─ TwinInspector\                   # Unity project (see §4)
└─ dataset\
   ├─ INFO, LICENSE
   ├─ benchmark\{CAB,HGE,LIN}\sessions\{map,query_*}\   # LaMAR benchmark (images, poses, wifi/bt) — useful later for WP2 evaluation
   ├─ colmap\HGE\                                        # COLMAP models of HGE
   └─ raw\HGE\
      ├─ metadata_hololens.json, metadata_phone.json
      └─ sessions\
         ├─ navvis_2021-02-18_10.02.08\   navvis_2021-02-18_11.33.08\   navvis_2021-05-10_19.34.30\
         ├─ navvis_2022-02-06_12.55.11\   ← USE THIS ONE (newest)
         ├─ navvis_2022-02-26_16.21.10\
         ├─ ios_*\  (phone sessions, complete)
         └─ hl_*\   (HoloLens sessions, still downloading — do not touch)
```

Each `navvis_*` session contains:

```
proc/meshes/mesh.ply              # binary_little_endian PLY "Created by Open3D": vertex {double x,y,z; uchar red,green,blue}, face {uchar count; uint idx[3]}
proc/alignment_global.txt         # "# label, reference_id, qw, qx, qy, qz, tx, ty, tz, [info]+"; use the row labelled pose_graph_optimized; p_world = R(q) * p + t
raw_data/pointcloud.ply           # 1 cm voxel point cloud (4.9 GB)
raw_data/images_undistr_center/   # ~3000 undistorted JPEGs (cam0..cam3)
trajectories.txt sensors.txt images.txt pointclouds.txt wifi.txt bt.txt   # LaMAR Capture format
```

Mesh sizes (do **not** load whole files; use `numpy.memmap` with the header offset):

| Scan | Vertices | Triangles | mesh.ply |
|---|---|---|---|
| navvis_2022-02-06_12.55.11 | 181,399,662 | 384,836,269 | 9.0 GB |
| navvis_2021-02-18_10.02.08 | 158,793,194 | 337,935,451 | 8.7 GB |

Coordinate facts (already measured with `tools/mesh_overview.py`, all 5 scans aligned with `alignment_global.txt`):
- Frame: metres, right-handed, **Z up**. Floor of level E at **z ≈ 0.0 m**; ceilings up to ~7 m; a few points at z ≈ −2.5 m (outdoor steps) — exclude.
- **Region A — main hall + rotunda (do first):** x ∈ [−20, 70], y ∈ [−5, 32], z ∈ [−1.0, 8.0].
- **Region B — whole indoor floor (do second):** x ∈ [−40, 90], y ∈ [−80, 60], z ∈ [−1.0, 8.0]. Outside B is street/terrace — drop.
- See `work/overview/floor_E_regions.png` (red = A, blue = B).

---

## 4. Unity project state (exact, verified — do not redo)

- Path `E:\MixedReality\TwinInspector`, template **Universal 3D (URP)**, Unity 6000.3.26f1, active build target **Android**.
- Packages (`Packages/manifest.json`, all resolved, no compile errors expected after the API-level fix below):
  - `com.unity.xr.androidxr-openxr` 1.4.1 (Unity OpenXR: Android XR)
  - `com.google.xr.extensions` 1.4.0 — local tarball `Packages/com.google.xr.extensions.tgz` (Android XR Extensions for Unity: marker/QR tracking, passthrough composition layer, unbounded reference space, scene meshing, samples)
  - `com.unity.xr.openxr` 1.18.0, `com.unity.xr.arfoundation` 6.6.2, `com.unity.xr.hands` 1.9.0, `com.unity.xr.interaction.toolkit` 3.6.1, `com.unity.xr.management` 4.6.1, `com.unity.xr.compositionlayers` 2.6.0, `com.unity.cloud.gltfast` 6.20.0, `com.unity.render-pipelines.universal` 17.3.0, `com.unity.inputsystem` 1.20.0, `com.unity.ugui` 2.0.0 (includes TextMeshPro), `com.unity.pipeline` 0.8.0-exp.1 (lets the CLI drive the Editor).
- XR Plug-in Management: Android → **OpenXR** loader, initialise on start.
- OpenXR (Android): render mode Single Pass Instanced (multiview). Enabled: feature set **Android XR** (Session, Camera/Passthrough, Planes, Anchors, Raycasts, Meshing, Occlusion, Display Utilities, Performance Metrics, Android XR Support), **Hand Tracking Subsystem**, **Hand Interaction Profile**, **Hand Interaction Poses**, **Palm Pose**, **Khronos Simple Controller Profile**, **Foveated Rendering**, **Composition Layers Support**. Disabled on purpose: Faces, Bounding Boxes. Extensions features enabled: **Session Management, Image Tracking (Marker), Image Tracking (QR Code), Passthrough Composition Layer, Unbounded Reference Space** (ids `com.google.xr.extensions.{session_management,marker_tracking,qrcode_tracking,passthrough_composition_layer,unboundedrefspace}`). Extensions Scene Meshing / Object Tracking / Body / Face / Geospatial / Google Cloud / Recommended Settings stay off.
- Player (Android): Graphics API **Vulkan only**; IL2CPP; ARM64; Application entry **GameActivity**; Resizeable Activity on; minSdk 25; API Compatibility Level = .NET Framework (`apiCompatibilityLevel: 3`; harmless, keep it).
- **Compile fix already applied (keep it):** `com.google.xr.extensions` 1.4.0 uses `System.Runtime.CompilerServices.Unsafe.As<,>()`; Unity Collections ≥ 2.6.8 (pulled in by glTFast 6.20) no longer bundles that assembly, so the package fails to compile out of the box (github.com/android/android-xr-unity-package/issues/25). The fix is `Assets/Plugins/System.Runtime.CompilerServices.Unsafe/System.Runtime.CompilerServices.Unsafe.dll` (v6.0.0, copied from the Unity install, see the README next to it). Do not remove it. If an APK build reports a duplicate `System.Runtime.CompilerServices.Unsafe` assembly, restrict that plugin's platform settings (Editor + Android as needed) rather than deleting it. Do not downgrade Collections and do not upgrade glTFast/Collections without re-checking this.
- OpenXR Project Validation for Android: 0 issues at hand-over. Re-run it after every settings change.
- Known harmless dialog: "assets located in immutable packages were unexpectedly altered … com.unity.xr.androidxr-openxr/Assets/URP/UniversalRenderPipelineAsset.asset" → click OK / ignore.
- Known constraint: when using Image/Marker/QR tracking, keep **Vulkan Subsampling off** (Android XR Extensions release notes).

---

## 5. Deliverable A — mesh pipeline (`E:\MixedReality\tools\twin_pipeline.py`)

### 5.1 Requirements
- Python 3.11, env `mr`. Add dependencies with pip if needed (`pygltflib` or trimesh's GLB export, `tqdm`). No CUDA.
- Streaming only: read vertices and faces through `np.memmap` in batches (e.g. 20 M faces per batch). Peak RAM ≤ 8 GB. Make every stage resumable and idempotent (skip outputs that exist unless `--force`).
- Sub-commands (argparse): `chunk`, `simplify`, `export`, `manifest`, `preview`, `all`. Parameters: `--session <path>`, `--region a|b|<xmin,xmax,ymin,ymax,zmin,zmax>`, `--chunk 10` (metres), `--out <dir>`, `--target-ratio`, `--lods 3`.

### 5.2 Stages
1. **chunk** — Apply `alignment_global.txt` (pose_graph_optimized) to vertices so all output is in the common world frame. Keep only faces whose centroid lies in the region box. Assign each face to a 10 m × 10 m × (full height) grid cell by centroid. Write per-chunk compact binary files (NPZ: float32 positions, uint8 colours, uint32 faces, re-indexed per chunk). Record per-chunk triangle counts and bounds.
2. **simplify** — Per chunk, quadric decimation with `fast_simplification` (keeps colours via index map; if colours are lost, re-sample colour from the nearest original vertex with a KD-tree on a subsample). Targets: LOD0 ≈ **1.3 %** of the original triangles (whole floor ≈ 5 M tris), LOD1 = 25 % of LOD0, LOD2 = 6 % of LOD0. Remove degenerate/duplicate faces and unreferenced vertices. Weld vertices (1 mm) before decimation.
3. **export** — One GLB per chunk per LOD: `tiles/<cx>_<cy>/lod0.glb`, `lod1.glb`, `lod2.glb`. Store vertex colours as `COLOR_0` (normalized unsigned byte). Positions in metres. **Axis convention:** glTF is Y-up right-handed; the twin frame is Z-up. Convert with a +90°-about-X rotation (x, y, z)_twin → (x, z, −y)_gltf … choose one convention, **document it in the manifest**, and make the Unity loader apply the inverse so that twin coordinates round-trip exactly (write a unit test in Python and in C#). glTFast converts glTF right-handed to Unity left-handed by itself — account for that once, verify with a known asymmetric landmark (e.g. the rotunda is west = −x).
4. **manifest** — `manifest.json` (schema in Appendix A): scan id, source file hashes, region, chunk size, the world-origin offset used (shift all tiles so coordinates stay small — keep `origin_world` in the manifest), tile list with bounds, triangle counts per LOD, file sizes, and the axis convention string.
5. **preview** — Top-down PNG of each LOD level (matplotlib) for visual QA; and a textual report (`report.txt`) with totals.

### 5.3 Budgets and checks
- Region A total LOD0 ≤ **1.5 M** triangles, ≤ 150 MB of GLB. Region B total LOD0 ≤ **5 M** triangles, ≤ 450 MB. Any single tile ≤ 30 MB.
- `tools/verify_tiles.py`: loads every GLB with trimesh, checks counts/bounds against the manifest, checks that COLOR_0 exists, prints a table. Must pass before the Unity work starts.
- Output directory: `E:\MixedReality\work\twin\HGE_2022-02-06\` (not inside the APK). Also copy **only the Region A LOD2 tiles** into `TwinInspector/Assets/StreamingAssets/twin_demo/` as a tiny built-in fallback (≤ 15 MB total) so the app shows something even before data is pushed.
- Provide `tools/push_twin.ps1`: `adb push` the output folder to `/sdcard/Android/data/<applicationId>/files/twin/HGE_2022-02-06/` (the app's `Application.persistentDataPath`).

---

## 6. Deliverable B — the Unity app

### 6.1 Driving Unity (use the CLI; do not hand-edit scene/prefab/asset YAML while the Editor is open)
```bash
export UNITY_NO_BANNER=1 UNITY_NON_INTERACTIVE=1 UNITY_NO_PAGER=1
U="C:/Users/zackz/AppData/Local/Unity/bin/unity.exe"; P="E:\\MixedReality\\TwinInspector"
"$U" open "$P"                                              # start the Editor
"$U" status --until-ready --timeout 600 --project-path "$P" --format json   # wait for state "ready"
"$U" command --project-path "$P"                            # list command tags; --tag <tag> lists commands; --query <text> searches
"$U" command console_status --project-path "$P" --result-only            # compiling? compilationFailed?
"$U" command recompile_status --project-path "$P" --result-only          # compile errors with file:line
"$U" command console --level error --tail 30 --project-path "$P" --result-only
"$U" command eval_file --file <path.cs> --project-path "$P" --result-only --timeout 120
"$U" command package_add --identifier <name@version|file:...> --confirm true --wait true --project-path "$P"
"$U" build --help ; "$U" command --tag build --project-path "$P"         # choose one way to build the APK
"$U" close "$P" [--force]
```
Rules learned on this machine:
- `eval` / `eval_file` code is compiled **inside a method body**: **no `using` directives**, use fully qualified names (`UnityEditor.PlayerSettings…`, `UnityEngine.XR.OpenXR.OpenXRSettings…`), end with `return <string>;`. `OpenXRFeatureAttribute` lives in `UnityEditor.XR.OpenXR.Features`.
- Right after a domain reload / package change the first request can fail with *"Main thread operation timed out after 5000ms"*. Poll `console_status` until `compiling=false`, then retry. If `compilationFailed=true`, read `recompile_status`, fix the C#, and if the Editor is wedged: `unity close --force`, fix, `unity open`.
- Prefer Pipeline commands (`create_scene`, `create_gameobject`, `add_component`, `set_serialized_field`, `save_scene`, `add_scene_to_build`, `set_player_settings`, `switch_build_target`) and `eval_file` with Editor APIs for everything else (importing package samples: `UnityEditor.PackageManager.UI.Sample.FindByPackage(name, version)` → `Import()`; OpenXR features: `OpenXRSettings.GetSettingsForBuildTargetGroup(Android).GetFeatures()`; validation: `UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(list, Android)`).
- Write C# scripts as files under `Assets/_TwinInspector/Scripts/…` with the Write tool; then `unity recompile` / `recompile_status` to confirm zero errors before touching scenes.
- Never commit `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, `Builds/`.

### 6.2 One-time project steps (verify each)
1. Verify (already done at hand-over) that the Extensions features **Session Management, Marker Tracking, QR Code Tracking, Passthrough Composition Layer, Unbounded Reference Space** are enabled for Android (`OpenXRSettings.GetSettingsForBuildTargetGroup(Android).GetFeatures()`, types in namespace `Google.XR.Extensions`). Do **not** enable the Extensions' own Scene Meshing duplicate — Unity's Android XR Meshing feature already provides scene meshes (the Extensions changelog notes an `ARMeshManager` compatibility fix for the duplicate). Re-run OpenXR validation → must stay at 0 issues.
2. Import samples: XRI **Starter Assets** and **Hands Interaction Demo** (rig prefabs, XR Device Simulator, hand visuals), XR Hands **HandVisualizer**, Extensions **Image Tracking** and **Composition Layer Passthrough** (read their README and scripts, reuse patterns, do not ship sample scenes).
3. Player settings: `companyName = "MR Twin Team"`, `productName = "TwinInspector"`, `applicationIdentifier = "com.mrtwin.inspector"` (Android), version `0.1.0`, Portrait/landscape irrelevant (XR). Keep Vulkan-only, IL2CPP, ARM64, GameActivity, .NET Framework. Target SDK: automatic (highest installed). Disable Vulkan Subsampling if the Android XR Session settings expose it.
4. URP: one URP asset for Android XR: **Forward+**, HDR off, MSAA 4x, render scale 1.0, Depth Texture **off**, Opaque Texture **off** (Google guidance: avoid GPU copies), shadows off, SSAO off, **GPU Resident Drawer = Instanced Drawing**, **GPU Occlusion Culling on** (Render Graph on), Foveated rendering via the OpenXR feature. Keep Graphics "Batch Renderer Group Variants = Keep All".
5. Scenes: `Assets/_TwinInspector/Scenes/Inspector.unity` (the only scene in the build).

### 6.3 Runtime architecture (C#, namespace `TwinInspector`)
Folder `Assets/_TwinInspector/` with `Scripts/{Core,Twin,Alignment,Annotation,UI,Diagnostics}`, `Prefabs/`, `Materials/`, `Shaders/`, `Fonts/`, `Resources/DesignTokens.asset`, `Tests/`.

- **Core**
  - `AppBootstrap` — order: request permissions (use `Google.XR.Extensions.AndroidXRPermissionUtil` or `UnityEngine.Android.Permission` for CAMERA plus the scene-understanding / hand-tracking permissions the enabled features require — read the Unity Android XR package docs and the Extensions README for the exact strings; the Unity package also injects manifest entries) → wait for `ARSession` state Ready → enable passthrough (ARCameraManager on, camera background transparent, clear colour alpha 0) → load `DesignTokens` → load manifest → restore alignment → restore annotations → show the Setup panel.
  - `TwinPaths` — resolves `Application.persistentDataPath/twin/<scan>/manifest.json`, falls back to `StreamingAssets/twin_demo/`. On Android read StreamingAssets with `UnityWebRequest`.
  - `Settings` (ScriptableObject): LOD distances (LOD0 < 12 m, LOD1 < 30 m, LOD2 < 80 m, unload beyond), max concurrently loaded triangles (1.2 M), tile load concurrency (2), target frame rate (90, fallback 72 via Display Utilities).
- **Twin**
  - `TwinManifest` (JSON DTOs) and `TwinLoader`: async tile loading with glTFast (`GltfImport.Load` + `InstantiateMainSceneAsync`), per-tile `TileHandle` with LOD switching by distance from the camera to the tile bounds, frustum-aware prioritisation, cancellation, memory budget, `Mesh.MarkNoLongerReadable`. Tiles are children of `TwinRoot`. Static batching is not possible for runtime meshes — rely on GPU Resident Drawer and keep one material per style.
  - `TwinRoot` — single transform = alignment; exposes `TwinToWorld`/`WorldToTwin`.
  - `TwinStyle` + shaders (URP, Shader Graph or HLSL, SPI/multiview-safe): **(a) "Colour"** unlit vertex colour with global alpha; **(b) "Technical"** flat ink tint with screen-space edge darkening (derivative-based) and optional wireframe-ish edge overlay — this makes decimated geometry look intentional. Overlay alpha and style switch are global material properties (`Shader.SetGlobalFloat`), not per-renderer loops.
- **Alignment**
  - `AlignmentState` {scan id, twinRoot pose in world, anchor id, timestamp} serialised to `persistentDataPath/alignment/<scan>.json`.
  - `MarkerAlignment`: `ARTrackedImageManager` with the Extensions marker/QR feature. `markers.json` (Appendix B) lists marker ids with their **pose in twin coordinates** (position + yaw, measured by the team). On first stable detection (tracking state Tracking, ≥ 1 s), compute `TwinRoot = T_marker_world · T_marker_twin⁻¹`, keep roll/pitch gravity-aligned (yaw-only correction) and show a confirm panel ("Aligned with marker 3 · residual n/a").
  - `ManualAlignment`: two-hand grab on the twin (XRI `XRGrabInteractable` on a large invisible proxy, or hand-pose delta): translate XY-plane + yaw; height locked unless the "Height" toggle is on; nudge buttons ±1 cm / ±5 cm / ±0.5° / ±2°; "Snap floor" (sets twin floor z to the detected floor plane).
  - `AnchorPersistence`: after alignment, create an `ARAnchor` at `TwinRoot` and persist it with the AR Foundation persistent-anchor API of the Android XR package; on start, try to load the saved anchor id and re-apply; fall back to the JSON pose. Log which path was used.
- **Annotation**
  - `Annotation` {id, type (Missing/New/Changed/Note), position_twin, normal_twin, created_utc, label (short preset), device_pose_twin at creation, screenshot file (optional)} — JSON list at `persistentDataPath/annotations/<scan>.json` (Appendix C).
  - `AnnotationPlacer`: XRI ray from the dominant hand (pinch) or controller trigger; raycast against tile `MeshCollider`s (generate colliders only for LOD2 to stay cheap) and against the twin surface; place a marker; open the type picker.
  - `AnnotationMarker` prefab (visual per §8.5), `AnnotationListPanel` (scrollable list, select → highlight + "look here" arrow, delete), `AnnotationExporter` (writes JSON + a CSV; `adb pull` instructions in README).
- **UI** (UGUI + TextMeshPro, XRI UI input): `SetupPanel` (data status, alignment mode, marker status), `ToolStrip` (hand-anchored: Mode, Align, Annotate, List), `ViewModePanel` (3 segmented buttons + blend slider + style toggle), `AlignPanel`, `AnnotationTypePicker`, `Toast` (single line, bottom of the active panel). All panels instantiate from one `PanelBase` prefab and use `DesignTokens`. No panel is head-locked; panels are world-locked at spawn (1.0 m in front, eye height −10 cm, facing the user) and can be re-summoned with the palm-up gesture / menu button.
- **Diagnostics**: `PerfHud` (dev toggle: fps, GPU/CPU ms from the Android XR performance metrics feature, loaded tiles, triangles), `DevLog` to `persistentDataPath/logs/`. Hidden in release unless enabled from the Setup panel.

### 6.4 Interaction map
| Action | Hands | Controller |
|---|---|---|
| Point / select | ray from index-thumb pinch (XRI hand interaction) | ray + trigger |
| Summon tool strip | left palm up (XRI "palm menu" pattern) | menu button |
| Place annotation | pinch on surface while in Annotate mode | trigger |
| Grab twin (Align mode) | two-hand pinch-grab | grip |
| Blend | slider on ViewModePanel (ray drag) | same |

### 6.5 Editor simulation
Keep a scene variant / toggle so the app runs in the Editor with the **XR Device Simulator**: passthrough is replaced by a neutral grey background, markers can be simulated with a keyboard shortcut that applies `markers.json` entry 0, tiles load from `E:\MixedReality\work\twin\...` via a dev path override. Use this to test everything that does not need the real cameras.

### 6.6 Build, install, run
- Build an APK: `TwinInspector/Builds/TwinInspector-<version>.apk`. Prefer `unity build` with the Android target; if the CLI's build needs a Build Profile, create `Assets/Settings/Build Profiles/AndroidXR.asset` with the Pipeline/Editor API.
- **[HUMAN]** Headset developer mode: Settings → About headset → Software information → tap *Build number* 7× → Developer options → USB debugging on; connect USB-C; accept the RSA prompt. Then `adb devices` must list the device.
- `adb install -r <apk>`; `adb shell am start -n com.mrtwin.inspector/com.unity3d.player.UnityPlayerGameActivity`; `adb logcat -s Unity` for logs; `tools/push_twin.ps1` to push tiles.
- **[HUMAN]** On device: grant the permission prompts, print `tools/markers/marker_00.png` (A4, ArUco 4×4_50 id 0, 15 cm) or the QR code, place it at the location recorded in `markers.json`, look at it.
- Record on-device results in `docs/DEVLOG.md` (fps with hall tiles, alignment behaviour, anything that failed).

---

## 7. Performance rules (Galaxy XR ≈ Snapdragon XR2+ Gen 2 class)
- Per frame: ≤ 1.5 M triangles, ≤ 300 draw calls, 90 Hz → 11.1 ms budget (72 Hz fallback via Display Utilities when loading).
- Single Pass Instanced (multiview), Vulkan, foveated rendering on, no post-processing, no real-time shadows, one unlit material per twin style, no per-frame allocations in loaders (use `Task` + pooling), tile loads spread over frames, `Application.targetFrameRate` unused in XR.
- Measure with the PerfHud and the Android XR performance metrics before and after the hall tiles load; write the numbers in the DEVLOG.

---

## 8. Design system — Swiss / International Typographic Style (mandatory)

The app must look designed, not generated. Model the UI on the Swiss style (Müller-Brockmann, Hofmann, Ruder): a strict grid, objective typography, flat geometric form, generous white space, asymmetric but aligned layout, one accent colour used sparingly, no decoration. Implement it once as **`DesignTokens`** (ScriptableObject) + a small set of prefabs, and use nothing else. Put the tokens in `docs/DESIGN.md` with a rendered contact sheet (screenshots from the Editor simulator).

### 8.1 Typography
- Typeface: **Inter** (SIL OFL; fetch from github.com/rsms/inter releases) — weights Regular 400, Medium 500, Semibold 600; tabular figures for numbers (`tnum`). Monospace for coordinates/ids: **IBM Plex Mono** (OFL) or Inter tabular. Generate TextMeshPro SDF font assets (atlas 2048, sampling 64, padding 8). No other fonts, never Arial/LiberationSans.
- Scale (panel design space 1 unit = 1 mm at 1 m): Display 32 mm Medium, Title 20 mm Semibold, Body 12 mm Regular, Label 9 mm Medium **UPPERCASE, +6 % tracking**, Caption 8 mm Regular, Numeric 12 mm tabular. Line height 1.3. Left-aligned, ragged right. No centred paragraphs, no justified text, no italics, no all-caps body text.
- Minimum legibility in XR: body text ≥ 1.2° of visual angle (≈ 21 mm at 1 m); recompute sizes if a panel is placed farther than 1 m (scale the whole panel, never only the text).

### 8.2 Colour tokens (sRGB)
| Token | Value | Use |
|---|---|---|
| `ink` | #111111 | text, rules, outlines, primary buttons |
| `paper` | #F2F1EC | panel background (opaque, 100 %) |
| `paper-2` | #E6E4DD | secondary surfaces, pressed state |
| `muted` | #6F6E69 | secondary text, disabled (40 % alpha of ink is also allowed) |
| `accent` | #E30613 (Swiss red) | the current/active state, the single primary action, errors — **never decorative** |
| `type-missing` | #E30613 | annotation: in twin, not in reality |
| `type-new` | #0046E6 | annotation: in reality, not in twin |
| `type-changed` | #F2B600 (ink text on it) | annotation: moved/altered |
| `type-note` | #6F6E69 | annotation: free note |
| `twin-tint` | #1F4FD8 at 45 % | Technical twin style tint in Overlay mode |
| `twin-edge` | #111111 at 70 % | edge lines of the Technical style |
Rules: panels are **opaque paper with ink text** (passthrough stays visible around them, not through them). Contrast ≥ 7:1 for text. No gradients, no transparency effects on panels, no glow, no neon, no dark-mode glassmorphism.

### 8.3 Layout and components
- Grid: 8 mm base unit in panel space; panel outer margin 24 mm; gutter 16 mm; 12-column grid on a 480 × 320 mm standard panel (0.48 × 0.32 m at 1 m). Everything snaps to the grid. Corner radius **0**. Borders: 1 mm ink rule where separation is needed; otherwise rely on spacing. **No drop shadows, no bevels, no "card" stacks, no rounded pill buttons, no icons-with-gradients, no emoji.**
- Panel anatomy: top-left Label (section name, uppercase), top-right small status/meta in Caption; Title; content; bottom action row aligned to the grid (primary action = filled ink button with paper text, secondary = ink outline 1 mm, tertiary = text only underlined on hover). Only one accent-coloured element per panel.
- Controls: buttons 48 mm high minimum, full-width or grid-aligned; segmented control for view modes (three equal cells, active = ink fill); slider = 1 mm ink rule with a 12 mm square knob and a tabular numeric readout right-aligned ("64 %"); toggles = square checkbox with ink fill when on, no iOS-style pills; list rows 56 mm high with a 1 mm rule between rows; selection = inverted row (ink background, paper text).
- Iconography: words first. If an icon is needed, use 2 mm monoline geometric glyphs (square, circle, triangle, arrow, cross) in ink, 24 mm box, baseline-aligned with the label. No icon fonts, no emoji, no illustration.
- Toast: a single line of Body text on paper, bottom edge of the active panel, 2.5 s, no icon.
- Motion: 120–180 ms ease-out for show/hide and state changes; no bounce, no spring, no parallax, no particle effects. Panels fade and move ≤ 20 mm. Hover/focus = instant inversion of the control.
- Spatial behaviour: panels world-locked, facing the user at spawn, gravity-aligned, never head-locked; the tool strip is hand-anchored (palm up) with 4 cells; keep interactive targets ≥ 32 mm at 1 m.

### 8.4 Twin rendering styles
- **Colour**: vertex colours, unlit, alpha from the blend slider; neutral, no tone-mapping tricks.
- **Technical**: single `twin-tint` fill + `twin-edge` derivative-based edge lines; alpha from the blend slider; a thin 1 mm ink floor grid (1 m spacing, 10 m majors) projected on the twin floor plane in Twin-only mode. This is the default in Overlay mode because it reads clearly over passthrough and makes the decimated mesh look intentional.
- Twin-only mode background: `paper-2` solid (opaque environment), the floor grid visible, no skybox.

### 8.5 Annotation marker visual
- A flat 40 mm disc oriented to the surface normal, filled with the type colour, 1 mm ink ring; a 1 mm ink leader line (80 mm) to a small label (Label style, tabular index "07", type word). Selected = inverted (ink fill, paper ring). Never a floating 3D pin, no bouncing, no glow.

### 8.6 Explicit anti-patterns (reject your own work if it has any)
Default Unity UI sprites or fonts; rounded translucent "cards" floating with shadows; gradients; purple/teal "AI" palettes; emoji or icon soup; centred hero text; multiple accent colours; inconsistent spacing; decorative lines; neon wireframes; skeuomorphic buttons; head-locked HUDs; text smaller than the legibility rule; "hacky but unfinished" placeholders (lorem ipsum, debug text in release UI).

---

## 9. Working method
1. Read the proposal, this brief, `tools/mesh_overview.py`, the Android XR package docs in `Library/PackageCache/com.unity.xr.androidxr-openxr@*/Documentation~` and the Extensions `README.md`/`CHANGELOG.md` and the two sample READMEs. Write a short plan to `docs/DEVLOG.md`, then execute it.
2. Order of work: pipeline for Region A → `verify_tiles.py` passes → Unity one-time steps (§6.2) → Core + Twin loading in the simulator → design system + panels → view modes → alignment (manual, then marker, then anchors) → annotations → APK → Region B pipeline (run in the background while doing Unity work) → README/DEVLOG → final commit.
3. After every Unity change: `recompile_status` = no errors, OpenXR validation = 0 issues, console has no new errors. After every pipeline stage: counts and sizes within budget.
4. Commit after each milestone with a descriptive message. Never force-push. Never commit data.
5. If something is impossible on this machine (e.g. the headset is not connected), finish everything that does not need it, write exact **[HUMAN]** instructions, and continue. Do not fabricate on-device results.
6. Keep code readable: small classes, XML doc comments on public APIs, no god objects, no singletons except `AppBootstrap`'s service locator, async/await with cancellation for IO, `[SerializeField]` over public fields, assembly definition `TwinInspector.Runtime` + `TwinInspector.Editor` + `TwinInspector.Tests`.

---

## Appendix A — `manifest.json`
```json
{
  "schema": "twin-manifest/1",
  "scan": "navvis_2022-02-06_12.55.11",
  "source": {"mesh": "raw/HGE/sessions/navvis_2022-02-06_12.55.11/proc/meshes/mesh.ply", "sha256_header": "..."},
  "frame": {"up": "z", "units": "m", "alignment": "pose_graph_optimized", "gltf_axis_convention": "twin(x,y,z)->gltf(x,z,-y); loader applies inverse"},
  "origin_world": [0.0, 0.0, 0.0],
  "region": {"name": "A", "min": [-20, -5, -1], "max": [70, 32, 8]},
  "chunk_size_m": 10,
  "lods": [{"level": 0, "max_distance_m": 12}, {"level": 1, "max_distance_m": 30}, {"level": 2, "max_distance_m": 80}],
  "tiles": [
    {"id": "x-2_y0", "min": [-20, -5, -1], "max": [-10, 5, 8],
     "files": {"0": "tiles/x-2_y0/lod0.glb", "1": "tiles/x-2_y0/lod1.glb", "2": "tiles/x-2_y0/lod2.glb"},
     "triangles": {"0": 61234, "1": 15300, "2": 3700}, "bytes": {"0": 2211345, "1": 551000, "2": 133000}}
  ],
  "totals": {"triangles_lod0": 1234567, "bytes": 123456789},
  "generated_utc": "2026-10-15T10:00:00Z", "tool_version": "twin_pipeline 0.1"
}
```

## Appendix B — `markers.json` (team-measured; ship with two placeholder entries and a README on how to measure)
```json
{"schema": "twin-markers/1", "scan": "navvis_2022-02-06_12.55.11",
 "markers": [
  {"id": 0, "kind": "aruco_4x4_50", "size_m": 0.15, "position_twin": [12.40, 3.10, 1.50], "yaw_deg": 90.0, "normal_twin": [0, -1, 0], "note": "east wall of hall, left of door"},
  {"id": "QR:TWIN-01", "kind": "qr", "size_m": 0.15, "position_twin": [0.0, 0.0, 1.5], "yaw_deg": 0.0, "normal_twin": [1, 0, 0], "note": "placeholder"}
 ]}
```

## Appendix C — `annotations.json`
```json
{"schema": "twin-annotations/1", "scan": "navvis_2022-02-06_12.55.11", "exported_utc": "...",
 "items": [
  {"id": "a-000007", "index": 7, "type": "missing", "label": "Missing", "position_twin": [14.21, 6.02, 0.95], "normal_twin": [0, -1, 0],
   "created_utc": "...", "device_pose_twin": {"position": [12.0, 4.0, 1.6], "rotation_quat_xyzw": [0, 0.7, 0, 0.7]}, "screenshot": "shots/a-000007.jpg", "comment": ""}
 ]}
```

## Appendix D — reference links (read when needed)
- Unity OpenXR: Android XR package manual: https://docs.unity3d.com/Packages/com.unity.xr.androidxr-openxr@1.4/manual/index.html
- Android XR Extensions for Unity (features, samples, release notes): https://github.com/android/android-xr-unity-package and https://developer.android.com/develop/xr/unity
- Google Unity project setup for Android XR: https://developer.android.com/develop/xr/unity/setup
- Android XR performance guidance (Vulkan, foveation, GPU Resident Drawer, occlusion): https://developer.android.com/develop/xr/unity/performance/gpu-rendering
- XR Interaction Toolkit 3.6 (hands, UI, Device Simulator): https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.6/manual/index.html
- AR Foundation 6.6 (ARSession, ARCameraManager, anchors, tracked images, meshing): https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.6/manual/index.html
- glTFast runtime loading: https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/index.html
- LaMAR Capture format: https://github.com/microsoft/lamar-benchmark/blob/main/CAPTURE.md and RAW-DATA.md
- Meta Quest 3-class performance targets (same SoC class): https://developers.meta.com/vr/resources/device-optimization-comparison/
- Inter typeface: https://github.com/rsms/inter/releases · IBM Plex: https://github.com/IBM/plex/releases
