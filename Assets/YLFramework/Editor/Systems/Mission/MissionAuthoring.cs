using System;
using UnityEditor;
using YLFramework.Core.Editor;

namespace YLFramework.MissionSystem
{
    internal static class MissionAuthoring
    {
        internal const string GeneratedRoot = "Assets/YLFrameworkData/Mission";
        internal static string GeneratedDirectory(MissionsConfig config) => GeneratedRoot + "/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config));
        internal static Type GetDataType(this MissionsConfig config)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.type_script_guid));
            var type = script ? script.GetClass() : null;
            if (type == null) return typeof(MissionConfigData);
            if (!typeof(MissionConfigData).IsAssignableFrom(type) || type.IsAbstract) throw new InvalidOperationException($"任务数据类型无效: {config.name}, {type.FullName}");
            return type;
        }
        internal static void Rename(this MissionConfigData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            AssetDatabase.RenameAsset(path, AssetNameTools.GetName(data.id.ToString(), System.IO.Path.GetDirectoryName(path)));
        }
    }
}
