using System;
using UnityEditor;
using YLFramework.Core.Editor;

namespace YLFramework.BuffSystem
{
    internal static class BuffAuthoring
    {
        internal static Type GetDataType(this BuffsConfig config)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.type_script_guid));
            var type = script ? script.GetClass() : null;
            if (type == null) return typeof(BuffConfigData);
            if (!typeof(BuffConfigData).IsAssignableFrom(type) || type.IsAbstract) throw new InvalidOperationException($"Buff数据类型无效: {config.name}, {type.FullName}");
            return type;
        }
        internal static void Rename(this BuffConfigData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            AssetDatabase.RenameAsset(path, AssetNameTools.GetName(data.buff_id.ToString(), System.IO.Path.GetDirectoryName(path)));
        }
    }
}
