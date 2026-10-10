using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YLFramework;
using YLFramework.Core.Singleton;
using YLFramework.Events;
using YLFramework.Foundation;

namespace YLFramework.Actions
{
    // 只承载模块全局消息、协程和 StartGlobal；与调用方对象的生命周期无关。
    internal sealed class ActionGlobalHost : MonoSingletonHost<ActionGlobalHost>
    {
        protected override Transform HostParent => YLFrameworkRoot.Instance.GetModuleRoot("Action");
        // 由协程动作登记的取消回调；宿主释放时统一调用，确保协程先于宿主被停止和清理。
        private readonly HashSet<Action> mCoroutineCancellations = new HashSet<Action>();

        private IUnRegister mUpdateToken;
        private IUnRegister mFixedUpdateToken;
        private IUnRegister mLateUpdateToken;
        private object mUpdateRound;
        private object mExecutionSession;
        private bool mQuitting;

        // owned 消息保持 Action 自己的上下文和 fail-fast；三相位只共享公共更新源。
        internal readonly EasyEvent OnUpdateEvent = new EasyEvent();
        internal readonly EasyEvent OnFixedUpdateEvent = new EasyEvent();
        internal readonly EasyEvent OnLateUpdateEvent = new EasyEvent();
        internal readonly EasyEvent OnGUIEvent = new EasyEvent();
        internal readonly EasyEvent OnApplicationQuitEvent = new EasyEvent();
        internal readonly EasyEvent<bool> OnApplicationFocusEvent = new EasyEvent<bool>();
        internal readonly EasyEvent<bool> OnApplicationPauseEvent = new EasyEvent<bool>();

        // 测试与会话清理入口：只清空单例缓存引用，不销毁现有宿主对象（由 ReleaseAll 负责）。
        internal static void ResetSession() => ResetHostReference();

        // 优先复用现有宿主；允许创建时先确保 ActionKit 已初始化，不能创建时返回 null，由调用方决定后续处理。
        internal new static ActionGlobalHost Instance
        {
            get
            {
                if (ActionKit.IsShuttingDown) return null;
                if (!HasInstance && (!CanCreateHost() || !SingletonSessionState.CanRunExecution)) return null;
                ActionKit.Initialize();
                return GetInstance();
            }
        }

        public override void OnSingletonInit()
        {
            mExecutionSession = SingletonSessionState.ExecutionSession;
            RefreshUpdateBridge();
        }

        private void OnEnable() => RefreshUpdateBridge();
        private void OnDisable() => DisconnectUpdateBridge();

        // 初始化补丁可以重入取得宿主；只有模块成功提交后才允许连接。
        internal void RefreshUpdateBridge()
        {
            if (mUpdateRound != null || !CanForwardUpdate()) return;
            var round = mUpdateRound = new object();
            try
            {
                mUpdateToken = UpdateManager.AddUpdateEvent(() => ForwardUpdate(OnUpdateEvent, round));
                mFixedUpdateToken = UpdateManager.AddFixedUpdateEvent(() => ForwardUpdate(OnFixedUpdateEvent, round));
                mLateUpdateToken = UpdateManager.AddLateUpdateEvent(() => ForwardUpdate(OnLateUpdateEvent, round));
            }
            catch
            {
                DisconnectUpdateBridge();
                throw;
            }
        }

        private bool CanForwardUpdate() => this && !HostReleased && IsCurrentHost && isActiveAndEnabled &&
            !mQuitting && ActionKit.Initialized && !ActionKit.IsShuttingDown &&
            SingletonSessionState.CanRunExecution &&
            ReferenceEquals(mExecutionSession, SingletonSessionState.ExecutionSession);

        private void ForwardUpdate(EasyEvent message, object round)
        {
            if (!ReferenceEquals(mUpdateRound, round) || !CanForwardUpdate() || !message.HasListener) return;
            message.Trigger();
        }

        private void DisconnectUpdateBridge()
        {
            // token 注销保留公共在途快照，先失效轮次；只释放本宿主的三个身份。
            mUpdateRound = null;
            var update = mUpdateToken;
            var fixedUpdate = mFixedUpdateToken;
            var lateUpdate = mLateUpdateToken;
            mUpdateToken = mFixedUpdateToken = mLateUpdateToken = null;
            try { update?.UnRegister(); }
            finally
            {
                try { fixedUpdate?.UnRegister(); }
                finally { lateUpdate?.UnRegister(); }
            }
        }

        private void OnGUI() => OnGUIEvent.Trigger();
        private void OnApplicationFocus(bool hasFocus) => OnApplicationFocusEvent.Trigger(hasFocus);
        private void OnApplicationPause(bool pauseStatus) => OnApplicationPauseEvent.Trigger(pauseStatus);

        protected override void OnApplicationQuit()
        {
            if (HostReleased || !IsCurrentHost || mQuitting ||
                !ReferenceEquals(mExecutionSession, SingletonSessionState.ExecutionSession)) return;
            mQuitting = true;
            SingletonSessionState.MarkApplicationQuitting();
            DisconnectUpdateBridge();
            try { OnApplicationQuitEvent.Trigger(); }
            finally { base.OnApplicationQuit(); }
        }

        public void ExecuteCoroutine(IEnumerator coroutine, Action onFinish)
        {
            StartActionCoroutine(coroutine, onFinish);
        }

        internal Coroutine StartActionCoroutine(IEnumerator coroutine, Action onFinish)
        {
            if (HostReleased || ActionKit.IsShuttingDown) throw new InvalidOperationException("ActionKit coroutine host is shutting down.");
            return StartCoroutine(DoExecuteCoroutine(coroutine, onFinish));
        }

        // 协程动作启动时登记取消回调，正常结束或取消时反登记；宿主释放时统一调用剩余登记项。
        internal void RegisterCoroutineCancellation(Action cancellation) => mCoroutineCancellations.Add(cancellation);
        internal void UnRegisterCoroutineCancellation(Action cancellation) => mCoroutineCancellations.Remove(cancellation);

        // 先快照再清空集合，避免取消回调重入登记或反登记改变正在遍历的集合。
        private void CancelOwnedCoroutineActions()
        {
            var cancellations = new List<Action>(mCoroutineCancellations);
            mCoroutineCancellations.Clear();
            List<Exception> errors = null;
            foreach (var cancellation in cancellations)
            {
                try { cancellation(); }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
            }
            if (errors != null) throw new AggregateException("ActionKit coroutine cancellation failed.", errors);
        }

        // 包装内层协程：内层正常跑完才触发完成回调；宿主销毁时 StopAllCoroutines 会直接中断它，
        // 此时完成回调不再执行，由 OnHostRelease 中的取消登记负责释放对应动作。
        private IEnumerator DoExecuteCoroutine(IEnumerator coroutine, Action onFinish)
        {
            yield return coroutine;
            onFinish?.Invoke();
        }

        protected override void OnHostRelease()
        {
            try
            {
                DisconnectUpdateBridge();
                CancelOwnedCoroutineActions();
            }
            finally
            {
                // 取消动作之后再清事件、停协程，避免动作清理期间仍收到宿主消息或继续被协程驱动。
                ClearEvents();
                StopAllCoroutines();
            }
        }

        // 宿主结束须终结消息代次，避免旧快照在 Shutdown/重建后继续进入业务。
        private void ClearEvents()
        {
            if (!OnUpdateEvent.IsDisposed) OnUpdateEvent.ResetOwner();
            if (!OnFixedUpdateEvent.IsDisposed) OnFixedUpdateEvent.ResetOwner();
            if (!OnLateUpdateEvent.IsDisposed) OnLateUpdateEvent.ResetOwner();
            if (!OnGUIEvent.IsDisposed) OnGUIEvent.ResetOwner();
            if (!OnApplicationQuitEvent.IsDisposed) OnApplicationQuitEvent.ResetOwner();
            if (!OnApplicationFocusEvent.IsDisposed) OnApplicationFocusEvent.ResetOwner();
            if (!OnApplicationPauseEvent.IsDisposed) OnApplicationPauseEvent.ResetOwner();
        }

        // 模块级释放：释放所有全局宿主实例（含场景中被收养的），清空单例缓存，并聚合各宿主的清理异常。
        internal static void ReleaseAll()
        {
            List<Exception> errors = null;
            foreach (var host in FindObjectsOfType<ActionGlobalHost>(true))
            {
                try { host.ReleaseHost(); }
                catch (Exception exception)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(exception);
                }
            }
            ResetHostReference();
            if (errors != null) throw new AggregateException("ActionKit global host cleanup failed.", errors);
        }
    }
}
