using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal static class ResourceBuildSourceLocator
    {
        internal static (int Group, int Selection, int Rule) Resolve(ResourceProject project, ResourceBuildSource source)
        {
            if (!project || source == null || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) != source.ProjectGuid ||
                project.ProjectId != source.ProjectId) throw new InvalidOperationException("来源资源项目已变化，不能用旧报告定位当前配置。");
            if (source.DirectoryIdentity != ResourceProjectPaths.SemanticIdentity(project))
                throw new InvalidOperationException("来源目录、地址前缀或图集生成目录已变化，请重新计算后再定位；原报告仍保留。");
            var groups = project.Groups.Select((value, index) => (value, index)).Where(x => x.value.Id == source.GroupId).ToArray();
            if (groups.Length != 1 || groups[0].value.Packing != source.Packing)
                throw new InvalidOperationException("来源分组已删除或打包方式已变化。请重新计算，不会按旧列表位置选择其他分组。");
            if (source.AtlasRuleId != null)
            {
                if (!ResourceAtlasGenerator.IsConfigured(project) || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project.AutomaticAtlases)) != source.AtlasConfigurationGuid)
                    throw new InvalidOperationException("来源图集配置已变化，请重新计算。");
                var rules = project.AutomaticAtlases.Rules.Select((value, index) => (value, index))
                    .Where(x => x.value != null && x.value.Id == source.AtlasRuleId).ToArray();
                if (rules.Length != 1 || JsonUtility.ToJson(rules[0].value) != source.Definition)
                    throw new InvalidOperationException("来源图集规则已删除或变化，请重新计算；原报告仍保留。");
                return (groups[0].index, -1, rules[0].index);
            }
            var selections = groups[0].value.Selections.Select((value, index) => (value, index))
                .Where(x => x.value != null && x.value.Guid == source.SelectionGuid).ToArray();
            if (selections.Length != 1 || JsonUtility.ToJson(selections[0].value) != source.Definition ||
                AssetDatabase.GUIDToAssetPath(source.SelectionGuid) != source.SelectionPath)
                throw new InvalidOperationException("来源资源选择项已删除、移动或修改。请重新计算；原报告不代表当前配置。");
            return (groups[0].index, selections[0].index, -1);
        }
    }
}
