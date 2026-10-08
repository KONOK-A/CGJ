using System;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// 以事件类型为 key 的全局事件系统：每个泛型类型对应一个事件。
    /// 事件实例由本系统持有、不参与对象池，随系统实例一起丢弃。
    /// <see cref="Dispose"/> 后旧实例的监听不再触发，下次访问 <see cref="Instance"/> 从空开始。
    /// </summary>
    public class TypeEventSystem : ISingleton
    {
        private EasyEvents _events = new();

        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static TypeEventSystem Instance => SingletonProperty<TypeEventSystem>.Instance;

        /// <summary>
        /// 创建并发送一个默认构造的事件实例；无监听时为空操作。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public void Send<T>() where T : new()
        {
            _events.GetEvent<EasyEvent<T>>()?.Trigger(new T());
        }

        /// <summary>
        /// 发送事件实例；无监听时为空操作。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="e">事件实例。</param>
        public void Send<T>(T e)
        {
            _events.GetEvent<EasyEvent<T>>()?.Trigger(e);
        }

        /// <summary>
        /// 注册指定事件类型的监听，返回注销句柄；类型不存在时自动创建对应事件。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register<T>(Action<T> eventAction)
        {
            return _events.GetOrAddEvent<EasyEvent<T>>().Register(eventAction);
        }

        /// <summary>
        /// 注销指定事件类型上的一个监听。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister<T>(Action<T> eventAction)
        {
            var e = _events.GetEvent<EasyEvent<T>>();
            e?.UnRegister(eventAction);
        }

        /// <inheritdoc />
        public void OnSingletonInit()
        {
            _events = new EasyEvents();
        }

        /// <summary>
        /// 清空全部监听并释放单例实例；下次访问 <see cref="Instance"/> 会创建新实例。
        /// </summary>
        public void Dispose()
        {
            _events.Clear();
            SingletonProperty<TypeEventSystem>.Dispose();
        }
    }
}
