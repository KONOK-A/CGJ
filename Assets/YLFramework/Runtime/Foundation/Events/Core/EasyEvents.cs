using System;
using System.Collections.Generic;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// 按类型存储一组事件实例的集合（类型 → 事件）。
    /// 实例版可用于子系统内部；静态入口维护一个全局实例，
    /// 其映射会在每次进入 Play 会话时通过 Singleton 的会话重置机制清空，
    /// 避免 Domain Reload disabled 时旧监听残留到下一次 Play。
    /// 会话运行期内有意注册的全局事件不会被清空。
    /// </summary>
    public class EasyEvents
    {
        private static readonly EasyEvents GlobalEasyEvents = new();

        static EasyEvents()
        {
            // Domain Reload disabled 时静态字段会跨 Play 会话保留，
            // 使用项目既有的会话重置机制清空全局映射（仅清空映射，不替换实例）
            SingletonSessionState.RegisterSessionReset(ClearGlobalEvents);
        }

        /// <summary>
        /// 获取全局集合中已注册的事件；类型未注册时返回 default。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public static T Get<T>() where T : IEasyEvent, new()
        {
            return GlobalEasyEvents.GetEvent<T>();
        }

        /// <summary>
        /// 向全局集合注册一个事件类型；同一类型重复注册会被忽略。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public static void Register<T>() where T : IEasyEvent, new()
        {
            GlobalEasyEvents.AddEvent<T>();
        }

        private static void ClearGlobalEvents()
        {
            GlobalEasyEvents.Clear();
        }

        private readonly Dictionary<Type, IEasyEvent> _eventTypeDict = new();

        /// <summary>
        /// 添加一个事件实例；同一类型已存在时忽略本次添加（不抛异常）。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public void AddEvent<T>() where T : IEasyEvent, new()
        {
            var eventType = typeof(T);
            if (!_eventTypeDict.ContainsKey(eventType))
            {
                _eventTypeDict.Add(eventType, new T());
            }
        }

        /// <summary>
        /// 获取已注册的事件实例；类型不存在时返回 default。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public T GetEvent<T>() where T : IEasyEvent, new()
        {
            return _eventTypeDict.TryGetValue(typeof(T), out var e) ? (T)e : default;
        }

        /// <summary>
        /// 获取已注册的事件实例，不存在时创建并保存。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var eventType = typeof(T);
            if (_eventTypeDict.TryGetValue(eventType, out var e))
            {
                return (T)e;
            }

            var instance = new T();
            _eventTypeDict.Add(eventType, instance);
            return instance;
        }

        /// <summary>
        /// 移除指定类型的事件映射。
        /// </summary>
        /// <typeparam name="T">事件类型。</typeparam>
        /// <returns>存在并被移除时返回 true。</returns>
        public bool Remove<T>() where T : IEasyEvent, new()
        {
            return _eventTypeDict.Remove(typeof(T));
        }

        /// <summary>
        /// 清空所有类型映射。只解除引用，不回收事件实例（事件生命周期由创建方管理）。
        /// </summary>
        public void Clear()
        {
            _eventTypeDict.Clear();
        }

        /// <summary>
        /// 注销所有已登记事件上的全部监听，再清空类型映射；不回收事件实例（生命周期由创建方管理）。
        /// </summary>
        public void ClearListeners()
        {
            foreach (var e in _eventTypeDict.Values) e.UnRegisterAll();
            _eventTypeDict.Clear();
        }
    }
}
