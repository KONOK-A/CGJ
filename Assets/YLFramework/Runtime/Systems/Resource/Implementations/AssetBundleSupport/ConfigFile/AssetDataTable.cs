using System.Collections.Generic;
using System.Linq;
using YLFramework.Utilities.Table;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Bundle 资源配置的运行时索引表：按小写资源名索引，支持按资源类型与所属 Bundle 过滤。
    /// <para>
    /// 类型过滤规则：当 key 指定了已知类型码且存在匹配条目时取匹配集；
    /// 没有匹配条目时保留同名全部条目（兼容类型码为 0 的旧数据）。
    /// 因此同名不同 Bundle 的资产通过 OwnerBundleName 过滤区分，同名不同默认类型的资产建议显式传 Type。
    /// </para>
    /// </summary>
    public class AssetDataTable : Table<AssetData>
    {
        /// <summary>按小写资源名建立的索引（键为 AssetName.ToLowerInvariant）。</summary>
        public TableIndex<string, AssetData> NameIndex { get; } = new(data => data.AssetName.ToLowerInvariant());

        /// <summary>
        /// 按查询键查找资产记录：同名多条时返回索引顺序的第一个，无命中返回 null；
        /// 类型与 Bundle 的过滤规则见类型注释。
        /// </summary>
        /// <exception cref="System.ObjectDisposedException">表已释放。</exception>
        public AssetData GetAssetDataByResKey(ResKey resKey)
        {
            var assetName = resKey.AssetName.ToLowerInvariant();
            var assetDatas = NameIndex.Get(assetName).ToArray();
            if (resKey.OwnerBundleName != null)
            {
                //找到资产名字相同的数据里所属AB包也相同的
                assetDatas = 
                    assetDatas.Where(data => data.OwnerBundleName == resKey.OwnerBundleName).ToArray();
            }

            if (resKey.AssetType != null)
            {
                var assetTypeCode = resKey.AssetType.ToCode();
                if (assetTypeCode == 0)
                {
                }
                else
                {
                    var newAssetDatas = 
                        assetDatas.Where(data => data.AssetObjectTypeCode == assetTypeCode).ToArray();
                    // 有可能是从旧的 AssetBundle 中加载出来的资源
                    if (newAssetDatas.Any())
                    {
                        assetDatas = newAssetDatas;
                    }
                }
            }
            return assetDatas.FirstOrDefault();
        }
        
        protected override void OnAdd(AssetData item)
        {
            NameIndex.Add(item);
        }

        protected override void OnRemove(AssetData item)
        {
            NameIndex.Remove(item);
        }

        protected override void OnClear()
        {
            NameIndex.Clear();
        }

        /// <summary>枚举全部资产记录；不保证全局顺序。</summary>
        public override IEnumerator<AssetData> GetEnumerator()
        {
            return NameIndex.Dictionary.SelectMany(kv => kv.Value).GetEnumerator();
        }

        protected override void OnDispose()
        {
            NameIndex.Dispose();
        }
    }
}