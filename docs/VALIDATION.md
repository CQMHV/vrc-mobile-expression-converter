# Validation and implementation notes

## Unreleased NDMF preview — October 18, 2026

The runtime and editor sources compiled with Unity 2022.3.22f1 against the existing CQMHVPinkChocolat project's NDMF 1.14.8 and SDK assemblies. An existing-project batch run passed 18 preview checks using disposable objects in an untitled batch scene. It exercised the initial disabled preview default (subsequently changed to enabled), target-group discovery, two meshes sharing a source with separate Multiply / Additive outputs, shared generated textures, preservation of opaque/null slots and original renderer/material/texture references, shape-only resource reuse, material-triggered rebuilds, material-switch invalidation, idempotent disposal, disabled-preview behavior and preservation of the build target.

The headless test explicitly pumped NDMF's editor synchronization queue because normal editor callback setup did not run reliably alongside this project's toolbar extensions in batch mode. This is a filter-level validation, not an interactive Scene view acceptance check. Temporary validation scripts and objects were removed after the successful run. No new Unity project was created and no scene asset was saved. Existing build conversion and mobile-target guards remain unchanged.


A subsequent live-editor check verified that newly added components enable NDMF Preview by default, the Chinese preview control reads NDMF 预览, and disabling the serialized toggle remains supported. The temporary preview scene was closed and the active scene retained its root count and dirty state. Unity recompiled the updated scripts without console errors.

Preview was subsequently restricted to Android/iOS build targets, sharing the build conversion platform predicate. A live-editor check verified Android/iOS acceptance and Windows rejection, one target group with the published mobile flag enabled and none with it disabled, and ComputeContext invalidation after the flag changed (explicitly pumping NDMF scheduling). The published flag was restored; the actual editor build target was not switched. Updated sources compiled without console errors.

## Version 1.2.0 multiple face renderers

On October 17, 2026, the runtime and editor sources compiled together with Unity 2022.3.22f1 Roslyn against the connected Test-Avatar project's assemblies. Thirteen checks ran in that editor using temporary objects in a preview scene: legacy single-renderer JSON, null additional arrays, multiple explicit selections, inactive meshes, empty and duplicate entries, additional-only selection, descriptor / multiple Body detection, serialized-property updates and JSON round trips, rejection of external renderers, and reference remapping in cloned avatars for both processing modes. Preview objects were removed, and the user's active scene kept its original root count and dirty state. No independent Unity test project was created.

These checks exercised renderer selection and serialization on the Windows editor target. The existing static-material collector and animated-material filter both consume the shared target selection; Android build processing and physical-device rendering were not repeated for this change.

After installation into Test-Avatar, Unity compiled the normal runtime and editor assemblies without console errors. Six further preview-scene checks exercised the unified Inspector list: one slot by default, adding an empty slot while preserving the first selection, avoiding duplicated references in new slots, removing the first mesh without changing the remaining order, removing another mesh, and retaining at least one slot. Existing serialized renderer fields remain unchanged so earlier selections survive the UI update.

The default was then restricted to one automatically detected mesh. Three checks against the installed assemblies confirmed that Body fallback skips meshes without mesh data and selects only the first valid mesh, Inspector detection fills one slot, and manually adding an inactive second mesh still selects both meshes. Extra meshes are selected explicitly, and are not automatically added to the default list.

Per-mesh modes were checked in the installed Test-Avatar package on both Windows and Android editor targets using preview-scene objects and an NDMF BuildContext with a NullAssetSaver. Ten Windows checks covered legacy global-mode fallback, independent mode initialization, default Multiply for added rows, paired removal, JSON serialization, conversion skipping, and source/scene preservation. Twenty Android checks exercised the actual MEC Resolve and Convert passes in both Multiply/Additive orders. Two meshes shared a source material, an animation-only material, and a BlendTree controller under different Animator roots. Each mesh received its own static and animated output shader, prepared textures were shared between modes, opaque/null keyframes and their times were preserved, and unselected mesh curves stayed unchanged. NDMF reported no conversion errors, and the source materials, clip, controller and user scene state were unchanged. This pass-level check did not build an Android application or repeat device testing.

## Version 1.1.1 pipeline fix — October 11, 2026

Unity 2022.3.22f1, SDK 3.10.5, NDMF 1.14.8, VQT 3.0.0, and Avatar Compressor 0.9.0 were used to check the revised build order. The actual NDMF resolver placed MEC's conversion in Transforming before VQT, and texture compression in Optimizing. There was no MEC Optimizing pass and no ordering cycle.

All 12 synthetic Android NDMF cases passed: Multiply / Additive, no VQT component / VQT Transforming / VQT Optimizing, and no compressor component / compressor enabled. The plugin assemblies remained installed in every case; these were component-presence checks, not package-removal tests. Static and animation-only output textures retained ASTC 6x6 compression when the compressor was enabled. Without it, generated textures remained RGBA32 as expected. Shared BlendTree clips, null and opaque material keyframes, keyframe times, the mesh's blendshape, and source materials and animation references were preserved.

A local SDK Android bundle was also built from a temporary copy of the saved MAYO scene. The saved avatar had no MEC component, so the test added one with the default Multiply mode only to the build copy. The resulting bundle was 10,365,495 bytes (9.885 MiB), below the 10 MiB download limit. The converted face overlay retained an ASTC 6x6 texture after compression and AAO processing. The processed face kept 135 blendshapes; jaw animation bindings referenced the renamed AAO shapes and produced approximately 20.03 mm of maximum world-space displacement at their recorded weights. SDK statistics reported 8 PhysBone components and 64 transforms. All 665 hashed source dependency files remained unchanged.

These checks validate the local build, texture references, and measurable jaw animation geometry. They do not simulate OSC input or confirm face tracking on a headset. Windows packaging and physical-device rendering were not repeated for this fix. The batch fixture, resolver output, bundle, and logs are retained in the local project's BuildAudit directory; avatar assets are not distributed with this package.

## Version 1.1.0 processing modes

On October 6, 2026, Unity 2022.3.22f1 compiled the updated runtime and editor assemblies in an isolated SDK 3.10.5 / NDMF 1.14.8 project. Full Android NDMF builds passed for both Multiply and Additive, including an animation-only material reached through a BlendTree. Static and animated materials used the selected shader and passed the SDK FindIllegalShaders check. Mesh/slot structure, UV transforms, unrelated opaque/null animation references, keyframe times, component GUIDs, and original resource serialization were preserved.

New components and legacy component JSON without a processing-mode field defaulted to Multiply. Both mode values survived serialized-property assignment and JSON round trips. Windows builds skipped material conversion for both selections. A Unity GPU rendering check applied a half-alpha white overlay to a 0.2 background: Multiply remained 0.2, while Additive produced approximately 0.702. This checks the shader blend behavior, not device performance or visual equivalence on every avatar. Additive has not been tested on a physical device.

The batch fixture and output logs are retained with the local release verification project. The fixture uses a minimal transparent lilToon-named shader to verify detection and build registration; it does not include VQT or the full lilToon / Poiyomi packages. Existing version 1.0.0 model and device checks remain applicable to the unchanged default Multiply conversion.

## Version 1.0.0 Multiply validation

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

## VPM import check

On October 2, 2026, the actual VPM ZIP was extracted into a separate Unity 2022.3.22f1 Avatar test project with SDK 3.10.5 and NDMF 1.14.8. Runtime and editor package assemblies compiled. The preserved component script GUID resolved to the component type, and a small synthetic transparent material fixture passed full Android NDMF processing, producing Multiply without inherited fallback tags while preserving its mesh, material slot, and source material.

The fixture did not install VQT, Modular Avatar, lilToon, or Poiyomi. Its minimal test shader exercised material detection and plugin registration, not visual equivalence to those shaders. Existing model checks and the owner's device feedback remain the visual validation. VPM package structure, archive scope, manifest/tag matching, AGPL-3.0-only metadata, and listing SHA-256 checks were also validated.

## Build stages

1. **Resolving:** Record the original transparent expression materials.
2. **Transforming:** Convert static face material slots and material-switching animations together, after Modular Avatar / lilycalInventory and before VQT.
3. **Downstream optimization:** Texture optimizers can collect the generated textures and update both renderer and animation references. MEC does not restore its cached materials afterward.

Animation handling uses NDMF Animator Services. Conversion caches belong to the current build. Replacements are scoped to the selected face renderer rather than replacing the same source material globally. Generated textures are registered immediately with NDMF's asset saver, which gives texture optimizers a build-asset path. Original textures are not saved or modified by this step. NDMF collects generated materials and textures.

VQT 3.0 preserves materials whose shaders are already allowed on mobile by default. An explicit material replacement remains the user's choice. Plugins that introduce face material-switching animations must run before MEC's Transforming pass; late creation of new animations is not covered by an additional restoration pass.

## Source layout

| File | Responsibility |
| --- | --- |
| Runtime/MobileExpressionSettings.cs | Avatar component, processing mode, and face renderer selection |
| Editor/MobileExpressionPlugin.cs | NDMF stages, target selection, and static / animated material replacement |
| Editor/ParticleTextureConverter.cs | Material detection, parameter validation, and texture preparation |
| Editor/LilToonCompatibilityTexture.shader | Main color and alpha mask composition for lilToon / Poiyomi |
| Editor/MobileExpressionInspectors.cs | Inspector and automatic face selection |
| Editor/MobileExpressionMenu.cs | Avatar context-menu entry |
| Editor/Localization.cs | NDMF localization for English, Simplified Chinese, and Japanese |

## Historical implementation

An early implementation used Alpha Blended output. It was replaced after confirming that the shader was not on the Android avatar whitelist. Its editor appearance and animation checks do not count as Android release validation. Release 1.0.0 used Multiply; version 1.1.0 adds the supported mobile Additive shader as an optional output mode.
