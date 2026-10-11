# Changelog

## 1.1.1 — 2026-10-11

- Convert static and animated expression materials together in the Transforming phase, before texture optimizers collect generated textures.
- Register generated build textures with NDMF's asset saver so optimizers that skip anonymous runtime textures can process them.
- Remove the late material restoration pass, which could reintroduce uncompressed textures after optimizers had cloned and compressed the materials.

## 1.1.0 — 2026-10-06

- Add a processing-mode selector: Multiply (recommended, default) or Additive.
- Apply the selected mode to static face materials and material-switching animations.
- Preserve Multiply behavior for existing components and keep Windows builds unchanged.
- Explain the selected mode in the English, Simplified Chinese, and Japanese Inspector.

## 1.0.0 — 2026-10-02

- Initial VPM release of Mobile Expression Converter.
- Convert supported lilToon / Poiyomi face materials to mobile Multiply materials during NDMF builds.
- Process face material-switching animations while preserving mesh and material-slot structure.
- Support automatic or manual face renderer selection and English, Simplified Chinese, and Japanese Inspector text.
- Declare VRChat SDK Avatars and NDMF dependencies and legacy Assets migration metadata.
- Publish a VPM repository listing and installation page through GitHub Pages.
- License the source under AGPL-3.0-only.

Known limitation: white tears and highlights may become faint or disappear. iOS device behavior has not been tested.
