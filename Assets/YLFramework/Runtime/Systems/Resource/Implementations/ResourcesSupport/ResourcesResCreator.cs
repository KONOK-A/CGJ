namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Resources 目录资源的创建器：匹配以 <c>resources/</c> 或 <c>resources://</c> 开头的资源名。
    /// </summary>
    public class ResourcesResCreator : IResCreator
    {
        /// <summary>资源名以 <c>resources/</c> 或 <c>resources://</c> 开头时命中。</summary>
        public bool Match(ResKey resKey)
        {
            return resKey.AssetName.StartsWith("resources/") || 
                   resKey.AssetName.StartsWith("resources://");
        }
        
        /// <summary>创建 <see cref="ResourcesRes"/>：按前缀类型（URL / Folder）去掉前缀，资源类型取自 key。</summary>
        public IRes Create(ResKey resKey)
        {
            var resourcesRes = ResourcesRes.Allocate(resKey.AssetName,
                resKey.AssetName.StartsWith("resources://") 
                    ? ResNamePrefixType.Url 
                    : ResNamePrefixType.Folder);
            resourcesRes.AssetType = resKey.AssetType;
            return resourcesRes;
        }
    }
}