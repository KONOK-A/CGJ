using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.DataAssets;
using YLFramework.DataAssets.Editor;

namespace YLFramework.Excel.Editor
{
    /// <summary>只在可选Excel程序集依赖普通DataAssets作者工具，不建立反向依赖。</summary>
    public static class DataAssetsExcelCommands
    {
        public static ExcelImportPlan PreviewImport(ScriptableObjectConfigs root, string path)
        {
            try
            {
                var type = RequireValidRoot(root);
                return ExcelAuthoring.PreviewScriptableObjects(path, root.configs, root,
                    ScriptableObjectConfigsController.GetDataPath(root), initialize: item => item.parent = root, dataType: type);
            }
            catch (Exception e)
            {
                var plan = new ExcelImportPlan();
                plan.Error(e.Message);
                return plan;
            }
        }

        public static void Export(ScriptableObjectConfigs root, string path)
        {
            var type = RequireValidRoot(root);
            ExcelAuthoring.ScriptableObjectToExcel(root.configs, path, dataType: type);
        }

        private static Type RequireValidRoot(ScriptableObjectConfigs root)
        {
            if (root == null || !AssetDatabase.IsMainAsset(root) || !AssetDatabase.GetAssetPath(root).StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("Select a saved YL DataAssets root under Assets/.");
            var type = root.GetDataType();
            if (type != typeof(ScriptableObjectData) && !TypeCache.GetTypesDerivedFrom<ScriptableObjectData>().Contains(type)) throw new InvalidOperationException("Bound data type is unavailable in TypeCache: " + type.FullName);
            var diagnostics = ScriptableObjectConfigsController.Validate(root);
            if (diagnostics.Count != 0) throw new InvalidOperationException(string.Join("\n", diagnostics.Select(d => "Row " + d.Row + ": " + d.Message)));
            foreach (var item in root.configs)
                if (!ScriptableObjectConfigsController.IsOwned(root, item)) throw new InvalidOperationException("Entry is not owned by this root: " + AssetDatabase.GetAssetPath(item));
            return type;
        }

        [MenuItem("Assets/YLFramework/Excel/Preview Import Selected Data Root", false, 2100)]
        private static void ImportSelected()
        {
            var root = Selection.activeObject as ScriptableObjectConfigs;
            string path = EditorUtility.OpenFilePanel("Import YL DataAssets .xlsx", string.Empty, "xlsx");
            if (path.Length == 0) return;
            var window = ExcelAuthoringCommands.ShowPreview(PreviewImport(root, path));
            window.Applied += () => { if (root != null) root.ClearCache(); };
        }

        [MenuItem("Assets/YLFramework/Excel/Preview Import Selected Data Root", true)]
        private static bool CanImportSelected() => Selection.activeObject is ScriptableObjectConfigs;

        [MenuItem("Assets/YLFramework/Excel/Export Selected Data Root", false, 2101)]
        private static void ExportSelected()
        {
            var root = Selection.activeObject as ScriptableObjectConfigs;
            string path = EditorUtility.SaveFilePanel("Export YL DataAssets .xlsx", string.Empty, root == null ? "Data" : root.name, "xlsx");
            if (path.Length == 0) return;
            if (File.Exists(path) && !EditorUtility.DisplayDialog("Replace Workbook", "Replace this entire workbook, including its sheets and formatting?\n" + path, "Replace", "Cancel")) return;
            try { Export(root, path); }
            catch (Exception e) { EditorUtility.DisplayDialog("Excel Export Failed", e.Message, "Close"); }
        }

        [MenuItem("Assets/YLFramework/Excel/Export Selected Data Root", true)]
        private static bool CanExportSelected() => Selection.activeObject is ScriptableObjectConfigs;
    }
}
