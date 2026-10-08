// 适配自本地 ActionKit ScreenTransition 源码，遵循模块的 MIT 许可证。
// https://github.com/liangxiegame/QFramework
using System;

namespace YLFramework.Actions
{
    /// <summary>配置并启动入场、出场动作的过渡组合。</summary>
    /// <typeparam name="TIn">入场动作类型。</typeparam>
    /// <typeparam name="TOut">出场动作类型。</typeparam>
    /// <remarks>
    /// 内部通过 StartGlobal 启动独立序列，需要播放模式下的全局宿主。
    /// 父动作暂停或取消不会控制已启动的子序列；取消不触发正常完成，子序列结束也不会自动完成父动作。
    /// </remarks>
    public class ScreenTransitionInOut<TIn, TOut> : AbstractAction<ScreenTransitionInOut<TIn, TOut>>
        where TIn : IAction
        where TOut : IAction
    {
        private Action<TIn> mOnSetIn;
        private Action<TOut> mOnSetOut;
        private TIn mIn;
        private TOut mOut;
        private Action mOnInFinish;

        /// <summary>从池分配组合，并保存传入的入场、出场动作。</summary>
        /// <param name="transitionIn">入场动作，由启动时另建的全局序列执行。</param>
        /// <param name="transitionOut">出场动作，在入场动作和 OnInFinish 回调之后执行。</param>
        /// <returns>尚未启动、已绑定两个子动作的过渡组合。</returns>
        public static ScreenTransitionInOut<TIn, TOut> Allocate(TIn transitionIn, TOut transitionOut)
        {
            var transitionInOut = Allocate();
            transitionInOut.mIn = transitionIn;
            transitionInOut.mOut = transitionOut;
            return transitionInOut;
        }

        /// <summary>设置入场完成、出场开始前调用的回调，并返回当前组合。</summary>
        public ScreenTransitionInOut<TIn, TOut> OnInFinish(Action onInFinish)
        {
            mOnInFinish = onInFinish;
            return this;
        }

        /// <summary>设置启动时配置入场动作的回调，并返回当前组合。</summary>
        public ScreenTransitionInOut<TIn, TOut> In(Action<TIn> onSetIn)
        {
            mOnSetIn = onSetIn;
            return this;
        }

        /// <summary>设置启动时配置出场动作的回调，并返回当前组合。</summary>
        public ScreenTransitionInOut<TIn, TOut> Out(Action<TOut> onSetOut)
        {
            mOnSetOut = onSetOut;
            return this;
        }

        /// <summary>依次应用两个配置回调，并在全局宿主上启动独立子序列。</summary>
        public override void OnStart()
        {
            mOnSetIn?.Invoke(mIn);
            mOnSetOut?.Invoke(mOut);
            ActionKit.Sequence().Append(mIn).Callback(() => mOnInFinish?.Invoke()).Append(mOut).StartGlobal();
        }
    }
}
