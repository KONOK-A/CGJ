using System;
using UnityEditor;
using YLFramework.Editor.Utilities;
using YLFramework.Skill;

namespace YLFramework.Editor.Skill
{
    internal static class SkillAuthoring
    {
        internal static Type GetDataType(this SkillsConfig config)
        {
            if (string.IsNullOrEmpty(config.typeScriptGuid))
            {
                return typeof(SkillConfigData);
            }

            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(config.typeScriptGuid));
            var type = script ? script.GetClass() : null;
            if (type == null)
            {
                throw new InvalidOperationException($"技能数据类型绑定失效: {config.name}, GUID={config.typeScriptGuid}");
            }

            if (!typeof(SkillConfigData).IsAssignableFrom(type) || type.IsAbstract)
            {
                throw new InvalidOperationException($"技能配表数据类型无效: {config.name}, {type.FullName}");
            }

            return type;
        }

        internal static void Rename(this SkillConfigData data)
        {
            DomainAssetOwnership.RenameEntry(data.parent, data, data.id.ToString(), SkillsConfigController.GetSkillConfigDataPath(data.parent));
            data.parent.ClearCache();
        }
    }

}
