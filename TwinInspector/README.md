# TwinInspector v1

Mixed-reality inspection of the LaMAR HGE NavVis scan on Samsung Galaxy XR. The v1 scope is CPU mesh decimation, marker/manual registration, persistent anchors, three comparison views and spatial discrepancy annotations. Unity 6000.3.26f1, URP, OpenXR, Android XR and glTFast.

**Build and device status:** [TwinInspector 0.1.0 APK](Builds/TwinInspector-0.1.0.apk) builds successfully for ARM64, with zero build errors and zero Android OpenXR validation issues. The hall data and Editor workflows are verified. No headset is connected, so device FPS, physical tracking and anchor relocalization remain unverified. See [docs/DEVLOG.md](docs/DEVLOG.md) for evidence and remaining device checks. APKs are local build outputs and are intentionally excluded from git.

## Prepare the data

From `E:\MixedReality`, use the provided `mr` environment:

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\tests\test_twin_pipeline.py
& 'D:\miniforge3\envs\mr\python.exe' -I tools\twin_pipeline.py all --session dataset\raw\HGE\sessions\navvis_2022-02-06_12.55.11 --region a --out work\twin\HGE_2022-02-06\region_a
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_tiles.py work\twin\HGE_2022-02-06\region_a
```

Repeat with `--region b` and `--out work\twin\HGE_2022-02-06\region_b` for the floor. The bounding boxes are the brief's measured A/B extents, not semantic indoor segmentation. Centroid clipping deliberately retains complete boundary triangles; a triangle can extend outside its cell or region.

The pipeline reads fixed-size PLY batches through read-only memory maps. Checkpointed batches and completed tiles are skipped on rerun. The `config.json` fingerprint rejects a different source/configuration in an existing output. Choose a new output directory for changed settings. `--force` reruns the selected stage; `all --force` rebuilds all stages. Do not run two writers against the same output directory. `--target-ratio` defaults to 0.013 and is reduced automatically if the region triangle budget would be exceeded.

Each compact tile is simplified as a whole up to eight million triangles (measured 2.57 GB peak for a 6.33 M triangle tile). Larger tiles use spatial median splits that preserve surface connectivity. Initial welding is 1 mm. When a target cannot be reached, the pipeline tries progressively coarser spatial welding followed by QEM, with maximum cell widths 80 / 160 / 320 mm for LOD0 / 1 / 2. Actual per-tile tolerances are recorded in `reduction` in the manifest. These are cleanup bounds, **not measured reconstruction accuracy**. Tiny boundary slivers retain up to 64 triangles at lower LODs; disconnected remnants may retain up to 1,024 excess triangles per tile/LOD, explicitly recorded and independently checked. Region budgets remain hard limits. Inspect previews and structural landmarks for seams, thin-structure loss and shifted edges before inspection use.

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

Only runtime GLBs, marker configuration and the manifest are transferred. Tiles go into a manifest-hash revision directory; the manifest is activated last, so interrupted transfers cannot replace files referenced by the previous manifest. Device destination: `/sdcard/Android/data/com.mrtwin.inspector/files/twin/HGE_2022-02-06/`. Previous tile revisions are retained. Restart the app after replacing a dataset. A missing external dataset uses the packaged hall LOD2 demo.

**[HUMAN]** Grant the camera, scene-understanding and hand-tracking permission prompts. Print `tools/markers/marker_00.png` on A4 at 100%, check the black square is 150 mm, and place it at a surveyed pose. Follow [marker measurement instructions](../tools/markers/README.md). Shipped coordinates are examples with `measured:false`; automatic headset alignment deliberately rejects them. A marker must remain tracked for one second before confirmation.

## Build, install and launch

Open `Assets/_TwinInspector/Scenes/Inspector.unity`. The scene uses the imported XRI Hands rig and an Editor-only XR Device Simulator. The generated assets are committed; `TwinInspector → Rebuild application assets and Inspector` reproduces them through Editor APIs if needed. This command replaces Inspector, so save any intentional scene edits first.

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

## Use and Editor simulation

Enter Play mode in Inspector. The Editor loads Region A from `Settings.editorDataPath` and substitutes neutral grey for camera passthrough. The packaged LOD2 demo is used when external data is absent. The simulator is excluded from Android builds.

Focus the Game view for keyboard input. **Tab** cycles the simulated left controller, right controller and headset. **W/A/S/D** move on the horizontal plane, **Q/E** move vertically, **R** toggles mouse translation/rotation, and **V** resets the selected device. Hold **right mouse** to manipulate the headset, **left Shift** for the left controller, or **Space** for the right controller. **H** switches hand/controller simulation. The complete bindings remain editable in the imported `XR Device Simulator Controls` input asset. Ordinary mouse clicks also operate the inspection panels.

| Action | Input |
|---|---|
| Summon the inspection panel | Left palm up, controller menu, or **F1** in Editor |
| Compare | Mode → Reality (passthrough) / Twin / Overlay; drag the blend slider; switch Technical / Colour |
| Align manually | Align → hold both grips or pinch both hands; translate and turn together; height stays locked |
| Adjust precisely | Fine adjustment: ±1 cm / ±0.5°, or coarse ±5 cm / ±2° |
| Set height | Height / floor: unlock, nudge height, or snap to a tracked horizontal floor |
| Align by marker | Use marker, hold it in view for one second, inspect the overlay, Confirm |
| Simulate a marker | **F2** in Editor; no physical camera or measured pose claim |
| Annotate | Annotate, then right-hand pinch/controller trigger on a twin or detected surface; choose one of four types |
| Editor annotation shortcut | **F3**, then **Alt + left click** on a visible mesh surface |
| Cycle view mode | **F4** in Editor |
| Select, retype or delete | List → select a row; the surface marker highlights and a directional prompt appears |
| Export | List → Export writes JSON and CSV, including original twin coordinates |
| Inspect performance | Setup → Diagnostics; unavailable CPU/GPU values say `n/a` |

Panels remain where summoned; the left palm strip follows the hand only while the palm faces upward. Controller/Editor use has a world-locked strip below the panel. Save alignment explicitly after manual adjustments. JSON fallback restores a pose in the previous session's world frame and must be checked after tracking-origin changes; a successfully restored persistent spatial anchor is the preferred path. The marker catalog intentionally rejects unmeasured example poses on the headset.

On first launch, an **unaligned** preview puts the hall's open interior around the viewer. Setup displays the alignment state. This initial placement is only for viewing; use a measured marker or manual registration before inspecting correspondence with the real building. Both faces of scanned triangles accept annotation rays, and annotation labels face the selecting viewer.

Data lives below `/sdcard/Android/data/com.mrtwin.inspector/files/`: `alignment/`, `annotations/`, `exports/`, and `logs/`. Annotation IDs remain stable, and deleting the highest index does not reuse its number. Exports do not leave the device automatically.

```powershell
& $adb pull /sdcard/Android/data/com.mrtwin.inspector/files/exports .\work\exports
& $adb pull /sdcard/Android/data/com.mrtwin.inspector/files/logs .\work\device-logs
& $unity command run_tests --mode editor --filter TwinInspector.Tests --filter_type assembly --async_tests true --project-path $project --result-only
& $unity command test_status --project-path $project --result-only
```

See [DEVLOG](docs/DEVLOG.md) for verified results and device-only checks, [DESIGN](docs/DESIGN.md) for visual tokens, and `docs/evidence/` for recorded validation outputs. Editor frame rates are not headset performance results.

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
