using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{

    /// <summary>
    /// 引用计数契约。
    /// <para>
    /// 语义：引用计数初始为 0，Retain / Release 必须成对；Release 到 0 只表示引用归零，
    /// 是否卸载资源或回收对象由实现方的零引用回调决定；计数为 0 时再次 Release 属于非法状态。
    /// </para>
    /// </summary>
    public interface IRefCounter
    {
        /// <summary>当前引用计数。</summary>
        int RefCount { get; }

        /// <summary>增加一次引用。</summary>
        void Retain(Object refOwner = null);

        /// <summary>
        /// 减少一次引用；计数从 1 减到 0 时触发实现方的零引用回调。
        /// </summary>
        /// <exception cref="InvalidOperationException">当前引用计数已为 0（非法的重复 Release）。</exception>
        void Release(Object refOwner = null);
    }

    /// <summary>
    /// 简单引用计数器。
    /// <para>
    /// 语义：初始值 0；Release 到 0 只触发 <see cref="OnZeroRef"/>（是否真正卸载由子类决定，
    /// 例如 <c>Res.OnZeroRef</c> 会调用 ReleaseRes 卸载缓存资产并回到 Waiting）；
    /// 计数为 0 时再次 Release 直接 fail-fast 抛异常，不会静默保持负数。
    /// </para>
    /// </summary>
    public abstract class SimpleRc : IRefCounter
    {
        /// <inheritdoc />
        public int RefCount { get; private set; }

        /// <inheritdoc />
        public void Retain(Object refOwner = null)
        {
            RefCount += 1;
        }

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">当前引用计数已为 0（Retain / Release 不成对）。</exception>
        public void Release(Object refOwner = null)
        {
            if (RefCount <= 0)
            {
                throw new InvalidOperationException(
                    $"[SimpleRc] 非法 Release：引用计数已为 {RefCount}，Retain/Release 不成对。Target:{this}");
            }

            RefCount -= 1;
            if (RefCount == 0)
            {
                OnZeroRef(refOwner);
            }
        }

        /// <summary>
        /// 引用计数归零时调用，仅在计数从 1 减到 0 时触发一次；
        /// 具体行为（卸载资产 / 回收对象 / 仅通知）由子类决定。
        /// </summary>
        protected abstract void OnZeroRef(Object refOwner = null);
    }
}
