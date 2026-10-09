// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/View.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using YLFramework.Core.Extensions;
using System;
using System.Collections.Generic; 
using UnityEngine;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using YLFramework.Core.RoslynAnalyzers;

namespace YLFramework.Core
{
    [DisallowMultipleComponent]
    public abstract class View : MonoBehaviour
    {

        #region 字段

        internal bool loaded = false;

        private Module module;
        internal object Session;
        internal object LastSession;
        internal int RegisteredId;
        internal bool IsInitializing;
        internal bool IsRemoving;
        internal bool UsesBackend;
        internal GameObject BackendInstance;
        internal View BackendRoot;
        internal object BackendRootSession;
        internal int SceneHandle;
        internal bool IsAttaching;
        internal bool DiscardOnExit;
        private Model boundModel;
        private Module modelOwner;
        private object modelRegistration;
        internal bool IsUnbinding;
        private bool eventsRegistered;
        private long eventEpoch;

        #endregion

        #region 属性

        /// <summary>
        /// 唯一标识(只读)
        /// </summary>
        public int Id => Session != null ? RegisteredId : GetInstanceID();

        /// <summary>
        /// 当前View所在的模块
        /// </summary>
        public Module Module
        {
            get
            {
                return module;
            }
            internal set
            {
                module = value;
            }
        }


        /// <summary>
        /// 预制体名称
        /// </summary>
        internal string AssetName { get; set; }

        /// <summary>
        /// 当前有效的主要Model绑定；解绑或Model注册身份失效后为null，不取得Model所有权。
        /// </summary>
        public Model Model => boundModel != null &&
            ReferenceEquals(boundModel.Module, modelOwner) &&
            ReferenceEquals(boundModel.Registration, modelRegistration) &&
            !boundModel.IsInitializing && !boundModel.IsExiting &&
            ReferenceEquals(ModelManager.GetModel(modelOwner, boundModel.GetType(), boundModel.Id), boundModel)
            ? boundModel : null;

        internal bool HasValidModelBinding => boundModel == null || Model != null;

        /// <summary>当前表现会话已完成加载；退出期间为false。</summary>
        public bool IsLoaded => loaded;

        /// <summary>绑定已完成注册的Model，可以显式借用其他Module的数据，不自动注册或移除。</summary>
        protected void BindModel(Model model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (Session == null || (!loaded && !IsInitializing) || IsRemoving || IsUnbinding)
                throw new InvalidOperationException("Cannot bind a Model outside an active View session.");
            if (model.Module == null || model.Registration == null || model.IsInitializing || model.IsExiting ||
                !ReferenceEquals(ModelManager.GetModel(model.Module, model.GetType(), model.Id), model))
                throw new InvalidOperationException("Model must have a completed registration.");
            if (boundModel != null)
            {
                if (ReferenceEquals(Model, model)) return;
                throw new InvalidOperationException("Unbind the previous Model before binding another registration.");
            }
            boundModel = model;
            modelOwner = model.Module;
            modelRegistration = model.Registration;
        }

        /// <summary>先结束框架绑定，再尝试业务解绑；不改变Model生命周期。</summary>
        protected void UnbindModel()
        {
            if (IsUnbinding || boundModel == null) return;
            var previous = boundModel;
            var owner = Module;
            boundModel = null;
            modelOwner = null;
            modelRegistration = null;
            IsUnbinding = true;
            if (owner != null) owner.viewTransactions++;
            try { OnModelUnbound(previous); }
            catch
            {
                DiscardOnExit = true;
                throw;
            }
            finally
            {
                IsUnbinding = false;
                if (owner != null) owner.viewTransactions--;
            }
        }

        /// <summary>清理业务自己的Model监听；previousModel可能已退出。</summary>
        protected virtual void OnModelUnbound(Model previousModel) { }

        #endregion

        #region 事件

        [Tooltip("View被移除时触发")]
        public Action onViewRemove;

        #endregion

        #region 生命周期

        [MustCallBase("在重写Start方法时,请通过 base.Start() 调用父类中的Start方法!")]
        protected virtual void Start()
        {
            // 场景及动态新增对象由Module显式挂载，Start不推断业务归属。
        }

        private void OnDestroy()
        {
            if (!ApplicationTool.isPlaying) return;
            // 同步初始化事务持有取得实例，并在每个提交边界检查存活及完成回滚。
            if (IsInitializing || IsAttaching) return;

            if (Module != null && Session != null)
            {
                // 移除View
                ViewManager.RemoveView(Module, this, Session, true);
            }
        }


        private void OnEnable()
        {
            RegisterEvents();
        }

        private void OnDisable()
        {
            UnRegisterEvents();
        }

        #endregion

        #region 方法

        /// <summary>Loaded且active时调用；首次激活可能早于加载，业务上下文应在此处使用。</summary>
        protected virtual void OnViewEnabled() { }

        /// <summary>暂停已开放的表现事件，不结束owner或Model绑定；与OnViewEnabled配对。</summary>
        protected virtual void OnViewDisabled() { }

        /// <summary>
        /// 结束当前表现会话，不移除Model；框架取得的健康实例归还缓存，其他实例仅隐藏。
        /// </summary>
        public void Close()
        {
            if (Session == null || Module == null) return;
            ViewManager.RemoveView(Module, this, Session);
        }

        /// <summary>
        /// 会话和实例收尾已提交后调用，可开启新会话；若移除广播已复用该组件，旧完成钩子不再派发。
        /// </summary>
        protected virtual void OnClosed() { }
        internal void NotifyClosed() => OnClosed();
        internal void DetachModel() => UnbindModel();
        internal void StopEvents() => UnRegisterEvents();
        internal void ResumeEvents() => RegisterEvents();


        /// <summary>
        /// 加载完成的回调(可以在当前方法中做一些数据更新的逻辑)
        /// </summary>
        /// <param name="param">加载View时传递的参数</param>
        public virtual void OnLoaded(params object[] param)
        {
        }


        /// <summary>仅在本回调期间借读参数；不得保存容器、修改或再次传入加载入口。</summary>
        public virtual void OnLoaded(LoadViewArgs args)
        {
        }

        /// <summary>
        /// View被回收 或 被移除时调用
        /// </summary>
        public virtual void OnRemove() {

        }


        [Obsolete("已过时,请使用该方法的重载函数代替!", true)]
        protected void RegisterEvent(string eventName, Action<object[]> callback)
        {
        }

        [Obsolete("已过时,请使用该方法的重载函数代替!", true)]
        protected void RegisterEvent(int event_name_hash, Action<object[]> callback)
        {
        }

        internal virtual void BeforeOnLoaded() { }

        #endregion


        #region 事件注册

        private Dictionary<int, Delegate> _events = new Dictionary<int, Delegate>();

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent(string event_name, XFEvent callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent(int event_name_hash,XFEvent callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1>(string event_name, XFEvent<T1> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1>(int event_name_hash, XFEvent<T1> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2>(string event_name, XFEvent<T1, T2> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1,T2>(int event_name_hash, XFEvent<T1,T2> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3>(string event_name, XFEvent<T1, T2, T3> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2,T3>(int event_name_hash, XFEvent<T1, T2,T3> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4>(string event_name, XFEvent<T1, T2, T3, T4> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3,T4>(int event_name_hash, XFEvent<T1, T2, T3,T4> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4, T5>(string event_name, XFEvent<T1, T2, T3, T4, T5> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4,T5>(int event_name_hash, XFEvent<T1, T2, T3, T4,T5> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4, T5, T6>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4, T5,T6>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5,T6> callback)
        {
            AddEvent(event_name_hash, callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            RegisterEvent(EventManager.StringToHash(event_name), callback);
        }

        /// <summary>
        /// 注册事件,通过该方法注册的事件会在 View.OnDisable 时自动移除!
        /// </summary> 
        [RestrictedCall("Awake")]
        protected void RegisterEvent<T1, T2, T3, T4, T5, T6,T7>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5, T6,T7> callback)
        {
            AddEvent(event_name_hash, callback);
        }
        
        private void AddEvent(int event_name_hash,Delegate callback)
        {

            if (callback == null) return;

            if (!_events.ContainsKey(event_name_hash))
                _events.Add(event_name_hash, null);


            if (_events[event_name_hash] != null)
            {
                Type origin_type = _events[event_name_hash].GetType();
                Type current_type = callback.GetType();

                if (origin_type == current_type)
                {
                    _events[event_name_hash] = Delegate.Combine(_events[event_name_hash], callback);
                }
                else
                {
                    Exception e = new Exception(string.Format("注册事件:{0}失败! 委托类型不一致! \n\t注册类型:{1}\n\t当前类型:{2}\n同一个事件的回调方法类型必须保持一致!", EventManager.HashToString(event_name_hash), current_type.GetDelegateDisplayName(), origin_type.GetDelegateDisplayName()));
                    Debug.LogException(e);
                }
            }
            else
            {
                _events[event_name_hash] = Delegate.Combine(_events[event_name_hash], callback);
            }


            //try
            //{
            //    _events[event_name_hash] = Delegate.Combine(_events[event_name_hash], callback);
            //}
            //catch (ArgumentException)
            //{
            //    Exception e = new Exception(string.Format("注册事件:{0} 类型:{1} {2} 不一致,同一个事件的回调方法类型必须保持一致!", EventManager.HashToString(event_name_hash), callback.GetType().FullName, _events[event_name_hash].GetType().FullName));
            //    Debug.LogException(e);
            //}
            //catch (Exception e)
            //{
            //    Debug.LogException(e);
            //}
        }


        private void RegisterEvents() 
        {
            if (!loaded || IsRemoving || !isActiveAndEnabled || eventsRegistered) return;
            var session = Session;
            long epoch = ++eventEpoch;
            eventsRegistered = true;
            try
            {
                foreach (var entry in _events)
                    EventManager.AddEvent(entry.Key, entry.Value);
                PanelManager.ActivateView(this);
                if (!ReferenceEquals(Session, session) || eventEpoch != epoch || !loaded || IsRemoving || !isActiveAndEnabled) return;
                OnViewEnabled();
            }
            catch (Exception error)
            {
                // 钩子可结束旧会话或重开订阅轮次，旧异常不能退订或污染新的监听。
                if (ReferenceEquals(Session, session) && eventEpoch == epoch)
                {
                    DiscardOnExit = true;
                    try { UnRegisterEvents(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
        }


        private void UnRegisterEvents()
        {
            if (!eventsRegistered) return;
            var session = Session;
            long epoch = ++eventEpoch;
            eventsRegistered = false;
            var errors = new List<Exception>();
            foreach (var entry in _events)
            {
                try { EventManager.RemoveEvent(entry.Key, entry.Value); }
                catch (Exception error) { errors.Add(error); }
            }
            try { PanelManager.DeactivateView(this); }
            catch (Exception error) { errors.Add(error); }
            try { if (ReferenceEquals(Session, session) && eventEpoch == epoch) OnViewDisabled(); }
            catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0 && ReferenceEquals(Session, session) && eventEpoch == epoch) DiscardOnExit = true;
            ViewManager.ThrowCleanupErrors(errors);
        }

        #endregion


    }
}
