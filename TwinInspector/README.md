# TwinInspector v1

Mixed-reality inspection of the LaMAR HGE NavVis scan on Samsung Galaxy XR. The v1 scope is CPU mesh decimation, marker/manual registration, persistent anchors, three comparison views and spatial discrepancy annotations. Unity 6000.3.26f1, URP, OpenXR, Android XR and glTFast.

**Build and device status:** implementation is in progress. Consult [docs/DEVLOG.md](docs/DEVLOG.md) for verified results. Do not infer a headset performance result from an Editor test.

## Prepare the data

From `E:\MixedReality`, use the provided `mr` environment:

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I tools\tests\test_twin_pipeline.py
& 'D:\miniforge3\envs\mr\python.exe' -I tools\twin_pipeline.py all --session dataset\raw\HGE\sessions\navvis_2022-02-06_12.55.11 --region a --out work\twin\HGE_2022-02-06\region_a
& 'D:\miniforge3\envs\mr\python.exe' -I tools\verify_tiles.py work\twin\HGE_2022-02-06\region_a
```

Repeat with `--region b` and `--out work\twin\HGE_2022-02-06\region_b` for the floor. The bounding boxes are the brief's measured A/B extents, not semantic indoor segmentation. Centroid clipping deliberately retains complete boundary triangles; a triangle can extend outside its cell or region.

The pipeline reads fixed-size PLY batches through read-only memory maps. Checkpointed batches and completed tiles are skipped on rerun. The `config.json` fingerprint rejects a different source/configuration in an existing output. Choose a new output directory for changed settings. `--force` reruns the selected stage; `all --force` rebuilds all stages. Do not run two writers against the same output directory. `--target-ratio` defaults to 0.013 and is reduced automatically if the region triangle budget would be exceeded. Large chunks are simplified in bounded face runs and welded before the final target pass.

Each compact tile is simplified as a whole up to eight million triangles (measured 2.57 GB peak for a 6.33 M triangle tile). Larger tiles use spatial median splits that preserve surface connectivity. Initial welding is 1 mm. When a target cannot be reached, the pipeline tries progressively coarser spatial welding followed by QEM, with maximum cell widths 80 / 160 / 320 mm for LOD0 / 1 / 2. Actual per-tile tolerances are recorded in `reduction` in the manifest. These are cleanup bounds, **not measured reconstruction accuracy**. Tiny boundary slivers retain up to 64 triangles at lower LODs. Inspect previews and structural landmarks for seams, thin-structure loss and shifted edges before inspection use. The pipeline fails if a larger target cannot be met within its cleanup bound.

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

Build, simulator, interaction and export commands will be completed alongside the verified Unity implementation.

## Attribution and dependencies

Source geometry: **LaMAR**, ETH Zurich and Microsoft, HGE scene, scan `navvis_2022-02-06_12.55.11`, [lamar.ethz.ch](https://lamar.ethz.ch/), **CC BY-SA 4.0**. Derived geometry changes: region/cell clipping, 1 mm welding, CPU quadric decimation, nearest-source vertex colour sampling and coordinate conversion. Redistributed derived tiles retain CC BY-SA 4.0; see the fallback's LICENSE.txt. LaMAR's reference poses were generated using SuperPoint/SuperGlue; this app's v1 registration uses measured markers and manual alignment.

Inter 4.1 by Rasmus Andersson: SIL Open Font License, bundled with its license. Google Extensions and Unity sample notices remain with their packages/imported samples. The `System.Runtime.CompilerServices.Unsafe` compatibility plugin must remain: see its adjacent README. No CUDA reconstruction is used.

References: [Unity Android XR](https://docs.unity3d.com/Packages/com.unity.xr.androidxr-openxr@1.4/manual/index.html), [Google Extensions](https://github.com/android/android-xr-unity-package), [GPU rendering guidance](https://developer.android.com/develop/xr/unity/performance/gpu-rendering), [glTFast runtime loading](https://docs.unity3d.com/Packages/com.unity.cloud.gltfast@6.20/manual/ImportRuntime.html).
