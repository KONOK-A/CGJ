using System;
using YLFramework.Events.Internal;

namespace YLFramework.Events
{
    /// <summary>
    /// 最常用的无参事件。
    /// 分发语义：<see cref="Trigger"/> 按调用瞬间的多播委托快照依次调用监听回调，
    /// 回调中的注册 / 注销只影响后续 Trigger；某个监听回调抛异常会中断后续监听回调并向上抛出；
    /// 同一委托重复注册会被多次调用，注销一次只移除一次注册。
    /// 回调中回收本事件是安全的：当前已开始的 Trigger 快照仍会执行完，
    /// 回收会清空监听回调，回收后的 <see cref="HasListener"/> 为 false、后续 Trigger 为空操作。
    /// 事件可通过 <see cref="Allocate"/> 从对象池租用、<see cref="Recycle"/> 归还，归还及再次分配时都会清空监听回调。
    /// </summary>
    public class EasyEvent : IEasyEvent
    {
        private Action _eventAction;

        /// <summary>
        /// 从对象池分配一个无参事件实例。
        /// </summary>
        /// <returns>可立即注册监听回调的事件实例。</returns>
        public static EasyEvent Allocate()
        {
            return EasyEventPool<EasyEvent>.Allocate(10);
        }

        /// <summary>
        /// 将无参事件实例归还对象池，归还时清空全部监听回调。
        /// 在监听回调中调用同样安全：当前 Trigger 快照会继续执行完。
        /// </summary>
        /// <param name="easyEvent">待回收的事件实例。</param>
        public static void Recycle(EasyEvent easyEvent)
        {
            EasyEventPool<EasyEvent>.Recycle(easyEvent, 10);
        }

        /// <summary>
        /// 注册事件监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="eventAction">事件监听回调，为 null 时忽略。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register(Action eventAction)
        {
            _eventAction += eventAction;
            return new CustomUnRegister(() => UnRegister(eventAction));
        }

        /// <summary>
        /// 注册事件监听并立即同步调用一次回调，随后保持注册。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister RegisterWithACall(Action eventAction)
        {
            eventAction?.Invoke();
            return Register(eventAction);
        }

        /// <summary>
        /// 注册只触发一次的监听：第一次分发时先自动注销再调用回调（回调抛异常也已注销）。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于提前注销的句柄。</returns>
        public IUnRegister RegisterOnce(Action eventAction)
        {
            IUnRegister unRegister = null;
            unRegister = Register(() =>
            {
                unRegister?.UnRegister();
                eventAction?.Invoke();
            });
            return unRegister;
        }

        /// <summary>
        /// 注销一个已注册的回调；未注册时为空操作。
        /// </summary>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister(Action eventAction)
        {
            _eventAction -= eventAction;
        }

        /// <summary>
        /// 注销所有回调。
        /// </summary>
        public void UnRegisterAll()
        {
            _eventAction = null;
        }

        /// <summary>
        /// 当前是否存在有效监听回调。
        /// </summary>
        public bool HasListener => _eventAction != null;

        /// <summary>
        /// 当前注册的监听回调数量（重复注册的同一委托会被重复计数）。
        /// </summary>
        public int ListenerCount => _eventAction?.GetInvocationList().Length ?? 0;

        /// <summary>
        /// 触发事件。无监听回调时为空操作；监听回调抛出的异常直接向上传播。
        /// </summary>
        public void Trigger()
        {
            _eventAction?.Invoke();
        }

        /// <inheritdoc />
        public void RecycleToCache()
        {
            Recycle(this);
        }

        /// <inheritdoc />
        public void OnAllocate()
        {
            // 防御旧引用在对象位于池中时注册回调导致状态污染下一次租用
            _eventAction = null;
        }

        /// <inheritdoc />
        public void OnRecycle()
        {
            _eventAction = null;
        }

        /// <inheritdoc />
        public bool IsRecycled { get; set; } = false;
    }

    /// <summary>
    /// 带一个参数的事件。
    /// 分发语义与 <see cref="EasyEvent"/> 一致：Trigger 调用瞬间快照多播委托，
    /// 回调中的注册 / 注销只影响后续 Trigger，异常会中断后续监听回调并向上抛出。
    /// </summary>
    /// <typeparam name="T">事件参数类型。</typeparam>
    public class EasyEvent<T> : IEasyEvent
    {
        private Action<T> _eventAction;

        /// <summary>
        /// 从对象池分配一个单参数事件实例。
        /// </summary>
        public static EasyEvent<T> Allocate()
        {
            return EasyEventPool<EasyEvent<T>>.Allocate(1);
        }

        /// <summary>
        /// 将单参数事件实例归还对象池，归还时清空全部监听回调。
        /// </summary>
        /// <param name="easyEvent">待回收的事件实例。</param>
        public static void Recycle(EasyEvent<T> easyEvent)
        {
            EasyEventPool<EasyEvent<T>>.Recycle(easyEvent, 1);
        }

        /// <summary>
        /// 注册事件监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="eventAction">事件监听回调，为 null 时忽略。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register(Action<T> eventAction)
        {
            _eventAction += eventAction;
            return new CustomUnRegister(() => UnRegister(eventAction));
        }

        /// <summary>
        /// 注册只触发一次的监听：第一次分发时先自动注销再调用回调（回调抛异常也已注销）。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于提前注销的句柄。</returns>
        public IUnRegister RegisterOnce(Action<T> eventAction)
        {
            IUnRegister unRegister = null;
            unRegister = Register(value =>
            {
                unRegister?.UnRegister();
                eventAction?.Invoke(value);
            });
            return unRegister;
        }

        /// <summary>
        /// 注销一个已注册的回调；未注册时为空操作。
        /// </summary>
        public void UnRegister(Action<T> eventAction)
        {
            _eventAction -= eventAction;
        }

        /// <summary>
        /// 注销所有回调。
        /// </summary>
        public void UnRegisterAll()
        {
            _eventAction = null;
        }

        /// <summary>
        /// 当前是否存在有效监听回调。
        /// </summary>
        public bool HasListener => _eventAction != null;

        /// <summary>
        /// 当前注册的监听回调数量（重复注册的同一委托会被重复计数）。
        /// </summary>
        public int ListenerCount => _eventAction?.GetInvocationList().Length ?? 0;

        /// <summary>
        /// 触发事件。无监听回调时为空操作；监听回调抛出的异常直接向上传播。
        /// </summary>
        public void Trigger(T param)
        {
            _eventAction?.Invoke(param);
        }

        /// <inheritdoc />
        public void RecycleToCache()
        {
            Recycle(this);
        }

        /// <inheritdoc />
        public void OnAllocate()
        {
            // 防御旧引用在对象位于池中时注册回调导致状态污染下一次租用
            _eventAction = null;
        }

        /// <inheritdoc />
        public void OnRecycle()
        {
            _eventAction = null;
        }

        /// <inheritdoc />
        public bool IsRecycled { get; set; } = false;

        // 接口的显式方法：把无参注册适配为丢弃参数的单参数注册
        IUnRegister IEventSource.Register(Action eventAction)
        {
            return eventAction == null ? EmptyUnRegister.Instance : Register(Action);
            void Action(T _) => eventAction();
        }
    }

    /// <summary>
    /// 带两个参数的事件。
    /// 分发语义与 <see cref="EasyEvent"/> 一致：Trigger 调用瞬间快照多播委托，
    /// 回调中的注册 / 注销只影响后续 Trigger，异常会中断后续监听回调并向上抛出。
    /// </summary>
    /// <typeparam name="T1">第一个事件参数类型。</typeparam>
    /// <typeparam name="T2">第二个事件参数类型。</typeparam>
    public class EasyEvent<T1, T2> : IEasyEvent
    {
        private Action<T1, T2> _eventAction;

        /// <summary>
        /// 从对象池分配一个双参数事件实例。
        /// </summary>
        public static EasyEvent<T1, T2> Allocate()
        {
            return EasyEventPool<EasyEvent<T1, T2>>.Allocate(1);
        }

        /// <summary>
        /// 将双参数事件实例归还对象池，归还时清空全部监听回调。
        /// </summary>
        /// <param name="easyEvent">待回收的事件实例。</param>
        public static void Recycle(EasyEvent<T1, T2> easyEvent)
        {
            EasyEventPool<EasyEvent<T1, T2>>.Recycle(easyEvent, 1);
        }

        /// <summary>
        /// 注册事件监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="eventAction">事件监听回调，为 null 时忽略。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register(Action<T1, T2> eventAction)
        {
            _eventAction += eventAction;
            return new CustomUnRegister(() => UnRegister(eventAction));
        }

        /// <summary>
        /// 注册只触发一次的监听：第一次分发时先自动注销再调用回调（回调抛异常也已注销）。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于提前注销的句柄。</returns>
        public IUnRegister RegisterOnce(Action<T1, T2> eventAction)
        {
            IUnRegister unRegister = null;
            unRegister = Register((param1, param2) =>
            {
                unRegister?.UnRegister();
                eventAction?.Invoke(param1, param2);
            });
            return unRegister;
        }

        /// <summary>
        /// 注销一个已注册的回调；未注册时为空操作。
        /// </summary>
        public void UnRegister(Action<T1, T2> eventAction)
        {
            _eventAction -= eventAction;
        }

        /// <summary>
        /// 注销所有回调。
        /// </summary>
        public void UnRegisterAll()
        {
            _eventAction = null;
        }

        /// <summary>
        /// 当前是否存在有效监听回调。
        /// </summary>
        public bool HasListener => _eventAction != null;

        /// <summary>
        /// 当前注册的监听回调数量（重复注册的同一委托会被重复计数）。
        /// </summary>
        public int ListenerCount => _eventAction?.GetInvocationList().Length ?? 0;

        /// <summary>
        /// 触发事件。无监听回调时为空操作；监听回调抛出的异常直接向上传播。
        /// </summary>
        public void Trigger(T1 param1, T2 param2)
        {
            _eventAction?.Invoke(param1, param2);
        }

        /// <inheritdoc />
        public void RecycleToCache()
        {
            Recycle(this);
        }

        /// <inheritdoc />
        public void OnAllocate()
        {
            // 防御旧引用在对象位于池中时注册回调导致状态污染下一次租用
            _eventAction = null;
        }

        /// <inheritdoc />
        public void OnRecycle()
        {
            _eventAction = null;
        }

        /// <inheritdoc />
        public bool IsRecycled { get; set; } = false;

        // 接口的显式方法：把无参注册适配为丢弃参数的注册
        IUnRegister IEventSource.Register(Action eventAction)
        {
            return eventAction == null ? EmptyUnRegister.Instance : Register(Action);
            void Action(T1 _, T2 __) => eventAction();
        }
    }

    /// <summary>
    /// 带三个参数的事件。
    /// 分发语义与 <see cref="EasyEvent"/> 一致：Trigger 调用瞬间快照多播委托，
    /// 回调中的注册 / 注销只影响后续 Trigger，异常会中断后续监听回调并向上抛出。
    /// </summary>
    /// <typeparam name="T1">第一个事件参数类型。</typeparam>
    /// <typeparam name="T2">第二个事件参数类型。</typeparam>
    /// <typeparam name="T3">第三个事件参数类型。</typeparam>
    public class EasyEvent<T1, T2, T3> : IEasyEvent
    {
        private Action<T1, T2, T3> _eventAction;

        /// <summary>
        /// 从对象池分配一个三参数事件实例。
        /// </summary>
        public static EasyEvent<T1, T2, T3> Allocate()
        {
            return EasyEventPool<EasyEvent<T1, T2, T3>>.Allocate(1);
        }

        /// <summary>
        /// 将三参数事件实例归还对象池，归还时清空全部监听回调。
        /// </summary>
        /// <param name="easyEvent">待回收的事件实例。</param>
        public static void Recycle(EasyEvent<T1, T2, T3> easyEvent)
        {
            EasyEventPool<EasyEvent<T1, T2, T3>>.Recycle(easyEvent, 1);
        }

        /// <summary>
        /// 注册事件监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="eventAction">事件监听回调，为 null 时忽略。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public IUnRegister Register(Action<T1, T2, T3> eventAction)
        {
            _eventAction += eventAction;
            return new CustomUnRegister(() => UnRegister(eventAction));
        }

        /// <summary>
        /// 注册只触发一次的监听：第一次分发时先自动注销再调用回调（回调抛异常也已注销）。
        /// </summary>
        /// <param name="eventAction">事件监听回调。</param>
        /// <returns>用于提前注销的句柄。</returns>
        public IUnRegister RegisterOnce(Action<T1, T2, T3> eventAction)
        {
            IUnRegister unRegister = null;
            unRegister = Register((param1, param2, param3) =>
            {
                unRegister?.UnRegister();
                eventAction?.Invoke(param1, param2, param3);
            });
            return unRegister;
        }

        /// <summary>
        /// 注销一个已注册的回调；未注册时为空操作。
        /// </summary>
        public void UnRegister(Action<T1, T2, T3> eventAction)
        {
            _eventAction -= eventAction;
        }

        /// <summary>
        /// 注销所有回调。
        /// </summary>
        public void UnRegisterAll()
        {
            _eventAction = null;
        }

        /// <summary>
        /// 当前是否存在有效监听回调。
        /// </summary>
        public bool HasListener => _eventAction != null;

        /// <summary>
        /// 当前注册的监听回调数量（重复注册的同一委托会被重复计数）。
        /// </summary>
        public int ListenerCount => _eventAction?.GetInvocationList().Length ?? 0;

        /// <summary>
        /// 触发事件。无监听回调时为空操作；监听回调抛出的异常直接向上传播。
        /// </summary>
        public void Trigger(T1 param1, T2 param2, T3 param3)
        {
            _eventAction?.Invoke(param1, param2, param3);
        }

        /// <inheritdoc />
        public void RecycleToCache()
        {
            Recycle(this);
        }

        /// <inheritdoc />
        public void OnAllocate()
        {
            // 防御旧引用在对象位于池中时注册回调导致状态污染下一次租用
            _eventAction = null;
        }

        /// <inheritdoc />
        public void OnRecycle()
        {
            _eventAction = null;
        }

        /// <inheritdoc />
        public bool IsRecycled { get; set; } = false;

        // 接口的显式方法：把无参注册适配为丢弃参数的注册
        IUnRegister IEventSource.Register(Action eventAction)
        {
            return eventAction == null ? EmptyUnRegister.Instance : Register(Action);
            void Action(T1 _, T2 __, T3 ___) => eventAction();
        }
    }
}
