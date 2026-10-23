using System;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// 以枚举为 key 的全局事件系统。
    /// 同一枚举值共享一个事件实例，<see cref="Send{T}"/> 时把参数数组传给所有监听回调；无监听回调时发送为空操作。
    /// 枚举类型和完整底层值共同决定 key，各系统持有独立 EventBus。
    /// Dispose 或 Play Session reset 永久关闭旧实例并截断旧在途通知。
    /// 关闭后注册与发送抛出 ObjectDisposedException，退订为空操作；Instance 可创建新实例。
    /// </summary>
    public class EnumEventSystem : ISingleton
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static EnumEventSystem Instance => SingletonProperty<EnumEventSystem>.Instance;

        private static EnumEventSystem _sessionInstance;
        private readonly EventBus _bus = new();

        static EnumEventSystem()
        {
            SingletonSessionState.RegisterSessionReset(ResetSessionState);
        }

        private static void ResetSessionState()
        {
            // Singleton 自身可能已先清入口，独立保留本轮待关闭实例。
            var previous = _sessionInstance;
            _sessionInstance = null;
            previous?.Dispose();
        }

        /// <summary>
        /// 创建事件系统实例；外部请通过 <see cref="Instance"/> 访问单例。
        /// </summary>
        protected EnumEventSystem()
        {
        }

        /// <summary>
        /// 注册指定枚举 key 的监听，返回注销句柄；key 不存在时自动创建对应事件。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnregister Register<T>(T key, Action<object[]> eventAction) where T : Enum
        {
            return _bus.Register(new EventKey(key), eventAction);
        }

        /// <summary>
        /// 注销指定 key 上的一个监听。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void Unregister<T>(T key, Action<object[]> eventAction) where T : Enum
        {
            _bus.Unregister(new EventKey(key), eventAction);
        }

        /// <summary>
        /// 终结并移除指定 key 的整个事件及在途通知（该 key 上所有监听失效）。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        public void Unregister<T>(T key) where T : Enum
        {
            _bus.RemoveKey(new EventKey(key));
        }

        /// <summary>
        /// 终结并移除所有 key 的事件及在途通知，系统仍可用；关闭后为空操作。
        /// </summary>
        public void UnregisterAll()
        {
            if (!_bus.IsDisposed)
            {
                _bus.ClearKeys();
            }
        }

        /// <summary>
        /// 向指定 key 发送事件，参数作为 object[] 传递；无监听时为空操作。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="key">事件 key。</param>
        /// <param name="args">事件参数。</param>
        public void Send<T>(T key, params object[] args) where T : Enum
        {
            _bus.Send(new EventKey(key), args);
        }

        /// <inheritdoc />
        public void OnSingletonInit()
        {
            _bus.ClearKeys();
            _sessionInstance = this;
        }

        /// <summary>
        /// 永久关闭自身并注销所有监听；仅释放仍指向自身的单例入口，重复调用为空操作。
        /// 下次访问 <see cref="Instance"/> 会创建新实例，旧实例不能影响它。
        /// </summary>
        public void Dispose()
        {
            if (_bus.IsDisposed)
            {
                return;
            }

            _bus.Dispose();
            if (ReferenceEquals(_sessionInstance, this))
            {
                _sessionInstance = null;
            }

            if (SingletonProperty<EnumEventSystem>.TryGetInstance(out var current) && ReferenceEquals(current, this))
            {
                SingletonProperty<EnumEventSystem>.Dispose();
            }
        }
    }
}
