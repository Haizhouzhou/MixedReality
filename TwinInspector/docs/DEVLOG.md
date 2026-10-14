# TwinInspector development log

## 2026-10-14 — implementation plan

Read the course proposal, implementation brief, existing memory-mapped PLY reader, installed Android XR permission/camera/anchor documentation, and Google Extensions changelog and image/passthrough samples. Preserve the configured package versions and Unsafe compatibility plugin. Do not access HoloLens session directories.

1. Implement resumable bounded-memory CPU chunking, QEM LOD generation, GLB export, manifest, previews and independent verification. Process and verify Region A first.
2. Configure Unity through the CLI and Editor APIs, import interaction samples, implement the twin coordinate adapter and budgeted tile loader, and verify in the simulator.
3. Build the Swiss token/prefab system, view controls, manual/marker alignment, persistent anchors and annotations. Test coordinate round trips and persistence.
4. Build the Android APK and process Region B; document results, installation and the two device-only checks. Commit milestones on main.

Initial observations: repository has no commits; CLI cannot yet see an Editor instance from the restricted shell; adb daemon startup needs investigation. No headset result has been obtained. Google Extensions 1.4 removed its generic image provider, but retains marker and QR support via its sample's reference-image library conventions.

All device performance, passthrough, physical marker alignment and anchor relocalization results remain **unverified** until measured on a connected headset.
