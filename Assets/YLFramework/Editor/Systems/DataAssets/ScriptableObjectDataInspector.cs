using UnityEditor;
using UnityEngine;

namespace YLFramework.DataAssets.Editor
{
    [CustomEditor(typeof(ScriptableObjectData), true)]
    public sealed class ScriptableObjectDataInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var data = (ScriptableObjectData)target;
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("Parent", data.parent, typeof(ScriptableObjectConfigs), false);
            var owned = ScriptableObjectConfigsController.IsOwned(data.parent, data);
            if (!owned) EditorGUILayout.HelpBox("This entry is not owned by the referenced YL root. It is read-only here; unlinking never deletes external assets.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(!owned))
            {
                EditorGUI.BeginChangeCheck();
                var id = EditorGUILayout.DelayedIntField("ID", data.id);
                var displayName = EditorGUILayout.DelayedTextField("Name", data.config_name);
                if (EditorGUI.EndChangeCheck())
                {
                    try { ScriptableObjectConfigsController.RenameData(data.parent, data, id, displayName); }
                    catch (System.InvalidOperationException error) { EditorUtility.DisplayDialog("DataAssets Identity", error.Message, "Close"); }
                }
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, "m_Script", "parent", "id", "config_name");
                if (serializedObject.ApplyModifiedProperties() && data.parent) data.parent.ClearCache();
            }
        }
    }
}
