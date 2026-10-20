// Adapted from ScriptableObjectSystem 1.1.4 (MIT). Copyright (c) 2024 弦风课堂.
using UnityEditor;
using UnityEngine;

namespace YLFramework.DataAssets.Editor
{
    [CustomEditor(typeof(ScriptableObjectConfigs), true)]
    public sealed class ScriptableObjectConfigsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var root = (ScriptableObjectConfigs)target;
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script", "configs", "type_script_guid");
            serializedObject.ApplyModifiedProperties();
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(root.type_script_guid));
            EditorGUI.BeginChangeCheck();
            var next = (MonoScript)EditorGUILayout.ObjectField("Data Type", script, typeof(MonoScript), false);
            if (EditorGUI.EndChangeCheck())
            {
                try { ScriptableObjectConfigsController.SetDataType(root, next); }
                catch (System.Exception error) when (error is System.ArgumentException || error is System.InvalidOperationException)
                { EditorUtility.DisplayDialog("DataAssets Type", error.Message, "Close"); }
            }
            if (!script && !string.IsNullOrEmpty(root.type_script_guid) && GUILayout.Button("Clear Invalid Type Binding"))
                ScriptableObjectConfigsController.SetDataType(root, null);
            EditorGUILayout.HelpBox("An empty type binding permits ScriptableObjectData and all its concrete subclasses. A script binding permits that type and its subclasses.", MessageType.Info);
            DrawDiagnostics(root);
            if (GUILayout.Button("Edit Data Assets")) ConfigsWindow.Open(root);
            if (GUILayout.Button("Save Root And Owned Entries")) ScriptableObjectConfigsController.Save(root);
        }

        public static void DrawDiagnostics(ScriptableObjectConfigs root)
        {
            foreach (var issue in ScriptableObjectConfigsController.Validate(root))
            {
                EditorGUILayout.HelpBox((issue.Row >= 0 ? "Row " + issue.Row + ": " : string.Empty) + issue.Message, MessageType.Error);
                GUILayout.BeginHorizontal();
                if (issue.Asset && GUILayout.Button("Locate Entry")) EditorGUIUtility.PingObject(issue.Asset);
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
            if (!root) return false;
            ConfigsWindow.Open(root);
            return true;
        }
    }
}
