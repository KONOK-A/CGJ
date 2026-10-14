using System.Collections.Generic;
using System.Linq;
using YLFramework.Utilities.Table;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Resource 的资源索引表：按小写资源名建索引，查询时再按类型与所属 Bundle 过滤。
    /// <para>
    /// 多值语义：同一名字下允许存在多个 IRes（不同类型 / 不同 Bundle）；
    /// <see cref="GetResByResKey"/> 返回命中集中索引顺序的第一个。
    /// 本类不做重复 Add 去重，ServiceManager 通过"先查后建"保证不会登记重复资源。
    /// 表不拥有 IRes 生命周期，不会调用 IRes 的 Release / Recycle。
    /// </para>
    /// <para>
    /// 生命周期：Add / Remove / Clear 由 Table 基类转发；Dispose 释放内部索引（幂等），
    /// 之后 Add / Get / 枚举都会抛 <see cref="System.ObjectDisposedException"/>。
    /// </para>
    /// </summary>
    public class ResTable : Table<IRes>
    {
        private readonly TableIndex<string, IRes> _nameIndex = new (res => res.AssetName.ToLowerInvariant());

        /// <summary>
        /// 按资源键查询：资源名必须相等（均为小写）；key 的 AssetType / OwnerBundleName 为 null 时不过滤。
        /// 同名多资源时返回第一个命中项；无命中返回 null。
        /// </summary>
        /// <exception cref="System.ArgumentNullException">key.AssetName 为 null（索引不支持 null 键）。</exception>
        /// <exception cref="System.ObjectDisposedException">表已释放。</exception>
        public IRes GetResByResKey(ResKey key)
        {
            var assetName = key.AssetName;
            var resArray = _nameIndex.Get(assetName);
            //进行类型和所属AssetBundle的双重查询
            if (key.AssetType != null)
            {
                resArray = resArray.Where(res => res.AssetType == key.AssetType);
            }
            if (key.OwnerBundleName != null)
            {
                resArray = resArray.Where(res => res.OwnerBundleName == key.OwnerBundleName);
            }
            return resArray.FirstOrDefault();
        }
        
        protected override void OnAdd(IRes item)
        {
            _nameIndex.Add(item);
        }

        protected override void OnRemove(IRes item)
        {
            _nameIndex.Remove(item);
        }

        protected override void OnClear()
        {
            _nameIndex.Clear();
        }

        /// <summary>
        /// 枚举全部资源；不保证全局顺序，同一资源名组内保持添加顺序。
        /// </summary>
        public override IEnumerator<IRes> GetEnumerator()
        {
            //返回存储的所有IRes的集合
            return _nameIndex.Dictionary.SelectMany(item => item.Value).GetEnumerator();
        }

        /// <summary>
        /// 释放内部索引（幂等）；之后访问会抛 <see cref="System.ObjectDisposedException"/>。
        /// </summary>
        protected override void OnDispose()
        {
            _nameIndex.Dispose();
        }
    }
}
