// Adapted from ScriptableObjectSystem 1.1.4 (MIT). Copyright (c) 2024 弦风课堂.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YLFramework.DataAssets.Editor
{
    public enum DataAssetSort { Id, Name }

    public readonly struct DataAssetDiagnostic
    {
        public int Row { get; }
        public ScriptableObjectData Asset { get; }
        public string Message { get; }
        internal DataAssetDiagnostic(int row, ScriptableObjectData asset, string message)
        { Row = row; Asset = asset; Message = message; }
    }

    public static class ScriptableObjectConfigsController
    {
        public static List<ScriptableObjectConfigs> GetAllConfigs()
        {
            var result = new List<ScriptableObjectConfigs>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(ScriptableObjectConfigs).FullName, new[] { "Assets" }))
            {
                var root = AssetDatabase.LoadAssetAtPath<ScriptableObjectConfigs>(AssetDatabase.GUIDToAssetPath(guid));
                if (root) result.Add(root);
            }
            return result.OrderBy(root => AssetDatabase.GetAssetPath(root), StringComparer.Ordinal).ToList();
        }

        public static Type GetDataType(this ScriptableObjectConfigs root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrEmpty(root.type_script_guid)) return typeof(ScriptableObjectData);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(root.type_script_guid));
            var type = script ? script.GetClass() : null;
            if (type == null || !typeof(ScriptableObjectData).IsAssignableFrom(type) || type.ContainsGenericParameters)
                throw new InvalidOperationException($"DataAssets '{root.name}': invalid type_script_guid '{root.type_script_guid}'.");
            return type;
        }

        public static IReadOnlyList<Type> GetDataTypes(ScriptableObjectConfigs root)
        {
            var parent = root.GetDataType();
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObjectData>()
                .Where(type => parent.IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters).ToList();
            if (!parent.IsAbstract && !parent.ContainsGenericParameters && !types.Contains(parent)) types.Add(parent);
            return types.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        }

        public static void SetDataType(ScriptableObjectConfigs root, MonoScript script)
        {
            RequireRoot(root);
            var type = script ? script.GetClass() : typeof(ScriptableObjectData);
            if (type == null || !typeof(ScriptableObjectData).IsAssignableFrom(type) || type.ContainsGenericParameters)
                throw new ArgumentException("Select a ScriptableObjectData script.", nameof(script));
            if (root.configs != null && root.configs.Any(item => item && !type.IsInstanceOfType(item)))
                throw new InvalidOperationException("The selected type would exclude existing entries; move or remove those references first.");
            var guid = script ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)) : string.Empty;
            if (script && string.IsNullOrEmpty(guid)) throw new ArgumentException("The type script must be a persistent asset.", nameof(script));
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(root, "Change DataAssets type");
            root.type_script_guid = guid;
            EditorUtility.SetDirty(root);
        }

        public static string GetDataPath(ScriptableObjectConfigs root)
        {
            var path = RequireRoot(root);
            return "Assets/YLFrameworkData/DataAssets/" + AssetDatabase.AssetPathToGUID(path);
        }

        public static ScriptableObjectData CreateData(Type type, ScriptableObjectConfigs root)
        {
            RequireType(root, type);
            var id = root.GenerateID();
            var data = (ScriptableObjectData)ScriptableObject.CreateInstance(type);
            return PersistNew(root, data, id, "Config " + id);
        }

        public static ScriptableObjectData CopyData(ScriptableObjectConfigs root, ScriptableObjectData source)
        {
            RequireMember(root, source);
            RequireType(root, source.GetType());
            var id = root.GenerateID();
            var copyName = GetCopyName(source.config_name, root);
            var copy = Object.Instantiate(source);
            return PersistNew(root, copy, id, copyName);
        }

        private static ScriptableObjectData PersistNew(ScriptableObjectConfigs root, ScriptableObjectData data, int id, string displayName)
        {
            var created = false;
            try
            {
                var folder = GetDataPath(root);
                EnsureFolder(folder);
                data.id = id;
                data.name = id.ToString();
                data.config_name = displayName;
                data.parent = root;
                var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + id + ".asset");
                AssetDatabase.CreateAsset(data, path);
                created = true;
                if (root.configs == null) root.configs = new List<ScriptableObjectData>();
                root.configs.Add(data);
                root.ClearCache();
                EditorUtility.SetDirty(root);
                EditorUtility.SetDirty(data);
                return data;
            }
            finally
            {
                if (!created && data) Object.DestroyImmediate(data);
            }
        }

        public static string GetCopyName(string name, ScriptableObjectConfigs root)
        {
            var stem = Regex.Replace(name ?? string.Empty, @"\s*\([0-9]+\)$", string.Empty).TrimEnd();
            for (var number = 1; number < int.MaxValue; number++)
            {
                var candidate = stem + " (" + number + ")";
                if (root.configs == null || !root.configs.Any(item => item && StringComparer.Ordinal.Equals(item.config_name, candidate))) return candidate;
            }
            throw new InvalidOperationException("No copy name is available.");
        }

        /// <summary>仅改变条目字段，不移动文件或GUID；已有引用保持稳定。</summary>
        public static void RenameData(ScriptableObjectConfigs root, ScriptableObjectData data, int id, string displayName)
        {
            RequireOwned(root, data);
            if (root.configs.Any(item => item && !ReferenceEquals(item, data) && item.id == id))
                throw new InvalidOperationException($"DataAssets '{root.name}': duplicate ID {id}.");
            if (data.id == id && data.config_name == displayName) return;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(data, "Edit DataAssets identity");
            data.id = id;
            data.config_name = displayName;
            root.ClearCache();
            EditorUtility.SetDirty(data);
        }

        public static bool IsOwned(ScriptableObjectConfigs root, ScriptableObjectData data)
        {
            if (!root || !data || data.parent != root || root.configs == null || !root.configs.Contains(data)) return false;
            var rootPath = AssetDatabase.GetAssetPath(root);
            if (!IsWritableAssetPath(rootPath)) return false;
            var path = AssetDatabase.GetAssetPath(data);
            if (path == rootPath) return AssetDatabase.IsSubAsset(data);
            return AssetDatabase.IsMainAsset(data) &&
                StringComparer.Ordinal.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), GetDataPath(root));
        }

        /// <summary>取消无副作用；外部主资产物理删除不可撤销，UI必须明确确认。根内子资产使用Undo销毁。</summary>
        public static bool DeleteData(ScriptableObjectConfigs root, ScriptableObjectData data, bool confirmed)
        {
            if (!confirmed) return false;
            RequireOwned(root, data);
            var path = AssetDatabase.GetAssetPath(data);
            if (AssetDatabase.IsSubAsset(data))
            {
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.RegisterCompleteObjectUndo(root, "Delete DataAssets child");
                root.configs.RemoveAll(item => ReferenceEquals(item, data));
                Undo.DestroyObjectImmediate(data);
                Undo.CollapseUndoOperations(group);
            }
            else
            {
                RequireSingleObjectFile(data);
                if (!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not delete DataAssets child: " + path);
                root.configs.RemoveAll(item => ReferenceEquals(item, data));
            }
            root.ClearCache();
            EditorUtility.SetDirty(root);
            return true;
        }

        /// <summary>只移除指定槽引用，永不删除被引用资产；快照不匹配时拒绝陈旧窗口操作。</summary>
        public static void RemoveReference(ScriptableObjectConfigs root, int row, ScriptableObjectData expected)
        {
            RequireRoot(root);
            if (root.configs == null || row < 0 || row >= root.configs.Count || !ReferenceEquals(root.configs[row], expected))
                throw new InvalidOperationException("The DataAssets row changed; refresh before removing it.");
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(root, "Unlink DataAssets entry");
            root.configs.RemoveAt(row);
            root.ClearCache();
            EditorUtility.SetDirty(root);
        }

        public static bool DeleteRoot(ScriptableObjectConfigs root, bool confirmed)
        {
            if (!confirmed) return false;
            var rootPath = RequireRoot(root);
            // 不注册递归删除监听；仅显式命令处理已验证的本根条目，外部引用和目录内未知文件保留。
            var owned = root.configs == null ? Array.Empty<ScriptableObjectData>() :
                root.configs.Where(item => IsOwned(root, item) && AssetDatabase.IsMainAsset(item)).Distinct().ToArray();
            foreach (var item in AssetDatabase.LoadAllAssetsAtPath(rootPath))
                if (item && !ReferenceEquals(item, root) && (!(item is ScriptableObjectData data) || !IsOwned(root, data)))
                    throw new InvalidOperationException("The root contains unowned sub-assets. Move them out before deleting the root file.");
            foreach (var item in owned) RequireSingleObjectFile(item);
            foreach (var item in owned) DeleteData(root, item, true);
            ScriptableObjectManager.RemoveConfig(root);
            return AssetDatabase.DeleteAsset(rootPath);
        }

        public static void Sort(ScriptableObjectConfigs root, DataAssetSort field, bool ascending)
        {
            RequireRoot(root);
            if (root.configs == null || root.configs.Count < 2) return;
            var sorted = root.configs.Select((item, index) => new { item, index }).ToList();
            sorted.Sort((a, b) =>
            {
                if (!a.item || !b.item) return a.item ? -1 : b.item ? 1 : a.index.CompareTo(b.index);
                var value = field == DataAssetSort.Id ? a.item.id.CompareTo(b.item.id) : StringComparer.Ordinal.Compare(a.item.config_name, b.item.config_name);
                if (!ascending) value = -value;
                return value == 0 ? a.index.CompareTo(b.index) : value;
            });
            if (sorted.Select(item => item.item).SequenceEqual(root.configs)) return;
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(root, "Sort DataAssets entries");
            root.configs = sorted.Select(item => item.item).ToList();
            root.ClearCache();
            EditorUtility.SetDirty(root);
        }

        public static IReadOnlyList<DataAssetDiagnostic> Validate(ScriptableObjectConfigs root)
        {
            var errors = new List<DataAssetDiagnostic>();
            if (!root) return errors;
            Type type = null;
            try { type = root.GetDataType(); }
            catch (InvalidOperationException error) { errors.Add(new DataAssetDiagnostic(-1, null, error.Message)); }
            if (root.configs == null) { errors.Add(new DataAssetDiagnostic(-1, null, "Missing entry list.")); return errors; }
            var ids = new Dictionary<int, int>();
            for (var row = 0; row < root.configs.Count; row++)
            {
                var item = root.configs[row];
                if (!item) { errors.Add(new DataAssetDiagnostic(row, null, "Missing entry reference.")); continue; }
                if (ids.TryGetValue(item.id, out var previous))
                    errors.Add(new DataAssetDiagnostic(row, item, $"duplicate ID {item.id}; first at row {previous}."));
                else ids.Add(item.id, row);
                if (item.parent != root) errors.Add(new DataAssetDiagnostic(row, item, "Parent does not match this root; reference is read-only here."));
                if (type != null && !type.IsInstanceOfType(item)) errors.Add(new DataAssetDiagnostic(row, item, "Entry does not match the bound type."));
            }
            return errors;
        }

        /// <summary>只保存本根及本根拥有的条目，不触发全局SaveAssets。</summary>
        public static void Save(ScriptableObjectConfigs root)
        {
            RequireRoot(root);
            if (root.configs != null)
                foreach (var item in root.configs) if (IsOwned(root, item)) AssetDatabase.SaveAssetIfDirty(item);
            AssetDatabase.SaveAssetIfDirty(root);
        }

        private static bool IsWritableAssetPath(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal);

        private static string RequireRoot(ScriptableObjectConfigs root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            var path = AssetDatabase.GetAssetPath(root);
            if (!IsWritableAssetPath(path) || !AssetDatabase.IsMainAsset(root))
                throw new InvalidOperationException("Save a YL DataAssets root as a main asset under Assets before editing it.");
            return path;
        }

        private static void RequireType(ScriptableObjectConfigs root, Type type)
        {
            RequireRoot(root);
            if (type == null || !root.GetDataType().IsAssignableFrom(type) || type.IsAbstract || type.ContainsGenericParameters)
                throw new ArgumentException("Select a concrete type compatible with the root binding.", nameof(type));
        }

        private static void RequireMember(ScriptableObjectConfigs root, ScriptableObjectData data)
        {
            RequireRoot(root);
            if (!data || root.configs == null || !root.configs.Contains(data)) throw new ArgumentException("The entry does not belong to this list.", nameof(data));
        }

        private static void RequireOwned(ScriptableObjectConfigs root, ScriptableObjectData data)
        {
            RequireMember(root, data);
            if (!IsOwned(root, data)) throw new InvalidOperationException("This root does not own the target asset. Unlink the reference instead.");
        }

        private static void RequireSingleObjectFile(ScriptableObjectData data)
        {
            if (AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(data)).Any(item => item && !ReferenceEquals(item, data)))
                throw new InvalidOperationException("The child file contains other objects; unlink it instead of deleting the file.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Could not create DataAssets folder: " + path);
        }
    }
}
