/****************************************************************************
 * Copyright (c) 2015 - 2024 liangxiegame UNDER MIT License
 * 
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 ****************************************************************************/

using System;

namespace YLFramework.Actions
{
    // 即时回调动作：OnStart 中调用一次回调并立刻标记完成，没有逐帧执行阶段；适合插入序列中做同步通知。
    internal class CallbackAction : IAction
    {
        private CallbackAction()
        {
        }

        private Action mCallback;

        private static ActionPool<CallbackAction> mSimpleObjectPool =
            new ActionPool<CallbackAction>(() => new CallbackAction(), null, 10);

        public static CallbackAction Allocate(Action callback)
        {
            var callbackAction = mSimpleObjectPool.Allocate();
            callbackAction.ActionID = ActionKit.ID_GENERATOR++;
            callbackAction.Reset();
            callbackAction.Deinited = false;
            callbackAction.mCallback = callback;
            return callbackAction;
        }

        public bool Paused { get; set; }
        public bool Deinited { get; set; }
        public ulong ActionID { get; set; }
        public ActionStatus Status { get; set; }

        public void OnStart()
        {
            mCallback?.Invoke();
            this.Finish();
        }

        public void OnExecute(float dt)
        {
        }

        public void OnFinish()
        {
        }

        public void Deinit()
        {
            if (!Deinited)
            {
                Deinited = true;
                mCallback = null;
                ActionQueue.AddCallback(new ActionQueueRecycleCallback<CallbackAction>(mSimpleObjectPool, this));
            }
        }

        public void Reset()
        {
            Paused = false;
            Status = ActionStatus.NotStart;
        }
    }

    /// <summary>
    /// 向组合追加同步回调动作的链式扩展。
    /// </summary>
    public static class CallbackExtension
    {
        /// <summary>
        /// 追加在启动时调用回调并立即完成的动作，空回调也会完成。
        /// </summary>
        /// <param name="self">要追加回调动作的父组合。</param>
        /// <param name="callback">动作启动时执行的回调。</param>
        /// <returns>父组合，可继续追加动作。</returns>
        public static ISequence Callback(this ISequence self, Action callback)
        {
            return self.Append(global::YLFramework.Actions.CallbackAction.Allocate(callback));
        }
    }
}
