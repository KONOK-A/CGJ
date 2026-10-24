using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Utilities;

namespace YLFramework.Editor.Domain
{
    internal sealed class DomainResourceIconGui : YLFramework.Attributes.CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty property)
        {
            EditorGUILayout.PropertyField(property, true);
            var project = property.FindPropertyRelative("projectName");
            var icon = property.FindPropertyRelative("iconName");
            if (EditorGUILayout.DropdownButton(new GUIContent("选择YL资源项目"), FocusType.Passive))
            {
                var menu = new GenericMenu();
                foreach (var guid in AssetDatabase.FindAssets("t:ResourceProject"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<YLFramework.Editor.Resource.ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!asset)
                    {
                        continue;
                    }

                    var id = asset.projectId;
                    menu.AddItem(new GUIContent(id), id == project.stringValue, () =>
                    {
                        project.stringValue = id;
                        project.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.ShowAsContext();
            }
            if (!string.IsNullOrEmpty(project.stringValue) && !string.IsNullOrEmpty(icon.stringValue))
            {
                var preview = EditorAssetTool.GetTextureGuiContent(project.stringValue, icon.stringValue);
                if (GUILayout.Button(preview, GUILayout.Width(48), GUILayout.Height(48)))
                {
                    EditorGUIUtility.PingObject(preview.image);
                }
            }
            EditorGUILayout.HelpBox("projectName填写YL Resource ProjectId，iconName填写资源地址。预览使用既有Resource预览解析器。", MessageType.Info);
        }
    }
}
