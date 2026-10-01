using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MobileExpressionConverter.Editor
{
    internal static class ParticleTextureConverter
    {
        private const string TextureShaderName = "Hidden/MobileExpressionConverter/LilToonTexture";

        internal static bool IsPoiyomi(Material material) => material != null && material.shader != null &&
            material.shader.name.IndexOf("poiyomi", StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool IsTransparentExpression(Material material) => IsTransparentLilToon(material) ||
            (IsPoiyomi(material) && material.HasProperty("_Mode") &&
             (material.GetInt("_Mode") == 2 || material.GetInt("_Mode") == 3));

        private static float Value(Material material, string property, float fallback = 0f) =>
            material.HasProperty(property) ? material.GetFloat(property) : fallback;

        internal static bool IsLilToon(Material material)
        {
            return material != null && material.shader != null &&
                   material.shader.name.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsTransparentLilToon(Material material)
        {
            if (!IsLilToon(material)) return false;
            // Multi variants encode the rendering mode in a property; other variants encode it in the shader name.
            // Render queue alone cannot distinguish transparent expressions from opaque skin or cutout materials.
            var shaderName = material.shader.name;
            var variant = shaderName.Substring(shaderName.LastIndexOf('/') + 1);
            if (variant.IndexOf("Multi", StringComparison.OrdinalIgnoreCase) >= 0)
                return material.HasProperty("_TransparentMode") && material.GetFloat("_TransparentMode") == 2f;
            return variant.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool TryPrepareTexture(Material material, out Texture2D output, out string reason)
        {
            output = null;
            reason = null;
            var poiyomi = IsPoiyomi(material);
            if (poiyomi && !ValidatePoiyomi(material, out reason)) return false;
            var sourceBlend = poiyomi && Value(material, "_AlphaPremultiply") < 0.5f
                ? UnityEngine.Rendering.BlendMode.SrcAlpha
                : UnityEngine.Rendering.BlendMode.One;
            if (!IsTransparentExpression(material) || !material.HasProperty("_SrcBlend") ||
                material.GetFloat("_SrcBlend") != (float)sourceBlend ||
                !material.HasProperty("_DstBlend") ||
                material.GetFloat("_DstBlend") != (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha ||
                (material.HasProperty("_BlendOp") && material.GetFloat("_BlendOp") != 0f))
            {
                reason = Texts.T("Unsupported transparent blend settings; this material was not converted.",
                    "不支持此材质的透明混合设置；该材质未转换。",
                    "この透過ブレンド設定には対応していません。マテリアルは変換されません。");
                return false;
            }
            var mainAsset = MobileExpressionPlugin.FindMainTextureAsset(material);
            var main = mainAsset as Texture2D;
            var mode = (int)Value(material, poiyomi ? "_MainAlphaMaskMode" : "_AlphaMaskMode");
            var maskAsset = mode != 0 && material.HasProperty("_AlphaMask") ? material.GetTexture("_AlphaMask") : null;
            var mask = maskAsset as Texture2D;
            if ((mainAsset != null && main == null) || (maskAsset != null && mask == null) || mode < 0 || mode > 4)
            {
                reason = Texts.T("Particle requires static Texture2D textures and alpha mask mode 0-4.",
                    "粒子转换需要静态 Texture2D 纹理，且透明遮罩模式必须为 0–4。",
                    "パーティクル変換には静的な Texture2D とアルファマスクモード 0～4 が必要です。");
                return false;
            }
            var tint = MobileExpressionPlugin.SourceTint(material);
            // No packing is necessary when the particle shader can use the original texture directly.
            if (!poiyomi && main != null && mode == 0 && tint == Color.white) return true;
            var shader = Shader.Find(TextureShaderName);
            if (shader == null || !shader.isSupported)
            {
                reason = Texts.T("The editor texture conversion shader is unavailable.",
                    "编辑器纹理转换 Shader 不可用。", "エディター用テクスチャ変換シェーダーを使用できません。");
                return false;
            }
            Material converter = null;
            RenderTexture target = null;
            var previous = RenderTexture.active;
            var previousSrgbWrite = GL.sRGBWrite;
            try
            {
                converter = new Material(shader);
                converter.SetColor("_Color", tint);
                converter.SetFloat("_Poiyomi", poiyomi ? 1f : 0f);
                converter.SetFloat("_IgnoreAlpha", poiyomi ? Value(material, "_MainIgnoreTexAlpha") : 0f);
                converter.SetFloat("_AlphaMod", poiyomi ? Value(material, "_AlphaMod") : 0f);
                converter.SetFloat("_Cutoff", poiyomi ? Value(material, "_Cutoff") : 0f);
                converter.SetFloat("_MaskInvert", poiyomi ? Value(material, "_AlphaMaskInvert") : 0f);
                converter.SetInt("_AlphaMaskMode", mode);
                converter.SetTexture("_AlphaMask", mask != null ? mask : Texture2D.whiteTexture);
                if (mode != 0)
                {
                    converter.SetTextureScale("_AlphaMask", material.GetTextureScale("_AlphaMask"));
                    converter.SetTextureOffset("_AlphaMask", material.GetTextureOffset("_AlphaMask"));
                    if (poiyomi)
                    {
                        // Poiyomi transforms mask UV independently of the main texture UV.
                        var mainScale = material.GetTextureScale("_MainTex");
                        var mainOffset = material.GetTextureOffset("_MainTex");
                        var scale = material.GetTextureScale("_AlphaMask");
                        var offset = material.GetTextureOffset("_AlphaMask");
                        scale = new Vector2(scale.x / mainScale.x, scale.y / mainScale.y);
                        converter.SetTextureScale("_AlphaMask", scale);
                        converter.SetTextureOffset("_AlphaMask", offset - Vector2.Scale(mainOffset, scale));
                    }
                }
                converter.SetFloat("_AlphaMaskScale", Value(material, poiyomi ? "_AlphaMaskBlendStrength" : "_AlphaMaskScale", 1f));
                var maskOffset = Value(material, "_AlphaMaskValue");
                if (poiyomi && !material.HasProperty("_AlphaMaskR") && Value(material, "_AlphaMaskInvert") != 0f)
                    maskOffset = -maskOffset;
                converter.SetFloat("_AlphaMaskValue", maskOffset);
                var width = Mathf.Max(main != null ? main.width : 2, mask != null ? mask.width : 2);
                var height = Mathf.Max(main != null ? main.height : 2, mask != null ? mask.height : 2);
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                // Sample in texture space; the resulting material keeps the source main UV transform.
                Graphics.Blit(main != null ? main : Texture2D.whiteTexture, target, converter);
                RenderTexture.active = target;
                output = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
                {
                    name = material.name + " (Mobile Particle)",
                    wrapModeU = main != null ? main.wrapModeU : TextureWrapMode.Repeat,
                    wrapModeV = main != null ? main.wrapModeV : TextureWrapMode.Repeat,
                    filterMode = main != null ? main.filterMode : FilterMode.Bilinear,
                    anisoLevel = main != null ? main.anisoLevel : 1
                };
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                output.Apply(true, false);
                var serialized = new SerializedObject(output);
                var streamingMipmaps = serialized.FindProperty("m_StreamingMipmaps");
                if (streamingMipmaps != null)
                {
                    streamingMipmaps.boolValue = true;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                return true;
            }
            catch (Exception ex)
            {
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                output = null;
                reason = Texts.T("Particle texture conversion failed: ", "表情纹理转换失败：",
                    "パーティクル変換のテクスチャ変換に失敗しました: ") + ex.Message;
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                GL.sRGBWrite = previousSrgbWrite;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (converter != null) UnityEngine.Object.DestroyImmediate(converter);
            }
        }

        private static bool ValidatePoiyomi(Material material, out string reason)
        {
            var unsupported = new List<string>();
            if (Value(material, "_ShaderOptimizerEnabled") != 0f ||
                material.shader.name.StartsWith("Hidden/", StringComparison.OrdinalIgnoreCase))
                unsupported.Add("Locked shader (unlock the material first)");
            foreach (var property in new[] { "_MainTexUV", "_AlphaMaskUV", "_MainTexStochastic", "_MainPixelMode",
                "_AlphaForceOpaque", "_EnableDissolve", "_AlphaDithering", "_AlphaAudioLinkEnabled",
                "_AlphaDistanceFade", "_AlphaAngular", "_AlphaFresnel", "_AlphaToCoverage" })
                if (Value(material, property) != 0f) unsupported.Add(property);
            foreach (var property in new[] { "_MainTexPan", "_AlphaMaskPan" })
                if (material.HasProperty(property) && material.GetVector(property) != Vector4.zero) unsupported.Add(property);
            var scale = material.GetTextureScale("_MainTex");
            if (Mathf.Abs(scale.x) < 0.00001f || Mathf.Abs(scale.y) < 0.00001f) unsupported.Add("_MainTex scale = 0");
            if (Value(material, "_MainAlphaMaskMode") != 0f && material.GetTexture("_AlphaMask") != null)
            {
                var maskScale = material.GetTextureScale("_AlphaMask");
                var mask = material.GetTexture("_AlphaMask");
                var ratio = new Vector2(maskScale.x / scale.x, maskScale.y / scale.y);
                if (mask.wrapModeU != TextureWrapMode.Repeat || mask.wrapModeV != TextureWrapMode.Repeat ||
                    Mathf.Abs(ratio.x - Mathf.Round(ratio.x)) > 0.0001f ||
                    Mathf.Abs(ratio.y - Mathf.Round(ratio.y)) > 0.0001f)
                    unsupported.Add("Non-periodic alpha mask UV mapping");
            }
            // The base conversion supports Poiyomi's default red-channel mask.
            if (material.HasProperty("_AlphaMaskR") && Value(material, "_MainAlphaMaskMode") != 0f)
            {
                if (Value(material, "_AlphaMaskR") != 1f || Value(material, "_AlphaMaskG") != 0f ||
                    Value(material, "_AlphaMaskB") != 0f || Value(material, "_AlphaMaskA") != 0f ||
                    Value(material, "_AlphaMaskGamma", 1f) != 1f ||
                    material.GetVector("_AlphaMaskMinMax") != new Vector4(0, 1, 0, 1))
                    unsupported.Add("Alpha mask channel/remap/gamma");
            }
            reason = unsupported.Count == 0 ? null : Texts.T(
                "Poiyomi settings cannot be preserved by this conversion: ",
                "以下 Poiyomi 设置无法在转换中保留，请先调整（锁定材质需先解锁）：",
                "次の Poiyomi 設定は変換できません。ロック済みの場合は解除してください: ") + string.Join(", ", unsupported);
            return reason == null;
        }

        internal static string GetApproximationWarning(Material material)
        {
            var unsupported = new List<string>();
            if (IsPoiyomi(material))
                foreach (var property in new[] { "_EnableEmission", "_EnableEmission1", "_EnableEmission2", "_EnableEmission3",
                    "_DecalEnabled", "_DecalEnabled1", "_DecalEnabled2", "_DecalEnabled3", "_MainHueShiftToggle", "_ColorThemeIndex" })
                    if (Value(material, property) != 0f) unsupported.Add(property);
            foreach (var property in new[] { "_UseMain2ndTex", "_UseMain3rdTex", "_UseEmission", "_UseEmission2nd" })
                if (material.HasProperty(property) && material.GetFloat(property) != 0f) unsupported.Add(property);
            if (material.HasProperty("_MainTexHSVG") && material.GetVector("_MainTexHSVG") != new Vector4(0, 1, 1, 1))
                unsupported.Add("_MainTexHSVG");
            if (material.HasProperty("_MainTex_ScrollRotate") && material.GetVector("_MainTex_ScrollRotate") != Vector4.zero)
                unsupported.Add("_MainTex_ScrollRotate");
            if (material.HasProperty("_DissolveParams") && material.GetVector("_DissolveParams").x != 0f)
                unsupported.Add("_DissolveParams");
            return unsupported.Count == 0 ? null : Texts.T(
                "Particle converts base color and alpha only. These enabled features are not reproduced: ",
                "粒子转换仅转换底图颜色与透明度，以下已启用功能不会被重现：",
                "パーティクル変換はベースカラーと透明度のみ変換します。次の有効な機能は再現されません: ") +
                string.Join(", ", unsupported.ToArray());
        }
    }
}
