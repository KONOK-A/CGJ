using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Domain;
using YLFramework.Editor.Utilities;
using YLFramework.Equipment;

namespace YLFramework.Editor.Equipment
{
    public static class EquipmentsConfigController
    {
        public static List<EquipmentsConfig> GetAllEquipmentsConfig() => AssetDatabase.FindAssets("t:" + typeof(EquipmentsConfig).FullName)
            .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentsConfig>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c).ToList();

        public static bool ContainsId(int id) => GetAllEquipmentsConfig().Any(c => c.Equipments.Any(d => d && d.id == id));
        public static Type GetDataType(EquipmentsConfig root) => EquipmentAuthoring.GetDataType(root);

        public static string GetEquipmentConfigDataPath(EquipmentsConfig root)
        {
            if (!root || !AssetDatabase.IsMainAsset(root))
            {
                throw new InvalidOperationException("装备配表必须是已保存的主资产。");
            }

            var path = AssetDatabase.GetAssetPath(root);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("只允许编辑 Assets 内的装备配表。");
            }

            return "Assets/YLFrameworkData/Equipment/" + AssetDatabase.AssetPathToGUID(path);
        }

        public static int GenerateId(EquipmentsConfig root)
        {
            var next = root.Equipments.Where(d => d).Select(d => d.id).Append(root.startId).Max();
            var occupied = new HashSet<int>(GetAllEquipmentsConfig().SelectMany(c => c.Equipments).Where(d => d).Select(d => d.id));
            do
            {
                next = checked(next + 1);
            } while (occupied.Contains(next));
            return next;
        }

        public static int CreateEquipmentConfigData(Type type, EquipmentsConfig root)
        {
            if (type == null || !root.GetDataType().IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
            {
                throw new InvalidOperationException("无效的装备配置类型: " + type);
            }

            var data = (EquipmentConfigData)ScriptableObject.CreateInstance(type);
            try
            {
                return CreateEquipmentConfigData(data, root);
            }
            catch
            {
                if (data && !EditorUtility.IsPersistent(data))
                {
                    UnityEngine.Object.DestroyImmediate(data);
                }
                throw;
            }
        }

        public static int CreateEquipmentConfigData(EquipmentConfigData data, EquipmentsConfig root)
        {
            var folder = GetEquipmentConfigDataPath(root);
            if (!data || EditorUtility.IsPersistent(data))
            {
                throw new InvalidOperationException("创建入口要求尚未保存的独立装备定义。");
            }

            if (!root.GetDataType().IsInstanceOfType(data))
            {
                throw new InvalidOperationException("装备类型不符合本配表数据类型。");
            }

            var id = GenerateId(root);
            EquipmentAuthoring.EnsureFolder(folder);
            data.id = id;
            data.parent = root;
            data.name = id.ToString();
            if (string.IsNullOrEmpty(data.equipmentName))
            {
                data.equipmentName = "装备:" + id;
            }

            AssetDatabase.CreateAsset(data, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset"));
            Undo.RegisterCreatedObjectUndo(data, "Create equipment");
            Undo.RecordObject(root, "Add equipment");
            root.Equipments.Add(data);
            root.ClearCache();
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.SaveAssetIfDirty(root);
            return id;
        }

        public static int CopyEquipmentConfigData(EquipmentConfigData source, EquipmentsConfig root)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            copy.equipmentName = GetCopyName(source.equipmentName, root);
            try
            {
                return CreateEquipmentConfigData(copy, root);
            }
            catch
            {
                if (copy && !EditorUtility.IsPersistent(copy))
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }
                throw;
            }
        }

        public static void DeleteEquipmentConfigData(EquipmentConfigData data, EquipmentsConfig root)
        {
            var folder = GetEquipmentConfigDataPath(root);
            var path = AssetDatabase.GetAssetPath(data);
            if (data && path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal) && AssetDatabase.IsMainAsset(data) && AssetDatabase.LoadAllAssetsAtPath(path).Length == 1)
            {
                EquipmentAssetDeletion.Delete(data, root, path);
            }
            else
            {
                DomainAssetOwnership.DeleteEntry(root, root.Equipments, data, folder);
            }

            root.ClearCache();
        }

        public static void RefreshEntries(EquipmentsConfig root)
        {
            var folder = GetEquipmentConfigDataPath(root);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var next = new List<EquipmentConfigData>(root.Equipments);
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(EquipmentConfigData).FullName, new[] { folder }).OrderBy(g => g, StringComparer.Ordinal))
            {
                var data = AssetDatabase.LoadAssetAtPath<EquipmentConfigData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data && ReferenceEquals(data.parent, root) && !next.Contains(data))
                {
                    next.Add(data);
                }
            }
            var ids = new HashSet<int>();
            foreach (var data in next)
            {
                if (!data || !ids.Add(data.id))
                {
                    throw new InvalidOperationException("刷新装备配表发现空条目或重复 ID: " + (data ? data.id.ToString() : "Missing"));
                }
            }

            if (next.SequenceEqual(root.Equipments))
            {
                return;
            }

            Undo.RecordObject(root, "Refresh equipment entries");
            root.Equipments.Clear();
            root.Equipments.AddRange(next);
            root.ClearCache();
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssetIfDirty(root);
        }

        public static void CreateAsset(ScriptableObject value, string path, bool ping = true)
        {
            EquipmentAuthoring.EnsureFolder(path);
            AssetDatabase.CreateAsset(value, AssetDatabase.GenerateUniqueAssetPath(path.TrimEnd('/') + "/" + value.GetType().Name + ".asset"));
            Selection.activeObject = value;
            if (ping)
            {
                EditorGUIUtility.PingObject(value);
            }
        }

        public static string GetCopyName(string name, EquipmentsConfig root)
        {
            var stem = Regex.Replace(name ?? "装备", @"\s*\([0-9]+\)$", "").TrimEnd();
            var i = 1;
            while (root.Equipments.Any(d => d && d.equipmentName == stem + " (" + i + ")"))
            {
                i++;
            }

            return stem + " (" + i + ")";
        }
    }
}
