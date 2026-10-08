using System;
using System.Collections.Generic;

namespace YLFramework.Events
{
    /// <summary>
    /// 组合事件：任一 source 事件触发时调用自身的监听回调。
    /// 多个监听回调可共存：注销单个监听回调不影响其他监听回调，
    /// 最后一个监听回调被注销时自动释放所有 source 订阅；<see cref="Dispose"/> 可整体释放，且可重复调用。
    /// 分发语义与 EasyEvent 一致：source 触发瞬间对监听回调做快照，
    /// 回调中注销其他监听回调 / Dispose 不会影响本次分发，只影响后续触发。
    /// 注意：对同一个 source 多次调用 <see cref="Or"/> 会注册多次，该 source 触发时监听回调会被多次调用。
    /// </summary>
    public class OrEvent : IUnRegisterList
    {
        /// <inheritdoc />
        public List<IUnRegister> UnRegisterList { get; } = new();

        private Action _eventAction;

        /// <summary>
        /// 追加一个 source 事件，任一 source 触发都会调用本事件的监听回调。
        /// </summary>
        /// <param name="easyEvent">参与组合的事件。</param>
        /// <returns>自身，便于链式组合。</returns>
        public OrEvent Or(IEasyEvent easyEvent)
        {
            easyEvent.Register(Trigger).AddToUnRegisterList(this);
            return this;
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
        /// 注销一个监听回调；仅当最后一个监听回调被注销时才释放所有 source 订阅，
        /// 与单监听回调场景的历史行为保持一致。
        /// </summary>
        /// <param name="eventAction">要注销的监听回调。</param>
        public void UnRegister(Action eventAction)
        {
            _eventAction -= eventAction;
            if (_eventAction == null)
            {
                this.UnRegisterAll();
            }
        }

        /// <summary>
        /// 整体释放：清空全部监听回调并注销所有 source 订阅，可重复调用。
        /// </summary>
        public void Dispose()
        {
            _eventAction = null;
            this.UnRegisterAll();
        }

        private void Trigger()
        {
            _eventAction?.Invoke();
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
        public static OrEvent Or(this IEasyEvent self, IEasyEvent e)
        {
            return new OrEvent().Or(self).Or(e);
        }
    }
}
