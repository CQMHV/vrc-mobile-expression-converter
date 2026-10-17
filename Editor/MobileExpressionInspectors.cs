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
            if (settings == null || MobileExpressionPlugin.FindSelectedRenderers(settings).Any() || EditorUtility.IsPersistent(settings)
                || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var candidates = MobileExpressionPlugin.FindTargetRenderers(settings)
                .Where(r => r.sharedMesh != null).ToArray();
            if (candidates.Length == 0) return;
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("faceRenderer").objectReferenceValue = candidates[0];
            var additional = serialized.FindProperty("additionalFaceRenderers");
            additional.arraySize = candidates.Length - 1;
            for (var index = 1; index < candidates.Length; index++)
                additional.GetArrayElementAtIndex(index - 1).objectReferenceValue = candidates[index];
            EnsureProcessingModes(serialized);
            serialized.ApplyModifiedProperties();
        }

        internal static void EnsureProcessingModes(SerializedObject serialized)
        {
            var modes = serialized.FindProperty("additionalProcessingModes");
            var previousSize = modes.arraySize;
            var count = serialized.FindProperty("additionalFaceRenderers").arraySize;
            if (modes.arraySize != count) modes.arraySize = count;
            for (var index = previousSize; index < modes.arraySize; index++)
                modes.GetArrayElementAtIndex(index).intValue = serialized.FindProperty("processingMode").intValue;
        }

        internal static void AddFaceRenderer(SerializedObject serialized)
        {
            EnsureProcessingModes(serialized);
            var additional = serialized.FindProperty("additionalFaceRenderers");
            var index = additional.arraySize;
            additional.arraySize++;
            additional.GetArrayElementAtIndex(index).objectReferenceValue = null;
            var modes = serialized.FindProperty("additionalProcessingModes");
            modes.arraySize = additional.arraySize;
            modes.GetArrayElementAtIndex(index).intValue = (int)ExpressionProcessingMode.Multiply;
        }

        internal static void RemoveFaceRenderer(SerializedObject serialized, int index)
        {
            EnsureProcessingModes(serialized);
            var additional = serialized.FindProperty("additionalFaceRenderers");
            var modes = serialized.FindProperty("additionalProcessingModes");
            if (additional.arraySize == 0) return;
            if (index == 0)
            {
                serialized.FindProperty("faceRenderer").objectReferenceValue =
                    additional.GetArrayElementAtIndex(0).objectReferenceValue;
                serialized.FindProperty("processingMode").intValue = modes.GetArrayElementAtIndex(0).intValue;
            }
            var additionalIndex = index == 0 ? 0 : index - 1;
            for (var next = additionalIndex + 1; next < additional.arraySize; next++)
            {
                additional.GetArrayElementAtIndex(next - 1).objectReferenceValue =
                    additional.GetArrayElementAtIndex(next).objectReferenceValue;
                modes.GetArrayElementAtIndex(next - 1).intValue = modes.GetArrayElementAtIndex(next).intValue;
            }
            additional.arraySize--;
            modes.arraySize--;
        }

        private static void DrawProcessingMode(SerializedProperty property)
        {
            EditorGUI.BeginChangeCheck();
            var mode = EditorGUILayout.Popup(
                Texts.T("Processing mode", "处理模式", "処理モード"), property.intValue,
                new[] { Texts.T("Multiply (recommended)", "Multiply（推荐）", "Multiply（推奨）"), "Additive" });
            if (EditorGUI.EndChangeCheck()) property.intValue = mode;
        }

        private void DrawFaceRenderers()
        {
            var primary = serializedObject.FindProperty("faceRenderer");
            var additional = serializedObject.FindProperty("additionalFaceRenderers");
            var count = additional.arraySize + 1;
            var removeIndex = -1;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(Texts.T("Face renderers", "脸部网格", "顔レンダラー"), EditorStyles.boldLabel);
                for (var index = 0; index < count; index++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var property = index == 0 ? primary : additional.GetArrayElementAtIndex(index - 1);
                        EditorGUILayout.PropertyField(property, new GUIContent(
                            Texts.T("Mesh ", "网格 ", "メッシュ ") + (index + 1)));
                        using (new EditorGUI.DisabledScope(count == 1))
                            if (GUILayout.Button(new GUIContent("−", Texts.T("Remove mesh", "移除网格", "メッシュを削除")), GUILayout.Width(24)))
                                removeIndex = index;
                    }
                    DrawProcessingMode(index == 0 ? serializedObject.FindProperty("processingMode") :
                        serializedObject.FindProperty("additionalProcessingModes").GetArrayElementAtIndex(index - 1));
                }
                if (removeIndex >= 0) RemoveFaceRenderer(serializedObject, removeIndex);
                if (GUILayout.Button(Texts.T("Add mesh", "添加网格", "メッシュを追加")))
                    AddFaceRenderer(serializedObject);
            }
        }

        public override void OnInspectorGUI()
        {
            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(Texts.T(
                "Add this component to the Avatar root. Android/iOS builds convert transparent lilToon and Poiyomi face materials using each mesh's processing mode. PC builds are unaffected.",
                "将此组件添加到 Avatar 根对象。构建 Android/iOS 版本时，会按各网格的处理模式转换脸部的 lilToon 和 Poiyomi 透明材质。PC 版本不受影响。",
                "アバターのルートに追加してください。Android/iOS ビルド時に顔の lilToon・Poiyomi 透過マテリアルをメッシュごとの処理モードで変換します。PC 版には影響しません。"),
                MessageType.Info);
            serializedObject.Update();
            EnsureProcessingModes(serializedObject);
            DrawFaceRenderers();
            serializedObject.ApplyModifiedProperties();
            var settings = (MobileExpressionSettings)target;
            EditorGUILayout.HelpBox(Texts.T(
                "Detection selects one face mesh by default. Add other meshes as needed and choose a processing mode for each mesh.",
                "默认自动识别一个脸部网格。可按需添加其他网格，并分别选择处理模式。",
                "初期状態では顔メッシュを1つ検出します。必要に応じて追加し、メッシュごとに処理モードを選択できます。"),
                MessageType.None);
            EditorGUILayout.HelpBox(Texts.T(
                "Multiply is recommended for blush and shadows. Additive brightens the background for white tears and highlights; it cannot darken overlays and may overexpose bright areas.",
                "Multiply 推荐用于腮红和阴影；Additive 用于白色眼泪和高光，无法还原暗色叠加，亮部可能过曝。",
                "Multiply は頬染めや影に推奨します。Additive は白い涙やハイライト向けですが、暗い表現は再現できず、明部が白飛びする場合があります。"),
                MessageType.Info);
            var hasSelection = MobileExpressionPlugin.FindSelectedRenderers(settings).Any();
            if (!hasSelection)
                EditorGUILayout.HelpBox(Texts.T(
                    "No face mesh is assigned. Drag the correct mesh here, or click Detect face mesh to try again.",
                    "尚未填写脸部网格。请拖入正确的网格，或点击“识别脸部网格”重试。",
                    "顔メッシュが未設定です。正しいメッシュをドラッグするか、「顔メッシュを検出」で再試行してください。"), MessageType.Warning);
            if (!hasSelection && GUILayout.Button(Texts.T(
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
