using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YLFramework;
using YLFramework.Core.Singleton;
using YLFramework.Events;

namespace YLFramework.Actions
{
    // 只承载模块全局消息、协程和 StartGlobal；与调用方对象的生命周期无关。
    internal sealed class ActionGlobalHost : MonoSingletonHost<ActionGlobalHost>
    {
        protected override Transform HostParent => YLFrameworkRoot.Instance.GetModuleRoot("Action");
        // 由协程动作登记的取消回调；宿主释放时统一调用，确保协程先于宿主被停止和清理。
        private readonly HashSet<Action> mCoroutineCancellations = new HashSet<Action>();

        // 宿主消息事件：由下方各 Unity 消息方法触发；订阅方应按自身生命周期解绑，宿主释放时会清空全部订阅。
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
                if (HasInstance) return GetInstance();
                if (!CanCreateHost()) return null;
                ActionKit.Initialize();
                return GetInstance();
            }
        }

        private void Update() => OnUpdateEvent.Trigger();
        private void FixedUpdate() => OnFixedUpdateEvent.Trigger();
        private void LateUpdate() => OnLateUpdateEvent.Trigger();
        private void OnGUI() => OnGUIEvent.Trigger();
        private void OnApplicationFocus(bool hasFocus) => OnApplicationFocusEvent.Trigger(hasFocus);
        private void OnApplicationPause(bool pauseStatus) => OnApplicationPauseEvent.Trigger(pauseStatus);

        protected override void OnApplicationQuit()
        {
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
            try { CancelOwnedCoroutineActions(); }
            finally
            {
                // 取消动作之后再清事件、停协程，避免动作清理期间仍收到宿主消息或继续被协程驱动。
                ClearEvents();
                StopAllCoroutines();
            }
        }

        // 清空全部消息订阅，防止已释放宿主继续持有外部回调。
        private void ClearEvents()
        {
            OnUpdateEvent.UnRegisterAll();
            OnFixedUpdateEvent.UnRegisterAll();
            OnLateUpdateEvent.UnRegisterAll();
            OnGUIEvent.UnRegisterAll();
            OnApplicationQuitEvent.UnRegisterAll();
            OnApplicationFocusEvent.UnRegisterAll();
            OnApplicationPauseEvent.UnRegisterAll();
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
