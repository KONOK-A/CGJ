using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using YLFramework.Timing;

namespace YLFramework.Core
{
    /// <summary>
    /// Inspector 计时组件，消费公共 Timing 服务；启用启动一轮，组件或 GameObject 停用取消本轮。
    /// onCompleted 仅在正常到期时调用，onAborted 仅在取消或 owner 失效时调用，故障不视为中断。
    /// </summary>
    public class TimerObject : MonoBehaviour
    {
        [Tooltip("延迟时间")]
        public float delay = 2;

        [Tooltip("忽略时间缩放")]
        public bool ignoreTimeScale = true;

        [Tooltip("事件回调")]
        [FormerlySerializedAs("Action")] public UnityEvent onCompleted;

        [Tooltip("中断回调")]
        [FormerlySerializedAs("Abort")] public UnityEvent onAborted;

        private ReusableTimer _timer;
        private TimerHandle _current;
        private System.Action _onAction;
        private System.Action<TimerCancellationReason> _onCancelled;
        private bool _isDestroyed;

        private void OnEnable()
        {
            if (_isDestroyed)
            {
                return;
            }

            _timer ??= new ReusableTimer();
            _onAction ??= OnAction;
            _onCancelled ??= OnCancelled;
            _current = _timer.Start(delay, _onAction,
                ignoreTimeScale ? TimerClock.Unscaled : TimerClock.Scaled, _onCancelled);
            _current.BindTo(gameObject);
        }

        private void OnAction()
        {
            onCompleted?.Invoke();
        }

        private void OnCancelled(TimerCancellationReason reason)
        {
            onAborted?.Invoke();
        }

        private void OnDisable() => _current.Cancel();

        private void OnDestroy()
        {
            _isDestroyed = true;
            _timer?.Dispose();
            _timer = null;
            _current = default;
            _onAction = null;
            _onCancelled = null;
        }

    }
}
