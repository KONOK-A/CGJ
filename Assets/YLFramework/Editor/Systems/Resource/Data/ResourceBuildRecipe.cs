using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>可随工程共享的非秘密执行配置；资源定义、压缩与加密只由引用的 Project 决定。</summary>
    [CreateAssetMenu(menuName = "YLFramework/Resource/Build Recipe", fileName = "ResourceBuildRecipe")]
    public sealed class ResourceBuildRecipe : ScriptableObject
    {
        public ResourceProject Project;
        [Tooltip("明确目标，执行时必须已激活、安装且匹配主机；不隐式切平台。")]
        public BuildTarget Target = BuildTarget.StandaloneWindows64;
        public string AppContentCompatibilityId = "app-v1";
        public ResourceBuildAction Action;
        public bool ForceRebuild;
        [Tooltip("仅 BuildAndPublish 可请求常量生成；生成失败不撤销已成功发布。")]
        public bool GenerateReferences;
        public string ReferenceDirectory = "Assets/YLFrameworkData";
    }

    public enum ResourceBuildAction { BuildOnly, BuildAndPublish }

    /// <summary>一次解析得到的只读执行输入，不持有密钥/provider；不替代 Builder 的完整输入冻结与后验。</summary>
    public sealed class ResourceBuildInvocation
    {
        public string RecipePath { get; }
        public string ProjectGuid { get; }
        public string ProjectId { get; }
        public BuildTarget Target { get; }
        public string AppContentCompatibilityId { get; }
        public ResourceBuildAction Action { get; }
        public bool ForceRebuild { get; }
        public bool GenerateReferences { get; }
        public string ReferenceDirectory { get; }
        public ResourceCompression Compression { get; }
        public ResourceContentEncryption ContentEncryption { get; }
        public string ContentKeyId { get; }

        internal ResourceBuildInvocation(ResourceBuildRecipe recipe)
        {
            RecipePath = AssetDatabase.GetAssetPath(recipe);
            ProjectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(recipe.Project));
            ProjectId = recipe.Project.ProjectId;
            Target = recipe.Target; AppContentCompatibilityId = recipe.AppContentCompatibilityId;
            Action = recipe.Action; ForceRebuild = recipe.ForceRebuild;
            GenerateReferences = recipe.GenerateReferences;
            ReferenceDirectory = GenerateReferences ? recipe.ReferenceDirectory : null;
            Compression = recipe.Project.Compression;
            ContentEncryption = recipe.Project.ContentEncryption;
            ContentKeyId = ContentEncryption == ResourceContentEncryption.None ? null : recipe.Project.ContentKeyId;
        }

        public override string ToString() => "Recipe=" + RecipePath + "\nProject=" + ProjectId + " / GUID=" + ProjectGuid +
            "\nTarget=" + Target + " / App=" + AppContentCompatibilityId + "\nAction=" + Action + " / ForceRebuild=" + ForceRebuild +
            " / Compression=" + Compression + " / Delivery=" + ContentEncryption +
            (ContentKeyId == null ? "" : " / KeyId=" + ContentKeyId) + "\nGenerateReferences=" + GenerateReferences +
            (GenerateReferences ? " / Directory=" + ReferenceDirectory : "");
    }
}
