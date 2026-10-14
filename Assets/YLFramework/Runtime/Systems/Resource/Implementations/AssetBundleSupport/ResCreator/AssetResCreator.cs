using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Bundle 内资产（<see cref="ResLoadType.ABAsset"/>）创建器。
    /// <para>
    /// 匹配规则：通过资源数据表查询 <see cref="ResKey"/>，查得 AssetData 且
    /// <c>AssetResType == ABAsset</c> 才算命中；AssetBundle 类型的 key（Bundle 文件本身）
    /// 永远不归本创建器，避免资源数据表在类型码不匹配时回退保留同名条目、
    /// 把 Bundle key 抢成 AssetRes（Bundle 由 <see cref="AssetBundleResCreator"/> 负责）。
    /// </para>
    /// </summary>
    public class AssetResCreator : IResCreator
    {
        /// <inheritdoc />
        public bool Match(ResKey resKey)
        {
            //Bundle 文件本身由 AssetBundleResCreator 负责，同名资产的类型回退不得抢占。
            if (resKey.AssetType == typeof(AssetBundle))
            {
                return false;
            }

            var assetData = AssetBundleSetting.AssetBundleDataFile.GetAssetData(resKey);
            if (assetData != null)
            {
                return assetData.AssetResType == ResLoadType.ABAsset;
            }

            foreach (var subProjectAssetBundleDataFile in AssetBundleSetting.SubProjectAssetBundleDataFiles)
            {
                assetData = subProjectAssetBundleDataFile.GetAssetData(resKey);
                if (assetData != null)
                {
                    return assetData.AssetResType == ResLoadType.ABAsset;
                }
            }
            return false;
        }

        /// <inheritdoc />
        public IRes Create(ResKey resKey)
        {
            return AssetRes.Allocate(resKey.AssetName,resKey.OwnerBundleName,resKey.AssetType);
        }
    }
}
