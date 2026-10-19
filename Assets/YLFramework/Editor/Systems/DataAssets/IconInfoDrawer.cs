// Adapted from ScriptableObjectSystem 1.1.4 (MIT). Copyright (c) 2024 弦风课堂.
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.DataAssets.Editor
{
    [CustomPropertyDrawer(typeof(IconInfo))]
    public sealed class IconInfoDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight * 3 + 6;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                var line = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(new Rect(position.x, position.y, position.width, line), label);
                var project = property.FindPropertyRelative("projectName");
                var address = property.FindPropertyRelative("iconName");
                var width = Mathf.Max(0, position.width - 48);
                EditorGUI.PropertyField(new Rect(position.x, position.y + line + 2, width, line), project, new GUIContent("ProjectId"));
                EditorGUI.PropertyField(new Rect(position.x, position.y + line * 2 + 4, width, line), address, new GUIContent("Address"));
                var preview = EditorAssetTool.GetTextureGUIContent(project.stringValue, address.stringValue);
                if (GUI.Button(new Rect(position.xMax - 42, position.y + line + 2, 40, 40), preview) && preview.image)
                    EditorGUIUtility.PingObject(preview.image);
            }
        }
    }
}
