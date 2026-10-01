using nadena.dev.ndmf;
using UnityEngine;

namespace MobileExpressionConverter
{
    [AddComponentMenu("VRChat/Mobile Expression Converter")]
    [DisallowMultipleComponent]
    public sealed class MobileExpressionSettings : MonoBehaviour, INDMFEditorOnly
    {
        [Tooltip("Optional face renderer inside this Avatar. Leave empty to use the Avatar Descriptor Face Mesh, then fall back to Body (case-insensitive).")]
        public SkinnedMeshRenderer faceRenderer;
    }
}
