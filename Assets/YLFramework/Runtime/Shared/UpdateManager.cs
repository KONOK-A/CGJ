using System;
using UnityEngine;
using YLFramework.Core.Singleton;
using YLFramework.Events;

namespace YLFramework.Foundation
{
    /// <summary>
    /// 公共 Unity 三相位消息桥。仅主线程使用，有效 Play 注册惰性创建独立 DDOL 宿主。
    /// 各相位按注册顺序通知，异常继续并统一报告；不承诺与其他 MonoBehaviour 的执行全序。
    /// </summary>
    /// <remarks>
    /// 每次注册均为独立身份。普通退订保留在途快照，领域结束仍需自己的存活/轮次检查。
    /// 空闲宿主保留到会话结束；外部销毁会结束全部旧订阅，后续新注册不会恢复旧工作。
    /// 不应由业务停用或销毁宿主，不提供公共 Tick、Clear 或 Shutdown。
    /// </remarks>
    public sealed class UpdateManager : MonoBehaviour
    {
        private static UpdateManager _instance;
        private readonly EventChannel _update = new();
        private readonly EventChannel _fixedUpdate = new();
        private readonly EventChannel _lateUpdate = new();
        private object _session;
        private bool _released;

        static UpdateManager() => SingletonSessionState.RegisterSessionReset(ResetSession);
        private enum Phase { Update, FixedUpdate, LateUpdate }
        private bool IsCurrent => !_released && ReferenceEquals(_instance, this) &&
            ReferenceEquals(_session, SingletonSessionState.ExecutionSession);
        private EventChannel Channel(Phase phase) => phase == Phase.Update ? _update :
            phase == Phase.FixedUpdate ? _fixedUpdate : _lateUpdate;

        /// <summary>注册 Update，无效输入或退出期间返回空句柄，不创建宿主。</summary>
        public static IUnRegister AddUpdateEvent(Action action) => Register(action, Phase.Update);
        /// <summary>注册 FixedUpdate，每次调用返回独立的精确注销身份。</summary>
        public static IUnRegister AddFixedUpdateEvent(Action action) => Register(action, Phase.FixedUpdate);
        /// <summary>注册 LateUpdate，保持相位内注册顺序。</summary>
        public static IUnRegister AddLateUpdateEvent(Action action) => Register(action, Phase.LateUpdate);
        /// <summary>移除最后相等的普通委托子序列；仅访问既有宿主。</summary>
        public static void RemoveUpdateEvent(Action action) => UnRegister(action, Phase.Update);
        /// <summary>移除最后相等的 FixedUpdate 委托子序列，不创建宿主。</summary>
        public static void RemoveFixedUpdateEvent(Action action) => UnRegister(action, Phase.FixedUpdate);
        /// <summary>移除最后相等的 LateUpdate 委托子序列，不创建宿主。</summary>
        public static void RemoveLateUpdateEvent(Action action) => UnRegister(action, Phase.LateUpdate);

        private static IUnRegister Register(Action action, Phase phase)
        {
            if (action == null || !SingletonSessionState.CanRunExecution) return EmptyUnRegister.Instance;
            var host = _instance;
            var created = !host;
            if (created) host = CreateHost();
            try { return host.Channel(phase).Register(action); }
            catch
            {
                if (created)
                {
                    host.ReleaseOwner();
                    Destroy(host.gameObject);
                }
                throw;
            }
        }

        private static UpdateManager CreateHost()
        {
            GameObject obj = null;
            UpdateManager host = null;
            try
            {
                obj = new GameObject("YLFramework.Foundation.UpdateManager");
                obj.SetActive(false);
                host = obj.AddComponent<UpdateManager>();
                host._session = SingletonSessionState.ExecutionSession;
                DontDestroyOnLoad(obj);
                _instance = host;
                obj.SetActive(true);
                return host;
            }
            catch
            {
                if (host) host.ReleaseOwner();
                if (obj) Destroy(obj);
                throw;
            }
        }

        private static void UnRegister(Action action, Phase phase)
        {
            var host = _instance;
            if (action != null && host && host.IsCurrent) host.Channel(phase).UnRegister(action);
        }

        private void Update() => Dispatch(_update);
        private void FixedUpdate() => Dispatch(_fixedUpdate);
        private void LateUpdate() => Dispatch(_lateUpdate);
        private void Dispatch(EventChannel channel)
        {
            if (!IsCurrent || !SingletonSessionState.CanRunExecution || !channel.HasListener) return;
            try { channel.TriggerAll(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void ReleaseOwner()
        {
            if (_released) return;
            _released = true;
            _update.Dispose();
            _fixedUpdate.Dispose();
            _lateUpdate.Dispose();
            if (ReferenceEquals(_instance, this)) _instance = null;
        }

        private static void ResetSession()
        {
            var old = _instance;
            _instance = null;
            if (!old) return;
            old.ReleaseOwner();
            old.enabled = false;
            Destroy(old.gameObject);
        }

        private void OnApplicationQuit()
        {
            // 迟到的旧宿主消息不能关闭新会话的门禁。
            if (!IsCurrent) return;
            SingletonSessionState.StopExecution();
            ReleaseOwner();
            enabled = false;
            Destroy(gameObject);
        }
        private void OnDestroy() => ReleaseOwner();
    }
}
