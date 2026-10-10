using System;
using System.Collections.Generic;

namespace YLFramework.Events
{
    /// <summary>
    /// 组合事件：任一 source 事件触发时调用自身的监听回调。
    /// 首个监听加入后才连接 source，末个监听离开时断开，下一次监听可以重新连接。
    /// 首次连接尚未完成时的同步注册属于该轮临时责任：连接失败时回滚全部临时注册并释放本次连接，
    /// 保留原始失败原因（必要时附清理错误）；明确断连后新建的轮次不受旧轮回滚影响。
    /// 普通取消保持已经开始的监听快照；Dispose 永久关闭并截断在途快照。
    /// source 描述保留到 Dispose；同一个 source 重复追加按次数连接，不去重。
    /// 取消失败的 source 句柄仍保留，可再次 Dispose 显式重试。
    /// </summary>
    public class OrEvent : IEventSource, IUnRegisterList, IDisposable
    {
        /// <inheritdoc />
        public List<IUnRegister> UnRegisterList { get; } = new();

        private readonly List<Source> _sources = new();
        private readonly ListenerChannel _listeners;
        private ConnectionRound _round;
        private bool _disposed;

        /// <summary>创建尚未连接 source 的组合事件。</summary>
        public OrEvent() => _listeners = new ListenerChannel(this);

        /// <summary>当前是否有监听。</summary>
        public bool HasListener => _listeners.HasListener;

        /// <summary>当前监听数量，重复监听独立计数。</summary>
        public int ListenerCount => _listeners.ListenerCount;

        private sealed class ListenerChannel : EventChannel
        {
            private readonly OrEvent _owner;

            public ListenerChannel(OrEvent owner) => _owner = owner;

            protected override void OnListenersChanged()
            {
                if (!HasListener && _owner._round != null) _owner.Disconnect(_owner._round);
            }
        }

        private sealed class Source
        {
            public readonly IEventSource Event;
            public bool Active = true;
            public Source(IEventSource source) => Event = source;
        }

        private sealed class ConnectionRound
        {
            public bool Active = true;
            public bool Connecting;
            public readonly List<Connection> Connections = new();
            // 连接尚未完成期间产生的监听注册；该轮失败时整体回滚，成功后转为正式监听。
            public readonly List<IUnRegister> Provisional = new();
        }

        private sealed class Connection : IUnRegister
        {
            private readonly OrEvent _owner;
            private readonly ConnectionRound _round;
            public readonly Source Source;
            private IUnRegister _token;
            private bool _active = true;
            private bool _canceling;

            public bool IsActive => _active;

            public Connection(OrEvent owner, ConnectionRound round, Source source)
            {
                _owner = owner;
                _round = round;
                Source = source;
            }

            public void Notify()
            {
                // source 的旧快照不能转发到重连后的新轮次；普通 listener 取消不截断 channel 快照。
                if (_active && Source.Active && _round.Active && ReferenceEquals(_owner._round, _round) && !_owner._disposed)
                    _owner._listeners.Trigger();
            }

            public void Attach(IUnRegister token)
            {
                _token = token ?? EmptyUnRegister.Instance;
                _owner.UnRegisterList.Add(this);
                // Register 可同步通知并重入关闭；迟到句柄只取消原来的连接。
                if (!_active || !Source.Active || !_round.Active || !ReferenceEquals(_owner._round, _round) || _owner._disposed)
                    UnRegister();
            }

            public void UnRegister()
            {
                _active = false;
                if (_canceling || _token == null) return;
                if (_owner.UnRegisterList.Contains(this))
                {
                    _owner.ReleaseConnections(new List<IUnRegister> { this });
                    return;
                }
                _canceling = true;
                try
                {
                    _token.UnRegister();
                    _token = null;
                    _owner.UnRegisterList.Remove(this);
                    _round.Connections.Remove(this);
                }
                finally { _canceling = false; }
            }
        }

        /// <summary>
        /// 追加一个 source 事件，任一 source 触发都会调用本事件的监听回调。
        /// </summary>
        /// <param name="source">参与组合的事件。</param>
        /// <returns>自身，便于链式组合。</returns>
        public OrEvent Or(IEventSource source)
        {
            ThrowIfDisposed();
            if (source == null) throw new ArgumentNullException(nameof(source));
            var descriptor = new Source(source);
            _sources.Add(descriptor);
            if (_round != null)
            {
                try { ConnectSource(descriptor, _round); }
                catch (Exception error)
                {
                    descriptor.Active = false;
                    _sources.Remove(descriptor);
                    // 同步通知可能已经重连；失败追加的描述在所有轮次中都需撤销，不能误撤其他描述。
                    var connections = new List<IUnRegister>();
                    foreach (var token in UnRegisterList)
                        if (token is Connection connection && connection.IsActive && ReferenceEquals(connection.Source, descriptor))
                            connections.Add(connection);
                    try { ReleaseConnections(connections); }
                    catch (Exception cleanupError) { throw new AggregateException(error, cleanupError); }
                    throw;
                }
            }
            return this;
        }

        /// <summary>
        /// 注册事件监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="eventAction">事件监听回调，为 null 时忽略。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register(Action eventAction)
        {
            return Register(eventAction, false);
        }

        /// <summary>注册只执行一次的监听，Once 重入规则由共享 channel 保证。</summary>
        public IUnRegister RegisterOnce(Action eventAction) => Register(eventAction, true);

        private IUnRegister Register(Action callback, bool once)
        {
            ThrowIfDisposed();
            var token = once ? _listeners.RegisterOnce(callback) : _listeners.Register(callback);
            if (callback == null) return token;
            var pending = _round;
            if (pending != null)
            {
                // 连接尚未完成期间的注册是同一轮的临时责任；失败时必须一并回滚。
                if (pending.Connecting) pending.Provisional.Add(token);
                return token;
            }
            if (!HasListener) return token;
            var round = new ConnectionRound { Connecting = true };
            round.Provisional.Add(token);
            _round = round;
            try
            {
                foreach (var source in _sources.ToArray())
                {
                    if (!round.Active || !ReferenceEquals(_round, round)) break;
                    ConnectSource(source, round);
                }
                // 连接成功：临时责任转为正式监听，本轮后续注册不再属于回滚范围。
                round.Connecting = false;
                round.Provisional.Clear();
                return token;
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                round.Active = false;
                round.Connecting = false;
                if (ReferenceEquals(_round, round)) _round = null;
                // 同一未完成连接轮的临时注册整体回滚；明确断连后新建的轮次不在其中。
                foreach (var provisional in round.Provisional)
                {
                    try { provisional.UnRegister(); }
                    catch (Exception cleanupError) { errors.Add(cleanupError); }
                }
                round.Provisional.Clear();
                try { Disconnect(round); }
                catch (Exception cleanupError) { errors.Add(cleanupError); }
                SubscriptionCleanup.ThrowErrors(errors);
                throw;
            }
        }

        private void ConnectSource(Source source, ConnectionRound round)
        {
            if (!source.Active) return;
            var connection = new Connection(this, round, source);
            round.Connections.Add(connection);
            IUnRegister token;
            try { token = source.Event.Register(connection.Notify); }
            catch
            {
                connection.UnRegister();
                round.Connections.Remove(connection);
                throw;
            }
            try { connection.Attach(token); }
            catch
            {
                // 迟到 token 的取消已经尝试过；责任留在公共清理列表，不在回滚中隐式重试。
                round.Connections.Remove(connection);
                throw;
            }
        }

        private void Disconnect(ConnectionRound round)
        {
            round.Active = false;
            if (ReferenceEquals(_round, round)) _round = null;
            var connections = new List<IUnRegister>(round.Connections);
            round.Connections.Clear();
            ReleaseConnections(connections);
        }

        private void ReleaseConnections(List<IUnRegister> connections)
        {
            // 同一责任只归还一次；整轮尝试期间不暴露给重入 Dispose，避免隐式重试或丢失。
            foreach (var connection in connections) UnRegisterList.Remove(connection);
            try { SubscriptionCleanup.ReleaseAll(connections, connection => connection.UnRegister()); }
            finally
            {
                foreach (var failed in connections)
                    if (!UnRegisterList.Contains(failed)) UnRegisterList.Add(failed);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(OrEvent));
        }

        /// <summary>
        /// 注册事件监听并立即同步调用一次回调，随后保持注册。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister RegisterWithACall(Action eventAction)
        {
            ThrowIfDisposed();
            eventAction?.Invoke();
            return Register(eventAction);
        }

        /// <summary>
        /// 注销一个监听回调；仅当最后一个监听回调被注销时才释放所有 source 订阅，
        /// 与单监听回调场景的历史行为保持一致。
        /// </summary>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister(Action eventAction)
        {
            _listeners.UnRegister(eventAction);
        }

        /// <summary>清空监听并断开 source；已开始的普通监听快照保持。</summary>
        public void ClearListeners() => _listeners.ClearListeners();

        /// <summary>
        /// 永久关闭并截断在途通知，尝试全部 source 取消；失败责任保留，重复调用可重试。
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_round != null) _round.Active = false;
                _round = null;
                _sources.Clear();
                _listeners.Dispose();
            }
            SubscriptionCleanup.ReleaseAll(UnRegisterList, token => token?.UnRegister());
        }
    }

    /// <summary>
    /// OrEvent 的扩展入口。
    /// </summary>
    public static class OrEventExtensions
    {
        /// <summary>
        /// 组合两个事件，任一触发都会调用返回的 OrEvent 的监听回调。
        /// </summary>
        /// <param name="self">第一个事件。</param>
        /// <param name="e">第二个事件。</param>
        /// <returns>组合后的事件。</returns>
        public static OrEvent Or(this IEventSource self, IEventSource e)
        {
            return new OrEvent().Or(self).Or(e);
        }
    }
}
