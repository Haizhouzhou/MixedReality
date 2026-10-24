# TwinInspector — setup and development

This guide covers the Unity project, controls, Android deployment and external scan data. For an illustrated overview, see the [repository README](../README.md).

TwinInspector v1.3 uses Unity **6000.3.26f1**, XRI **3.6.1**, OpenXR, URP, glTFast and Unity Inference Engine/Sentis **2.6.1**. The Android application version is **1.3.0**, code **3**, with package ID `com.mrtwin.inspector`. The [tracked APK](Builds/TwinInspector-0.1.0.apk) includes a 32-tile hall demo and can update an existing installation with `adb install -r`, preserving saved data. The existing filename is retained by the delivery scripts. External scans, caches, checkpoints and ONNX intermediates are excluded from Git. Serialized SAM models are tracked separately under `tools/sam/models` and excluded from the APK.

![Numbered inspection panel and tool strip in the HGE hall](docs/screenshots/v12-mode.png)

*Unity Editor capture. The panel and strip share a world-space dock.*

See [DEVLOG](docs/DEVLOG.md) for build and verification results. Editor and synthetic input checks do not establish physical tracking, stereo correctness or headset frame rate.

Verified v1.3 APK: **77,210,373 bytes (73.63 MiB)**; SHA-256 `3d8ddab95b8afadd32b939e8595b0bf6e0a4e0e75416931e0b29384342297824`. All 32 Unity and 11 Python tests pass; C# compilation and Android OpenXR project validation report zero errors/warnings and zero issues respectively. The Android build has 354 warnings: 353 from Inference Engine shaders and one existing Pipeline editor-service warning, with zero TwinInspector script warnings. SAM remains disabled. On 2026-10-24, installation and external Region A startup succeeded on Galaxy XR; the user confirmed the Guide/menu, visible hall and working pinch clicks. Initial device samples were 38.0–46.0 FPS, below the 72 FPS target. This is partial device evidence, not completed acceptance; see [first device run](docs/evidence/device-first-run-v13.txt).

## Prepare the data

The planned workflow is standalone Galaxy XR: prepare/install/push on a workstation, disconnect for local inspection, reconnect to retrieve exports or update assets. Neither geometry comparison nor SAM uses a server. The headset needs its supported Android XR tracking and scene-meshing runtime plus the requested permissions; actual availability and frame rate remain unverified until device acceptance. The 16 GB development laptop uses CPU model export and needs no CUDA. It is not a tethered rendering host.

The packaged demo is sufficient to try the application. Process external data only when a full region is needed. The commands below use the development laptop's checkout and `mr` Python environment; adjust those paths on another machine. Python dependencies are listed in [requirements-twin.txt](../tools/requirements-twin.txt).

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\tests\test_twin_pipeline.py
& 'D:\miniforge3\envs\mr\python.exe' -I tools\twin_pipeline.py all --session dataset\raw\HGE\sessions\navvis_2022-02-06_12.55.11 --region a --out work\twin\HG_MainHall
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_seams.py work\twin\HG_MainHall
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_tiles.py work\twin\HG_MainHall
```

Repeat with `--region b` and `--out work\twin\HG_RegionB` for the floor. Regions A and B use fixed measured extents rather than semantic indoor segmentation. Centroid clipping retains complete boundary triangles; a triangle can extend outside its cell or region.

The pipeline reads fixed-size PLY batches through read-only memory maps. Checkpointed batches and completed tiles are skipped on rerun. The `config.json` fingerprint rejects a different source/configuration in an existing output. Choose a new output directory for changed settings. `--force` reruns the selected stage; `all --force` rebuilds all stages. Do not run two writers against the same output directory. `--target-ratio` defaults to 0.013 and is reduced automatically if the region triangle budget would be exceeded.

Each compact tile is simplified as a whole up to eight million triangles. Larger tiles use spatial median splits. v1.1 disables independent vertex welding and enables `fast_simplification` 0.2.0 `preserve_border=True` at every reduction. Centroid cuts create zig-zag shared edges that are not necessarily on a 10 m grid plane; preserving only modulo-grid vertices would miss them. Open boundary vertices and edges remain exact across all LODs. Lower LOD triangle ratios may therefore be exceeded; the manifest records the exact excess and boundary policy. The 1.5 M / 5 M regional LOD0 budgets remain hard limits.

Run `tools/verify_seams.py <output>` before `tools/verify_tiles.py <output>`. The seam verifier independently compares every exported GLB against the source chunk's open edges, with a 20 micrometre export-rounding tolerance and zero missing edges. v1.1 reduction checkpoints are versioned by boundary policy; old reductions are rebuilt automatically. The original Region B baseline is verified for geometry and budgets but has not been regenerated with the new seam policy.

Stages can be run separately: `chunk`, `simplify`, `export`, `manifest`, `preview`. Raw data and intermediate chunks stay outside the APK. Every space is named by one id, used everywhere: its entry in `Assets/StreamingAssets/twins/spaces.json`, its packaged LOD2 folder `twins/<id>/` (spaces with `"packaged": true`), its pipeline output `work/twin/<id>/`, its headset folder `files/twin/<id>/` and its annotation/alignment/dismissal files. HG Region A is packaged with:

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\package_demo.py work\twin\HG_MainHall TwinInspector\Assets\StreamingAssets\twins\HG_MainHall
```

`CAB_MainHall` comes from a cropped mesh in the CAB session (`proc/meshes/entrance_hall.ply`). `--region auto` uses the whole mesh, `--origin auto` picks the nearest 10 m grid point to its centre, and `--datum-z` records the main floor height (−4.8 m in the CAB frame):

```bash
python -I tools/twin_pipeline.py all --session <CAB>/navvis_2022-06-25_11.14.36 --mesh proc/meshes/entrance_hall.ply --region auto --region-name "Main Hall" --origin auto --building CAB --datum-z -4.8 --workers 2 --out work/twin/CAB_MainHall
python -I tools/package_demo.py work/twin/CAB_MainHall TwinInspector/Assets/StreamingAssets/twins/CAB_MainHall --building CAB
```

To add another space, choose its id, run the pipeline with `--out work/twin/<id>`, add an entry to `twins/spaces.json`, and either package it (`package_demo.py` into `twins/<id>`, `"packaged": true`; create its `.meta` files by opening the project in Unity before committing) or transfer it with `push_space.py <id>`.

At start-up the app lists `twins/spaces.json` and asks which space to open; Skip opens `Settings.defaultSpace` (`HG_MainHall`). Later, the gear cell → Space switches after a confirmation: the per-space services are destroyed and rebuilt for the new space. Data on the headset (`files/twin/<id>`) replaces the packaged copy when it holds a `manifest.json`; in the Editor, `Settings.editorDataPath` (default `work/twin`, relative to the repository) is checked first, so full pipeline outputs load on the workstation. Spaces with neither packaged nor transferred data appear greyed out. HG_E3_Hall and HG_E7_Hall are not packaged (their LOD2 adds about 20 MB, which would push the APK past 95 MB):

```bash
python -I tools/push_space.py HG_E3_Hall
python -I tools/push_space.py HG_E7_Hall
```

Both were cut from the 2022-02-06 HG scan with `--region 16,69,-33,4,-1,8 --region-name "E3 Hall"` and `--region 16,69,16,52,-1,8 --region-name "E7 Hall"` (`--origin auto`, `--out work/twin/HG_E3_Hall` / `HG_E7_Hall`). Annotations, alignment and dismissed candidates are stored per space id, so spaces never share them, even when cut from the same scan: each space is aligned on its own. A space without `markers.json` loads normally with Use marker disabled. The manifest's `datum_z` sets the datum grid, contours and the diff floor; manifests without it use 0.

Chinese text uses subsets of Noto Sans SC in `Resources/Fonts`, added at runtime as fallbacks of the Inter fonts. After changing any `Strings/*.json`, regenerate the subsets from the full fonts ([notofonts/noto-cjk](https://github.com/notofonts/noto-cjk), `Sans/SubsetOTF/SC`):

```bash
python -I tools/subset_cjk_font.py <folder with NotoSansSC-Regular/Medium/Bold.otf>
```

## Coordinates

Persisted twin coordinates are aligned LaMAR world coordinates, metres, right-handed Z-up. Tiles subtract `origin_world = [20, 0, 0]`, then export `(x,y,z) → (x,z,-y)`. glTFast reflects X into Unity handedness. A single 180° Unity Y rotation on the imported tile container gives local `(x,z,y)`. The root applies only alignment; it never scales the building. The inverse is local `(x,z,y) + origin_world`. Python and C# tests use asymmetric points so a double reflection cannot silently pass. Confirm the rotunda remains west / negative X in the scene.

Annotations and measured marker coordinates always use the original twin frame, with the origin offset restored. Do not measure in the shifted glTF frame.

## Headset setup and data transfer

On the headset, open Settings → About headset → Software information, tap Build number seven times, then enable USB debugging in Developer options. Connect USB-C and accept the RSA prompt. `adb devices` must show an authorized device.

After installing and first launching the APK, push a space's verified data (`push_space.py` works on macOS, Linux and Windows; `push_twin.ps1` is the Windows equivalent):

```bash
python -I tools/push_space.py HG_MainHall
```

```powershell
.\tools\push_twin.ps1 -Space HG_MainHall
.\tools\push_twin.ps1 -Models
# -Serial selects a device when more than one is connected.
```

Only runtime GLBs, marker configuration and the manifest are transferred. Tiles go into a manifest-hash revision directory; the manifest is activated last, so interrupted transfers cannot replace files referenced by the previous manifest. Device destination: `/sdcard/Android/data/com.mrtwin.inspector/files/twin/<space id>/`; SAM models go to `files/models/`. Previous tile revisions are retained. Restart the app after replacing a dataset. A missing external dataset uses a packaged selection of complete nearby Region A LOD2 tiles, capped below 15 MB. External data retains the entire region.

Grant the camera, scene-understanding and hand-tracking permissions. For marker alignment, print `tools/markers/marker_00.png` on A4 at 100%, check the black square is 150 mm, and place it at a surveyed pose. Follow the [marker measurement instructions](../tools/markers/README.md). Shipped coordinates are examples with `measured:false`; automatic headset alignment rejects them. A marker must remain tracked for one second before confirmation.

### Download space data from GitHub Releases

Full-detail space data is not stored in Git. Release [`data-2026-10-24`](https://github.com/Haizhouzhou/MixedReality/releases/tag/data-2026-10-24) holds one zip per space; each contains a folder named by the space id, with `manifest.json`, all LOD tiles and `LICENSE.txt` (CC BY-SA 4.0, LaMAR).

| Asset | Space | Headset folder | Tiles | Size |
|---|---|---|---|---|
| `twin-HG_MainHall.zip` | HG Main Hall (Region A, full detail) | `HG_MainHall` | 45 | 43.5 MB |
| `twin-HG_E3_Hall.zip` | HG E3 Hall (external only) | `HG_E3_Hall` | 28 | 26.5 MB |
| `twin-HG_E7_Hall.zip` | HG E7 Hall (external only) | `HG_E7_Hall` | 28 | 20.1 MB |
| `twin-CAB_MainHall.zip` | CAB Main Hall (full detail) | `CAB_MainHall` | 8 | 12.8 MB |

HG E3 Hall and HG E7 Hall stay greyed out in the app until their data is transferred; the other two also work from their built-in LOD2 copies. To install the data, connect and authorize the headset, install and launch the app once, then from the repository root:

```bash
mkdir -p work/release && cd work/release
# download the zips and SHA256SUMS.txt from the release page into this folder, then:
shasum -a 256 -c SHA256SUMS.txt
for z in twin-*.zip; do unzip -oq "$z" -d ../twin; done
cd ../..
for space in HG_MainHall HG_E3_Hall HG_E7_Hall CAB_MainHall; do python3 -I tools/push_space.py "$space"; done
```

Each zip unpacks to `work/twin/<id>`, the same place the pipeline writes, so the Editor also loads them in full detail. `push_space.py` needs only Python 3 and `adb`; it finds `adb` on the `PATH` or in Unity Hub's Android module, otherwise pass `--adb <path>` (on Windows use `py -3` and `Get-FileHash` instead of `shasum`). Reopen the app afterwards. Uninstalling the app deletes these folders; an update from the same computer keeps them.

## Build, install and launch

Open `Assets/_TwinInspector/Scenes/Inspector.unity`. The scene uses the XRI modality manager with two hand groups and two optional controller groups. The Editor-only XR Device Simulator is disabled by default. The generated assets are committed; `TwinInspector → Rebuild application assets and Inspector` reproduces them through Editor APIs if needed. This command replaces Inspector, so save any intentional scene edits first.

From PowerShell at the repository root, using the development laptop's paths:

```powershell
$env:UNITY_NO_BANNER='1'
$env:UNITY_NON_INTERACTIVE='1'
$env:UNITY_NO_PAGER='1'
$unity='C:\Users\zackz\AppData\Local\Unity\bin\unity.exe'
$project='E:\MixedReality\TwinInspector'
& $unity recompile --project-path $project --timeout 300 --format json
& $unity command eval_file E:\MixedReality\tools\unity\validate.cs 60000 --timeout 120 --project-path $project --result-only
& $unity command build --target Android --outputPath Builds/TwinInspector-0.1.0.apk --confirm true --project-path $project --result-only
& $unity command build_status --project-path $project --result-only
```

Wait for the build status to complete successfully. Vulkan, IL2CPP, ARM64, GameActivity and the existing .NET Framework compatibility setting are retained. Forward+, Render Graph, 4× MSAA, foveation, GPU Resident Drawer and GPU occlusion are configured; HDR, shadows, post-processing and optional camera copies are disabled. Transparent overlay rendering is not eligible for every GPU Resident Drawer optimization; performance still requires headset measurement.

For an unattended build, start the closed project with `& $unity open $project --args='-batchmode'` before issuing the Pipeline commands. The project's Android build callback merges duplicate manifest entries generated by the Unity and Google XR packages, retaining the highest required spatial API version. Keep that callback; otherwise these package versions produce a duplicate-feature Gradle error.

```powershell
$adb='E:\Unity\Editor\6000.3.26f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
& $adb devices -l
& $adb install -r TwinInspector\Builds\TwinInspector-0.1.0.apk
& $adb shell am start -n com.mrtwin.inspector/com.unity3d.player.UnityPlayerGameActivity
& $adb logcat -s Unity
```

## Use in the Editor and on Galaxy XR

Enter Play mode in Inspector and focus the Game view. With no actively tracked HMD, the Editor selects mouse and keyboard automatically. The XRI device simulator stays disabled; v1.3's independent scene-surface fixture simulates reality when external Region A exists. The Editor loads the chosen space from `work/twin/<id>` (`Settings.editorDataPath`, relative to the repository) when that pipeline output exists; otherwise it uses the packaged LOD2 copy, and spaces without either stay greyed out.

| Action | Editor input |
|---|---|
| Look | Hold right mouse; yaw/pitch with zero roll |
| Move | W/A/S/D horizontally; E/Q or Space/Left Shift vertically |
| Speed | Hold Alt for 3× speed; right mouse + wheel adjusts base speed |
| Tool pages | 1 Mode, 2 Align, 3 Annotate, 4 List |
| Interact | Left click world-space buttons, sliders or an annotation surface |
| Recenter | Tab summons the active panel and recenters the strip |
| Marker simulation | M uses `markers.json` entry zero; this is an Editor fixture |
| Developer hints | Setup → Diagnostics shows PerfHud and the corner key guide |

On Galaxy XR, point and pinch to activate UI or place an annotation; near UI also accepts poke. A tracked controller uses its trigger through the same XRI action readers. The modality manager activates the tracked hand or controller group automatically; no controller model or long sample beam is rendered.

The numbered ToolStrip and active panel share a world-space dock: 0.55 m forward, 22° below eye level, a 25° heading dead zone and a 150 ms recenter. The panel remains centered **24 mm above** the strip. Targeting or manipulating a control suspends automatic movement. Briefly raise either available palm to summon the dock, then turn the same hand over to operate it; lowering the palm re-arms the gesture. Tab and Setup → Preferences → Recenter provide explicit alternatives.

- **00 Setup → Guide / Preferences:** six short practice cards open on first launch and can be reopened. Preferences saves the language (English, German, French or Italian), Left/Right dominant hand, feedback sound and volume. The available hand always works when the preferred hand is absent. Visual feedback remains when muted.
- **01 Mode:** Reality / Twin / Overlay, Technical / Colour, and the Lens toggle. Opacity / Lens opens the blend and 0.5–5 m radius controls. Lens defaults off and is available in Overlay. Its sphere follows the stabilized building hit; UI retains the last center, while no-hit holds briefly and fades out.
- **01 Mode → Twin → Roam:** hold Forward / Backwards / Left / Right to move at 0.5 m/s; turn in 30° steps, or Return to room. Two-hand pinch world-drag remains available. Leaving Twin restores the physical inspection frame before comparison. Roaming never saves a building-registration offset.
- **02 Align:** enters Overlay, including from Reality or a roamed Twin view. Point at the building and pinch/drag with one hand; a second pinch adds translation/yaw. Height starts locked. Fine/coarse nudges, height controls, floor snap, surveyed marker confirmation and Save Alignment remain available. The interface shows the original-frame datum.
- **03 Annotate:** point at a visibly revealed building surface and select. Choose circle/minus Missing, square/plus New, triangle/delta Changed or diamond/dot Note. Geometry, high-contrast symbols and text identify types independently of color. Presentation faces the center-eye camera, while the stored surface anchor and normal remain unchanged.
- **04 List:** review geometric change candidates alongside annotations. Confirm creates a persistent region; Dismiss suppresses that candidate, including after recomputation. Export writes original-frame JSON/CSV and nearby scene-mesh OBJ patches for New/Changed regions. Background results wait until the pointer leaves and dragging ends before refreshing the list, preserving its scroll position. Selection prioritizes visible, best-aligned annotations, with building occlusion and stable tie-breaking.
- **05 Diagnostics:** performance readings, a one-frame depth-descriptor probe and a Camera2 metadata probe. Editor probes report unavailable. Device metrics remain unavailable when no runtime supplies them.

The pointing filter acts on derived XRI ray transforms before hit testing, using the installed readers' continuous pinch/trigger value. A rising 0.4 strength crossing briefly increases angular damping; 0.3 re-arms it, and confirmation restores responsive dragging. These values and the 150 ms window are serialized tuning values on each inspection ray. Mouse pointing bypasses this filter.

Technical rendering caches mesh adjacency, area-weighted normals and restrained concavity at tile/LOD import. Creases and view-dependent silhouettes use screen-expanded ink segments; unpaired cut edges are excluded so arbitrary zig-zag chunk seams are not treated as architectural edges. No vertex positions are changed. Contours use restored twin Z every 0.5 m, with a stronger zero datum. The existing datum grid uses 1 m crosshairs and 10 m major lines in the same original frame. Its depth-tested marks can be occluded by measured floor surfaces above the datum.

The Lens uses a depth-writing inner region and a separately blended, non-depth-writing outer shell. Both explicitly discard excluded geometry, and the same physical sphere clips outlines, contours and the datum grid. Passthrough outside the reveal is untouched. The fill, edge cache and shell are released with their tile; there is no per-frame mesh processing. Colour mode disables the additional edge renderers.

Glyphs use at least 1.5° angular height at 10–30 m, independent 2° selection targets and separately sized text. The reticle uses approximately 0.35° and contracts continuously with selection strength; only accepted actions produce confirmation. Hover/confirmation sounds are two cached synthesized clips, with a quiet non-spatial default.

Current rendered comparisons and interaction evidence are in [docs/screenshots](docs/screenshots/) and [docs/evidence](docs/evidence/). Rebuilding application assets through the existing menu retains the scene wiring, materials, glyph prefab, UI layers and filtered modality rays.

## Device acceptance run

v1.3 adds the following acceptance steps. Installation, external-data startup and user-observed Guide/menu visibility, hall rendering and pinch clicks have initial physical evidence; the full workflows below remain **pending physical acceptance**. The first test is in a different room from the scan and cannot validate real change-detection accuracy.

SAM is an experimental implementation, **disabled in the release Settings asset** because cold GPU dispatch exceeded the frame budget. Its functional tests and external models are retained. Step 5 requires an explicitly enabled evaluation build; see [SAM status](../tools/sam/README.md). Transferring models alone does not enable the release flag.

1. Record `adb devices -l`, device model/runtime and build ID. Install, push Region A and external models, launch and grant permissions. Complete the first-launch guide, reopen it from Setup, then switch Preferences → Language to Deutsch and back. English/German terminology is agent-reviewed against the glossary; French/Italian are machine translated, unreviewed.
2. Align manually or with a measured marker and save. In Diagnostics run **Depth probe** and **Camera probe**, then pull the log. Record texture descriptor IDs/names, dimensions, array layers, format, FOV/poses/near-far data, CPU depth availability and Camera2 characteristics. These buttons only inspect metadata; Camera2 never opens a camera. The depth probe disables occlusion after one frame or an eight-second timeout.
3. Walk for two minutes in Overlay with Diff on. Record first-candidate latency, counts by kind, `Diff chunks=`, FPS/CPU/GPU readings and false candidates. Confirm a known change, retype, restart and verify its region persists. Dismiss another and check suppression after recomputation.
4. Export and pull JSON/CSV plus the `*-patches` directory. Inspect the OBJ in the original twin frame. New/Changed exports contain observed scene triangles within 1.5 m, capped at 200,000 triangles per annotation; no scene mesh means no patch file and an explicit log message.
5. Create a fresh annotation on a visible twin column, press **Pick object**, and pinch the surface. Check type preservation, plausible region, cold/warm latency, sampled memory and responsiveness. Close the page or move more than 1 m to check cancellation. Missing/invalid models must leave ordinary inspection usable.

```powershell
adb pull /sdcard/Android/data/com.mrtwin.inspector/files/exports work/device-exports
adb pull /sdcard/Android/data/com.mrtwin.inspector/files/logs work/device-logs
& 'D:\miniforge3\envs\mr\python.exe' -I tools/evaluate_annotations.py work/device-exports/EXPORT.json tools/fixtures/ground_truth_example.csv --out work/evaluation.md
```

The shipped ground truth describes the deterministic Editor fixture, not physical hall measurements. Replace it with surveyed changes for a device evaluation. Time between annotation timestamps is an interval proxy, not measured task duration.

Retain the baseline rendering and interaction acceptance checks below as well, after installing, granting permissions and transferring Region A:

1. Confirm camera passthrough around the opaque Setup panel and inspect the loaded hall. Open Diagnostics; record FPS, CPU/GPU time, tile/triangle counts and display rate before loading and after 60 seconds of steady viewing. Acceptance is at least 72 FPS, with 90 as the target. Record draw calls from the Unity profiler when connected; the budget is 300.
2. Switch Reality, Twin and Overlay; test opacity, both styles, contours and datum grid. Enable Lens and test 0.5 / 1.5 / 5 m radii at near and far hits, UI hold and no-hit fade. Confirm both eyes match and peripheral passthrough is unchanged.
3. Independently with left only and right only, summon the dock, change preferences, roam/return, create and edit annotations, scroll and export. Repeat with an optional controller. Try one- and two-hand alignment, fine/coarse nudges, locked/unlocked height and floor snap. Save and restart; record whether Setup reports a persistent anchor or the JSON fallback, and check the overlay against the physical building.
4. Survey and enter the marker pose, set `measured:true`, push again, and confirm stable detection followed by explicit confirmation. Check the west rotunda to detect a coordinate reflection.
5. Create each annotation type, select/retype/delete one, restart, and export. Pull the JSON/CSV and logs with the commands above. Confirm original twin coordinates and stable IDs.

Record the actual device model/runtime, test date, measurements and failures in `docs/DEVLOG.md`. The app's local logs distinguish Editor metrics from device metrics. A configured refresh rate or passing Editor test alone does not satisfy the device FPS requirement.

## Attribution and dependencies

Source geometry: **LaMAR**, ETH Zurich and Microsoft, HGE scene, scan `navvis_2022-02-06_12.55.11`, [lamar.ethz.ch](https://lamar.ethz.ch/), **CC BY-SA 4.0**. Derived geometry changes: region/cell clipping, 1 mm welding, CPU quadric decimation, nearest-source vertex colour sampling and coordinate conversion. Redistributed derived tiles retain CC BY-SA 4.0; see the fallback's LICENSE.txt. LaMAR's reference poses were generated using SuperPoint/SuperGlue; this app's v1 registration uses measured markers and manual alignment.

Inter 4.1 by Rasmus Andersson: SIL Open Font License, bundled with its license. Google Extensions and Unity sample notices remain with their packages/imported samples. The `System.Runtime.CompilerServices.Unsafe` compatibility plugin must remain: see its adjacent README. No CUDA reconstruction is used.

References: [Unity Android XR](https://docs.unity3d.com/Packages/com.unity.xr.androidxr-openxr@1.4/manual/index.html), [Google Extensions](https://github.com/android/android-xr-unity-package), [GPU rendering guidance](https://developer.android.com/develop/xr/unity/performance/gpu-rendering), [glTFast runtime loading](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportRuntime.html).
