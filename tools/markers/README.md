# Alignment markers

Run `D:\miniforge3\envs\mr\python.exe -I tools/generate_markers.py` to reproduce the A4 sheets. Print at **100% / actual size**, with fit-to-page disabled. Measure the outer black ArUco square: 150 mm. The QR's symbol excluding its white quiet zone is also 150 mm. The PNGs contain 300 dpi physical-resolution metadata.

The two shipped entries in `markers.json` are **unmeasured examples**. They are enabled for simulator testing only; the headset refuses automatic alignment to entries whose `measured` flag is false.

1. Choose a flat, stable wall visible in the scan; avoid doors, furniture and glass. Keep the print vertical, top edge horizontal.
2. Locate the marker centre in the aligned LaMAR world frame using the pipeline preview and original mesh. Record X, Y, Z in metres (Z up), not glTF or Unity coordinates. Confirm position with three measurements to stable building features.
3. Record the outward-facing surface normal in `normal_twin`. For wall prints this is a horizontal unit vector. `yaw_deg` records the surveyed heading; for a horizontal print it defines printed-top direction in the XY plane. The runtime derives wall yaw from the outward normal so pitch and roll remain gravity aligned.
4. Set `measured: true`, retain `size_m: 0.15`, update the note and push with `tools/push_twin.ps1`. QR content is `TWIN-01`; its identifier in JSON is `QR:TWIN-01`.
5. Look at the marker continuously for at least one second, review the overlay and confirm. Residual is shown as n/a because one marker supplies a pose, not an independent error estimate. Check a second asymmetric landmark: the rotunda is west (negative X).

Camera tracking accuracy and persistent-anchor relocalization require headset verification.
