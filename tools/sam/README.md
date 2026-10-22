# MobileSAM build tools

The app runs MobileSAM locally on a colour rendering of the **old twin**. It does not acquire a passthrough image, identify real-world changes, or contact an inference server. Geometry comparison remains independent of SAM.

**Release status: experimental, disabled by default.** Functional column/wall and cancellation tests pass, but the installed package's first GPU dispatch blocked the Editor for about 30 seconds and warm scheduling also exceeded the strict 4 ms target. The iterable API cannot preempt an individual layer/driver compilation. `Settings.objectPickEnabled=false` is the v1.3 fallback authorized by the brief. To evaluate explicitly, enable that field in the Settings asset before Play/build, with external models present; run `pick_qa_v13.cs` and `pick_cancel_v13.cs`, then restore the release gate with `sam_release_gate_v13.cs`. Measure cold/warm frames and total GPU memory on Galaxy XR before enabling it for deployment. See `sam-performance-v13.txt` for the failed performance gate and `pick-v13.txt` for functional results.

Upstream: [official MobileSAM repository](https://github.com/ChaoningZhang/MobileSAM), revision `f706ad9c4eb7f219c00d9050e46328518ffb65d2`.

Checkpoint: [mobile_sam.pt at the pinned revision](https://raw.githubusercontent.com/ChaoningZhang/MobileSAM/f706ad9c4eb7f219c00d9050e46328518ffb65d2/weights/mobile_sam.pt), SHA-256 `6dbb90523a35330fedd7f1d3dfc66f995213d81b29a5ca8108dbcdd4e37d6c2f`. The official decoder export script is fetched from the same revision, SHA-256 `611adce2b32d126ec08f9ec3788100e7c1b947e1030156031bf22d5329c70482`.

Reproduce from the repository root (PowerShell):

```powershell
& 'D:\miniforge3\envs\mr\python.exe' -I -m venv work/sam-venv
& ./work/sam-venv/Scripts/python.exe -I -m pip install -r tools/requirements-sam.txt
& ./work/sam-venv/Scripts/python.exe -I tools/sam/export_mobilesam.py
```

Export is CPU-only, with two encoder/validation threads. The decoder uses the pinned repository exporter at ONNX opset 17. The permitted read-only NavVis photo is `00000-cam0__center.jpg`; the column prompt is `(260,1100)` in original pixels. Validation requires a nonempty mask and predicted IoU above 0.5. That score is model confidence, not measured accuracy. Exact inputs, hashes and result are in `TwinInspector/docs/evidence/sam-export-v13.txt`.

For serialization, temporarily copy both `work/sam/*.onnx` into `TwinInspector/Assets/_TwinInspector/ModelsImport/`, let Unity import them, and run **TwinInspector → Serialize SAM models**. Unity Inference Engine/Sentis **2.6.1** supplies `Unity.InferenceEngine`, `Worker`, `BackendType.GPUCompute`, `ScheduleIterable`, `ModelLoader.Load(string)` and `ModelWriter.Save`. Then run `tools/unity/finish_sam_import_v13.cs` through the Unity CLI to remove only those temporary import assets. Both runtime files are reloaded before cleanup. Nothing under `ModelsImport` belongs in a release commit or APK.

The first attempt with package 2.6.0 reported invalid Unity Hub authentication. The retry with compatible patch 2.6.1 succeeded through the official package manager; no alternate registry or authentication bypass was used. Other existing package versions remain pinned.

Deploy with `tools/push_twin.ps1 -Models` after installing and launching the app. Model hashes are verified before transfer and on app startup. Missing or corrupt models hide Pick object while ordinary inspection continues. See [model contract](models/MODELS.md).
