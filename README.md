# Mobile Expression Converter

Adapt transparent facial effects for VRChat Android avatars with NDMF.

Mobile Expression Converter (MEC) converts supported lilToon and Poiyomi face materials to `VRChat/Mobile/Particles/Multiply` during the avatar build. It also updates material-switching animations while preserving the mesh, UVs, bone weights, blendshapes, and material slot count and order.

**This is an approximate conversion.** Blush, blue shading, and other dark overlays work well with Multiply. White tears and highlights may become faint or disappear.

## Installation with VCC / ALCOM

Open the [VPM installation page](https://cqmhv.github.io/vrc-mobile-expression-converter/) and click **Add to VCC / ALCOM**. You can also add this repository URL manually in the package manager:

```text
https://cqmhv.github.io/vrc-mobile-expression-converter/index.json
```

Ensure that the official VRChat and [NDMF repository](https://vpm.nadena.dev/vpm.json) are available to your package manager, then add **Mobile Expression Converter** to an Avatar project. The manifest declares the SDK and NDMF dependencies.

For manual installation, download the VPM ZIP from [Releases](https://github.com/CQMHV/vrc-mobile-expression-converter/releases/latest) and extract it to `Packages/com.cqmhv.mobile-expression-converter`. Install the required dependencies first. If upgrading from the old Assets distribution, remove `Assets/MobileExpressionConverter` before manual installation; VPM uses the legacy folder migration metadata to remove it automatically.

Releases also include an embedded `.unitypackage` for **Assets → Import Package → Custom Package**. Like the VPM ZIP, it installs to `Packages/com.cqmhv.mobile-expression-converter` and requires the SDK and NDMF dependencies to be installed first. Use one installation method. Remove the old Assets copy before importing manually.

The package includes the component source, documentation, and license. It does not bundle SDKs, shader packages, or avatar assets. There is no bundled legacy Assets copy.

### Tested environment

| Dependency | Tested version |
| --- | --- |
| Unity | 2022.3.22f1 |
| VRChat SDK Avatars | 3.10.5 |
| NDMF | 1.14.8 |

These are tested versions, not a claim of compatibility with earlier versions. Modular Avatar is not a direct dependency. The source lilToon or Poiyomi shaders must be available in your project; MEC does not directly reference their editor assemblies.

**VRCQuestTools (VQT)** is optional. It can convert the rest of your avatar's materials. MEC only adapts the selected face renderer and does not make the entire avatar Android-compatible by itself.

## Usage

1. In the Hierarchy, right-click the avatar root with a **VRC Avatar Descriptor** and choose **VRChat → Mobile Expression Converter**. You can also use Add Component.
2. Check the **Face renderer** field. MEC first detects the descriptor's **LipSync → Face Mesh**, then a `Body` SkinnedMeshRenderer, case-insensitively. You can manually assign another renderer inside the same avatar.
3. Select the **Android** build target and build your avatar normally. NDMF processes the build copy automatically.

The face field is filled when there is one valid candidate. An existing selection is preserved. Adding the component supports Undo and does not create duplicates. The Inspector supports English, Simplified Chinese, and Japanese.

## Supported scope

- Transparent lilToon materials and Poiyomi **Fade / Transparent** materials on the selected face renderer.
- Material-switching animations, including nested BlendTrees, Override Controllers, and materials used only in animations.
- Main texture color and alpha, static alpha masks, and supported UV transforms.
- lilToon alpha mask modes 0–4, strength, and offset.
- Poiyomi Toon 9.3 / 10.0 parameters: default red-channel masks, strength, offset, inversion, ignored main-texture alpha, Alpha Mod, and Cutoff.

Opaque, Cutout, Poiyomi TransClipping, Additive, and Multiply source modes are not converted. MEC does not require specific avatar or blendshape names, or require the mesh to have blendshapes.

The original Prefab, Mesh, Material, Texture, AnimationClip, and AnimatorController assets are preserved. Windows builds skip conversion. Unrelated animation curves, opaque and null material references, and keyframe times are preserved.

## Limitations

Multiply uses alpha to control how strongly the texture color multiplies the rendered background. Alpha 0 leaves it unchanged; alpha 1 applies the full multiplication. It can darken the background, but cannot paint a bright white overlay. This is why white tears and highlights may become very faint or disappear. The original lilToon / Poiyomi lighting is not reproduced.

- Unlock Poiyomi materials before conversion.
- Unsupported Poiyomi settings, such as non-UV0 inputs, panning, stochastic sampling, dynamic alpha, special mask channels, and non-periodic mask UV mapping, report build errors.
- Additional color layers, emission, color adjustments, panning, and dissolve effects are not fully reproduced. Detected settings produce approximation warnings.
- Animation of texture, color, and shader parameters is not guaranteed to remain equivalent. Material object switching is supported.
- Inputs must be static Texture2D assets. A renderer outside the avatar or a material slot changed during the build reports an error.
- The code also runs for iOS targets, but **iOS device behavior has not been tested**.

Generated materials do not inherit the source material's `VRCFallback` override tag. Multiply passed the tested SDK's Android avatar shader whitelist check. See [VRChat Android content limitations](https://creators.vrchat.com/platforms/android/quest-content-limitations/) for platform restrictions.

## Validation

Editor checks cover Chocolat, Chiffon, Milk-Re, Shinano, and Mafuyu; full NDMF + VQT processing; material animations; shader whitelist validation; and preservation of source resources.

On October 2, 2026, the project owner reported that one processed avatar worked on an Android device. This confirms a real-device result for that avatar, without claiming that every avatar has been tested on hardware.

See [validation notes](docs/VALIDATION.md) for the verification scope and build stages.

## License

Copyright (c) 2026 CQMHV. Licensed under **GNU Affero General Public License version 3 only** (`AGPL-3.0-only`). See [LICENSE](LICENSE). This is not an "or any later version" grant.

## Releasing

Bump `package.json` and its release URL, update `CHANGELOG.md`, and run the **Release VPM package** workflow. Release assets are a VPM ZIP, an embedded UnityPackage, and `package.json`. Re-running the workflow for an existing version can add a missing UnityPackage using the already published ZIP without replacing published assets.

The Pages workflow uses the official `vrchat-community/template-package` website and `vrchat-community/package-list-action` renderer, following VRCLearn's distribution approach. Listing metadata lives in `.github/source.json`. The listing builder reads published release ZIPs to generate an index with SHA-256 checksums, retaining earlier versions. Checksums are part of the VPM index; there is no separate checksum release asset.
