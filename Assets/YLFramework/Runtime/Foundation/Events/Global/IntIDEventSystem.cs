using System;
using System.Collections.Generic;
using YLFramework.Core.Singleton;
using YLFramework.Events.Internal;

namespace YLFramework.Events
{
    /// <summary>
    /// 以整数 ID 为 key 的全局事件系统。
    /// key 通过 <see cref="IConvertible.ToInt32"/> 归一化，因此不同数值类型但数值相同的 key 会共享同一事件。
    /// 同一 key 共享一个事件实例；无监听回调时发送为空操作。
    /// 实例由 SingletonProperty 管理：<see cref="Dispose"/> 后旧实例的监听全部失效。
    /// </summary>
    public class IntIDEventSystem : ISingleton
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static IntIDEventSystem Instance => SingletonProperty<IntIDEventSystem>.Instance;

        private readonly Dictionary<int, EasyEvent<int, object[]>> _events = new();

        /// <summary>
        /// 创建事件系统实例；外部请通过 <see cref="Instance"/> 访问单例。
        /// </summary>
        protected IntIDEventSystem() { }

        /// <summary>
        /// 注册指定整数 key 的监听，返回注销句柄；key 不存在时自动创建对应事件。
        /// 回调参数依次为归一化后的整数 key 与发送时的参数数组。
        /// </summary>
        /// <typeparam name="T">可转换为 int 的 key 类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register<T>(T key, Action<int, object[]> eventAction) where T : IConvertible
        {
            var intKey = key.ToInt32(null);
            if (_events.TryGetValue(intKey, out var e))
            {
                return e.Register(eventAction);
            }
            else
            {
                var easyEvent = EasyEvent<int, object[]>.Allocate();
                _events.Add(intKey, easyEvent);
                return easyEvent.Register(eventAction);
            }
        }

        /// <summary>
        /// 注销指定 key 上的一个监听。
        /// </summary>
        /// <typeparam name="T">可转换为 int 的 key 类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister<T>(T key, Action<int, object[]> eventAction) where T : IConvertible
        {
            var intKey = key.ToInt32(null);
            if (_events.TryGetValue(intKey, out var easyEvent))
            {
                easyEvent?.UnRegister(eventAction);
            }
        }

        /// <summary>
        /// 移除并回收指定 key 的整个事件（该 key 上所有监听失效）。
        /// </summary>
        /// <typeparam name="T">可转换为 int 的 key 类型。</typeparam>
        /// <param name="key">事件 key。</param>
        public void UnRegister<T>(T key) where T : IConvertible
        {
            var intKey = key.ToInt32(null);

            if (_events.TryGetValue(intKey, out var easyEvent))
            {
                easyEvent?.RecycleToCache();
                _events.Remove(intKey);
            }
        }

        /// <summary>
        /// 移除并回收所有 key 的事件。
        /// </summary>
        public void UnRegisterAll()
        {
            EventSystemInternal.RecycleAllAndClear(_events);
        }

        /// <summary>
        /// 向指定 key 发送事件，参数作为 object[] 传递；无监听时为空操作。
        /// 发送时回调收到的第一个参数是归一化后的整数 key。
        /// </summary>
        /// <typeparam name="T">可转换为 int 的 key 类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="args">事件参数。</param>
        public void Send<T>(T key, params object[] args) where T : IConvertible
        {
            var intKey = key.ToInt32(null);

            if (_events.TryGetValue(intKey, out var e))
            {
                e?.Trigger(intKey, args);
            }
        }

        /// <inheritdoc />
        public void OnSingletonInit()
        {
            _events.Clear();
        }

        /// <summary>
        /// 注销所有监听并释放单例实例；下次访问 <see cref="Instance"/> 会创建新实例。
        /// </summary>
        public void Dispose()
        {
            UnRegisterAll();
            SingletonProperty<IntIDEventSystem>.Dispose();
        }
    }
}
