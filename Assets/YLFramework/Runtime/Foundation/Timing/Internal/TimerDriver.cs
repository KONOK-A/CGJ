using System;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Timing.Internal
{
    // 唯一默认运行宿主；只提供渲染样本和 owner 生命周期，不广播 Update 或执行 Action。
    internal sealed class TimerDriver : MonoBehaviour
    {
        private static TimerDriver _instance;
        private static bool _isShuttingDown;
        private TimerScheduler _scheduler;
        private object _session;
        private bool _isReleased;
        private bool _isReady;
        private int _unityFrame;
        private long _frame;
        private long _secondsFrame;
        private double _scaled;
        private double _unscaled;

        static TimerDriver() => SingletonSessionState.RegisterSessionReset(ResetSession);
        private bool IsCurrent => !_isReleased && ReferenceEquals(_instance, this) &&
            ReferenceEquals(_session, SingletonSessionState.ExecutionSession);

        internal static TimerScheduler GetScheduler()
        {
            if (_isShuttingDown || !SingletonSessionState.CanRunExecution)
            {
                throw new InvalidOperationException("Timer execution is not available.");
            }

            var host = _instance;
            if (!ReferenceEquals(host, null))
            {
                if (!host)
                {
                    _instance = null;
                    host.ReleaseOwner();
                }
                else if (!host.IsCurrent || !host.isActiveAndEnabled)
                {
                    throw new InvalidOperationException("Timer driver is disabled or released; shut it down before restarting.");
                }
            }
            if (!_instance)
            {
                CreateHost();
            }

            return _instance._scheduler;
        }

        private static void CreateHost()
        {
            GameObject obj = null;
            TimerDriver host = null;
            try
            {
                obj = new GameObject("YLFramework.Timing.TimerDriver");
                obj.SetActive(false);
                host = obj.AddComponent<TimerDriver>();
                host._session = SingletonSessionState.ExecutionSession;
                host._scheduler = new TimerScheduler(host.Synchronize);
                DontDestroyOnLoad(obj);
                _instance = host;
                obj.SetActive(true);
            }
            catch
            {
                if (host)
                {
                    host.ReleaseOwner();
                }

                if (ReferenceEquals(_instance, host))
                {
                    _instance = null;
                }

                if (obj)
                {
                    Destroy(obj);
                }

                throw;
            }
        }

        private void Synchronize() => Synchronize(false);
        private void Synchronize(bool update)
        {
            if (!IsCurrent || !SingletonSessionState.CanRunExecution || !this || !isActiveAndEnabled)
            {
                ReleaseOwner();
                return;
            }
            // 首个 Update 之前不读取 Awake 中未定义的时间/帧；Fixed 步只更新已知帧身份。
            if (!_isReady && !update)
            {
                return;
            }

            var unityFrame = Time.frameCount;
            var newFrame = !_isReady || unityFrame != _unityFrame;
            if (newFrame)
            {
                _frame = checked(_frame + 1);
                _unityFrame = unityFrame;
            }
            var validSeconds = update || !Time.inFixedTimeStep;
            if (validSeconds && _secondsFrame != _frame)
            {
                _scaled = Time.timeAsDouble;
                _unscaled = Time.unscaledTimeAsDouble;
                _secondsFrame = _frame;
            }
            _isReady = true;
            _scheduler.Sample(_scaled, _unscaled, _frame, validSeconds);
        }

        private void Update()
        {
            Synchronize(true);
            if (_isReleased)
            {
                return;
            }

            _scheduler.AdvanceAutomatic();
        }

        private void ReleaseOwner()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            _scheduler?.Dispose();
        }

        internal static void Shutdown()
        {
            if (_isShuttingDown)
            {
                return;
            }

            _isShuttingDown = true;
            try
            {
                var old = _instance;
                _instance = null;
                if (ReferenceEquals(old, null))
                {
                    return;
                }

                old.ReleaseOwner();
                if (old)
                {
                    old.enabled = false;
                    Destroy(old.gameObject);
                }
            }
            finally
            {
                _isShuttingDown = false;
            }
        }

        private static void ResetSession() => Shutdown();
        private void OnDisable() => ReleaseOwner();
        private void OnDestroy()
        {
            ReleaseOwner();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
        private void OnApplicationQuit()
        {
            if (!IsCurrent)
            {
                return;
            }

            SingletonSessionState.StopExecution();
            ReleaseOwner();
        }
    }
}
