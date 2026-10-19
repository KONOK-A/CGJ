using System;
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.SkillSystem
{
    internal static class SkillAuthoring
    {
        internal static Type GetDataType(this SkillsConfig config)
        {
            if (string.IsNullOrEmpty(config.type_script_guid)) return typeof(SkillConfigData);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.type_script_guid));
            var type = script ? script.GetClass() : null;
            if (type == null) throw new InvalidOperationException($"技能数据类型绑定失效: {config.name}, GUID={config.type_script_guid}");
            if (!typeof(SkillConfigData).IsAssignableFrom(type) || type.IsAbstract) throw new InvalidOperationException($"技能配表数据类型无效: {config.name}, {type.FullName}");
            return type;
        }

        internal static void Rename(this SkillConfigData data)
        {
            YLFramework.Editor.Domain.DomainAssetOwnership.RenameEntry(data.parent, data, data.id.ToString(), SkillsConfigController.GetSkillConfigDataPath(data.parent));
            data.parent.ClearCache();
        }
    }

}

namespace YLFramework.Editor.Domain
{
    internal sealed class DomainResourceIconGUI : YLFramework.Foundation.Attributes.CustomPropertyGUI
    {
        public override void OnPropertyGUI(SerializedProperty property)
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
                    if (!asset) continue;
                    var id = asset.ProjectId;
                    menu.AddItem(new GUIContent(id), id == project.stringValue, () => { project.stringValue = id; project.serializedObject.ApplyModifiedProperties(); });
                }
                menu.ShowAsContext();
            }
            if (!string.IsNullOrEmpty(project.stringValue) && !string.IsNullOrEmpty(icon.stringValue))
            {
                var preview = EditorAssetTool.GetTextureGUIContent(project.stringValue, icon.stringValue);
                if (GUILayout.Button(preview, GUILayout.Width(48), GUILayout.Height(48))) EditorGUIUtility.PingObject(preview.image);
            }
            EditorGUILayout.HelpBox("projectName填写YL Resource ProjectId，iconName填写资源地址。预览使用既有Resource预览解析器。", MessageType.Info);
        }
    }
}
