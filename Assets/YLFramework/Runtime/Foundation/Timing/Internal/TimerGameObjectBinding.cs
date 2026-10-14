using UnityEngine;

namespace YLFramework.Timing.Internal
{
    // 持有精确 run 身份；清理旧快照不能取消重新启用或复用后产生的新代次。
    internal sealed class TimerGameObjectBinding
    {
        private TimerHandle _handle;
        private TimerScheduler _scheduler;
        private GameObject _owner;
        private TimerLifetimeListener _listener;
        private readonly TimerGameObjectLifetime _lifetime;

        internal TimerGameObjectBinding(TimerHandle handle, GameObject owner,
            TimerGameObjectLifetime lifetime, TimerLifetimeListener listener)
        {
            _handle = handle;
            _scheduler = handle.Run.Owner;
            _owner = owner;
            _lifetime = lifetime;
            _listener = listener;
        }

        internal void OwnerEnded(bool destroyed)
        {
            if (_scheduler != null && (destroyed || _lifetime == TimerGameObjectLifetime.CancelOnDisable))
                _handle.Cancel();
        }

        internal void Detach()
        {
            if (_scheduler == null) return;
            _scheduler.RemoveBinding(_listener);
            // Remove 只操作托管集合；对象已 fake-null 时也必须释放旧监听责任。
            _listener.Remove(this);
            var run = _handle.Run;
            if (run != null && ReferenceEquals(run.Binding, this)) run.Binding = null;
            _handle = default;
            _scheduler = null;
            _owner = null;
            _listener = null;
        }
    }
}
