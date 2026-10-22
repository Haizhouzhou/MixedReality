# External MobileSAM models

These serialized models are generated from the official Apache-2.0 MobileSAM checkpoint, revision `f706ad9c4eb7f219c00d9050e46328518ffb65d2`. See [export provenance](../README.md) and [upstream license](../LICENSE).

The v1.3 release disables object picking by default because the package's cold GPU dispatch exceeds its frame budget. These models support explicit prototype evaluation; transferring them alone does not override the release flag.

`models.json` is the runtime source of filenames, SHA-256 integrity hashes and preprocessing constants. Both `.sentis` files are below 50 MB. They live outside Unity Assets and are never bundled in the APK.

Input: one RGB image, float32 NCHW `1×3×1024×1024`, 0–255 values normalized by mean `[123.675,116.28,103.53]` and standard deviation `[58.395,57.12,57.375]`. For arbitrary photos, resize the longest side to 1024, normalize, then zero-pad bottom/right. Runtime captures are already square. Pixel coordinates use a top-left origin.

The encoder yields `1×256×64×64` embeddings. The decoder receives one positive point plus a padding point (`labels=[1,-1]`), an empty `1×1×256×256` mask, `has_mask_input=0`, and original size `[1024,1024]`. The complete dynamic decoder imports successfully in 2.6.1. Runtime uses `low_res_masks`, bilinearly samples its logits and thresholds at zero; no dynamic Resize workaround was required.

Back-projection samples a 64×64 grid, rejects depths beyond `max(0.5 m, 25% of prompt distance)`, and requires at least 12 accepted hits. A conservative normal-coherence gate additionally excludes surfaces more than 30° from the prompted surface (sign-independent for two-sided meshes). The region is fitted to actual accepted mask samples, with adjacent sample triangles and a 0.5 m discontinuity cutoff; original LOD2 triangle references are retained separately. This produces a visible surface patch rather than guaranteeing a complete semantic 3D object, and can omit sharp side faces. The gate was added after a real test exposed bleeding onto an adjoining angled surface.

On the headset these files belong in `/sdcard/Android/data/com.mrtwin.inspector/files/twin/HGE_2022-02-06/models/`. On this workstation the configured path is `E:/MixedReality/tools/sam/models`. `tools/push_twin.ps1 -Models` transfers only the two models and their manifest. Restart the app after transfer.

Headset latency, GPU memory and selection quality remain unverified until physical device acceptance. A model-predicted IoU score is not an accuracy measurement against annotated ground truth.
