using System;
using UnityEditor;
using YLFramework.Buff;

namespace YLFramework.Editor.Buff
{
    internal static class BuffAuthoring
    {
        internal static Type GetDataType(this BuffsConfig config)
        {
            if (string.IsNullOrEmpty(config.typeScriptGuid))
            {
                return typeof(BuffConfigData);
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.typeScriptGuid));
            var type = script ? script.GetClass() : null;
            if (type == null)
            {
                throw new InvalidOperationException($"Buff数据类型绑定失效: {config.name}, GUID={config.typeScriptGuid}");
            }

            if (!typeof(BuffConfigData).IsAssignableFrom(type) || type.IsAbstract)
            {
                throw new InvalidOperationException($"Buff数据类型无效: {config.name}, {type.FullName}");
            }

            return type;
        }
        internal static void Rename(this BuffConfigData data)
        {
            YLFramework.Editor.Utilities.DomainAssetOwnership.RenameEntry(data.parent, data, data.id.ToString(), BuffsConfigController.GetBuffConfigDataPath(data.parent));
            data.parent.ClearCache();
        }
    }
}
