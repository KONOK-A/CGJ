using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.Editor.Domain
{
    /// <summary>三个领域作者工具共用的资产身份边界，不把文件路径当作subasset身份。</summary>
    internal static class DomainAssetOwnership
    {
        internal static void DeleteEntry<T>(ScriptableObject owner, IList<T> values, T data, string folder) where T : ScriptableObject
        {
            var ownerPath = OwnerPath(owner);
            var index = values.IndexOf(data);
            if (index < 0) return;
            var path = AssetDatabase.GetAssetPath(data);
            Undo.RecordObject(owner, "Remove domain entry");
            values.RemoveAt(index);
            if (AssetDatabase.IsSubAsset(data) && path == ownerPath) Undo.DestroyObjectImmediate(data);
            else if (OwnedFile(data, path, folder) && !AssetDatabase.DeleteAsset(path))
            {
                values.Insert(index, data);
                throw new InvalidOperationException("不能删除领域条目资产: " + path);
            }
            // 外部借用对象只解引用，绝不删除它的文件或同文件中的其他对象。
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssetIfDirty(owner);
        }

        internal static void RenameEntry(ScriptableObject owner, ScriptableObject data, string name, string folder)
        {
            var ownerPath = OwnerPath(owner);
            var path = AssetDatabase.GetAssetPath(data);
            if (AssetDatabase.IsSubAsset(data) && path == ownerPath)
            {
                Undo.RecordObject(data, "Rename domain entry");
                data.name = name;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            else if (OwnedFile(data, path, folder))
            {
                var error = AssetDatabase.RenameAsset(path, AssetNameTools.GetName(name, folder));
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
        }

        private static bool OwnedFile(ScriptableObject data, string path, string folder) =>
            AssetDatabase.IsMainAsset(data) && path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal) &&
            AssetDatabase.LoadAllAssetsAtPath(path).Length == 1;

        private static string OwnerPath(ScriptableObject owner)
        {
            if (!owner || !AssetDatabase.IsMainAsset(owner)) throw new InvalidOperationException("领域配表必须是已保存的主资产。");
            var path = AssetDatabase.GetAssetPath(owner);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal)) throw new InvalidOperationException("只允许修改Assets内的领域配表。");
            return path;
        }
    }
}
