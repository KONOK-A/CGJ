using System;
using YLFramework.Core.Pool;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源查询键（查询规则）。
    /// <para>
    /// 身份规则：<see cref="AssetName"/> 与 <see cref="OwnerBundleName"/> 在创建时统一转为小写，
    /// 原始大小写保留在 <see cref="OriginalAssetName"/>；真正的值匹配由 <see cref="Match"/> 完成。
    /// 本类型不重写 <see cref="Equals"/> / <see cref="GetHashCode"/>，即保持引用语义，
    /// 它不是值对象，不要作为字典 / 哈希容器的 key 使用。
    /// </para>
    /// <para>
    /// Ownership：通过 <see cref="Allocate"/> 从 <see cref="SafeObjectPool{T}"/> 取出，
    /// 调用方使用完毕后必须 <see cref="RecycleToCache"/> 归还；归还后字段会被清空，重复回收由对象池拒绝。
    /// </para>
    /// </summary>
    public class ResKey : IOfPool,IPoolType
    {
        /// <summary>小写化后的资源名，参与匹配。</summary>
        public string AssetName { get; private set; }

        /// <summary>小写化后的所属 AssetBundle 名；为 null 表示不限 Bundle（空字符串保持原样）。</summary>
        public string OwnerBundleName { get; private set; }

        /// <summary>资源类型；为 null 表示不限类型。</summary>
        public Type AssetType { get; set; }

        /// <summary>
        /// 资产没有统一小写前的名字（保留原始大小写，不参与匹配）。
        /// </summary>
        public string OriginalAssetName { get; private set; }

        /// <summary>
        /// 创建资源查询键：资源名 / Bundle 名统一转小写（保留原始名到 <see cref="OriginalAssetName"/>）。
        /// </summary>
        /// <param name="assetName">资源名，不能为 null 或空白。</param>
        /// <param name="ownerBundle">所属 AssetBundle 名，可为 null；非 null 时转小写。</param>
        /// <param name="assetType">资源类型，可为 null 表示不限类型。</param>
        /// <exception cref="ArgumentNullException"><paramref name="assetName"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="assetName"/> 为空白。</exception>
        public static ResKey Allocate(string assetName, string ownerBundle = null, Type assetType = null)
        {
            if (assetName == null)
            {
                throw new ArgumentNullException(nameof(assetName), "ResKey 的 assetName 不能为 null。");
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new ArgumentException("ResKey 的 assetName 不能为空白。", nameof(assetName));
            }

            var resKey = SafeObjectPool<ResKey>.Instance.Allocate();
            resKey.AssetName = assetName.ToLowerInvariant();
            resKey.OwnerBundleName = ownerBundle?.ToLowerInvariant();
            resKey.AssetType = assetType;
            resKey.OriginalAssetName = assetName;
            return resKey;
        }

        /// <summary>
        /// 判断资源是否命中本查询键：资源名必须完全相等（双方均为小写）；
        /// <see cref="AssetType"/> 为 null 时不过滤类型，否则必须相等；
        /// <see cref="OwnerBundleName"/> 为 null 时不过滤 Bundle，否则必须相等；
        /// <see cref="OriginalAssetName"/> 不参与匹配。
        /// </summary>
        /// <param name="res">待匹配资源；为 null 返回 false。</param>
        public bool Match(IRes res)
        {
            if (res == null)
            {
                return false;
            }

            if (res.AssetName == AssetName)
            {
                var isMatch = true;
                if (AssetType != null)
                {
                    isMatch = res.AssetType == AssetType;
                }

                if (OwnerBundleName != null)
                {
                    isMatch = isMatch && res.OwnerBundleName == OwnerBundleName;
                }
                return isMatch;
            }
            return false;
        }

        public override string ToString()
        {
            return $"AssetName:{AssetName} OwnerBundle:{OwnerBundleName} AssetType:{AssetType}";
        }

        /// <summary>
        /// 归还对象池时清空全部字段；对象池会保证重复回收被拒绝。
        /// </summary>
        void IOfPool.OnRecycle()
        {
            AssetName = null;
            OwnerBundleName = null;
            AssetType = null;
            OriginalAssetName = null;
        }

        /// <summary>是否已被对象池回收。</summary>
        public bool IsRecycled { get; set; }

        /// <summary>
        /// 归还到对象池；重复调用由对象池拒绝，不会重复入池。
        /// </summary>
        public void RecycleToCache()
        {
            SafeObjectPool<ResKey>.Instance.Recycle(this);
        }
    }
}
