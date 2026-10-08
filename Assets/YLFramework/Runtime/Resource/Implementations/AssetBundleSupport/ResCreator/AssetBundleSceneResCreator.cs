namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Bundle 内场景（<see cref="ResLoadType.ABScene"/>）创建器。
    /// <para>
    /// 场景加载由 <see cref="ResLoaderExtensions"/> 直接使用本创建器，不走 <see cref="ResFactory"/> 的默认顺序；
    /// 创建的 <see cref="AssetBundleSceneRes"/> 只作为场景所属 Bundle 的就绪门控，
    /// 场景本体的加载与卸载由 SceneManager 负责。
    /// </para>
    /// </summary>
    public class AssetBundleSceneResCreator : IResCreator
    {
        /// <inheritdoc />
        public bool Match(ResKey resKey)
        {
            var assetData = AssetBundleSetting.AssetBundleDataFile.GetAssetData(resKey);
            if (assetData != null)
            {
                return assetData.AssetResType == ResLoadType.ABScene;
            }
            return false;
        }

        /// <inheritdoc />
        public IRes Create(ResKey resKey)
        {
            return AssetBundleSceneRes.Allocate(resKey.AssetName);
        }
    }
}
