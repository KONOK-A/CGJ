using System;
using System.IO;
using UnityEditor;

namespace YLFramework.DataAssets.Editor
{
    /// <summary>
    /// 按资产GUID记住最近导出的工作簿，只保存在本机 EditorPrefs，不写入配置资产或仓库。
    /// 资产改名/移动仍沿用同一GUID；删除资产后可调用 Forget 清理。
    /// </summary>
    public static class DataAssetsWorkbookMemory
    {
        private const string Prefix = "YLFramework.DataAssets.LastWorkbook.";

        public static bool Remember(ScriptableObjectConfigs root, string path)
        {
            if (!TryKey(root, out var key) || !IsWorkbookPath(path)) return false;
            EditorPrefs.SetString(key, Path.GetFullPath(path));
            return true;
        }

        public static bool TryGetPath(ScriptableObjectConfigs root, out string path)
        {
            path = null;
            if (!TryKey(root, out var key)) return false;
            var stored = EditorPrefs.GetString(key, string.Empty);
            if (!IsWorkbookPath(stored)) return false;
            path = stored;
            return true;
        }

        public static bool CanReveal(ScriptableObjectConfigs root) => TryGetPath(root, out var path) && File.Exists(path);

        public static bool Reveal(ScriptableObjectConfigs root)
        {
            if (!TryGetPath(root, out var path) || !File.Exists(path)) return false;
            EditorUtility.RevealInFinder(path);
            return true;
        }

        public static void Forget(ScriptableObjectConfigs root)
        {
            if (TryKey(root, out var key)) EditorPrefs.DeleteKey(key);
        }

        private static bool TryKey(ScriptableObjectConfigs root, out string key)
        {
            key = null;
            if (!root) return false;
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root));
            if (string.IsNullOrEmpty(guid)) return false;
            key = Prefix + guid;
            return true;
        }

        private static bool IsWorkbookPath(string path) =>
            !string.IsNullOrWhiteSpace(path) && string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase);
    }
}
