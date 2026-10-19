// Adapted from XF ExcelSystem 1.0.7 (MIT, Copyright (c) 2025 弦风课堂).
// YL: preflight, explicit ownership, staged conversion and file-backed rollback.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YLFramework.Excel.Editor
{
    public static partial class ExcelAuthoring
    {
        public static void ScriptableObjectToExcel<T>(IEnumerable<T> values, string path, string sheetName = "Sheet1", Type dataType = null) where T : ScriptableObject
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var rows = values.Cast<object>().ToList();
            dataType = ValidateDataType<T>(dataType);
            if (rows.Any(item => item != null && !dataType.IsInstanceOfType(item))) throw new InvalidOperationException("An entry does not match the bound data type: " + dataType.FullName);
            Snapshots(rows, true);
            Export(rows, dataType, path, sheetName, true);
        }

        /// <summary>
        /// 默认只管理owner同文件的子资产；assetFolder显式选择独占目录下的独立主资产。
        /// initialize仅作用于暂存副本，可用于绑定领域parent；不得修改其他资产或全局状态。
        /// </summary>
        public static ExcelImportPlan PreviewScriptableObjects<T>(string path, List<T> values, ScriptableObject owner,
            string assetFolder = null, string sheetName = null, Action<T> initialize = null, Type dataType = null) where T : ScriptableObject
        {
            var plan = new ExcelImportPlan();
            var staged = new List<T>();
            plan.Prepare(null, () => { foreach (var item in staged) if (item != null) Object.DestroyImmediate(item); });
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Excel asset authoring is only available outside Play Mode.");
                if (owner == null || values == null) throw new ArgumentNullException(owner == null ? nameof(owner) : nameof(values));
                dataType = ValidateDataType<T>(dataType);
                string ownerPath = AssetDatabase.GetAssetPath(owner);
                if (!AssetDatabase.IsMainAsset(owner) || !IsAssetPath(ownerPath)) throw new InvalidOperationException("Owner must be a saved main ScriptableObject asset under Assets/.");
                var listField = OwnerListField(owner, values);
                string folder = assetFolder == null ? null : NormalizeFolder(assetFolder);
                var previous = values.ToArray();
                if (previous.Any(item => item != null && !dataType.IsInstanceOfType(item))) throw new InvalidOperationException("An existing entry does not match the bound data type: " + dataType.FullName);
                ValidateOwnedAssets(owner, ownerPath, previous, folder);
                var before = Snapshots(previous.Cast<object>(), true);
                string ownerState = EditorJsonUtility.ToJson(owner);
                var originalStates = previous.ToDictionary(item => item, item => EditorJsonUtility.ToJson(item));
                var files = CaptureAssetFiles(previous.Select(AssetDatabase.GetAssetPath).Concat(new[] { ownerPath }));
                var table = ExcelTable.Read(path, sheetName);
                var existing = previous.ToDictionary(item => ExcelValues.Key(item, 0, true));
                var importedKeys = new HashSet<string>(StringComparer.Ordinal);
                var typeColumn = table.Columns.FirstOrDefault(c => c.Name == DataTypeColumn);
                var allowedTypes = TypeCache.GetTypesDerivedFrom<T>().Concat(new[] { typeof(T) }).Where(t => dataType.IsAssignableFrom(t) && !t.IsAbstract && !t.ContainsGenericParameters).ToArray();
                foreach (var row in table.Rows)
                {
                    try
                    {
                        string typeName = typeColumn == null ? dataType.FullName : ExcelTable.Text(table.Value(row.Value, typeColumn));
                        var candidates = allowedTypes.Where(t => t.FullName == typeName || t.AssemblyQualifiedName == typeName).ToArray();
                        if (candidates.Length != 1) throw new InvalidOperationException("Missing, ambiguous or incompatible ScriptableObject type: " + typeName);
                        var type = candidates[0];
                        var id = ExcelValues.Id(type);
                        if (id == null) throw new InvalidOperationException("Missing ExcelScriptableObjectID or id field: " + type.FullName);
                        var idColumn = table.Columns.FirstOrDefault(c => c.Name == id.Name);
                        if (idColumn == null) throw new InvalidOperationException("Missing ID column " + id.Name);
                        string key = RawIdentity(table.Value(row.Value, idColumn), id);
                        if (!importedKeys.Add(key)) throw new InvalidOperationException("Duplicate ID: " + key);
                        T target = existing.TryGetValue(key, out var old) && old.GetType() == type ? Object.Instantiate(old) : (T)ScriptableObject.CreateInstance(type);
                        staged.Add(target);
                        if (old != null && old.GetType() == type) target.name = old.name;
                        target.hideFlags = HideFlags.HideAndDontSave;
                        initialize?.Invoke(target);
                        ImportRow(table, row, target, plan);
                        if (ExcelValues.Key(target, 0, true) != key) throw new InvalidOperationException("ID converter changed identity: " + key);
                    }
                    catch (Exception e) { plan.Error(table.Source + ": row " + row.Key + ": " + e.Message); }
                }
                var removed = previous.Where(old => !staged.Any(item => item.GetType() == old.GetType() && ExcelValues.Key(item, 0, true) == ExcelValues.Key(old, 0, true))).ToArray();
                foreach (var item in staged)
                foreach (var field in ExcelValues.Fields(item.GetType(), true).Where(f => typeof(Object).IsAssignableFrom(f.FieldType)))
                {
                    var reference = field.GetValue(item) as T;
                    if (reference != null && removed.Contains(reference)) plan.Error(table.Source + ": ID " + ExcelValues.Key(item, 0, true) + " field " + field.Name + " references an asset listed for deletion.");
                }
                if (plan.Errors.Count == 0) plan.Diff(before, Snapshots(staged.Cast<object>(), true));
                var stagedStates = staged.Select(item => ExcelValues.State(item)).ToArray();
                var destinations = new Dictionary<string, string>();
                foreach (var item in staged)
                {
                    string key = ExcelValues.Key(item, 0, true);
                    string suffix = existing.TryGetValue(key, out var old) && old.GetType() != item.GetType() ? "-" + item.GetType().Name : string.Empty;
                    destinations.Add(key, folder == null ? ownerPath : folder + "/" + SafeAssetName(key + suffix) + ".asset");
                }
                ValidateDestinations(staged, existing, destinations, folder);
                plan.Prepare(() =>
                {
                    table.AssertUnchanged();
                    if (owner == null || !ReferenceEquals(listField.GetValue(owner), values) || !values.SequenceEqual(previous)
                        || ownerState != EditorJsonUtility.ToJson(owner) || previous.Any(item => item == null || originalStates[item] != EditorJsonUtility.ToJson(item)))
                        throw new InvalidOperationException("Assets changed after preview; preview again.");
                    if (!staged.Select(item => ExcelValues.State(item)).SequenceEqual(stagedStates)) throw new InvalidOperationException("Staged data or asset references changed after preview; preview again.");
                    ValidateOwnedAssets(owner, ownerPath, previous, folder);
                    AssertAssetFilesUnchanged(files);
                    ValidateDestinations(staged, existing, destinations, folder);
                    ApplyAssets(values, owner, listField, ownerPath, previous, staged, existing, destinations, files, folder);
                }, () => { foreach (var item in staged) if (item != null) Object.DestroyImmediate(item); });
            }
            catch (Exception e) { plan.Error(path + ": " + e.Message); }
            return plan;
        }

        private static Type ValidateDataType<T>(Type type) where T : ScriptableObject
        {
            type = type ?? typeof(T);
            if (!typeof(T).IsAssignableFrom(type) || type.ContainsGenericParameters) throw new ArgumentException("Data type must be compatible with " + typeof(T).FullName, nameof(type));
            return type;
        }

        private static string RawIdentity(object value, FieldInfo field)
        {
            if (field.GetCustomAttribute<CustomExcelFieldImportAndExport>() != null) throw new InvalidOperationException("Custom conversion on identity is not supported; keep a stable scalar ID.");
            string raw = ExcelTable.Text(value);
            if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("Empty ID.");
            if (field.FieldType == typeof(string)) return raw;
            if (field.FieldType.IsEnum) return Enum.Parse(field.FieldType, raw, true).ToString();
            if (!field.FieldType.IsPrimitive || field.FieldType == typeof(bool) || field.FieldType == typeof(float) || field.FieldType == typeof(double))
                throw new FormatException("ID must be a string, enum or integer: " + field.Name);
            return ExcelTable.Text(Convert.ChangeType(raw, field.FieldType, CultureInfo.InvariantCulture));
        }

        private static FieldInfo OwnerListField<T>(ScriptableObject owner, List<T> values)
        {
            for (var type = owner.GetType(); type != typeof(ScriptableObject); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!field.IsInitOnly && !field.IsNotSerialized && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    && ReferenceEquals(field.GetValue(owner), values)) return field;
            }
            throw new InvalidOperationException("The supplied List must be the owner's serialized field, not a copy.");
        }
        private static bool IsAssetPath(string path) => path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Split('/').Any(p => p == "." || p == "..");
        private static string NormalizeFolder(string folder)
        {
            folder = folder.Replace('\\', '/').TrimEnd('/');
            if (!IsAssetPath(folder)) throw new InvalidOperationException("Select an owned asset folder under Assets/: " + folder);
            string current = "Assets";
            foreach (var part in folder.Substring("Assets/".Length).Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part)) throw new InvalidOperationException("Invalid asset folder: " + folder);
                current += "/" + part;
                if (File.Exists(current) || !Directory.Exists(current) && File.Exists(current + ".meta")) throw new IOException("Folder path conflicts with an existing asset/meta: " + current);
            }
            return folder;
        }
        private static string SafeAssetName(string key)
        {
            if (key == "." || key == ".." || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key.TrimEnd('.', ' ') != key) throw new FormatException("ID is not a safe asset file name: " + key);
            return key;
        }
        private static void ValidateOwnedAssets<T>(ScriptableObject owner, string ownerPath, IEnumerable<T> previous, string folder) where T : ScriptableObject
        {
            if (EditorUtility.IsDirty(owner)) throw new InvalidOperationException("Save the owner before preview/import: " + ownerPath);
            foreach (var item in previous)
            {
                if (item == null || ReferenceEquals(item, owner)) throw new InvalidOperationException("Missing item or owner itself in list.");
                string path = AssetDatabase.GetAssetPath(item);
                bool owned = path == ownerPath && AssetDatabase.IsSubAsset(item)
                    || folder != null && IsAssetPath(path) && Path.GetDirectoryName(path).Replace('\\', '/') == folder && AssetDatabase.IsMainAsset(item)
                      && AssetDatabase.LoadAllAssetsAtPath(path).Length == 1;
                if (!owned) throw new InvalidOperationException("Refusing to update/delete an asset outside the explicit ownership boundary: " + path);
                if (EditorUtility.IsDirty(item)) throw new InvalidOperationException("Save the item before preview/import: " + path);
            }
        }
        private static void ValidateDestinations<T>(IEnumerable<T> staged, IDictionary<string, T> existing, IDictionary<string, string> paths, string folder) where T : ScriptableObject
        {
            if (folder == null) return;
            var newPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in staged)
            {
                string key = ExcelValues.Key(item, 0, true);
                if (existing.TryGetValue(key, out var old) && old.GetType() == item.GetType()) continue;
                string destination = paths[key];
                if (!newPaths.Add(destination)) throw new IOException("Colliding asset paths: " + destination);
                if (File.Exists(destination) || File.Exists(destination + ".meta")) throw new IOException("Destination exists; never overwrite unrelated asset/type: " + destination);
            }
        }

        private static Dictionary<string, byte[]> CaptureAssetFiles(IEnumerable<string> assetPaths)
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in assetPaths.Distinct())
            {
                files[path] = File.ReadAllBytes(path);
                files[path + ".meta"] = File.ReadAllBytes(path + ".meta");
            }
            return files;
        }
        private static void AssertAssetFilesUnchanged(IDictionary<string, byte[]> files)
        {
            foreach (var file in files)
                if (!File.Exists(file.Key) || !File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) throw new IOException("Asset file changed after preview: " + file.Key);
        }

        private static void ApplyAssets<T>(List<T> values, ScriptableObject owner, FieldInfo listField, string ownerPath,
            T[] previous, List<T> staged, IDictionary<string, T> existing, IDictionary<string, string> destinations,
            IDictionary<string, byte[]> files, string folder) where T : ScriptableObject
        {
            var created = new List<T>();
            var detached = new List<T>();
            var current = new List<T>();
            var createdFolders = new List<string>();
            string recovery = CreateRecovery(files);
            bool keepRecovery = false;
            try
            {
                if (folder != null) EnsureFolder(folder, createdFolders);
                foreach (var item in staged)
                {
                    string key = ExcelValues.Key(item, 0, true);
                    T target;
                    if (existing.TryGetValue(key, out var old) && old.GetType() == item.GetType())
                    {
                        target = old;
                        var flags = target.hideFlags;
                        EditorUtility.CopySerialized(item, target);
                        target.hideFlags = flags;
                    }
                    else
                    {
                        target = Object.Instantiate(item);
                        target.name = key;
                        target.hideFlags = HideFlags.None;
                        created.Add(target);
                        if (folder == null) AssetDatabase.AddObjectToAsset(target, owner);
                        else AssetDatabase.CreateAsset(target, destinations[key]);
                    }
                    EditorUtility.SetDirty(target);
                    current.Add(target);
                }
                foreach (var item in previous.Where(item => !current.Contains(item)))
                {
                    if (AssetDatabase.IsSubAsset(item)) { AssetDatabase.RemoveObjectFromAsset(item); detached.Add(item); }
                }
                values.Clear();
                values.AddRange(current);
                EditorUtility.SetDirty(owner);
                foreach (var item in current) AssetDatabase.SaveAssetIfDirty(item);
                AssetDatabase.SaveAssetIfDirty(owner);
                if (EditorUtility.IsDirty(owner) || current.Any(EditorUtility.IsDirty)) throw new IOException("Unity did not save all imported assets.");
                foreach (var item in previous.Where(item => !current.Contains(item) && !detached.Contains(item)))
                {
                    string path = AssetDatabase.GetAssetPath(item);
                    if (!AssetDatabase.DeleteAsset(path)) throw new IOException("Could not delete owned asset: " + path);
                }
            }
            catch (Exception failure)
            {
                var rollbackErrors = new List<Exception> { failure };
                // 恢复的是原始备份字节而非重写序列化格式，GUID/localFileID均由原文件保留。
                foreach (var item in created)
                {
                    try
                    {
                        if (item == null) continue;
                        if (AssetDatabase.IsSubAsset(item)) { AssetDatabase.RemoveObjectFromAsset(item); Object.DestroyImmediate(item); }
                        else if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(item)) && !AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(item))) throw new IOException("Could not remove created asset.");
                        else if (item != null && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
                    }
                    catch (Exception e) { rollbackErrors.Add(e); }
                }
                foreach (var file in files)
                {
                    try
                    {
                        if (File.Exists(file.Key) && File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) continue;
                        ExcelFiles.WriteAtomic(file.Key, temporary => File.WriteAllBytes(temporary, file.Value));
                    }
                    catch (Exception e) { rollbackErrors.Add(e); }
                }
                foreach (var path in files.Keys.Where(p => !p.EndsWith(".meta", StringComparison.Ordinal)))
                {
                    try { AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                    catch (Exception e) { rollbackErrors.Add(e); }
                }
                foreach (var directory in createdFolders.AsEnumerable().Reverse())
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(directory).Length == 0 && !AssetDatabase.DeleteAsset(directory)) throw new IOException("Could not remove created folder: " + directory);
                    }
                    catch (Exception e) { rollbackErrors.Add(e); }
                }
                try
                {
                    var restored = ((IEnumerable)listField.GetValue(owner)).Cast<T>().ToArray();
                    values.Clear();
                    values.AddRange(restored);
                    listField.SetValue(owner, values);
                    AssertAssetFilesUnchanged(files);
                    foreach (var item in detached) if (item != null && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
                }
                catch (Exception e) { rollbackErrors.Add(e); }
                if (rollbackErrors.Count > 1)
                {
                    keepRecovery = true;
                    throw new AggregateException("Import failed; rollback incomplete. Original files and manifest are retained at: " + recovery, rollbackErrors);
                }
                throw;
            }
            finally
            {
                if (!keepRecovery)
                {
                    try { Directory.Delete(recovery, true); }
                    catch (IOException e) { Debug.LogWarning("Excel recovery cleanup failed: " + recovery + "\n" + e.Message); }
                    catch (UnauthorizedAccessException e) { Debug.LogWarning("Excel recovery cleanup denied: " + recovery + "\n" + e.Message); }
                }
            }
            foreach (var item in detached) if (item != null) Object.DestroyImmediate(item);
        }

        private static void EnsureFolder(string folder, List<string> created)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent, created);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder)))) throw new IOException("Could not create Excel target folder: " + folder);
            created.Add(folder);
        }

        private static string CreateRecovery(IDictionary<string, byte[]> files)
        {
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp/Extensions.Data.Excel/Recovery", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var manifest = new Dictionary<string, string>();
            int index = 0;
            foreach (var file in files)
            {
                string backup = (index++).ToString("D4", CultureInfo.InvariantCulture) + ".bin";
                File.WriteAllBytes(Path.Combine(directory, backup), file.Value);
                manifest.Add(file.Key, backup);
            }
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonConvert.SerializeObject(manifest, Formatting.Indented));
            return directory;
        }
    }
}
