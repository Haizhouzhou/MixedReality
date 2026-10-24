# TwinInspector

TwinInspector is a Unity application for inspecting a scanned building on Samsung Galaxy XR. It combines a digital twin with camera passthrough, supports manual and surveyed-marker alignment, and stores inspection notes in the building's original coordinate frame. The same project runs in the Unity Editor with a mouse and keyboard.

**Version 1.4.1** · Android XR · Unity 6000.3.26f1

[Releases](https://github.com/Haizhouzhou/MixedReality/releases) · [Setup and development guide](TwinInspector/README.md) · [Development notes](TwinInspector/docs/DEVLOG.md)

![TwinInspector comparison controls above the four-part spatial tool strip](TwinInspector/docs/screenshots/v12-mode.png)

*The inspection panel sits above the tool strip in a shared world-space dock. All images on this page are captures from the Unity Editor.*

## Inspection workflow

v1.3 adds automatic geometry-change candidates, region annotations, OBJ refinement exports, a six-card guide, four UI languages, an experimental MobileSAM object selector and opt-in depth/camera diagnostics. English and German terminology are reviewed against the project glossary; French and Italian are machine translated, unreviewed. German review was performed by the implementation agent, not a human translator.

v1.4 adds inspection spaces. The app starts by asking which space to inspect — **HG Main Hall**, **CAB Main Hall**, **HG E3 Hall** or **HG E7 Hall** (Skip opens HG Main Hall) — and a gear cell left of the tool strip opens **00 Setup** from any view to switch space after a confirmation, open Preferences (including Language), the Guide, or close the panel. Each space keeps its own annotations, alignment and dismissed candidates, even when cut from the same scan. HG E3/E7 Hall are not packaged in the APK and stay greyed out until their data is transferred. The interface starts in English and offers five languages: English, 中文, Deutsch, Français and Italiano. Chinese is machine translated by the implementation agent and unreviewed; its glyphs come from a subset of Noto Sans SC (SIL OFL 1.1).

| Candidate review | German interface |
| --- | --- |
| ![Confirm or dismiss a detected change](TwinInspector/docs/screenshots/v13-picker-candidate.png) | ![Four-language comparison controls](TwinInspector/docs/screenshots/v13-de-mode.png) |

With Diff enabled, the app compares observed scene surfaces against the old twin: **New**, **Missing**, or paired **Changed**. Candidates are suggestions requiring confirmation; alignment error, incomplete observation and mesh quality affect them. Unobserved or unloaded regions are excluded conservatively. Realignment invalidates pending results. The Editor reproduces deterministic planted changes using external Region A.

After placing a fresh point annotation, **Pick object** optionally segments the old twin's rendered image with MobileSAM and attaches a region. It preserves the selected type. It does not detect changes from passthrough images. Missing or corrupt external model files hide this action; the rest of the app still works.

**SAM is disabled by default in this release.** Its functional prototype passes column/wall selection and cancellation tests, but cold GPU dispatch in the installed package exceeds the strict frame budget. The complete code and external models are included for explicit evaluation; see [status and re-enable procedure](tools/sam/README.md). Automatic geometry candidates remain enabled and require no neural model.

The four tool sections cover comparison, registration and annotation. Setup provides handedness and feedback preferences; Diagnostics shows the available runtime measurements. A gear cell left of the four tools opens **00 Setup** from any view: switch space (after a confirmation, the app reloads), Preferences (including Language), Guide and Close.

| Section | Use |
| --- | --- |
| **01 Mode** | Switch between Reality, Twin and Overlay. Choose Colour or Technical rendering, adjust opacity, or enable the local inspection Lens. |
| **02 Align** | Register the twin with a single-hand drag, optional two-hand translation/yaw, measured marker poses, or numerical adjustments. Height is locked by default. |
| **03 Annotate** | Place a surface note and classify it as Missing, New, Changed or Note. Each type has its own shape and symbol. |
| **04 List** | Review confirmed annotations and candidates; confirm/dismiss, retype, delete, and export JSON/CSV plus nearby New/Changed scene-mesh OBJ patches. |

In Twin mode, the Roam page provides movement and snap turns. Returning to Overlay or Reality restores the physical inspection frame; roaming does not alter saved building alignment.

## Architectural views

Colour retains the scan's sampled colours. Technical uses restrained surface shading, crease and silhouette lines, and elevation contours at 0.5 m intervals. Both views use the same measured geometry, including its reconstruction defects.

| Colour | Technical |
| --- | --- |
| ![HGE hall with sampled scan colours](TwinInspector/docs/screenshots/v12-hall-colour.png) | ![Same hall and camera with architectural edges and elevation contours](TwinInspector/docs/screenshots/v12-hall-technical.png) |

The datum grid uses 1 m crosshair ticks and 10 m major lines. It follows the building's original coordinates and appears in Twin mode and during manual alignment.

Lens is an optional Overlay control. It reveals the twin inside a sphere around the pointed surface, with a default radius of 1.5 m and an adjustable range of 0.5–5 m. The last position is retained while operating the controls; losing the surface briefly holds and then fades the reveal.

| Lens at a nearby surface | Lens at a distant surface |
| --- | --- |
| ![Local reveal around a nearby floor and column](TwinInspector/docs/screenshots/v12-lens-near.png) | ![Smaller apparent reveal at a more distant surface](TwinInspector/docs/screenshots/v12-lens-far.png) |

*The sphere has the same physical radius in both captures. Grey outside the reveal is the Editor background, not a headset passthrough recording.*

## Annotations and interaction

Annotations retain their surface position, normal and stable ID when their display size changes. Glyphs and labels face the viewer and remain readable at inspection distances; their selection targets are independent of the building mesh. Type identity uses both geometry and text:

| Type | Glyph | Colour |
| --- | --- | --- |
| Missing | Circle with a minus | Red |
| New | Square with a plus | Blue |
| Changed | Triangle with a delta | Yellow |
| Note | Diamond with a dot | Grey |

| Type selection | Annotation list |
| --- | --- |
| ![Four annotation types with distinct geometric symbols](TwinInspector/docs/screenshots/v12-picker.png) | ![Annotation list with type, index, distance and editing controls](TwinInspector/docs/screenshots/v12-list.png) |

Hands use pointing and pinch, with near poke for UI. Optional controllers use the same interaction path. The dock (tool strip and panel) keeps a saved offset from your body direction, by default 30° to the right, 60 cm away and 20 cm below eye level; drag its grab bar to move it, or set the offset in Setup → Preferences → Menu position. Either hand can summon it with a brief palm-up gesture and then operate it. The pin cell at the strip's right end keeps it visible; unpinned, it fades 2 s after the hand is down and you look away, until the next palm-up. The palm gesture can be turned off there (the dock then stays pinned). A persistent dominant-hand preference selects the default pointer without requiring that hand to remain tracked. Sound can be adjusted or muted without losing visual feedback.

## Run the application

### Unity Editor

1. Clone this repository and open the **`TwinInspector` folder** in Unity **6000.3.26f1**. Keep the committed package versions.
2. Open `Assets/_TwinInspector/Scenes/Inspector.unity`.
3. Enter Play mode and focus the Game view. The packaged 32-tile hall demo loads when the configured external dataset is unavailable.

No XR Device Simulator key combinations are needed for desktop testing.

| Input | Action |
| --- | --- |
| Right mouse + mouse movement | Look around |
| W / A / S / D | Move horizontally |
| E / Q or Space / Left Shift | Move vertically |
| Alt; right mouse + scroll | Increase or adjust movement speed |
| Left mouse | Operate UI or place/select an annotation |
| 1 / 2 / 3 / 4 | Mode / Align / Annotate / List |
| Tab | Show the dock at its saved offset |
| M | Simulate the first marker entry for Editor testing |

### Samsung Galaxy XR

The intended deployment is a **standalone headset application**. A computer prepares the scan, installs the APK and transfers tiles/models; ordinary inspection then runs locally without a continuous PC connection or an inference server. Reconnect to update data, retrieve exports or profile the app. Headset scene meshing, tracking and performance still require physical acceptance; a successful Android build does not verify them.

Enable USB debugging on the headset, connect it to the development computer, and accept the authorization prompt. Build the APK in Unity (File → Build Profiles → Android → Build) or download it from [Releases](https://github.com/Haizhouzhou/MixedReality/releases) once published; with Android SDK platform tools on your path, run:

```powershell
adb devices -l
adb install -r TwinInspector-1.4.1.apk
adb shell am start -n com.mrtwin.inspector/com.unity3d.player.UnityPlayerGameActivity
```

Grant the requested camera, scene-understanding and hand-tracking permissions. The APK includes LOD2 copies of HG Main Hall and CAB Main Hall; full-detail data and the HG E3/E7 Hall spaces come from the [space data release](TwinInspector/README.md#download-space-data-from-github-releases). See the [project guide](TwinInspector/README.md#headset-setup-and-data-transfer) for full-region transfer and surveyed-marker setup.

The application version is **1.4.1** (Android version code **5**) and the package ID is **`com.mrtwin.inspector`**; a published release lists the APK's size and SHA-256. Installing with `-r` updates an existing installation while preserving its data, provided it was signed with the same key; an APK built on another computer must be uninstalled first, which deletes the app's data folder. The previous v1.3 build remains at `TwinInspector/Builds/TwinInspector-0.1.0.apk`. Repository access is required to download these private builds.

## Verification status

v1.4.1 (dock placement, pin and palm summon) was checked on 24 October 2026: the Runtime, Editor and Test assemblies compile with 0 errors, the 11 Python tests pass, and `tools/check_text_fit.py` measures 219 labels and readouts in five languages within 95% of their boxes. The user tested the 1.4.1 build on the Galaxy XR and reported no problems; the new `DockTests` were written for the Unity Test Runner, but no run of them was recorded here.

v1.4 was checked on 24 October 2026 (Europe/Paris): the Runtime, Editor and Test assemblies compile with Unity's compiler with 0 errors, the 11 Python tests pass, and the four packaged/transferred spaces pass the tile seam and budget verifiers. On the Galaxy XR, the start-up space choice loaded CAB Main Hall and HG Main Hall (its full Region A data), and Space switching tore down HG Main Hall and started HG E3 Hall; that run exposed a marker-loading fault for unpackaged spaces, after whose fix HG E3 Hall loaded its transferred data. The Unity EditMode tests (including the new space and language tests) were not run for this release, and HG E7 Hall, Chinese rendering and per-space annotation persistence were checked by the user on the headset without log evidence. The v1.3 results below still describe the unchanged inspection features.

The v1.3 build was checked on 23 October 2026 (Europe/Paris).

| Check | Result |
| --- | --- |
| C# compilation | 0 errors, 0 warnings |
| Shader compilation | 0 errors |
| Android OpenXR project validation | 0 issues |
| Unity / Python tests | 32 / 11 passed |
| Desktop interaction | Mouse UI, placement, flight and shortcuts passed |
| Synthetic XRI workflows | Left hand, right hand, left controller and right controller passed independently |
| Persistence and export | Legacy/v2 annotations, region reload, JSON/CSV and scene OBJ patch export passed |
| Geometry fixture | All three edits found: 2 Missing, 1 New, 1 Changed; 1 other candidate |
| Guide and languages | First-launch/reopen passed; 21 states × 4 languages, zero overflow or missing keys/glyphs |
| Experimental SAM | Functional column/wall tests passed; disabled by default because GPU dispatch exceeds frame budget |
| Android package | Build succeeded; ARM64 IL2CPP and all 32 bundled tile hashes verified |

The build reports 354 warnings: 353 from Inference Engine shaders (including unsupported compute variants) and one existing Pipeline Editor-service warning. TwinInspector scripts have zero warnings. SAM stays disabled; these package warnings and device behavior still require evaluation before enabling it. No headset was connected during this verification. Physical hand/controller tracking, near poke, both-eye rendering, runtime OpenXR logs, anchor relocalization and device performance still need a headset acceptance run. The **72 FPS minimum, 90 FPS target and 300-draw-call budget are targets, not measured results**.

Full-detail space data (HG Main Hall, HG E3 Hall, HG E7 Hall, CAB Main Hall) is published as zips in the [`data-2026-10-24` release](https://github.com/Haizhouzhou/MixedReality/releases/tag/data-2026-10-24); see [Download space data](TwinInspector/README.md#download-space-data-from-github-releases) for the transfer steps.

Detailed [test evidence](TwinInspector/docs/evidence/), [screenshots](TwinInspector/docs/screenshots/) and the [device acceptance procedure](TwinInspector/README.md#device-acceptance-run) are included in the repository.

## Repository layout

```text
TwinInspector/                       Unity project
  Assets/_TwinInspector/             Application code, shaders, prefabs and scene
  Assets/StreamingAssets/twins/       Packaged spaces (HG_MainHall, CAB_MainHall) and spaces.json index
  Builds/TwinInspector-0.1.0.apk      Previous v1.3 build (v1.4.0 is attached to its GitHub release)
  docs/                              Development notes and verification evidence
tools/                               Scan processing, deployment and Unity checks
```

Raw scans, intermediate meshes, Unity caches and local working data are excluded from Git. The [project guide](TwinInspector/README.md) documents the coordinate conventions, build process and external tile pipeline.

## Credits

The hall geometry is derived from the **LaMAR HGE** dataset by ETH Zurich and Microsoft, scan `navvis_2022-02-06_12.55.11`. Redistributed tiles retain **CC BY-SA 4.0**; the [bundled notice](TwinInspector/Assets/StreamingAssets/twins/HG_MainHall/LICENSE.txt) records the source and processing changes.

The CAB entrance hall (`CAB_MainHall`) is derived from the **LaMAR CAB** dataset, scan `navvis_2022-06-25_11.14.36`, cropped to the main hall; its [notice](TwinInspector/Assets/StreamingAssets/twins/CAB_MainHall/LICENSE.txt) records the same CC BY-SA 4.0 terms.

The interface uses **Inter 4.1** by Rasmus Andersson under the SIL Open Font License. Unity, Google XR and other dependency notices remain with their packages and imported samples.

The experimental object picker uses [MobileSAM](https://github.com/ChaoningZhang/MobileSAM), Apache-2.0. Model provenance, hashes and the upstream license accompany the [external serialized models](tools/sam/models/MODELS.md).
