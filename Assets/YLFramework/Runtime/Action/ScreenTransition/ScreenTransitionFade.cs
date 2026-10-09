using UnityEngine;

namespace YLFramework.Actions
{
    /// <summary>通过共享画布遮罩调整透明度的屏幕过渡动作。</summary>
    /// <remarks>
    /// 请在 Unity 主线程执行，提供可用的画布及 ColorImage 绑定；无场景画布时按资源键 <c>ActionKit/ScreenTransitionCanvas</c> 加载。
    /// 所有过渡共用同一遮罩，不保证并行执行互不影响。取消不触发正常完成，也不会自动应用目标透明度。
    /// </remarks>
    public class ScreenTransitionFade : AbstractAction<ScreenTransitionFade>
    {
        private Color mColor = UnityEngine.Color.black;
        private float mFromAlpha = 0;
        private float mToAlpha = 0;
        private float mDuration = 1.0f;
        private float mCurrentSeconds = 0;

        /// <summary>设置遮罩颜色，并返回当前动作。</summary>
        public ScreenTransitionFade Color(Color color)
        {
            mColor = color;
            return this;
        }

        /// <summary>设置起始透明度，并返回当前动作。</summary>
        public ScreenTransitionFade FromAlpha(float alpha)
        {
            mFromAlpha = alpha;
            return this;
        }

        /// <summary>设置目标透明度，并返回当前动作。</summary>
        public ScreenTransitionFade ToAlpha(float alpha)
        {
            mToAlpha = alpha;
            return this;
        }

        /// <summary>设置过渡时长（秒），并返回当前动作。</summary>
        public ScreenTransitionFade Duration(float duration)
        {
            mDuration = duration;
            return this;
        }

        /// <summary>启动时应用遮罩颜色与起始透明度，并重置累计时间。</summary>
        public override void OnStart()
        {
            ScreenTransitionCanvas.Instance.ColorImage.color = mColor;
            SetAlpha(mFromAlpha);
            mCurrentSeconds = 0;
        }

        /// <summary>按时间增量推进透明度，到达时长时标记完成。</summary>
        /// <param name="dt">本次执行的时间增量，单位为秒。</param>
        public override void OnExecute(float dt)
        {
            mCurrentSeconds += dt;
            var alpha = Mathf.Lerp(mFromAlpha, mToAlpha, mCurrentSeconds / mDuration);
            SetAlpha(alpha);
            if (mCurrentSeconds >= mDuration) this.Finish();
        }

        /// <summary>正常完成时将遮罩透明度设为目标值。</summary>
        public override void OnFinish() => SetAlpha(mToAlpha);

        private static void SetAlpha(float alpha)
        {
            var image = ScreenTransitionCanvas.Instance.ColorImage;
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }
    }
}
