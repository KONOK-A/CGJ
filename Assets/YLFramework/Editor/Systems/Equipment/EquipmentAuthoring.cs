using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Domain;
using YLFramework.Editor.Resource;
using YLFramework.Editor.Utilities;
using YLFramework.Equipment;

namespace YLFramework.Editor.Equipment
{
    internal static class EquipmentAuthoring
    {
        internal static Type GetDataType(this EquipmentsConfig root)
        {
            if (string.IsNullOrEmpty(root.typeScriptGuid))
            {
                return typeof(EquipmentConfigData);
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(root.typeScriptGuid));
            var type = script ? script.GetClass() : null;
            if (type == null || !typeof(EquipmentConfigData).IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new InvalidOperationException($"装备配表数据类型失效: {root.name}, GUID={root.typeScriptGuid}");
            }

            return type;
        }
        internal static void Rename(this EquipmentConfigData data)
        {
            if (!data.parent || data.name == data.id.ToString())
            {
                return;
            }

            DomainAssetOwnership.RenameEntry(data.parent, data, data.id.ToString(), EquipmentsConfigController.GetEquipmentConfigDataPath(data.parent));
            data.parent.ClearCache();
        }
        internal static void EnsureFolder(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Expected an Assets folder.", nameof(path));
            }

            var current = "Assets";
            foreach (var part in path.Substring(7).Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    throw new ArgumentException("Invalid asset folder.", nameof(path));
                }

                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, part);
                }

                current = next;
            }
        }
        internal static void SetString(SerializedProperty property, string value)
        {
            var target = property.serializedObject.targetObject;
            var path = property.propertyPath;
            if (!target)
            {
                return;
            }

            using var serialized = new SerializedObject(target);
            serialized.FindProperty(path).stringValue = value;
            serialized.ApplyModifiedProperties();
        }
        internal static void DrawProject(SerializedProperty property)
        {
            EditorGUILayout.PropertyField(property);
            if (!EditorGUILayout.DropdownButton(new GUIContent("选择 YL 资源项目"), FocusType.Passive))
            {
                return;
            }

            var menu = new GenericMenu();
            foreach (var guid in AssetDatabase.FindAssets("t:ResourceProject"))
            {
                var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!project)
                {
                    continue;
                }

                var id = project.projectId;
                menu.AddItem(new GUIContent(id), id == property.stringValue, () => SetString(property, id));
            }
            menu.ShowAsContext();
        }
        internal static void DrawAddress(SerializedProperty project, SerializedProperty address, Type type)
        {
            EditorGUILayout.PropertyField(address);
            if (!EditorGUILayout.DropdownButton(new GUIContent("选择正式资源地址"), FocusType.Passive))
            {
                return;
            }

            var menu = new GenericMenu();
            try
            {
                var projects = AssetDatabase.FindAssets("t:ResourceProject").Select(g => AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(g)))
                    .Where(p => p && p.projectId == project.stringValue).ToArray();
                if (projects.Length != 1)
                {
                    throw new InvalidOperationException("请选择唯一的 YL ResourceProject: " + project.stringValue);
                }

                foreach (var entry in ResourceBuildMap.Create(projects[0]).Assets.Where(a => type.IsAssignableFrom(a.AssetType)))
                {
                    var path = entry.Address.Path;
                    menu.AddItem(new GUIContent(path), path == address.stringValue, () => SetString(address, path));
                }
                menu.ShowAsContext();
            }
            catch (Exception error)
            {
                EditorUtility.DisplayDialog("装备资源地址", error.Message, "关闭");
            }
        }
    }
}
