using System;
using System.Collections.Generic;
using YLFramework.Core.Singleton;
using YLFramework.Events.Internal;

namespace YLFramework.Events
{
    /// <summary>
    /// 以字符串为 key 的全局事件系统，支持 0~3 个参数的重载。
    /// 同一 key 共享一个事件实例；无监听回调时发送为空操作。
    /// 类型安全：同一个 key 必须始终使用相同的参数签名。
    /// 若按其他签名访问已存在的 key（注册 / 注销 / 发送），会抛出
    /// <see cref="InvalidOperationException"/>，错误信息包含 key、已绑定的事件类型与请求类型；
    /// 如需更换签名，请先调用 <see cref="UnRegister(string)"/> 移除旧事件。
    /// 实例由 SingletonProperty 管理：<see cref="Dispose"/> 后旧实例的监听全部失效。
    /// </summary>
    public class StringEventSystem : ISingleton
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static StringEventSystem Instance => SingletonProperty<StringEventSystem>.Instance;

        private readonly Dictionary<string, IEasyEvent> _events = new();

        /// <summary>
        /// 按期望参数签名取回 key 上已有的事件；key 不存在返回 null，
        /// 已存在但签名不一致时抛出 <see cref="InvalidOperationException"/>。
        /// </summary>
        private TEvent GetExistingEvent<TEvent>(string key) where TEvent : class, IEasyEvent
        {
            if (!_events.TryGetValue(key, out var existing))
            {
                return null;
            }

            if (existing is TEvent typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                $"[Events] StringEventSystem 的 key \"{key}\" 已绑定 {DescribeEventType(existing.GetType())}，" +
                $"无法按 {DescribeEventType(typeof(TEvent))} 访问；同一 key 必须保持相同的参数签名，" +
                "如需更换签名请先调用 UnRegister(key) 移除旧事件。");
        }

        /// <summary>
        /// 生成便于阅读的事件类型名（如 EasyEvent&lt;Int32&gt;）。
        /// </summary>
        private static string DescribeEventType(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }

            var name = type.Name;
            var tickIndex = name.IndexOf('`');
            if (tickIndex >= 0)
            {
                name = name.Substring(0, tickIndex);
            }

            var arguments = type.GetGenericArguments();
            var argumentNames = new string[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                argumentNames[i] = DescribeEventType(arguments[i]);
            }

            return $"{name}<{string.Join(", ", argumentNames)}>";
        }

        #region 零参

        /// <summary>
        /// 注册无参事件的监听，key 不存在时自动创建对应事件。
        /// </summary>
        /// <param name="key">事件 key。</param>
        /// <param name="action">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public IUnRegister Register(string key, Action action)
        {
            var existing = GetExistingEvent<EasyEvent>(key);
            if (existing != null)
            {
                return existing.Register(action);
            }

            var easyEvent = EasyEvent.Allocate();
            _events.Add(key, easyEvent);
            return easyEvent.Register(action);
        }

        /// <summary>
        /// 注销无参事件上的一个监听。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void UnRegister(string key, Action action)
        {
            GetExistingEvent<EasyEvent>(key)?.UnRegister(action);
        }

        /// <summary>
        /// 发送无参事件；无监听时为空操作。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void Send(string key)
        {
            GetExistingEvent<EasyEvent>(key)?.Trigger();
        }

        #endregion

        #region 1参

        /// <summary>
        /// 注册单参数事件的监听，key 不存在时自动创建对应事件。
        /// </summary>
        /// <returns>用于注销本次注册的句柄。</returns>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public IUnRegister Register<T>(string key, Action<T> action)
        {
            var existing = GetExistingEvent<EasyEvent<T>>(key);
            if (existing != null)
            {
                return existing.Register(action);
            }

            var easyEvent = EasyEvent<T>.Allocate();
            _events.Add(key, easyEvent);
            return easyEvent.Register(action);
        }

        /// <summary>
        /// 注销单参数事件上的一个监听。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void UnRegister<T>(string key, Action<T> action)
        {
            GetExistingEvent<EasyEvent<T>>(key)?.UnRegister(action);
        }

        /// <summary>
        /// 发送单参数事件；无监听时为空操作。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void Send<T>(string key, T value)
        {
            GetExistingEvent<EasyEvent<T>>(key)?.Trigger(value);
        }

        #endregion

        #region 2参

        /// <summary>
        /// 注册双参数事件的监听，key 不存在时自动创建对应事件。
        /// </summary>
        /// <returns>用于注销本次注册的句柄。</returns>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public IUnRegister Register<T1, T2>(string key, Action<T1, T2> action)
        {
            var existing = GetExistingEvent<EasyEvent<T1, T2>>(key);
            if (existing != null)
            {
                return existing.Register(action);
            }

            var easyEvent = EasyEvent<T1, T2>.Allocate();
            _events.Add(key, easyEvent);
            return easyEvent.Register(action);
        }

        /// <summary>
        /// 注销双参数事件上的一个监听。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void UnRegister<T1, T2>(string key, Action<T1, T2> action)
        {
            GetExistingEvent<EasyEvent<T1, T2>>(key)?.UnRegister(action);
        }

        /// <summary>
        /// 发送双参数事件；无监听时为空操作。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void Send<T1, T2>(string key, T1 param1, T2 param2)
        {
            GetExistingEvent<EasyEvent<T1, T2>>(key)?.Trigger(param1, param2);
        }

        #endregion

        #region 3参

        /// <summary>
        /// 注册三参数事件的监听，key 不存在时自动创建对应事件。
        /// </summary>
        /// <returns>用于注销本次注册的句柄。</returns>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public IUnRegister Register<T1, T2, T3>(string key, Action<T1, T2, T3> action)
        {
            var existing = GetExistingEvent<EasyEvent<T1, T2, T3>>(key);
            if (existing != null)
            {
                return existing.Register(action);
            }

            var easyEvent = EasyEvent<T1, T2, T3>.Allocate();
            _events.Add(key, easyEvent);
            return easyEvent.Register(action);
        }

        /// <summary>
        /// 注销三参数事件上的一个监听。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void UnRegister<T1, T2, T3>(string key, Action<T1, T2, T3> action)
        {
            GetExistingEvent<EasyEvent<T1, T2, T3>>(key)?.UnRegister(action);
        }

        /// <summary>
        /// 发送三参数事件；无监听时为空操作。
        /// </summary>
        /// <exception cref="InvalidOperationException">key 已被其他参数签名占用。</exception>
        public void Send<T1, T2, T3>(string key, T1 param1, T2 param2, T3 param3)
        {
            GetExistingEvent<EasyEvent<T1, T2, T3>>(key)?.Trigger(param1, param2, param3);
        }

        #endregion

        /// <summary>
        /// 移除并回收指定 key 的整个事件（该 key 上所有监听失效）。
        /// 与签名无关，始终可用；移除后可安全地用其他参数签名重新使用该 key。
        /// </summary>
        /// <param name="key">事件 key。</param>
        public void UnRegister(string key)
        {
            if (_events.TryGetValue(key, out var e))
            {
                e?.RecycleToCache();
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
            SingletonProperty<StringEventSystem>.Dispose();
        }
    }
}
