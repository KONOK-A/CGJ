using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// 单例触发器：任意场景卸载时注销容器内的所有句柄。
    /// 实际语义（与上游 QFramework 一致）：监听 <see cref="SceneManager.sceneUnloaded"/>，
    /// 任何场景卸载都会触发，包括 additive 加载的场景；不区分 active scene，也不区分句柄注册时所在场景。
    /// 实例常驻（DontDestroyOnLoad）并由 MonoSingletonProperty 管理；仅运行时可用。
    /// 同时承接显式 Scene.handle 的订阅；场景宿主迁移不会改变已捕获的场景所有权。
    /// </summary>
    public class OnCurSceneUnLoadUnRegisterTrigger : UnRegisterTriggerBase, ISingleton
    {
        private readonly Dictionary<int, SubscriptionSet<IUnRegister>> _sceneSubscriptions = new();
        private bool _cleaningScenes;

        /// <summary>托管到指定的已加载场景；无效或已卸载场景立即取消，不保留存活责任。</summary>
        public IUnRegister AddUnRegister(IUnRegister unRegister, Scene scene)
        {
            if (unRegister == null) return null;
            if (IsClosed || !scene.IsValid() || !scene.isLoaded)
            {
                if (IsClosed) return base.AddUnRegister(unRegister);
                try { unRegister.UnRegister(); }
                catch
                {
                    base.AddUnRegister(unRegister);
                    throw;
                }
                return unRegister;
            }
            if (!_sceneSubscriptions.TryGetValue(scene.handle, out var subscriptions))
                _sceneSubscriptions.Add(scene.handle, subscriptions = new SubscriptionSet<IUnRegister>());
            subscriptions.Add(unRegister);
            return unRegister;
        }

        /// <summary>仅解除该宿主对句柄的全部托管（AnyScene 及指定 Scene），不会取消订阅。</summary>
        public override void RemoveUnRegister(IUnRegister unRegister)
        {
            base.RemoveUnRegister(unRegister);
            foreach (var subscriptions in _sceneSubscriptions.Values) subscriptions.Remove(unRegister);
        }

        /// <summary>
        /// 全局唯一实例；Play 模式下首次访问时自动创建。
        /// </summary>
        public static OnCurSceneUnLoadUnRegisterTrigger Instance =>
            MonoSingletonProperty<OnCurSceneUnLoadUnRegisterTrigger>.Instance;

        /// <inheritdoc />
        public void OnSingletonInit() { }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            hideFlags = HideFlags.HideInHierarchy;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        protected override void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            base.OnDestroy();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            try { ReleaseSubscriptions(scene.handle); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        /// <summary>显式清理及宿主销毁涵盖 AnyScene 与所有指定 Scene 的责任。</summary>
        protected override void TriggerUnRegisters() => ReleaseSubscriptions(null);

        private void ReleaseSubscriptions(int? sceneHandle)
        {
            if (_cleaningScenes) return;
            _cleaningScenes = true;
            // 在任何取消回调前捕获本轮场景集合，重入新增场景留到下一轮。
            var snapshot = new Dictionary<int, List<IUnRegister>>();
            foreach (var pair in _sceneSubscriptions)
                if (!sceneHandle.HasValue || pair.Key == sceneHandle.Value)
                    snapshot.Add(pair.Key, new List<IUnRegister>(pair.Value.DetachAll()));
            List<Exception> errors = null;
            try
            {
                try { base.TriggerUnRegisters(); }
                catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                foreach (var pair in snapshot)
                {
                    try { SubscriptionCleanup.ReleaseAll(pair.Value, token => token.UnRegister()); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                    var subscriptions = _sceneSubscriptions[pair.Key];
                    foreach (var failure in pair.Value) subscriptions.Add(failure);
                    if (subscriptions.Count == 0) _sceneSubscriptions.Remove(pair.Key);
                }
            }
            finally { _cleaningScenes = false; }
            SubscriptionCleanup.ThrowErrors(errors);
        }
    }
}
