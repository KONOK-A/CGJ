using UnityEditor;
using UnityEngine;
using YLFramework.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>可随工程共享的非秘密执行配置；资源定义、压缩与加密只由引用的 Project 决定。</summary>
    [CreateAssetMenu(menuName = "YLFramework/Resource/Build Recipe", fileName = "ResourceBuildRecipe")]
    public sealed class ResourceBuildRecipe : ScriptableObject
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("Project")]
        public ResourceProject project;
        [global::UnityEngine.Serialization.FormerlySerializedAs("Target")]
        [Tooltip("明确目标，执行时必须已激活、安装且匹配主机；不隐式切平台。")]
        public BuildTarget target = BuildTarget.StandaloneWindows64;
        [global::UnityEngine.Serialization.FormerlySerializedAs("AppContentCompatibilityId")]
        public string appContentCompatibilityId = "app-v1";
        [global::UnityEngine.Serialization.FormerlySerializedAs("Action")]
        public ResourceBuildAction action;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ForceRebuild")]
        public bool forceRebuild;
        [global::UnityEngine.Serialization.FormerlySerializedAs("GenerateReferences")]
        [Tooltip("仅 BuildAndPublish 可请求常量生成；生成失败不撤销已成功发布。")]
        public bool generateReferences;
        [global::UnityEngine.Serialization.FormerlySerializedAs("ReferenceDirectory")]
        public string referenceDirectory = "Assets/YLFrameworkData";
    }

    public enum ResourceBuildAction
    {
        BuildOnly,
        BuildAndPublish
    }

    /// <summary>一次解析得到的只读执行输入，不持有密钥/provider；不替代 Builder 的完整输入冻结与后验。</summary>
    public sealed class ResourceBuildInvocation
    {
        public string RecipePath
        {
            get;
        }
        public string ProjectGuid
        {
            get;
        }
        public string ProjectId
        {
            get;
        }
        public BuildTarget Target
        {
            get;
        }
        public string AppContentCompatibilityId
        {
            get;
        }
        public ResourceBuildAction Action
        {
            get;
        }
        public bool ForceRebuild
        {
            get;
        }
        public bool GenerateReferences
        {
            get;
        }
        public string ReferenceDirectory
        {
            get;
        }
        public ResourceCompression Compression
        {
            get;
        }
        public ResourceContentEncryption ContentEncryption
        {
            get;
        }
        public string ContentKeyId
        {
            get;
        }

        internal ResourceBuildInvocation(ResourceBuildRecipe recipe)
        {
            RecipePath = AssetDatabase.GetAssetPath(recipe);
            ProjectGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(recipe.project));
            ProjectId = recipe.project.projectId;
            Target = recipe.target;
            AppContentCompatibilityId = recipe.appContentCompatibilityId;
            Action = recipe.action;
            ForceRebuild = recipe.forceRebuild;
            GenerateReferences = recipe.generateReferences;
            ReferenceDirectory = GenerateReferences ? recipe.referenceDirectory : null;
            Compression = recipe.project.compression;
            ContentEncryption = recipe.project.contentEncryption;
            ContentKeyId = ContentEncryption == ResourceContentEncryption.None ? null : recipe.project.contentKeyId;
        }

        public override string ToString() => "Recipe=" + RecipePath + "\nProject=" + ProjectId + " / GUID=" + ProjectGuid +
            "\nTarget=" + Target + " / App=" + AppContentCompatibilityId + "\nAction=" + Action + " / ForceRebuild=" + ForceRebuild +
            " / Compression=" + Compression + " / Delivery=" + ContentEncryption +
            (ContentKeyId == null ? "" : " / KeyId=" + ContentKeyId) + "\nGenerateReferences=" + GenerateReferences +
            (GenerateReferences ? " / Directory=" + ReferenceDirectory : "");
    }
}
