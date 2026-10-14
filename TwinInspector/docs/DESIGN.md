# TwinInspector design system

Swiss / International Typographic Style: opaque paper, ink typography, square controls, explicit words, one active red accent and a consistent 8 mm rhythm. This document records implementation decisions; the Editor contact sheet will be added after rendering the finished panels.

## Tokens and physical scale

| Token | sRGB | Role |
|---|---|---|
| Ink | `#111111` | Text, rules and primary controls |
| Paper | `#F2F1EC` | Opaque panel background |
| Paper 2 | `#E6E4DD` | Secondary surfaces / twin-only background |
| Muted | `#6F6E69` | Non-text separators and disabled controls |
| Accent / Missing | `#E30613` | Active action / missing geometry |
| New | `#0046E6` | New geometry annotation |
| Changed | `#F2B600` | Changed geometry annotation, ink label |
| Note | `#6F6E69` | Note annotation |
| Twin tint | `#1F4FD8`, 45% | Technical overlay fill |
| Twin edge | `#111111`, 70% | Derivative-based surface edges |

Text uses ink on paper (or paper on ink), including secondary text: the provided muted colour does not meet the brief's 7:1 text contrast requirement. Annotation colours communicate type but their labels use ink/paper.

Panel space is millimetres, at a world scale of 0.001. Standard panel 480 × 320 mm; outer margin 24, gutter 16, controls 48 high, square slider handle 12, separator rules 1. Long content scrolls rather than shrinking. World-locked placement is 1 m ahead, 0.10 m below eye height, gravity aligned. A hand strip is the sole hand-anchored surface. No head-locked panels.

## Typography and legibility resolution

Inter 4.1, SIL OFL. Regular 400, Medium 500, SemiBold 600. TextMeshPro SDF atlases: 2048², sampling 64, padding 8. Numeric readouts use tabular figures. No default UI sprites or fallback system typefaces.

The brief's nominal 12 mm body is only 0.69° at 1 m, below its own 1.2° minimum. Therefore body/control text is **21 mm**, with 32 mm display, 24 mm title, 12 mm uppercase section labels with 6% tracking and 10 mm metadata. Body line height is 1.3. Layouts paginate/scroll to preserve readable body size. Scale the entire panel by viewing distance if spawned farther away.

## Components and state

One PanelBase supplies paper, header, content area and bottom toast. Buttons invert immediately on hover/focus; selected segments are ink filled. There are no radii, shadows, gradients, decorative illustrations or icon fonts. Show/hide motion lasts 150 ms, at most 20 mm. The four tool-strip actions are Mode, Align, Annotate, List.

Annotations use a 40 mm surface disc, 1 mm ink ring, 80 mm × 1 mm leader and a short numbered label. Selected annotations invert; the list selection reveals a directional look-here arrow. Annotation marker colours are the only multi-colour semantic set.

Colour style renders the dataset's sampled vertex colour. Technical style uses blue unlit fill and derivative-based edge emphasis. Blend is a global shader property, with no per-renderer updates. Twin-only uses an opaque paper-2 background and a metre grid.

## Verification

Contact sheet, actual font assets and rendered panel checks are pending the Unity implementation. This is not a claim of completed visual QA.
