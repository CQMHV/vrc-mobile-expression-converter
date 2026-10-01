// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CQMHV

using System.Linq;
using nadena.dev.ndmf.localization;
using nadena.dev.ndmf.ui;
using UnityEditor;
using UnityEngine;

namespace MobileExpressionConverter.Editor
{
    [CustomEditor(typeof(MobileExpressionSettings))]
    internal sealed class MobileExpressionSettingsEditor : UnityEditor.Editor
    {
        private void OnEnable()
        {
            LanguagePrefs.RegisterLanguageChangeCallback(this, editor => editor.Repaint());
            FillDetectedRenderer((MobileExpressionSettings)target);
        }

        internal static void FillDetectedRenderer(MobileExpressionSettings settings)
        {
            if (settings == null || settings.faceRenderer != null || EditorUtility.IsPersistent(settings)
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var candidates = MobileExpressionPlugin.FindTargetRenderers(settings)
                .Where(r => r.sharedMesh != null).Take(2).ToArray();
            if (candidates.Length != 1) return;
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("faceRenderer").objectReferenceValue = candidates[0];
            serialized.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(Texts.T(
                "Add this component to the Avatar root. Android/iOS builds convert transparent lilToon and Poiyomi face materials to particle Multiply materials. PC builds are unaffected.",
                "将此组件添加到 Avatar 根对象。构建 Android/iOS 版本时，会将脸部的 lilToon 和 Poiyomi 透明材质转换为粒子 Multiply 材质。PC 版本不受影响。",
                "アバターのルートに追加してください。Android/iOS ビルド時に顔の lilToon・Poiyomi 透過マテリアルをパーティクル Multiply 用に変換します。PC 版には影響しません。"),
                MessageType.Info);
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("faceRenderer"),
                new GUIContent(Texts.T("Face renderer", "脸部网格", "顔レンダラー"),
                    Texts.T("Assign a SkinnedMeshRenderer inside this Avatar to process only that renderer.",
                        "拖入当前 Avatar 内的 SkinnedMeshRenderer，指定后仅处理该网格。",
                        "このアバター内の SkinnedMeshRenderer を指定すると、そのレンダラーのみ処理します。")));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox(Texts.T(
                "The face mesh is detected from the Avatar Descriptor, then Body. You can assign another mesh here.",
                "优先识别 Avatar Descriptor 的脸部网格，其次识别 Body；也可手动指定其他网格。",
                "Avatar Descriptor の顔メッシュ、次に Body を検出します。別のメッシュを手動で指定することもできます。"),
                MessageType.None);
            EditorGUILayout.HelpBox(Texts.T(
                "Multiply preserves alpha masks and darkens the background. Blush and shadows are retained approximately; white highlights such as tears become faint or disappear.",
                "Multiply 保留透明遮罩并压暗底色。腮红和阴影可近似保留，眼泪等白色高光会变淡或消失。",
                "Multiply はアルファマスクを維持し、背景を暗くします。頬染めや影を近似的に再現しますが、涙などの白いハイライトは薄くなるか、消える場合があります。"),
                MessageType.Info);
            var settings = (MobileExpressionSettings)target;
            if (settings.faceRenderer == null)
                EditorGUILayout.HelpBox(Texts.T(
                    "No face mesh is assigned. Drag the correct mesh here, or click Detect face mesh to try again.",
                    "尚未填写脸部网格。请拖入正确的网格，或点击“识别脸部网格”重试。",
                    "顔メッシュが未設定です。正しいメッシュをドラッグするか、「顔メッシュを検出」で再試行してください。"), MessageType.Warning);
            if (settings.faceRenderer == null && GUILayout.Button(Texts.T(
                "Detect face mesh", "识别脸部网格", "顔メッシュを検出")))
                FillDetectedRenderer(settings);
            var error = MobileExpressionPlugin.GetSelectionError(settings);
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
            var count = MobileExpressionPlugin.FindTargetRenderers(settings)
                .Where(r => r.sharedMesh != null)
                .Sum(r => r.sharedMaterials.Count(ParticleTextureConverter.IsTransparentExpression));
            EditorGUILayout.LabelField(Texts.T("Static material slots to convert", "待转换的静态材质槽", "変換対象の静的マテリアルスロット"), count.ToString());
        }
    }
}
