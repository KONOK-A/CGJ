using System;
using System.Collections.Generic;
using YLFramework.Core.Singleton;
using YLFramework.Events.Internal;

namespace YLFramework.Events
{
    /// <summary>
    /// 以枚举为 key 的全局事件系统。
    /// 同一枚举值共享一个事件实例，<see cref="Send{T}"/> 时把参数数组传给所有监听回调；无监听回调时发送为空操作。
    /// 实例由 SingletonProperty 管理：<see cref="Dispose"/> 后旧实例的监听全部失效，下次访问 <see cref="Instance"/> 会创建新实例。
    /// </summary>
    public class EnumEventSystem : ISingleton
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static EnumEventSystem Instance => SingletonProperty<EnumEventSystem>.Instance;

        private readonly Dictionary<Enum, EasyEvent<object[]>> _events = new();

        /// <summary>
        /// 创建事件系统实例；外部请通过 <see cref="Instance"/> 访问单例。
        /// </summary>
        protected EnumEventSystem() { }

        /// <summary>
        /// 注册指定枚举 key 的监听，返回注销句柄；key 不存在时自动创建对应事件。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register<T>(T key, Action<object[]> eventAction) where T : Enum
        {
            if (_events.TryGetValue(key, out var e))
            {
                return e.Register(eventAction);
            }

            var easyEvent = EasyEvent<object[]>.Allocate();
            _events.Add(key, easyEvent);
            return easyEvent.Register(eventAction);
        }

        /// <summary>
        /// 注销指定 key 上的一个监听。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister<T>(T key, Action<object[]> eventAction) where T : Enum
        {
            if (_events.TryGetValue(key, out var easyEvent))
            {
                easyEvent?.UnRegister(eventAction);
            }
        }

        /// <summary>
        /// 移除并回收指定 key 的整个事件（该 key 上所有监听失效）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        public void UnRegister<T>(T key) where T : Enum
        {
            if (_events.TryGetValue(key, out var easyEvent))
            {
                easyEvent?.RecycleToCache();
                _events.Remove(key);
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
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="args">事件参数。</param>
        public void Send<T>(T key, params object[] args) where T : Enum
        {
            if (_events.TryGetValue(key, out var e))
            {
                e?.Trigger(args);
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
            SingletonProperty<EnumEventSystem>.Dispose();
        }
    }
}
