using System.IO;
using UnityEditor;
using UnityEngine;
using YLFramework.DataAssets;

namespace YLFramework.Editor.DataAssets
{
    [CustomEditor(typeof(ScriptableObjectConfigs), true)]
    public sealed class ScriptableObjectConfigsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var root = (ScriptableObjectConfigs)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "configs", "typeScriptGuid");
            serializedObject.ApplyModifiedProperties();
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(root.typeScriptGuid));
            EditorGUI.BeginChangeCheck();
            var next = (MonoScript)EditorGUILayout.ObjectField("Data Type", script, typeof(MonoScript), false);
            if (EditorGUI.EndChangeCheck())
            {
                try
                {
                    ScriptableObjectConfigsController.SetDataType(root, next);
                }
                catch (System.Exception error) when (error is System.ArgumentException || error is System.InvalidOperationException)
                {
                    EditorUtility.DisplayDialog("DataAssets Type", error.Message, "Close");
                }
            }
            if (!script && !string.IsNullOrEmpty(root.typeScriptGuid) && GUILayout.Button("Clear Invalid Type Binding"))
            {
                ScriptableObjectConfigsController.SetDataType(root, null);
            }

            EditorGUILayout.HelpBox("An empty type binding permits ScriptableObjectData and all its concrete subclasses. A script binding permits that type and its subclasses.", MessageType.Info);
            DrawDiagnostics(root);
            if (GUILayout.Button("Edit Data Assets"))
            {
                ConfigsWindow.Open(root);
            }

            if (GUILayout.Button("Save Root And Owned Entries"))
            {
                ScriptableObjectConfigsController.Save(root);
            }
            // 路径只记在本机EditorPrefs，按资产GUID关联；不写入配置资产，也不随仓库分发。
            var remembered = DataAssetsWorkbookMemory.TryGetPath(root, out var workbook);
            var exists = remembered && File.Exists(workbook);
            using (new EditorGUI.DisabledScope(!exists))
            {
                var content = new GUIContent("在文件夹中打开Excel",
                    !remembered ? "尚未导出过Excel" : exists ? workbook : "最近导出的文件不存在: " + workbook);
                if (GUILayout.Button(content) && exists)
                {
                    DataAssetsWorkbookMemory.Reveal(root);
                }
            }
        }

        public static void DrawDiagnostics(ScriptableObjectConfigs root)
        {
            foreach (var issue in ScriptableObjectConfigsController.Validate(root))
            {
                EditorGUILayout.HelpBox((issue.Row >= 0 ? "Row " + issue.Row + ": " : string.Empty) + issue.Message, MessageType.Error);
                GUILayout.BeginHorizontal();
                if (issue.Asset && GUILayout.Button("Locate Entry"))
                {
                    EditorGUIUtility.PingObject(issue.Asset);
                }

                var unlink = issue.Row >= 0 && GUILayout.Button("Unlink Reference (Undo)");
                GUILayout.EndHorizontal();
                if (unlink)
                {
                    ScriptableObjectConfigsController.RemoveReference(root, issue.Row, issue.Asset);
                    GUIUtility.ExitGUI();
                }
            }
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            var root = EditorUtility.InstanceIDToObject(instanceId) as ScriptableObjectConfigs;
            if (!root)
            {
                return false;
            }

            ConfigsWindow.Open(root);
            return true;
        }
    }
}
