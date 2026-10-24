using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Integrations.Excel.Editor
{
    /// <summary>可选领域Editor接线入口；关闭文件对话框不创建计划，关闭预览即取消。</summary>
    public static class ExcelAuthoringCommands
    {
        public static void ImportList<T>(List<T> values) where T : class, new()
        {
            string path = EditorUtility.OpenFilePanel("Import .xlsx", string.Empty, "xlsx");
            if (path.Length != 0)
            {
                ShowPreview(ExcelAuthoring.PreviewList(path, values));
            }
        }
        public static void ExportList<T>(IEnumerable<T> values)
        {
            string path = SavePath();
            if (path.Length != 0)
            {
                ExcelAuthoring.ListToExcel(values, path);
            }
        }
        public static void ImportScriptableObjects<T>(List<T> values, ScriptableObject owner, string assetFolder = null, Action<T> initialize = null) where T : ScriptableObject
        {
            string path = EditorUtility.OpenFilePanel("Import .xlsx", string.Empty, "xlsx");
            if (path.Length != 0)
            {
                ShowPreview(ExcelAuthoring.PreviewScriptableObjects(path, values, owner, assetFolder, initialize: initialize));
            }
        }
        public static void ExportScriptableObjects<T>(IEnumerable<T> values) where T : ScriptableObject
        {
            string path = SavePath();
            if (path.Length != 0)
            {
                ExcelAuthoring.ScriptableObjectToExcel(values, path);
            }
        }
        public static ExcelPreviewWindow ShowPreview(ExcelImportPlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var window = ScriptableObject.CreateInstance<ExcelPreviewWindow>();
            window.SetPlan(plan);
            window.ShowUtility();
            return window;
        }
        private static string SavePath()
        {
            string path = EditorUtility.SaveFilePanel("Export .xlsx", string.Empty, "Data", "xlsx");
            if (path.Length != 0 && File.Exists(path) && !EditorUtility.DisplayDialog("Replace workbook", "Replace this entire workbook? Existing sheets and formatting will be replaced.\n" + path, "Replace", "Cancel"))
            {
                return string.Empty;
            }

            return path;
        }
    }

    public sealed class ExcelPreviewWindow : EditorWindow
    {
        private ExcelImportPlan _plan;
        private Vector2 _scroll;
        public ExcelImportPlan Plan => _plan;
        public event Action Applied;
        internal void SetPlan(ExcelImportPlan value)
        {
            _plan?.Dispose();
            _plan = value;
            titleContent = new GUIContent("Excel Import Preview");
            minSize = new Vector2(620, 380);
        }
        private void OnGUI()
        {
            if (_plan == null)
            {
                EditorGUILayout.HelpBox("The preview was closed or scripts reloaded. Preview again before importing.", MessageType.Info);
                return;
            }
            EditorGUILayout.HelpBox("No target is changed until Confirm Import. Review additions, updates and removals. Saved asset changes are destructive; keep version-control backups.", MessageType.Info);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var error in _plan.Errors)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            foreach (var change in _plan.Changes)
            {
                EditorGUILayout.LabelField(change.Kind + "  " + change.Key, EditorStyles.boldLabel);
                if (change.Before != null)
                {
                    EditorGUILayout.SelectableLabel("Before: " + change.Before, EditorStyles.textArea, GUILayout.MinHeight(35));
                }

                if (change.After != null)
                {
                    EditorGUILayout.SelectableLabel("After:  " + change.After, EditorStyles.textArea, GUILayout.MinHeight(35));
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.LabelField("Result", _plan.Result.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_plan.CanCommit))
                {
                    if (GUILayout.Button("Confirm Import") && EditorUtility.DisplayDialog("Confirm Excel Import", "Apply this preview, including all listed removals?", "Import", "Cancel"))
                    {
                        if (_plan.Commit(true) == ExcelCommitResult.Applied)
                        {
                            try
                            {
                                Applied?.Invoke();
                            }
                            finally
                            {
                                Close();
                            }
                        }
                    }
                }
                if (GUILayout.Button("Cancel"))
                {
                    _plan.Commit(false);
                    Close();
                }
            }
        }
        private void OnDisable() => _plan?.Dispose();
    }

    public sealed class ExcelJsonWindow : EditorWindow
    {
        private string _workbook = string.Empty;
        private string _destination = string.Empty;
        private string _sheet = string.Empty;
        private string _excludedPrefix = string.Empty;
        [MenuItem("YLFramework/Excel/Workbook To JSON")]
        private static void Open() => GetWindow<ExcelJsonWindow>("Excel To JSON");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Rows 1-3: field / type / description. Data starts at row 4. Enum values without a CLR target type are retained as strings. Object references remain GUID[:localFileID] strings.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                _workbook = EditorGUILayout.TextField("Workbook", _workbook);
                if (GUILayout.Button("Browse", GUILayout.Width(75)))
                {
                    string value = EditorUtility.OpenFilePanel("Workbook", string.Empty, "xlsx");
                    if (value.Length != 0)
                    {
                        _workbook = value;
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _destination = EditorGUILayout.TextField("JSON Output", _destination);
                if (GUILayout.Button("Browse", GUILayout.Width(75)))
                {
                    string value = EditorUtility.SaveFilePanel("JSON Output", string.Empty, "Data", "json");
                    if (value.Length != 0)
                    {
                        _destination = value;
                    }
                }
            }
            _sheet = EditorGUILayout.TextField("Sheet (blank = first)", _sheet);
            _excludedPrefix = EditorGUILayout.TextField("Excluded Field Prefix", _excludedPrefix);
            if (GUILayout.Button("Preview JSON Changes"))
            {
                ExcelAuthoringCommands.ShowPreview(ExcelAuthoring.PreviewJson(_workbook, _destination, _excludedPrefix, string.IsNullOrWhiteSpace(_sheet) ? null : _sheet));
            }
        }
    }
}
