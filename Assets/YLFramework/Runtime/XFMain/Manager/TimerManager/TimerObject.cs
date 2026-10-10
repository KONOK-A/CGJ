using UnityEngine;
using UnityEngine.Events; 
using YLFramework.Timing;

namespace YLFramework.Core
{ 
    public class TimerObject : MonoBehaviour
    {
        // Fix编码
        [Tooltip("延迟时间")]
        public float delay = 2;

        [Tooltip("忽略时间缩放")]
        public bool ignoreTimeScale = true;

        [Tooltip("事件回调")]
        public UnityEvent Action;



        [Tooltip("中断回调")]
        public UnityEvent Abort;

        private ReusableTimer timer;
        private TimerHandle current;
        private System.Action onAction;
        private System.Action<TimerCancellationReason> onCancelled;
        private bool destroyed;

        private void OnEnable()
        {
            if (destroyed) return;
            timer ??= new ReusableTimer();
            onAction ??= OnAction;
            onCancelled ??= OnCancelled;
            current = timer.Start(delay, onAction,
                ignoreTimeScale ? TimerClock.Unscaled : TimerClock.Scaled, onCancelled);
            current.BindTo(gameObject);
        }

        

        private void OnAction()
        {
            Action?.Invoke();
        }


        private void OnCancelled(TimerCancellationReason reason)
        {
            Abort?.Invoke();
        }

        private void OnDisable() => current.Cancel();

        private void OnDestroy()
        {
            destroyed = true;
            timer?.Dispose();
            timer = null;
            current = default;
            onAction = null;
            onCancelled = null;
        }

    } 
}
