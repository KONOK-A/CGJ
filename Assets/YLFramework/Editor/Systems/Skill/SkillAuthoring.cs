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
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.type_script_guid));
            var type = script ? script.GetClass() : null;
            if (type == null) return typeof(SkillConfigData);
            if (!typeof(SkillConfigData).IsAssignableFrom(type) || type.IsAbstract) throw new InvalidOperationException($"技能配表数据类型无效: {config.name}, {type.FullName}");
            return type;
        }

        internal static void Rename(this SkillConfigData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            AssetDatabase.RenameAsset(path, AssetNameTools.GetName(data.id.ToString(), System.IO.Path.GetDirectoryName(path)));
        }
    }

    internal sealed class CustomSkillConfigDataIconGUI : YLFramework.Foundation.Attributes.CustomPropertyGUI
    {
        public override void OnPropertyGUI(SerializedProperty property)
        {
            EditorGUILayout.PropertyField(property, true);
            var project = property.FindPropertyRelative("projectName");
            var icon = property.FindPropertyRelative("iconName");
            if (!string.IsNullOrEmpty(project.stringValue) && !string.IsNullOrEmpty(icon.stringValue))
            {
                var preview = EditorAssetTool.GetTextureGUIContent(project.stringValue, icon.stringValue);
                if (GUILayout.Button(preview, GUILayout.Width(48), GUILayout.Height(48))) EditorGUIUtility.PingObject(preview.image);
            }
            EditorGUILayout.HelpBox("projectName填写YL Resource ProjectId，iconName填写资源地址。预览使用既有Resource预览解析器。", MessageType.Info);
        }
    }
}
