# Validation and implementation notes

## Current Multiply implementation

- **October 1, 2026:** The Chocolat scene avatar was compared with blush, blue shading, and tears. Full NDMF + VQT processing passed with VQT running in both the Transforming and Optimizing phases; static and animated output materials remained Multiply.
- Android plugin-pass checks covered lilToon / Poiyomi materials, nested BlendTrees, Override Controllers, multiple Animators sharing clips, and materials appearing only in animation.
- Unrelated clothing curves, opaque and null material references, keyframe times, and blendshape curves were preserved. Source material, mesh, clip, and controller serialization checks passed.
- Generated materials did not inherit source VRCFallback override tags. Converted avatars passed SDK 3.10.5 FindIllegalShaders checks.
- **October 1, 2026:** Chiffon, Milk-Re, Shinano, and Mafuyu were compared in original, VQT-only, and VQT + MEC variants. Converted variants ran the full NDMF pipeline. Face material slot, submesh, and blendshape counts were preserved.
- An initial Milk-Re test observed default parameter entries added to the source material's in-memory serialization. The observed changes were restored from captured JSON. The final test used independent material copies and passed source-data checks; no source asset changes were saved.
- Comparison images used temporary baked pose meshes because edit-mode SkinnedMeshRenderer updates were unreliable with manual Camera.Render. They were editor renderings, not Android screenshots. Only cropping, resizing, and layout were applied to the captured images.
- **October 2, 2026:** The project owner reported successful Android device testing of one processed avatar. This is owner-reported validation, not a claim that the developer performed the device test.
- iOS device testing has not been performed. Final SDK packaging, upload, and device testing were not repeated for every sampled avatar.

| Avatar | Face material slots / submeshes | Blendshapes |
| --- | --- | --- |
| Chocolat | 2 / 2 | 618 |
| Chiffon | 2 / 2 | 524 |
| Milk-Re | 2 / 2 | 222 |
| Shinano | 2 / 2 | 526 |
| Mafuyu | 3 / 3 | 237 |

Windows skip behavior, manual renderer selection, rejection of external renderers, and build-copy renderer remapping have also been checked in the editor.

## Code organization check

On October 2, 2026, runtime and editor sources were compiled separately using Unity 2022.3.22f1 Roslyn and the original project's dependency assemblies. Both compilations completed without diagnostics. Existing component, menu, shader, and asset GUIDs were preserved. This organization check did not repeat Unity importing, NDMF builds, or device tests.

## Build stages

1. **Resolving:** Record the original transparent expression materials.
2. **Transforming:** Process face material-switching animations after Modular Avatar / lilycalInventory and before VQT.
3. **Optimizing:** Recheck animated material references after VQT, then replace static material slots.

Animation handling uses NDMF Animator Services. Conversion caches belong to the current build. Replacements are scoped to the selected face renderer rather than replacing the same source material globally. NDMF collects generated materials and textures.

## Source layout

| File | Responsibility |
| --- | --- |
| MobileExpressionSettings.cs | Avatar component and face renderer selection |
| Editor/MobileExpressionPlugin.cs | NDMF stages, target selection, and static / animated material replacement |
| Editor/ParticleTextureConverter.cs | Material detection, parameter validation, and texture preparation |
| Editor/LilToonCompatibilityTexture.shader | Main color and alpha mask composition for lilToon / Poiyomi |
| Editor/MobileExpressionInspectors.cs | Inspector and automatic face selection |
| Editor/MobileExpressionMenu.cs | Avatar context-menu entry |
| Editor/Localization.cs | NDMF localization for English, Simplified Chinese, and Japanese |

## Historical implementation

An early implementation used Alpha Blended output. It was replaced after confirming that the shader was not on the Android avatar whitelist. Its editor appearance and animation checks do not count as Android release validation. The current implementation and published validation use Multiply.
