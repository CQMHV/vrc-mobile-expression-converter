// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 CQMHV

using System;
using System.IO;
using nadena.dev.ndmf.ui;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MobileExpressionConverter.Editor
{
    internal sealed class MobileExpressionUpdateWindow : EditorWindow
    {
        private const string PackageName = "com.cqmhv.mobile-expression-converter";
        private const string ListingUrl = "https://cqmhv.github.io/vrc-mobile-expression-converter/index.json";

        private UnityWebRequest request;
        private string currentVersion;
        private string latestVersion;
        private string status;
        private MessageType statusType;

        internal static void Open()
        {
            var window = GetWindow<MobileExpressionUpdateWindow>();
            window.titleContent = new GUIContent("Mobile Expression Converter");
            window.minSize = new Vector2(420, 260);
            window.CheckForUpdates();
            window.Show();
        }

        private void OnDisable() => CancelRequest();

        private void CancelRequest()
        {
            EditorApplication.update -= PollRequest;
            if (request == null) return;
            request.Abort();
            request.Dispose();
            request = null;
        }

        private void CheckForUpdates()
        {
            if (request != null) return;
            latestVersion = null;
            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MobileExpressionSettings).Assembly);
                var root = package != null ? package.resolvedPath :
                    Path.GetDirectoryName(Path.GetDirectoryName(AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this))));
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(root, "package.json")));
                if ((string)manifest["name"] != PackageName) throw new InvalidDataException("Unexpected package manifest.");
                currentVersion = (string)manifest["version"];
                Version parsed;
                if (!TryStableVersion(currentVersion, out parsed)) throw new InvalidDataException("Invalid installed version.");

                status = Texts.T("Checking for updates…", "正在检查更新…", "更新を確認しています…");
                statusType = MessageType.Info;
                request = UnityWebRequest.Get(ListingUrl);
                request.timeout = 15;
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.SendWebRequest();
                EditorApplication.update += PollRequest;
            }
            catch (Exception exception)
            {
                CancelRequest();
                ShowError(exception.Message);
            }
            Repaint();
        }

        internal static bool TryStableVersion(string text, out Version version)
        {
            return Version.TryParse(text, out version) && version.Build >= 0 && version.Revision < 0;
        }

        internal static string GetVpmRepositoryLink() => "vcc://vpm/addRepo?url=" + Uri.EscapeDataString(ListingUrl);

        internal static string FindLatestVersion(string json)
        {
            var versions = JObject.Parse(json)["packages"]?[PackageName]?["versions"] as JObject;
            if (versions == null) throw new InvalidDataException("Package is missing from the update listing.");
            Version latest = null;
            string result = null;
            foreach (var entry in versions.Properties())
            {
                Version candidate;
                if (!TryStableVersion(entry.Name, out candidate)) continue;
                if ((string)entry.Value["name"] != PackageName || (string)entry.Value["version"] != entry.Name)
                    throw new InvalidDataException("Update version and package manifest do not match.");
                if (latest != null && candidate <= latest) continue;
                latest = candidate;
                result = entry.Name;
            }
            if (result == null) throw new InvalidDataException("No stable package version is available.");
            return result;
        }

        private void PollRequest()
        {
            if (request == null || !request.isDone) return;
            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException(request.error);
                latestVersion = FindLatestVersion(request.downloadHandler.text);
                var hasUpdate = new Version(latestVersion) > new Version(currentVersion);
                status = hasUpdate
                    ? Texts.T("A new version is available. Update Mobile Expression Converter in this project's VCC / ALCOM package list.",
                        "发现新版本。请在 VCC / ALCOM 中打开当前项目的包列表，更新 Mobile Expression Converter。",
                        "新しいバージョンがあります。VCC / ALCOM でこのプロジェクトのパッケージ一覧を開き、Mobile Expression Converter を更新してください。")
                    : Texts.T("You are using the latest version.", "当前已是最新版本。", "現在のバージョンは最新です。");
                statusType = MessageType.Info;
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
            }
            finally
            {
                CancelRequest();
                Repaint();
            }
        }

        private void ShowError(string detail)
        {
            status = Texts.T("Could not check for updates. Check your connection and try again.",
                "无法检查更新，请检查网络后重试。", "更新を確認できませんでした。ネットワーク接続を確認して再試行してください。");
            statusType = MessageType.Warning;
            Debug.LogWarning("[Mobile Expression Converter] Update check failed: " + detail);
        }

        private void OnGUI()
        {
            LanguageSwitcher.DrawImmediate();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Mobile Expression Converter", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Texts.T("Installed version", "当前版本", "現在のバージョン"), currentVersion ?? "—");
            EditorGUILayout.LabelField(Texts.T("Latest version", "最新版本", "最新バージョン"), latestVersion ?? "—");
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
            using (new EditorGUI.DisabledScope(request != null))
                if (GUILayout.Button(Texts.T("Check again", "重新检查", "再確認"))) CheckForUpdates();
            EditorGUILayout.HelpBox(Texts.T(
                "Updates use VPM for all editions. Add this repository once, then select your project in VCC / ALCOM to install or update the package.",
                "所有版本统一通过 VPM 更新。首次添加此仓库后，在 VCC / ALCOM 中选择项目，即可安装或更新此包。",
                "すべての配布版を VPM で更新します。初回にこのリポジトリを追加し、VCC / ALCOM でプロジェクトを選んでインストール・更新してください。"), MessageType.None);
            if (GUILayout.Button(Texts.T("Add repository to VCC / ALCOM", "添加仓库到 VCC / ALCOM", "VCC / ALCOM にリポジトリを追加")))
                Application.OpenURL(GetVpmRepositoryLink());
            if (GUILayout.Button(Texts.T("Copy repository URL", "复制仓库地址", "リポジトリ URL をコピー")))
                EditorGUIUtility.systemCopyBuffer = ListingUrl;
        }
    }
}
