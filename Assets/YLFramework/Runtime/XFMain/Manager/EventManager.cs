using System;
using YLFramework.Core.RoslynAnalyzers;
using YLFramework.Events;

namespace YLFramework.Core
{
    /// <summary>
    /// Core 的强类型事件入口，全部转接 EventBus.Default，不持有独立事件状态。
    /// string 保留完整 Ordinal 身份，与 int 键互不相通；同一键必须使用精确一致的参数签名。
    /// AddEvent 返回本次注册的精确取消句柄，null 回调返回空句柄但仍校验键及已有签名。
    /// RemoveEvent 只取消最后匹配的委托子序列，保留签名及普通在途快照。
    /// TriggerEvent 保留原异常并立即中断；TriggerEventAll 尝试所有观察者后汇总异常。
    /// 发布、取消及清理不创建 Default，缺失键为空操作。
    /// </summary>
    public static class EventManager
    {
        private static EventBus GetDefault(EventKey key, bool create = false)
        {
            key.Validate();
            return EventBus.TryGetDefault(out var bus) ? bus : create ? EventBus.Default : null;
        }

        #region int 键

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent(int event_name_hash, Action callback)
            => GetDefault(event_name_hash, callback != null)?.Register(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent(int event_name_hash, Action callback)
            => GetDefault(event_name_hash)?.UnRegister(event_name_hash, callback);

        public static void TriggerEvent(int event_name_hash)
            => GetDefault(event_name_hash)?.Send(event_name_hash);

        public static void TriggerEventAll(int event_name_hash)
            => GetDefault(event_name_hash)?.SendAll(event_name_hash);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1>(int event_name_hash, Action<T1> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1>(int event_name_hash, Action<T1> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1>(event_name_hash, callback);

        public static void TriggerEvent<T1>(int event_name_hash, T1 p)
            => GetDefault(event_name_hash)?.Send<T1>(event_name_hash, p);

        public static void TriggerEventAll<T1>(int event_name_hash, T1 p1)
            => GetDefault(event_name_hash)?.SendAll<T1>(event_name_hash, p1);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2>(int event_name_hash, Action<T1, T2> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2>(int event_name_hash, Action<T1, T2> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2>(int event_name_hash, T1 p, T2 p2)
            => GetDefault(event_name_hash)?.Send<T1, T2>(event_name_hash, p, p2);

        public static void TriggerEventAll<T1, T2>(int event_name_hash, T1 p1, T2 p2)
            => GetDefault(event_name_hash)?.SendAll<T1, T2>(event_name_hash, p1, p2);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3>(int event_name_hash, Action<T1, T2, T3> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2, T3>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3>(int event_name_hash, Action<T1, T2, T3> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2, T3>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2, T3>(int event_name_hash, T1 p, T2 p2, T3 p3)
            => GetDefault(event_name_hash)?.Send<T1, T2, T3>(event_name_hash, p, p2, p3);

        public static void TriggerEventAll<T1, T2, T3>(int event_name_hash, T1 p1, T2 p2, T3 p3)
            => GetDefault(event_name_hash)?.SendAll<T1, T2, T3>(event_name_hash, p1, p2, p3);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4>(int event_name_hash, Action<T1, T2, T3, T4> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2, T3, T4>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4>(int event_name_hash, Action<T1, T2, T3, T4> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2, T3, T4>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2, T3, T4>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4)
            => GetDefault(event_name_hash)?.Send<T1, T2, T3, T4>(event_name_hash, p, p2, p3, p4);

        public static void TriggerEventAll<T1, T2, T3, T4>(int event_name_hash, T1 p1, T2 p2, T3 p3, T4 p4)
            => GetDefault(event_name_hash)?.SendAll<T1, T2, T3, T4>(event_name_hash, p1, p2, p3, p4);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5>(int event_name_hash, Action<T1, T2, T3, T4, T5> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2, T3, T4, T5>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5>(int event_name_hash, Action<T1, T2, T3, T4, T5> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2, T3, T4, T5>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4, T5 p5)
            => GetDefault(event_name_hash)?.Send<T1, T2, T3, T4, T5>(event_name_hash, p, p2, p3, p4, p5);

        public static void TriggerEventAll<T1, T2, T3, T4, T5>(int event_name_hash, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5)
            => GetDefault(event_name_hash)?.SendAll<T1, T2, T3, T4, T5>(event_name_hash, p1, p2, p3, p4, p5);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5, T6>(int event_name_hash, Action<T1, T2, T3, T4, T5, T6> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2, T3, T4, T5, T6>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6>(int event_name_hash, Action<T1, T2, T3, T4, T5, T6> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2, T3, T4, T5, T6>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6)
            => GetDefault(event_name_hash)?.Send<T1, T2, T3, T4, T5, T6>(event_name_hash, p, p2, p3, p4, p5, p6);

        public static void TriggerEventAll<T1, T2, T3, T4, T5, T6>(int event_name_hash, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6)
            => GetDefault(event_name_hash)?.SendAll<T1, T2, T3, T4, T5, T6>(event_name_hash, p1, p2, p3, p4, p5, p6);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, Action<T1, T2, T3, T4, T5, T6, T7> callback)
            => GetDefault(event_name_hash, callback != null)?.Register<T1, T2, T3, T4, T5, T6, T7>(event_name_hash, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, Action<T1, T2, T3, T4, T5, T6, T7> callback)
            => GetDefault(event_name_hash)?.UnRegister<T1, T2, T3, T4, T5, T6, T7>(event_name_hash, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7)
            => GetDefault(event_name_hash)?.Send<T1, T2, T3, T4, T5, T6, T7>(event_name_hash, p, p2, p3, p4, p5, p6, p7);

        public static void TriggerEventAll<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7)
            => GetDefault(event_name_hash)?.SendAll<T1, T2, T3, T4, T5, T6, T7>(event_name_hash, p1, p2, p3, p4, p5, p6, p7);

        #endregion

        #region string 键

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent(string event_name, Action callback)
            => GetDefault(event_name, callback != null)?.Register(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent(string event_name, Action callback)
            => GetDefault(event_name)?.UnRegister(event_name, callback);

        public static void TriggerEvent(string event_name)
            => GetDefault(event_name)?.Send(event_name);

        public static void TriggerEventAll(string event_name)
            => GetDefault(event_name)?.SendAll(event_name);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1>(string event_name, Action<T1> callback)
            => GetDefault(event_name, callback != null)?.Register<T1>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1>(string event_name, Action<T1> callback)
            => GetDefault(event_name)?.UnRegister<T1>(event_name, callback);

        public static void TriggerEvent<T1>(string event_name, T1 p)
            => GetDefault(event_name)?.Send<T1>(event_name, p);

        public static void TriggerEventAll<T1>(string event_name, T1 p1)
            => GetDefault(event_name)?.SendAll<T1>(event_name, p1);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2>(string event_name, Action<T1, T2> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2>(string event_name, Action<T1, T2> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2>(event_name, callback);

        public static void TriggerEvent<T1, T2>(string event_name, T1 p, T2 p2)
            => GetDefault(event_name)?.Send<T1, T2>(event_name, p, p2);

        public static void TriggerEventAll<T1, T2>(string event_name, T1 p1, T2 p2)
            => GetDefault(event_name)?.SendAll<T1, T2>(event_name, p1, p2);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3>(string event_name, Action<T1, T2, T3> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2, T3>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3>(string event_name, Action<T1, T2, T3> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2, T3>(event_name, callback);

        public static void TriggerEvent<T1, T2, T3>(string event_name, T1 p, T2 p2, T3 p3)
            => GetDefault(event_name)?.Send<T1, T2, T3>(event_name, p, p2, p3);

        public static void TriggerEventAll<T1, T2, T3>(string event_name, T1 p1, T2 p2, T3 p3)
            => GetDefault(event_name)?.SendAll<T1, T2, T3>(event_name, p1, p2, p3);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4>(string event_name, Action<T1, T2, T3, T4> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2, T3, T4>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4>(string event_name, Action<T1, T2, T3, T4> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2, T3, T4>(event_name, callback);

        public static void TriggerEvent<T1, T2, T3, T4>(string event_name, T1 p, T2 p2, T3 p3, T4 p4)
            => GetDefault(event_name)?.Send<T1, T2, T3, T4>(event_name, p, p2, p3, p4);

        public static void TriggerEventAll<T1, T2, T3, T4>(string event_name, T1 p1, T2 p2, T3 p3, T4 p4)
            => GetDefault(event_name)?.SendAll<T1, T2, T3, T4>(event_name, p1, p2, p3, p4);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5>(string event_name, Action<T1, T2, T3, T4, T5> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2, T3, T4, T5>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5>(string event_name, Action<T1, T2, T3, T4, T5> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2, T3, T4, T5>(event_name, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5)
            => GetDefault(event_name)?.Send<T1, T2, T3, T4, T5>(event_name, p, p2, p3, p4, p5);

        public static void TriggerEventAll<T1, T2, T3, T4, T5>(string event_name, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5)
            => GetDefault(event_name)?.SendAll<T1, T2, T3, T4, T5>(event_name, p1, p2, p3, p4, p5);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5, T6>(string event_name, Action<T1, T2, T3, T4, T5, T6> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2, T3, T4, T5, T6>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6>(string event_name, Action<T1, T2, T3, T4, T5, T6> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2, T3, T4, T5, T6>(event_name, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6)
            => GetDefault(event_name)?.Send<T1, T2, T3, T4, T5, T6>(event_name, p, p2, p3, p4, p5, p6);

        public static void TriggerEventAll<T1, T2, T3, T4, T5, T6>(string event_name, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6)
            => GetDefault(event_name)?.SendAll<T1, T2, T3, T4, T5, T6>(event_name, p1, p2, p3, p4, p5, p6);

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static IUnRegister AddEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, Action<T1, T2, T3, T4, T5, T6, T7> callback)
            => GetDefault(event_name, callback != null)?.Register<T1, T2, T3, T4, T5, T6, T7>(event_name, callback) ?? EmptyUnRegister.Instance;

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, Action<T1, T2, T3, T4, T5, T6, T7> callback)
            => GetDefault(event_name)?.UnRegister<T1, T2, T3, T4, T5, T6, T7>(event_name, callback);

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7)
            => GetDefault(event_name)?.Send<T1, T2, T3, T4, T5, T6, T7>(event_name, p, p2, p3, p4, p5, p6, p7);

        public static void TriggerEventAll<T1, T2, T3, T4, T5, T6, T7>(string event_name, T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7)
            => GetDefault(event_name)?.SendAll<T1, T2, T3, T4, T5, T6, T7>(event_name, p1, p2, p3, p4, p5, p6, p7);

        #endregion

        /// <summary>
        /// 终结并删除整个 Default 的全部键、签名及在途快照，包括通过其他 EventBus.Default 入口建立的键。
        /// Default 实例保持可用；不影响独立 EventBus 或 YL Global facade，不应作为 Module/View 的退出清理。
        /// </summary>
        public static void RemoveAllEvent()
        {
            if (EventBus.TryGetDefault(out var bus)) bus.ClearKeys();
        }

        /// <summary>终结并删除 Default 中此完整 string 键及在途快照，允许重新绑定签名；旧 token 失效。</summary>
        public static void RemoveAllEvent(string event_name)
            => GetDefault(event_name)?.RemoveKey(event_name);

        /// <summary>终结并删除 Default 中此 int 键及在途快照，允许重新绑定签名；旧 token 失效。</summary>
        public static void RemoveAllEvent(int event_name_hash)
            => GetDefault(event_name_hash)?.RemoveKey(event_name_hash);
    }
}
