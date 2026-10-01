using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

[assembly: ExportsPlugin(typeof(MobileExpressionConverter.Editor.MobileExpressionPlugin))]

namespace MobileExpressionConverter.Editor
{
    internal sealed class BuildState
    {
        internal MobileExpressionSettings Settings;
        internal readonly List<ExpressionSource> Sources = new List<ExpressionSource>();
        internal readonly Dictionary<Material, Material> ConvertedMaterials = new Dictionary<Material, Material>();
        internal readonly HashSet<Material> FailedMaterials = new HashSet<Material>();
    }

    internal sealed class ExpressionSource
    {
        internal SkinnedMeshRenderer Renderer;
        internal string RendererPath;
        internal int MaterialSlot;
        internal Material Material;
    }

    internal sealed class MobileExpressionPlugin : Plugin<MobileExpressionPlugin>
    {
        private const string OutputShaderName = "VRChat/Mobile/Particles/Multiply";

        public override string QualifiedName => "com.mobile-expression-converter";
        public override string DisplayName => "Mobile Expression Converter";

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving).Run("Locate transparent expressions", Resolve);
            InPhase(BuildPhase.Transforming)
                .AfterPlugin("nadena.dev.modular-avatar")
                .AfterPlugin("jp.lilxyzw.lilycalinventory")
                .BeforePlugin("com.github.kurotu.vrc-quest-tools")
                .Run("Convert animated expression materials", ConvertAnimations);
            InPhase(BuildPhase.Optimizing)
                .AfterPlugin("nadena.dev.modular-avatar")
                .AfterPlugin("com.github.kurotu.vrc-quest-tools")
                .Run("Convert particle expressions", Convert);
        }

        private static bool IsMobile =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android ||
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        private static void Resolve(BuildContext context)
        {
            if (!IsMobile) return;
            var settings = context.AvatarRootObject.GetComponent<MobileExpressionSettings>();
            if (settings == null || !settings.enabled) return;
            var state = context.GetState<BuildState>();
            state.Settings = settings;
            var selectionError = GetSelectionError(settings);
            if (selectionError != null)
            {
                Fail(settings, selectionError);
                return;
            }
            foreach (var renderer in FindTargetRenderers(settings))
            {
                if (renderer.sharedMesh == null) continue;
                var materials = renderer.sharedMaterials;
                for (var slot = 0; slot < materials.Length; slot++)
                {
                    if (!ParticleTextureConverter.IsTransparentExpression(materials[slot])) continue;
                    state.Sources.Add(new ExpressionSource
                    {
                        Renderer = renderer,
                        RendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, context.AvatarRootObject.transform),
                        MaterialSlot = slot,
                        Material = materials[slot]
                    });
                }
            }
        }

        internal static bool IsBodyRenderer(SkinnedMeshRenderer renderer) =>
            renderer != null && string.Equals(renderer.name, "Body", StringComparison.OrdinalIgnoreCase);

        internal static IEnumerable<SkinnedMeshRenderer> FindBodyRenderers(GameObject avatar) =>
            avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(IsBodyRenderer);

        internal static string GetSelectionError(MobileExpressionSettings settings)
        {
            if (settings.faceRenderer != null && !settings.faceRenderer.transform.IsChildOf(settings.transform))
                return Texts.T("The selected face renderer must belong to this Avatar.",
                    "指定的脸部网格必须位于当前 Avatar 根对象下。",
                    "指定する顔レンダラーは、このアバター内にある必要があります。");
            return null;
        }

        internal static IEnumerable<SkinnedMeshRenderer> FindTargetRenderers(MobileExpressionSettings settings)
        {
            if (GetSelectionError(settings) != null) return Enumerable.Empty<SkinnedMeshRenderer>();
            if (settings.faceRenderer != null) return new[] { settings.faceRenderer };
            var descriptor = settings.GetComponent<VRCAvatarDescriptor>();
            var face = descriptor != null ? descriptor.VisemeSkinnedMesh : null;
            if (face != null && face.sharedMesh != null && face.transform.IsChildOf(settings.transform))
                return new[] { face };
            return FindBodyRenderers(settings.gameObject);
        }

        private static void Convert(BuildContext context)
        {
            if (!IsMobile) return;
            var state = context.GetState<BuildState>();
            if (state.Settings == null) return;
            ConvertAnimations(context);
            var shader = Shader.Find(OutputShaderName);
            if (shader == null)
            {
                Fail(state.Settings, Texts.T("The mobile particle multiply shader is unavailable.",
                    "移动端粒子 Multiply Shader 不可用。", "モバイル用パーティクル Multiply シェーダーが使用できません。"));
                return;
            }
            foreach (var source in state.Sources)
            {
                var renderer = source.Renderer;
                if (renderer == null || renderer.sharedMesh == null ||
                    !renderer.transform.IsChildOf(context.AvatarRootObject.transform) ||
                    !FindTargetRenderers(state.Settings).Contains(renderer))
                {
                    Fail(state.Settings, Texts.T("The selected face renderer changed during build: ",
                        "构建期间选定的脸部网格发生变化：", "ビルド中に選択した顔レンダラーが変更されました: ") + source.RendererPath);
                    continue;
                }
                var materials = renderer.sharedMaterials;
                var slot = source.MaterialSlot;
                if (slot >= materials.Length || slot >= renderer.sharedMesh.subMeshCount)
                {
                    Fail(renderer, Texts.T("The expression material slot changed during build: ",
                        "构建期间表情材质槽发生变化：", "ビルド中に表情マテリアルのスロットが変更されました: ") + slot);
                    continue;
                }
                Material replacement;
                if (!TryConvertMaterial(state, source.Material, renderer, shader, out replacement)) continue;
                // Replace exactly one existing slot; never assign a Mesh or change the array length.
                materials[slot] = replacement;
                renderer.sharedMaterials = materials;
            }
            if (state.ConvertedMaterials.Count == 0 && state.FailedMaterials.Count == 0)
                Warn(state.Settings, Texts.T("No supported transparent lilToon or Poiyomi material was found on the face renderer or its material animations.",
                    "未在脸部网格及其材质切换动画中找到支持转换的 lilToon 或 Poiyomi 透明材质。",
                    "顔メッシュとマテリアル切替アニメーションに変換可能な lilToon または Poiyomi の透過マテリアルが見つかりません。"));
        }

        private static bool TryConvertMaterial(BuildState state, Material source, UnityEngine.Object owner,
            Shader shader, out Material replacement)
        {
            if (state.ConvertedMaterials.TryGetValue(source, out replacement)) return true;
            if (state.FailedMaterials.Contains(source)) return false;
            Texture2D texture;
            string reason;
            if (!ParticleTextureConverter.TryPrepareTexture(source, out texture, out reason))
            {
                state.FailedMaterials.Add(source);
                Fail(owner, source.name + ": " + reason);
                return false;
            }
            replacement = CreateMaterial(source, shader, texture);
            state.ConvertedMaterials.Add(source, replacement);
            var approximation = ParticleTextureConverter.GetApproximationWarning(source);
            if (approximation != null) Warn(owner, source.name + ": " + approximation);
            return true;
        }

        private static void ConvertAnimations(BuildContext context)
        {
            // Activate animator services only after the platform/component checks. Requiring the extension
            // unconditionally in Configure would clone Windows controllers even when the pass does nothing.
            if (!IsMobile) return;
            var state = context.GetState<BuildState>();
            if (state.Settings == null || !state.Settings.enabled || GetSelectionError(state.Settings) != null) return;
            var targets = new HashSet<SkinnedMeshRenderer>(FindTargetRenderers(state.Settings));
            if (targets.Count == 0) return;
            var shader = Shader.Find(OutputShaderName);
            if (shader == null)
            {
                Fail(state.Settings, Texts.T("The mobile particle multiply shader is unavailable.",
                    "移动端粒子 Multiply Shader 不可用。", "モバイル用パーティクル Multiply シェーダーが使用できません。"));
                return;
            }
            var services = context.ActivateExtensionContextRecursive<AnimatorServicesContext>();
            try
            {
                foreach (var controller in services.ControllerContext.Controllers)
                {
                    var animator = controller.Key as Animator;
                    var animationRoot = animator != null ? animator.transform : context.AvatarRootTransform;
                    foreach (var clip in controller.Value.AllReachableNodes().OfType<VirtualClip>().ToArray())
                    {
                        foreach (var binding in clip.GetObjectCurveBindings().ToArray())
                        {
                            int slot;
                            if (!TryGetMaterialSlot(binding, out slot)) continue;
                            var obj = animationRoot == context.AvatarRootTransform
                                ? services.ObjectPathRemapper.GetObjectForPath(binding.path)
                                : (binding.path.Length == 0 ? animationRoot : animationRoot.Find(binding.path))?.gameObject;
                            var renderer = obj != null ? obj.GetComponent<SkinnedMeshRenderer>() : null;
                            if (renderer == null || !targets.Contains(renderer) || renderer.sharedMesh == null) continue;
                            var frames = clip.GetObjectCurve(binding);
                            if (frames == null) continue;
                            // VirtualClip's arrays are cached. Copy before editing, then commit via SetObjectCurve.
                            var rewritten = (ObjectReferenceKeyframe[])frames.Clone();
                            var changed = false;
                            for (var index = 0; index < rewritten.Length; index++)
                            {
                                var material = rewritten[index].value as Material;
                                if (material == null) continue;
                                var source = material;
                                if (!ParticleTextureConverter.IsTransparentExpression(source))
                                    source = ObjectRegistry.GetReference(material).Object as Material;
                                // VQT may replace our generated particle material as well. Restore that exact
                                // replacement on face bindings instead of baking an already converted material again.
                                var generated = source != null && state.ConvertedMaterials.Values.Contains(source);
                                if (!generated && !ParticleTextureConverter.IsTransparentExpression(source)) continue;
                                if (slot >= renderer.sharedMaterials.Length || slot >= renderer.sharedMesh.subMeshCount)
                                {
                                    Fail(renderer, Texts.T("The animated expression material slot does not exist: ",
                                        "材质切换动画引用的表情材质槽不存在：",
                                        "マテリアル切替アニメーションのスロットが存在しません: ") + binding.propertyName);
                                    break;
                                }
                                Material replacement = source;
                                if (!generated && !TryConvertMaterial(state, source, renderer, shader, out replacement)) continue;
                                if (rewritten[index].value == replacement) continue;
                                rewritten[index].value = replacement;
                                changed = true;
                            }
                            if (changed) clip.SetObjectCurve(binding, rewritten);
                        }
                    }
                }
            }
            finally
            {
                context.DeactivateExtensionContext<AnimatorServicesContext>();
                context.DeactivateExtensionContext<VirtualControllerContext>();
            }
        }

        private static bool TryGetMaterialSlot(EditorCurveBinding binding, out int slot)
        {
            const string prefix = "m_Materials.Array.data[";
            slot = -1;
            return binding.type != null && typeof(Renderer).IsAssignableFrom(binding.type) &&
                binding.propertyName.StartsWith(prefix, StringComparison.Ordinal) &&
                binding.propertyName.EndsWith("]", StringComparison.Ordinal) &&
                int.TryParse(binding.propertyName.Substring(prefix.Length,
                    binding.propertyName.Length - prefix.Length - 1), out slot) && slot >= 0;
        }

        private static Material CreateMaterial(Material source, Shader shader, Texture2D prepared)
        {
            // Create a fresh material so desktop VRCFallback override tags are not inherited.
            var result = new Material(shader) { name = source.name + " (Mobile Multiply)" };
            result.mainTexture = prepared != null ? prepared : FindMainTextureAsset(source);
            if (result.HasProperty("_Color"))
                result.SetColor("_Color", prepared == null ? SourceTint(source) : Color.white);
            var property = source.HasProperty("_MainTex") ? "_MainTex" : "_BaseMap";
            if (source.HasProperty(property))
            {
                result.mainTextureScale = source.GetTextureScale(property);
                result.mainTextureOffset = source.GetTextureOffset(property);
            }
            return result;
        }

        internal static Texture FindMainTextureAsset(Material material)
        {
            if (material == null) return null;
            if (material.HasProperty("_MainTex") && material.GetTexture("_MainTex") != null)
                return material.GetTexture("_MainTex");
            return material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
        }

        internal static Color SourceTint(Material material)
        {
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return Color.white;
        }

        private static void Warn(UnityEngine.Object context, string message) =>
            Debug.LogWarning("[Mobile Expression Converter] " + message, context);

        private static void Fail(UnityEngine.Object context, string reason)
        {
            const string key = "Mobile particle expression conversion failed for {0}: {1}";
            Texts.T(key, "移动端粒子表情转换失败（{0}）：{1}",
                "モバイル用パーティクル表情の変換に失敗しました（{0}）: {1}");
            ErrorReport.ReportError(Texts.NdmfLocalizer, ErrorSeverity.Error, key, context.name, reason, context);
        }
    }
}
