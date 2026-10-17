// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CQMHV

using nadena.dev.ndmf;
using UnityEngine;

namespace MobileExpressionConverter
{
    public enum ExpressionProcessingMode
    {
        Multiply = 0,
        Additive = 1
    }

    [AddComponentMenu("Mobile Expression Converter")]
    [DisallowMultipleComponent]
    public sealed class MobileExpressionSettings : MonoBehaviour, INDMFEditorOnly
    {
        [Tooltip("First entry in the Face renderers list. Leave all entries empty for Avatar Descriptor / Body detection.")]
        public SkinnedMeshRenderer faceRenderer;

        [Tooltip("Remaining entries in the Face renderers list. All assigned renderers are processed together; empty and duplicate entries are ignored.")]
        public SkinnedMeshRenderer[] additionalFaceRenderers = new SkinnedMeshRenderer[0];

        [Tooltip("Processing mode for the first mesh. Also preserves the mode of older mesh entries without a saved per-mesh mode.")]
        public ExpressionProcessingMode processingMode = ExpressionProcessingMode.Multiply;

        [Tooltip("Preview mobile expressions in NDMF without changing the original avatar or the build target.")]
        public bool previewInEditor = true;

        [HideInInspector]
        public ExpressionProcessingMode[] additionalProcessingModes = new ExpressionProcessingMode[0];

        public ExpressionProcessingMode GetProcessingMode(SkinnedMeshRenderer renderer)
        {
            if (renderer == null || renderer == faceRenderer || additionalFaceRenderers == null) return processingMode;
            for (var index = 0; index < additionalFaceRenderers.Length; index++)
            {
                if (additionalFaceRenderers[index] != renderer) continue;
                return additionalProcessingModes != null && index < additionalProcessingModes.Length
                    ? additionalProcessingModes[index] : processingMode;
            }
            return processingMode;
        }
    }
}
