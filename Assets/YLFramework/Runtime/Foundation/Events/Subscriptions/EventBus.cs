using System;
using System.Collections.Generic;
using YLFramework.Singletons;

namespace YLFramework.Events
{
    /// <summary>
    /// 非线程安全的强类型事件上下文。每个键只绑定一个精确参数签名，广播由共享 EventChannel 执行。
    /// 缺失键的发布、退订及空回调注册不建键；清监听保留签名，RemoveKey 才允许重新绑定。
    /// 普通 Send 保留原异常并立即中断；SendAll 尝试所有观察者并汇总异常。
    /// Dispose 永久关闭实例；独立实例不随 Play Session 自动关闭。
    /// </summary>
    public sealed class EventBus : IDisposable
    {
        private static EventBus _default;
        private static bool _isSessionResetRegistered;
        private readonly Dictionary<EventKey, EventChannelBase> _channels = new();
        private bool _isDisposed;

        /// <summary>显式选择全局上下文；仅此入口会惰性创建 Default。</summary>
        public static EventBus Default
        {
            get
            {
                if (!_isSessionResetRegistered)
                {
                    SingletonSessionState.RegisterSessionReset(ResetDefault);
                    _isSessionResetRegistered = true;
                }
                return _default ??= new EventBus();
            }
        }

        /// <summary>非创建式取得 Default，可在成功后调用其 Send/SendAll/RemoveKey。</summary>
        public static bool TryGetDefault(out EventBus bus)
        {
            bus = _default;
            return bus != null;
        }

        /// <summary>非创建式关闭旧 Default；下一次显式访问 Default 建立新实例。</summary>
        public static void ResetDefault()
        {
            var previous = _default;
            _default = null;
            previous?.Dispose();
        }

        public bool IsDisposed => _isDisposed;
        public int KeyCount
        {
            get
            {
                ThrowIfDisposed();
                return _channels.Count;
            }
        }

        public bool HasKey(EventKey key)
        {
            ThrowIfDisposed();
            key.Validate();
            return _channels.ContainsKey(key);
        }

        public bool HasListener(EventKey key) => ListenerCount(key) != 0;

        public int ListenerCount(EventKey key)
        {
            ThrowIfDisposed();
            key.Validate();
            return _channels.TryGetValue(key, out var channel) ? channel.ListenerCount : 0;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(EventBus));
            }
        }

        private TChannel GetExisting<TChannel>(EventKey key) where TChannel : EventChannelBase
        {
            ThrowIfDisposed();
            key.Validate();
            if (!_channels.TryGetValue(key, out var channel))
            {
                return null;
            }

            if (channel.GetType() != typeof(TChannel))
            {
                throw new InvalidOperationException(
                    $"Event key {key} is bound to {channel.GetType()}, not {typeof(TChannel)}. RemoveKey before changing its signature.");
            }
            return (TChannel)channel;
        }

        public IUnregister Register(EventKey key, Action callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce(EventKey key, Action callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore(EventKey key, Action callback, bool once)
        {
            var channel = GetExisting<EventChannel>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister(EventKey key, Action callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel>(key);
            channel?.Unregister(callback);
        }

        public void Send(EventKey key) => GetExisting<EventChannel>(key)?.Trigger();
        public void SendAll(EventKey key) => GetExisting<EventChannel>(key)?.TriggerAll();

        public IUnregister Register<T1>(EventKey key, Action<T1> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1>(EventKey key, Action<T1> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1>(EventKey key, Action<T1> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1>(EventKey key, Action<T1> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1>(EventKey key, T1 p1) => GetExisting<EventChannel<T1>>(key)?.Trigger(p1);
        public void SendAll<T1>(EventKey key, T1 p1) => GetExisting<EventChannel<T1>>(key)?.TriggerAll(p1);

        public IUnregister Register<T1, T2>(EventKey key, Action<T1, T2> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2>(EventKey key, Action<T1, T2> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2>(EventKey key, Action<T1, T2> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2>(EventKey key, Action<T1, T2> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2>(EventKey key, T1 p1, T2 p2) => GetExisting<EventChannel<T1, T2>>(key)?.Trigger(p1, p2);
        public void SendAll<T1, T2>(EventKey key, T1 p1, T2 p2) => GetExisting<EventChannel<T1, T2>>(key)?.TriggerAll(p1, p2);

        public IUnregister Register<T1, T2, T3>(EventKey key, Action<T1, T2, T3> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2, T3>(EventKey key, Action<T1, T2, T3> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2, T3>(EventKey key, Action<T1, T2, T3> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2, T3>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2, T3>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2, T3>(EventKey key, Action<T1, T2, T3> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2, T3>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2, T3>(EventKey key, T1 p1, T2 p2, T3 p3) => GetExisting<EventChannel<T1, T2, T3>>(key)?.Trigger(p1, p2, p3);
        public void SendAll<T1, T2, T3>(EventKey key, T1 p1, T2 p2, T3 p3) => GetExisting<EventChannel<T1, T2, T3>>(key)?.TriggerAll(p1, p2, p3);

        public IUnregister Register<T1, T2, T3, T4>(EventKey key, Action<T1, T2, T3, T4> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2, T3, T4>(EventKey key, Action<T1, T2, T3, T4> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2, T3, T4>(EventKey key, Action<T1, T2, T3, T4> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2, T3, T4>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2, T3, T4>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2, T3, T4>(EventKey key, Action<T1, T2, T3, T4> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2, T3, T4>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2, T3, T4>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4) => GetExisting<EventChannel<T1, T2, T3, T4>>(key)?.Trigger(p1, p2, p3, p4);
        public void SendAll<T1, T2, T3, T4>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4) => GetExisting<EventChannel<T1, T2, T3, T4>>(key)?.TriggerAll(p1, p2, p3, p4);

        public IUnregister Register<T1, T2, T3, T4, T5>(EventKey key, Action<T1, T2, T3, T4, T5> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2, T3, T4, T5>(EventKey key, Action<T1, T2, T3, T4, T5> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2, T3, T4, T5>(EventKey key, Action<T1, T2, T3, T4, T5> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2, T3, T4, T5>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2, T3, T4, T5>(EventKey key, Action<T1, T2, T3, T4, T5> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2, T3, T4, T5>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) => GetExisting<EventChannel<T1, T2, T3, T4, T5>>(key)?.Trigger(p1, p2, p3, p4, p5);
        public void SendAll<T1, T2, T3, T4, T5>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) => GetExisting<EventChannel<T1, T2, T3, T4, T5>>(key)?.TriggerAll(p1, p2, p3, p4, p5);

        public IUnregister Register<T1, T2, T3, T4, T5, T6>(EventKey key, Action<T1, T2, T3, T4, T5, T6> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2, T3, T4, T5, T6>(EventKey key, Action<T1, T2, T3, T4, T5, T6> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2, T3, T4, T5, T6>(EventKey key, Action<T1, T2, T3, T4, T5, T6> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5, T6>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2, T3, T4, T5, T6>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2, T3, T4, T5, T6>(EventKey key, Action<T1, T2, T3, T4, T5, T6> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5, T6>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2, T3, T4, T5, T6>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) => GetExisting<EventChannel<T1, T2, T3, T4, T5, T6>>(key)?.Trigger(p1, p2, p3, p4, p5, p6);
        public void SendAll<T1, T2, T3, T4, T5, T6>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) => GetExisting<EventChannel<T1, T2, T3, T4, T5, T6>>(key)?.TriggerAll(p1, p2, p3, p4, p5, p6);

        public IUnregister Register<T1, T2, T3, T4, T5, T6, T7>(EventKey key, Action<T1, T2, T3, T4, T5, T6, T7> callback) => RegisterCore(key, callback, false);
        public IUnregister RegisterOnce<T1, T2, T3, T4, T5, T6, T7>(EventKey key, Action<T1, T2, T3, T4, T5, T6, T7> callback) => RegisterCore(key, callback, true);

        private IUnregister RegisterCore<T1, T2, T3, T4, T5, T6, T7>(EventKey key, Action<T1, T2, T3, T4, T5, T6, T7> callback, bool once)
        {
            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5, T6, T7>>(key);
            if (callback == null)
            {
                return EmptyUnregister.Instance;
            }

            if (channel == null)
            {
                _channels.Add(key, channel = new EventChannel<T1, T2, T3, T4, T5, T6, T7>());
            }

            return once ? channel.RegisterOnce(callback) : channel.Register(callback);
        }

        public void Unregister<T1, T2, T3, T4, T5, T6, T7>(EventKey key, Action<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            if (_isDisposed)
            {
                return;
            }

            var channel = GetExisting<EventChannel<T1, T2, T3, T4, T5, T6, T7>>(key);
            channel?.Unregister(callback);
        }

        public void Send<T1, T2, T3, T4, T5, T6, T7>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) => GetExisting<EventChannel<T1, T2, T3, T4, T5, T6, T7>>(key)?.Trigger(p1, p2, p3, p4, p5, p6, p7);
        public void SendAll<T1, T2, T3, T4, T5, T6, T7>(EventKey key, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) => GetExisting<EventChannel<T1, T2, T3, T4, T5, T6, T7>>(key)?.TriggerAll(p1, p2, p3, p4, p5, p6, p7);

        /// <summary>按 typeof(T) 注册 payload；与命名键入口区分，不按实际 payload 子类型路由。</summary>
        public IUnregister RegisterMessage<T>(Action<T> callback) => Register(EventKey.ForType<T>(), callback);
        /// <summary>在 typeof(T) 的 payload 通道注册一次性回调。</summary>
        public IUnregister RegisterMessageOnce<T>(Action<T> callback) => RegisterOnce(EventKey.ForType<T>(), callback);
        /// <summary>只取消 typeof(T) 通道上最后一个相等的普通委托子序列；已关闭时为空操作。</summary>
        public void UnregisterMessage<T>(Action<T> callback) => Unregister(EventKey.ForType<T>(), callback);
        /// <summary>按泛型声明类型发布 payload；string/int/enum payload 不会被解释为命名键。</summary>
        public void SendMessage<T>(T payload) => Send(EventKey.ForType<T>(), payload);
        /// <summary>构造并发布 typeof(T) 的消息；缺失键时不构造 payload。</summary>
        public void SendMessage<T>() where T : new() => GetExisting<EventChannel<T>>(EventKey.ForType<T>())?.Trigger(new T());
        /// <summary>按 typeof(T) 发布 payload，逐观察者继续并汇总异常。</summary>
        public void SendMessageAll<T>(T payload) => SendAll(EventKey.ForType<T>(), payload);

        /// <summary>仅清监听，保留已有签名；普通在途快照仍按 channel 契约完成。</summary>
        public void ClearListeners(EventKey key)
        {
            ThrowIfDisposed();
            key.Validate();
            if (_channels.TryGetValue(key, out var channel))
            {
                channel.ClearListeners();
            }
        }

        /// <summary>仅清所有键的监听，不删除键或签名。</summary>
        public void ClearListeners()
        {
            ThrowIfDisposed();
            foreach (var channel in _channels.Values)
            {
                channel.ClearListeners();
            }
        }

        /// <summary>终结指定键的 channel 与旧在途快照；删除签名，允许后续重新绑定。已关闭时返回 false。</summary>
        public bool RemoveKey(EventKey key)
        {
            if (_isDisposed)
            {
                return false;
            }

            key.Validate();
            if (!_channels.TryGetValue(key, out var channel))
            {
                return false;
            }

            _channels.Remove(key);
            channel.Dispose();
            return true;
        }

        /// <summary>终结并删除全部键，但 bus 本身保持可用。</summary>
        public void ClearKeys()
        {
            ThrowIfDisposed();
            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }

            _channels.Clear();
        }

        /// <summary>永久关闭 bus，清除监听及签名；重复调用无副作用。</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            if (ReferenceEquals(_default, this))
            {
                _default = null;
            }

            foreach (var channel in _channels.Values)
            {
                channel.Dispose();
            }

            _channels.Clear();
        }
    }
}
