using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Audio
{
    /// <summary>一次性、逐条显式映射的配置转换。只创建新资产，不修改输入或解释旧 BundleName。</summary>
    public static class AudioClipLibraryMigration
    {
        public static AudioClipLibrary CreateCopy(AudioClipLibrary source, string targetPath,
            IReadOnlyDictionary<AudioClipEntry, ResourceAddress> mappings)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (mappings == null) throw new ArgumentNullException(nameof(mappings));
            if (string.IsNullOrEmpty(targetPath) || !targetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !targetPath.EndsWith(".asset", StringComparison.Ordinal) || targetPath.Contains('\\') ||
                targetPath.Split('/').Any(x => x.Length == 0 || x == "." || x == "..") ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(targetPath).Replace('\\', '/')))
                throw new ArgumentException("Select an existing Assets folder and a new .asset path.", nameof(targetPath));
            if (File.Exists(targetPath) || File.Exists(targetPath + ".meta") || AssetDatabase.LoadMainAssetAtPath(targetPath))
                throw new ArgumentException("Conversion never overwrites an existing asset or identity.", nameof(targetPath));
            var legacy = (source.Groups ?? new()).Where(x => x?.Entries != null).SelectMany(x => x.Entries)
                .Where(IsLegacy).ToArray();
            if (legacy.Length != mappings.Count || legacy.Any(x => !mappings.TryGetValue(x, out var address) || !address.IsValid || address.SubAsset != null))
                throw new ArgumentException("Map every legacy entry to an explicit ProjectId and main-asset logical address; no identity is inferred.", nameof(mappings));
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                for (var i = 0; i < (source.Groups?.Count ?? 0); i++)
                {
                    var entries = source.Groups[i]?.Entries;
                    if (entries == null) continue;
                    for (var j = 0; j < entries.Count; j++)
                    {
                        if (!IsLegacy(entries[j])) continue;
                        var address = mappings[entries[j]];
                        var converted = copy.Groups[i].Entries[j];
                        converted.Source = AudioClipSource.Resource;
                        converted.ProjectId = address.ProjectId;
                        converted.Key = address.Path;
                        converted.BundleName = "";
                    }
                }
                AssetDatabase.CreateAsset(copy, targetPath);
                AssetDatabase.SaveAssetIfDirty(copy);
                return copy;
            }
            catch
            {
                if (copy && !EditorUtility.IsPersistent(copy)) UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
        }

        internal static bool IsLegacy(AudioClipEntry entry) => entry != null &&
            (entry.Source == AudioClipSource.Resources || entry.Source == AudioClipSource.AssetBundle);

        internal static void Open(AudioClipLibrary source) => AudioClipLibraryMigrationWindow.Open(source);
    }

    internal sealed class AudioClipLibraryMigrationWindow : EditorWindow
    {
        private AudioClipLibrary source;
        private readonly Dictionary<AudioClipEntry, (string Project, string Address)> mappings = new();
        private Vector2 scroll;

        internal static void Open(AudioClipLibrary source)
        {
            var window = GetWindow<AudioClipLibraryMigrationWindow>("Audio Resource Migration");
            window.source = source;
            window.mappings.Clear();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("仅创建新 GUID 副本。逐条核对正式 ResourceProject 的 ProjectId 和逻辑地址，不从旧 BundleName 或资源名推断。原配置、直接 Clip、权重和外部 Provider 保持不变。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Source", source, typeof(AudioClipLibrary), false);
            if (!source) return;
            var entries = (source.Groups ?? new()).Where(x => x?.Entries != null).SelectMany(x => x.Entries)
                .Where(AudioClipLibraryMigration.IsLegacy).ToArray();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
            {
                EditorGUILayout.LabelField(entry.Source + " | " + entry.BundleName + " | " + entry.Key);
                mappings.TryGetValue(entry, out var mapped);
                mapped.Project = EditorGUILayout.TextField("ProjectId", mapped.Project);
                mapped.Address = EditorGUILayout.TextField("Resource Address", mapped.Address);
                mappings[entry] = mapped;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
            if (!GUILayout.Button("创建迁移副本")) return;
            try
            {
                var explicitMap = entries.ToDictionary(x => x, x => new ResourceAddress(mappings[x].Project, mappings[x].Address));
                var path = EditorUtility.SaveFilePanelInProject("Create migrated Audio library", source.name + "-Resource", "asset", "Select a new YL configuration path.");
                if (string.IsNullOrEmpty(path)) return;
                Selection.activeObject = AudioClipLibraryMigration.CreateCopy(source, path, explicitMap);
                Close();
            }
            catch (ArgumentException error) { EditorUtility.DisplayDialog("Audio Resource Migration", error.Message, "Close"); }
        }
    }
}
