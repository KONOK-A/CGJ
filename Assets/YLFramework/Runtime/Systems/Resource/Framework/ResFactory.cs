using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源创建工厂：按注册顺序匹配 <see cref="IResCreator"/> 并创建 <see cref="IRes"/>。
    /// <para>
    /// 选择规则：按注册顺序，第一个 <see cref="IResCreator.Match"/> 命中且 <see cref="IResCreator.Create"/>
    /// 返回非 null 的创建器胜出；匹配但返回 null 时继续尝试后续匹配者；
    /// 全部失败时记录错误日志并返回 null。Creator 内部抛出的异常原样向上传播，不做静默回退。
    /// </para>
    /// </summary>
    public static class ResFactory
    {
        /// <summary>内置场景资源创建器；场景加载扩展会直接用它做匹配 / 创建。</summary>
        public static readonly AssetBundleSceneResCreator AssetBundleSceneResCreator = new();

        private static readonly List<IResCreator> ResCreators = new()
        {
            new ResourcesResCreator(),
            new AssetResCreator(),
            new AssetBundleResCreator(),
            AssetBundleSceneResCreator,
            new LocalImageResCreator(),
            new NetImageResCreator()
        };

        /// <summary>
        /// 按注册顺序选择创建器并创建资源。
        /// </summary>
        /// <param name="resKey">资源查询键，不能为 null。</param>
        /// <returns>创建出的资源；没有可用创建器时返回 null 并输出错误日志。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="resKey"/> 为 null。</exception>
        public static IRes Create(ResKey resKey)
        {
            if (resKey == null)
            {
                throw new ArgumentNullException(nameof(resKey), "ResFactory.Create 的 resKey 不能为 null。");
            }

            //按注册顺序匹配：先注册者优先；匹配后创建返回 null 则继续尝试后续匹配者。
            for (var i = 0; i < ResCreators.Count; i++)
            {
                var creator = ResCreators[i];
                if (!creator.Match(resKey))
                {
                    continue;
                }

                var retRes = creator.Create(resKey);
                if (retRes != null)
                {
                    return retRes;
                }
            }

            Debug.LogError("#YLFramework: Failed to Create Res. Not Find By ResKey:" + resKey);
            return null;
        }

        /// <summary>注册创建器（追加到末尾，优先级低于先注册者）。</summary>
        public static void AddResCreator<T>() where T : IResCreator, new()
        {
            ResCreators.Add(new T());
        }

        /// <summary>移除指定类型的所有创建器实例。</summary>
        public static void RemoveResCreator<T>() where T : IResCreator, new()
        {
            ResCreators.RemoveAll(c => c.GetType() == typeof(T));
        }

        /// <summary>注册创建器实例（追加到末尾，优先级低于先注册者）。</summary>
        public static void AddResCreator(IResCreator resCreator)
        {
            ResCreators.Add(resCreator);
        }
    }
}
