using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    // Unity 的对象 Undo 不包含独立文件删除；用同组内存记录保留文件及 meta 原始身份。
    [InitializeOnLoad]
    internal static class EquipmentAssetDeletion
    {
        static EquipmentAssetDeletion()
        {
            foreach (var state in Resources.FindObjectsOfTypeAll<EquipmentDeletionRecord>()) state.applied = state.deleted;
            Undo.undoRedoPerformed += Synchronize;
            EditorApplication.quitting += () =>
            {
                Undo.undoRedoPerformed -= Synchronize;
                foreach (var state in Resources.FindObjectsOfTypeAll<EquipmentDeletionRecord>()) UnityEngine.Object.DestroyImmediate(state);
            };
        }

        internal static void Delete(EquipmentConfigData data, EquipmentsConfig root, string path)
        {
            var state = ScriptableObject.CreateInstance<EquipmentDeletionRecord>();
            state.hideFlags = HideFlags.HideAndDontSave; state.name = "Equipment file deletion";
            state.root = root; state.path = path; state.guid = AssetDatabase.AssetPathToGUID(path);
            state.assetBytes = File.ReadAllBytes(path); state.metaBytes = File.ReadAllBytes(path + ".meta");
            state.memory = EditorJsonUtility.ToJson(data); state.wasDirty = EditorUtility.IsDirty(data);
            state.indices = Enumerable.Range(0, root.Equipments.Count).Where(i => ReferenceEquals(root.Equipments[i], data)).ToArray();
            Undo.IncrementCurrentGroup(); var group = Undo.GetCurrentGroup();
            Undo.RegisterCompleteObjectUndo(state, "Delete equipment");
            Undo.RecordObject(root, "Delete equipment");
            state.deleted = true; root.Equipments.RemoveAll(item => ReferenceEquals(item, data));
            Undo.FlushUndoRecordObjects();
            if (!AssetDatabase.DeleteAsset(path))
            {
                state.applied = !File.Exists(path);
                Undo.RevertAllDownToGroup(group); root.ClearCache();
                throw new IOException("Could not delete equipment asset: " + path);
            }
            state.applied = true;
            Undo.CollapseUndoOperations(group);
            root.ClearCache(); EditorUtility.SetDirty(root); AssetDatabase.SaveAssetIfDirty(root);
        }

        private static void Synchronize()
        {
            foreach (var state in Resources.FindObjectsOfTypeAll<EquipmentDeletionRecord>())
            {
                if (!state.root || state.applied == state.deleted) continue;
                try
                {
                    if (state.deleted)
                    {
                        if (!File.Exists(state.path)) continue;
                        RequireOriginal(state);
                        if (!AssetDatabase.DeleteAsset(state.path)) throw new IOException("Could not redo equipment deletion: " + state.path);
                    }
                    else
                    {
                        if (File.Exists(state.path)) continue;
                        if (!Directory.Exists(Path.GetDirectoryName(state.path))) throw new IOException("Equipment owner directory was removed outside Undo: " + state.path);
                        if (File.Exists(state.path + ".meta") && !File.ReadAllBytes(state.path + ".meta").SequenceEqual(state.metaBytes)) throw new IOException("Foreign equipment meta occupies the deleted path: " + state.path);
                        var current = AssetDatabase.GUIDToAssetPath(state.guid);
                        if (!string.IsNullOrEmpty(current) && current != state.path) throw new IOException("Equipment GUID moved outside Undo: " + current);
                        File.WriteAllBytes(state.path, state.assetBytes); File.WriteAllBytes(state.path + ".meta", state.metaBytes);
                        AssetDatabase.ImportAsset(state.path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        var restored = AssetDatabase.LoadAssetAtPath<EquipmentConfigData>(state.path);
                        if (!restored || AssetDatabase.AssetPathToGUID(state.path) != state.guid) throw new IOException("Could not restore equipment identity: " + state.path);
                        EditorJsonUtility.FromJsonOverwrite(state.memory, restored);
                        if (state.wasDirty) EditorUtility.SetDirty(restored); else EditorUtility.ClearDirty(restored);
                        foreach (var index in state.indices)
                            if (index < state.root.Equipments.Count && !state.root.Equipments[index]) state.root.Equipments[index] = restored;
                    }
                    state.root.ClearCache(); EditorUtility.SetDirty(state.root); AssetDatabase.SaveAssetIfDirty(state.root);
                }
                catch (Exception error) { Debug.LogException(error); }
                finally { state.applied = state.deleted; }
            }
        }
        private static void RequireOriginal(EquipmentDeletionRecord state)
        {
            if (AssetDatabase.AssetPathToGUID(state.path) != state.guid || !File.ReadAllBytes(state.path).SequenceEqual(state.assetBytes) ||
                !File.Exists(state.path + ".meta") || !File.ReadAllBytes(state.path + ".meta").SequenceEqual(state.metaBytes))
                throw new IOException("Equipment file changed outside Undo; preserving it: " + state.path);
        }
    }
}
