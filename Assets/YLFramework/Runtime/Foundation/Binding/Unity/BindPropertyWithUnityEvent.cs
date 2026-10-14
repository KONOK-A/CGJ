using System;
using UnityEngine.Events;

namespace YLFramework.Binding
{
    /// <summary>
    /// 同时向代码监听者与 Inspector 配置的 UnityEvent 广播值变化的可绑定属性。
    /// 触发顺序：先执行基类的代码监听回调，再调用 <see cref="unityEvent"/>；
    /// 回收进池时会清空 UnityEvent 的全部监听，避免复用实例携带上一次的绑定。
    /// 仅可在主线程使用（UnityEvent 限制）。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    [Serializable]
    public class BindPropertyWithUnityEvent<T> : BindableProperty<T>
    {
        /// <summary>
        /// Inspector 中配置的额外值变化回调，参数为变化后的值；代码监听请使用基类的 Register。
        /// </summary>
        public UnityEvent<T> unityEvent = new();

        /// <summary>
        /// 创建值为 default 的属性。
        /// </summary>
        public BindPropertyWithUnityEvent()
        {
        }

        /// <summary>
        /// 创建带初始值的属性。
        /// </summary>
        /// <param name="defaultValue">初始值。</param>
        public BindPropertyWithUnityEvent(T defaultValue) : base(defaultValue)
        {
        }

        /// <summary>
        /// 触发值变化：先执行基类通知，再调用 Inspector 中配置的 UnityEvent（参数为当前值）。
        /// </summary>
        protected override void OnValueChange()
        {
            base.OnValueChange();
            unityEvent?.Invoke(Value);
        }

        /// <summary>
        /// 池回收时调用：先执行基类清理，再清空 UnityEvent 的全部监听。
        /// </summary>
        public override void OnRecycle()
        {
            base.OnRecycle();
            unityEvent.RemoveAllListeners();
        }
    }
}