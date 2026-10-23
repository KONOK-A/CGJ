using System;

namespace YLFramework.Actions
{
    /// <summary>
    /// 配置自定义动作的回调与数据，并由调用方主动标记完成。
    /// </summary>
    /// <typeparam name="TData">自定义数据的类型。</typeparam>
    public interface ICustomApi<TData>
    {
        /// <summary>
        /// 当前租约的数据；Reset 保留，Deinit 重置为默认值，不负责 Dispose。
        /// </summary>
        TData Data
        {
            get; set;
        }

        /// <summary>
        /// 设置启动回调，重复设置会替换先前回调。
        /// </summary>
        /// <param name="onStart">每次启动时执行的回调。</param>
        /// <returns>当前配置接口。</returns>
        ICustomApi<TData> OnStart(Action onStart);

        /// <summary>
        /// 设置接收本次 dt 的执行回调，重复设置会替换先前回调。
        /// </summary>
        /// <param name="onExecute">启动后未暂停执行时调用，参数为本次时间步长。</param>
        /// <returns>当前配置接口。</returns>
        ICustomApi<TData> OnExecute(Action<float> onExecute);

        /// <summary>
        /// 设置正常完成回调；取消或 Deinit 不触发，重复设置会替换先前回调。
        /// </summary>
        /// <param name="onFinish">执行流程检测到完成时调用的回调。</param>
        /// <returns>当前配置接口。</returns>
        ICustomApi<TData> OnFinish(Action onFinish);

        /// <summary>
        /// 仅标记完成，由执行流程调用结束回调，不立即回收。
        /// </summary>
        void Finish();
    }

    // 生命周期完全由配置回调和调用方 Finish 驱动：OnStart/OnExecute 转发给回调，是否完成由回调内部决定。
    internal class Custom<TData> : IAction, ICustomApi<TData>
    {
        public TData Data
        {
            get; set;
        }

        protected Action StartCallback;
        protected Action<float> ExecuteCallback;
        protected Action FinishCallback;

        private static ActionPool<Custom<TData>> _pool =
            new ActionPool<Custom<TData>>(() => new Custom<TData>(), null, 10);

        protected Custom()
        {
        }

        public static Custom<TData> Allocate()
        {
            var custom = _pool.Allocate();
            custom.ActionId = ActionKit.IdGenerator++;
            custom.IsDeinitialized = false;
            custom.Reset();
            return custom;
        }

        public bool IsPaused
        {
            get; set;
        }

        public virtual void Deinit()
        {
            if (!IsDeinitialized)
            {
                IsDeinitialized = true;
                StartCallback = null;
                ExecuteCallback = null;
                FinishCallback = null;
                Data = default;

                ActionQueue.AddCallback(new ActionQueueRecycleCallback<Custom<TData>>(_pool, this));
            }
        }

        public void Reset()
        {
            IsPaused = false;
            Status = ActionStatus.NotStart;
        }

        public bool IsDeinitialized
        {
            get; set;
        }
        public ulong ActionId
        {
            get; set;
        }
        public ActionStatus Status
        {
            get; set;
        }

        public void OnStart()
        {
            StartCallback?.Invoke();
        }

        public void OnExecute(float dt)
        {
            ExecuteCallback?.Invoke(dt);
        }

        public void OnFinish()
        {
            FinishCallback?.Invoke();
        }

        public ICustomApi<TData> OnStart(Action onStart)
        {
            StartCallback = onStart;
            return this;
        }

        public ICustomApi<TData> OnExecute(Action<float> onExecute)
        {
            ExecuteCallback = onExecute;
            return this;
        }

        public ICustomApi<TData> OnFinish(Action onFinish)
        {
            FinishCallback = onFinish;
            return this;
        }

        public void Finish()
        {
            Status = ActionStatus.Finished;
        }
    }

    // Custom 是 Custom<object> 的封闭派生类，需要独立池来分配和回收自身类型（基类泛型池只会创建 Custom<object>）。
    internal class Custom : Custom<object>
    {
        private static ActionPool<Custom> _pool =
            new ActionPool<Custom>(() => new Custom(), null, 10);

        protected Custom()
        {
        }

        public new static Custom Allocate()
        {
            var custom = _pool.Allocate();
            custom.ActionId = ActionKit.IdGenerator++;
            custom.IsDeinitialized = false;
            custom.Reset();
            return custom;
        }

        public override void Deinit()
        {
            if (!IsDeinitialized)
            {
                IsDeinitialized = true;

                StartCallback = null;
                ExecuteCallback = null;
                FinishCallback = null;
                Data = default;

                ActionQueue.AddCallback(new ActionQueueRecycleCallback<Custom>(_pool, this));
            }
        }
    }

    /// <summary>
    /// 向组合追加带生命周期回调和用户数据的自定义动作。
    /// </summary>
    public static class CustomExtension
    {
        /// <summary>
        /// 立即配置并追加使用 object 数据的自定义动作，需要主动调用配置接口的 Finish 完成。
        /// </summary>
        /// <param name="self">要追加自定义动作的父组合。</param>
        /// <param name="onCustomSetting">追加前立即执行的配置回调。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Custom(this ISequence self, Action<ICustomApi<object>> onCustomSetting)
        {
            var custom = ActionKit.Custom(onCustomSetting);
            return self.Append(custom);
        }

        /// <summary>
        /// 立即配置并追加使用指定数据类型的自定义动作，需要主动调用配置接口的 Finish 完成。
        /// </summary>
        /// <typeparam name="TData">自定义数据的类型。</typeparam>
        /// <param name="self">要追加自定义动作的父组合。</param>
        /// <param name="onCustomSetting">追加前立即执行的配置回调。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Custom<TData>(this ISequence self, Action<ICustomApi<TData>> onCustomSetting)
        {
            var custom = ActionKit.Custom(onCustomSetting);
            return self.Append(custom);
        }
    }
}
