# TwinInspector v1.1

Mixed-reality inspection of the LaMAR HGE NavVis scan on Samsung Galaxy XR. Mouse-first Editor inspection, bare-hand Galaxy XR interaction, optional controllers, marker/manual registration, persistent anchors, three comparison views and spatial discrepancy annotations. Unity 6000.3.26f1, URP, OpenXR, Android XR and glTFast.

**Build and device status:** [Download the test APK](https://github.com/Haizhouzhou/MixedReality/raw/refs/heads/main/TwinInspector/Builds/TwinInspector-0.1.0.apk). This ARM64 build contains the v1.1 implementation, with zero build errors and zero Android OpenXR validation issues. The hall data and Editor workflows are verified. Physical tracking, headset FPS and anchor relocalization still require device acceptance. See [docs/DEVLOG.md](docs/DEVLOG.md) for evidence and remaining device checks.

## Download for teammate testing

Repository members can download the APK above while signed into GitHub, or obtain it by cloning/pulling this private repository. It is **66,014,471 bytes (62.96 MiB)** and was built from application source commit `55d82ce`. The Android package version and filename remain `0.1.0`; the implementation is TwinInspector v1.1. The APK contains the 32-tile hall demo, so a first headset test does not require the external dataset or a local Unity build.

SHA-256: `23e08596c75b053ca28634cab7a8bb326e3bec14d3c6d08608fa80f21ff2b189`.

Download to `TwinInspector/Builds/TwinInspector-0.1.0.apk` and follow [Build, install and launch](#build-install-and-launch) from the install commands onward. Adjust the example Unity SDK/ADB paths for your computer. This APK is the sole exception to the ignored build outputs; raw scans, full external datasets, caches and other builds remain excluded.

## Prepare the data

From `E:\MixedReality`, use the provided `mr` environment:

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\tests\test_twin_pipeline.py
& 'D:\miniforge3\envs\mr\python.exe' -I tools\twin_pipeline.py all --session dataset\raw\HGE\sessions\navvis_2022-02-06_12.55.11 --region a --out work\twin\HGE_2022-02-06\region_a
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_seams.py work\twin\HGE_2022-02-06\region_a
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_tiles.py work\twin\HGE_2022-02-06\region_a
```

Repeat with `--region b` and `--out work\twin\HGE_2022-02-06\region_b` for the floor. The bounding boxes are the brief's measured A/B extents, not semantic indoor segmentation. Centroid clipping deliberately retains complete boundary triangles; a triangle can extend outside its cell or region.

The pipeline reads fixed-size PLY batches through read-only memory maps. Checkpointed batches and completed tiles are skipped on rerun. The `config.json` fingerprint rejects a different source/configuration in an existing output. Choose a new output directory for changed settings. `--force` reruns the selected stage; `all --force` rebuilds all stages. Do not run two writers against the same output directory. `--target-ratio` defaults to 0.013 and is reduced automatically if the region triangle budget would be exceeded.

Each compact tile is simplified as a whole up to eight million triangles. Larger tiles use spatial median splits. v1.1 disables independent vertex welding and enables `fast_simplification` 0.2.0 `preserve_border=True` at every reduction. Centroid cuts create zig-zag shared edges that are not necessarily on a 10 m grid plane; preserving only modulo-grid vertices would miss them. Open boundary vertices and edges remain exact across all LODs. Lower LOD triangle ratios may therefore be exceeded; the manifest records the exact excess and boundary policy. The 1.5 M / 5 M regional LOD0 budgets remain hard limits.

Run `tools/verify_seams.py <output>` before `tools/verify_tiles.py <output>`. The seam verifier independently compares every exported GLB against the source chunk's open edges, with a 20 micrometre export-rounding tolerance and zero missing edges. v1.1 reduction checkpoints are versioned by boundary policy; old reductions are rebuilt automatically. The original Region B baseline is verified for geometry and budgets but has not been regenerated with the new seam policy.

Stages can be run separately: `chunk`, `simplify`, `export`, `manifest`, `preview`. Raw data and intermediate chunks stay outside the APK. The only bundled data is Region A LOD2, packaged with:

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\package_demo.py work\twin\HGE_2022-02-06\region_a TwinInspector\Assets\StreamingAssets\twin_demo
```

## Coordinates

Persisted twin coordinates are aligned LaMAR world coordinates, metres, right-handed Z-up. Tiles subtract `origin_world = [20, 0, 0]`, then export `(x,y,z) → (x,z,-y)`. glTFast reflects X into Unity handedness. A single 180° Unity Y rotation on the imported tile container gives local `(x,z,y)`. The root applies only alignment; it never scales the building. The inverse is local `(x,z,y) + origin_world`. Python and C# tests use asymmetric points so a double reflection cannot silently pass. Confirm the rotunda remains west / negative X in the scene.

Annotations and measured marker coordinates always use the original twin frame, with the origin offset restored. Do not measure in the shifted glTF frame.

## Headset setup and data transfer

**[HUMAN]** On the headset: Settings → About headset → Software information → tap Build number seven times → enable USB debugging in Developer options. Connect USB-C and accept the RSA prompt. `adb devices` must show one authorized device.

After installing and first launching the APK, push a verified region:

```powershell
.\tools\push_twin.ps1 -Region region_a
# Or region_b; -Serial selects a device when more than one is connected.
```

Only runtime GLBs, marker configuration and the manifest are transferred. Tiles go into a manifest-hash revision directory; the manifest is activated last, so interrupted transfers cannot replace files referenced by the previous manifest. Device destination: `/sdcard/Android/data/com.mrtwin.inspector/files/twin/HGE_2022-02-06/`. Previous tile revisions are retained. Restart the app after replacing a dataset. A missing external dataset uses a packaged selection of complete nearby Region A LOD2 tiles, capped below 15 MB. External data retains the entire region.

**[HUMAN]** Grant the camera, scene-understanding and hand-tracking permission prompts. Print `tools/markers/marker_00.png` on A4 at 100%, check the black square is 150 mm, and place it at a surveyed pose. Follow [marker measurement instructions](../tools/markers/README.md). Shipped coordinates are examples with `measured:false`; automatic headset alignment deliberately rejects them. A marker must remain tracked for one second before confirmation.

## Build, install and launch

Open `Assets/_TwinInspector/Scenes/Inspector.unity`. The scene uses the XRI modality manager with two hand groups and two optional controller groups. The Editor-only XR Device Simulator is disabled by default. The generated assets are committed; `TwinInspector → Rebuild application assets and Inspector` reproduces them through Editor APIs if needed. This command replaces Inspector, so save any intentional scene edits first.

From PowerShell at the repository root:

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

Enter Play mode in Inspector and focus the Game view. With no actively tracked HMD, the Editor selects mouse and keyboard automatically. The simulator stays disabled. The Editor loads `Settings.editorDataPath`; the packaged LOD2 demo is used when external data is absent.

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

The strip uses opaque paper/ink cells with uppercase Inter Medium labels and one millimetre dividers. Palm up anchors it near the left palm. With hands down or in the Editor, it remains in world space 0.55 m ahead and 22° below eye level, recentering over 150 ms after a heading change beyond 25° or an explicit summon. Looking within the comfort cone does not move it. Other panels remain world-locked until summoned.

**Roam:** in Twin mode, pinch both hands in air and pull/turn to move the viewer rig horizontally. Release either pinch to stop. Tracking loss resets the gesture. UI, annotation placement and active registration take priority. **Register:** Align moves `TwinRoot` relative to the room; height remains locked until explicitly unlocked. In the Editor use Fine adjustment buttons. Save alignment or confirm a stable surveyed marker to persist it.

Annotation placement uses the same collider hit and persistence path for mouse, hand pinch and controller trigger. List supports selection, retyping, deletion and JSON/CSV export. `F1` summon, `F2` marker, `F3` annotate and `F4` mode cycling remain compatibility shortcuts.

Verified v1.1 captures are in [docs/screenshots](docs/screenshots/), including [the hall](docs/screenshots/hall-desktop.png), [Technical view](docs/screenshots/hall-technical.png), [reticle](docs/screenshots/reticle-surface.png) and [surface annotation](docs/screenshots/annotation-surface.png). Physical hand tracking, controller pickup, marker tracking, anchor relocalization and headset performance still require a connected device.

## Device acceptance run

After the two **[HUMAN]** steps above, install the APK, push Region A, restart, and use this sequence to finish the device-only acceptance checks:

1. Confirm camera passthrough around the opaque Setup panel and inspect the loaded hall. Open Diagnostics; record FPS, CPU/GPU time, tile/triangle counts and display rate before loading and after 60 seconds of steady viewing. Acceptance is at least 72 FPS, with 90 as the target. Record draw calls from the Unity profiler when connected; the budget is 300.
2. Switch Reality, Twin and Overlay, then test 0%, 45% and 100% blend and both styles. Confirm both eyes render consistently.
3. Try two-hand adjustment, fine/coarse nudges, locked/unlocked height and floor snap. Save and restart; record whether Setup reports a persistent anchor or the JSON fallback, and check the overlay against the physical building.
4. Survey and enter the marker pose, set `measured:true`, push again, and confirm stable detection followed by explicit confirmation. Check the west rotunda to detect a coordinate reflection.
5. Create each annotation type, select/retype/delete one, restart, and export. Pull the JSON/CSV and logs with the commands above. Confirm original twin coordinates and stable IDs.

Record the actual device model/runtime, test date, measurements and failures in `docs/DEVLOG.md`. The app's local logs distinguish Editor metrics from device metrics. A configured refresh rate or passing Editor test alone does not satisfy the device FPS requirement.

## Attribution and dependencies

Source geometry: **LaMAR**, ETH Zurich and Microsoft, HGE scene, scan `navvis_2022-02-06_12.55.11`, [lamar.ethz.ch](https://lamar.ethz.ch/), **CC BY-SA 4.0**. Derived geometry changes: region/cell clipping, 1 mm welding, CPU quadric decimation, nearest-source vertex colour sampling and coordinate conversion. Redistributed derived tiles retain CC BY-SA 4.0; see the fallback's LICENSE.txt. LaMAR's reference poses were generated using SuperPoint/SuperGlue; this app's v1 registration uses measured markers and manual alignment.

Inter 4.1 by Rasmus Andersson: SIL Open Font License, bundled with its license. Google Extensions and Unity sample notices remain with their packages/imported samples. The `System.Runtime.CompilerServices.Unsafe` compatibility plugin must remain: see its adjacent README. No CUDA reconstruction is used.

References: [Unity Android XR](https://docs.unity3d.com/Packages/com.unity.xr.androidxr-openxr@1.4/manual/index.html), [Google Extensions](https://github.com/android/android-xr-unity-package), [GPU rendering guidance](https://developer.android.com/develop/xr/unity/performance/gpu-rendering), [glTFast runtime loading](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportRuntime.html).
