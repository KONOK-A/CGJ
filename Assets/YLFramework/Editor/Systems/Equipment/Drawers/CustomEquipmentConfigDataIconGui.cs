using UnityEditor;
using UnityEngine;
using YLFramework.Attributes;
using YLFramework.Editor.Utilities;
namespace YLFramework.Editor.Equipment
{
    internal sealed class CustomEquipmentConfigDataIconGui : CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty property)
        {
            EditorGUILayout.LabelField("Icon", EditorStyles.boldLabel);
            var project = property.FindPropertyRelative("projectName");
            var path = property.FindPropertyRelative("iconName");
            EquipmentAuthoring.DrawProject(project);
            EquipmentAuthoring.DrawAddress(project, path, typeof(UnityEngine.Object));
            if (!string.IsNullOrEmpty(project.stringValue) && !string.IsNullOrEmpty(path.stringValue))
            {
                GUILayout.Label(EditorAssetTool.GetTextureGuiContent(project.stringValue, path.stringValue), GUILayout.Width(48), GUILayout.Height(48));
            }
        }
    }
}
