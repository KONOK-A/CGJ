// Adapted from XF Localization authoring. Copyright (c) 2024 弦风课堂. MIT; see Runtime/Systems/Localization/LICENSE.txt.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YLFramework.Localization;

namespace YLFramework.Editor.Localization
{
    /// <summary>只编辑显式目标根及其语言资产；不安装全工程删除/自动保存监听。</summary>
    public static class LocalizationAuthoring
    {
        public static string GetLanguageFolder(MultiLanguageConfigs root)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root));
            if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Save the YL localization root asset before authoring languages.");
            return "Assets/YLFrameworkData/Localization/" + guid;
        }

        public static LanguageConfig CreateLanguage(MultiLanguageConfigs root, SystemLanguage language)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            RequireValid(root);
            if (!Enum.IsDefined(typeof(SystemLanguage), language) || language == SystemLanguage.Unknown || root.GetLanguageConfigData(language))
                throw new InvalidOperationException("Unknown or duplicate language: " + language);
            var folder = GetLanguageFolder(root);
            EnsureFolder(folder);
            var asset = ScriptableObject.CreateInstance<LanguageConfig>();
            asset.language = language;
            asset.displayName = language.ToString();
            // 新语言沿用现有所有ID/类型，不拷贝另一语言的内容冒充翻译。
            var ids = new HashSet<int>();
            foreach (var source in root.Languages)
                foreach (var entry in source.languagesInfo)
                    if (ids.Add(entry.id)) asset.languagesInfo.Add(new LanguageInfo { id = entry.id, type = entry.type, notes = entry.notes });
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + language + ".asset");
            AssetDatabase.CreateAsset(asset, path);
            Undo.RegisterCreatedObjectUndo(asset, "Create YL language");
            Undo.RecordObject(root, "Add YL language reference");
            root.Languages.Add(asset);
            EditorUtility.SetDirty(root);
            return asset;
        }

        public static void AddEntry(MultiLanguageConfigs root, int id, MultiLanguageType type, string notes = null)
        {
            RequireValid(root);
            if (root.Languages.Count == 0) throw new InvalidOperationException("Create a language first.");
            foreach (var language in root.Languages)
                if (language.GetLanguageInfo(id) != null) throw new InvalidOperationException($"Localization '{root.name}': ID {id} already exists in {language.language}.");
            foreach (var language in root.Languages)
            {
                Undo.RecordObject(language, "Add YL localization entry");
                language.languagesInfo.Add(new LanguageInfo { id = id, type = type, notes = notes });
                language.ClearCache();
                EditorUtility.SetDirty(language);
            }
        }

        public static void DuplicateEntry(MultiLanguageConfigs root, int sourceId, int newId)
        {
            RequireValid(root);
            var copies = new Dictionary<LanguageConfig, LanguageInfo>();
            foreach (var language in root.Languages)
            {
                if (language.GetLanguageInfo(newId) != null) throw new InvalidOperationException("Duplicate destination ID: " + newId);
                var source = language.GetLanguageInfo(sourceId);
                if (source == null) continue;
                var copy = JsonUtility.FromJson<LanguageInfo>(JsonUtility.ToJson(source));
                copy.id = newId;
                copies.Add(language, copy);
            }
            if (copies.Count == 0) throw new InvalidOperationException("Missing source ID: " + sourceId);
            foreach (var pair in copies)
            {
                Undo.RecordObject(pair.Key, "Duplicate YL localization entry");
                pair.Key.languagesInfo.Add(pair.Value);
                pair.Key.ClearCache();
                EditorUtility.SetDirty(pair.Key);
            }
        }

        public static void RemoveEntry(MultiLanguageConfigs root, int id)
        {
            RequireValid(root);
            foreach (var language in root.Languages)
            {
                if (language.GetLanguageInfo(id) == null) continue;
                Undo.RecordObject(language, "Remove YL localization entry");
                language.languagesInfo.RemoveAll(entry => entry.id == id);
                language.ClearCache();
                EditorUtility.SetDirty(language);
            }
        }

        public static void RemoveLanguageReference(MultiLanguageConfigs root, LanguageConfig language)
        {
            if (!root || !root.Languages.Contains(language)) return;
            Undo.RecordObject(root, "Remove YL language reference");
            root.Languages.Remove(language);
            root.ClearCache();
            EditorUtility.SetDirty(root);
            // 引用可共享；不因移除列表项而删除语言文件或递归删除外来目录。
        }

        public static void RequireValid(MultiLanguageConfigs root) => LocalizationManager.Validate(new[] { root });

        private static void EnsureFolder(string path)
        {
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
