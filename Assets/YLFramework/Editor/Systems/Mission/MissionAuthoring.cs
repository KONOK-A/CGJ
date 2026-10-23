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
            if (string.IsNullOrEmpty(config.typeScriptGuid))
            {
                return typeof(MissionConfigData);
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.typeScriptGuid));
            var type = script ? script.GetClass() : null;
            if (type == null)
            {
                throw new InvalidOperationException($"任务数据类型绑定失效: {config.name}, GUID={config.typeScriptGuid}");
            }

            if (!typeof(MissionConfigData).IsAssignableFrom(type) || type.IsAbstract)
            {
                throw new InvalidOperationException($"任务数据类型无效: {config.name}, {type.FullName}");
            }

            return type;
        }
        internal static void Rename(this MissionConfigData data)
        {
            YLFramework.Editor.Domain.DomainAssetOwnership.RenameEntry(data.parent, data, data.id.ToString(), GeneratedDirectory(data.parent));
            data.parent.ClearCache();
        }
    }
}
