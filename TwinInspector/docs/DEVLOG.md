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
