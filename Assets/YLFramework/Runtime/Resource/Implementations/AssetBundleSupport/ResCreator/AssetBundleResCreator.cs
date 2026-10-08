using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// AssetBundle 文件本身的创建器：仅匹配 <c>AssetType == typeof(AssetBundle)</c> 的 key。
    /// 依赖信息由 <see cref="AssetBundleRes.Allocate"/> 从资源数据表读取；
    /// 依赖的实际加载与持有由 ResLoader 统一完成。
    /// </summary>
    public class AssetBundleResCreator : IResCreator
    {

        /// <summary>仅当 key 的 AssetType 为 <see cref="AssetBundle"/>（Bundle 文件本身）时命中。</summary>
        public bool Match(ResKey resKey)
        {
            return resKey.AssetType == typeof(AssetBundle);
        }

        /// <inheritdoc />
        public IRes Create(ResKey resKey)
        {
            return AssetBundleRes.Allocate(resKey.AssetName);
        }
    }
}
