// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/View.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using YLFramework.Core.Extensions;
using System;
using System.Collections.Generic; 
using UnityEngine;
using GameObjectLoader = XFABManager.GameObjectLoader;
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

        #endregion

        #region 属性

        /// <summary>
        /// 唯一标识(只读)
        /// </summary>
        public int Id => GetInstanceID();

        /// <summary>
        /// 当前View所在的模块
        /// </summary>
        public Module Module
        {
            get
            {
                if (loaded && module == null)
                    module = ModuleManager.GetCurrentModule();

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
        /// 当前View对应的数据；索引内View移除时，仅尝试移除所属Module拥有且精确匹配的Model。
        /// 显式借用其他Module的Model不改变owner，也不会随当前View退出。
        /// </summary>
        public virtual Model Model { get; }

        #endregion

        #region 事件

        [Tooltip("View被移除时触发")]
        public Action onViewRemove;

        #endregion

        #region 生命周期

        [MustCallBase("在重写Start方法时,请通过 base.Start() 调用父类中的Start方法!")]
        protected virtual void Start()
        {
            LoadOnStart();
        }

        protected virtual void OnDestroy()
        {
            if (!ApplicationTool.isPlaying) return;

            if (Module != null && loaded)
            {
                // 移除View
                ViewManager.RemoveView(Module, this);
            }
        }


        [MustCallBase("在重写OnEnable方法时,请通过 base.OnEnable() 调用父类中的OnEnable方法!")]
        protected virtual void OnEnable()
        {
            RegisterEvents();
        }

        [MustCallBase("在重写OnDisable方法时,请通过 base.OnDisable() 调用父类中的OnDisable方法!")]
        protected virtual void OnDisable()
        {
            UnRegisterEvents();
        }

        #endregion

        #region 方法

        /// <summary>
        /// 关闭View(这里的关闭并不是销毁,而是放到缓存池中,所以不用担心销毁和创建的开销)
        /// </summary>
        public virtual void Close()
        {
            if (this.IsDestroy()) return;
            if (gameObject.IsDestroy()) return;
            // 如果是false 说明已经关闭,直接return 就可以了
            if (!loaded) return;
            loaded = false;
            if (Module != null)
            {
                // 移除View
                ViewManager.RemoveView(Module, this);
                //// 移除数据 
                //ModelManager.RemoveModel(Module, model);
            }

            GameObjectLoader.UnLoad(gameObject);
        }


        /// <summary>
        /// 加载完成的回调(可以在当前方法中做一些数据更新的逻辑)
        /// </summary>
        /// <param name="param">加载View时传递的参数</param>
        public virtual void OnLoaded(params object[] param)
        {
        }


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

        private void LoadOnStart()
        {
            if (Module == null)
            {
                if (transform.parent != null)
                {
                    // 尝试去父节点中查找
                    View view = transform.parent.GetComponentInParent<View>();
                    if (view != null)
                    {
                        Module = view.Module;
                    }
                    else
                    {
                        Module = ModuleManager.GetCurrentModule();
                    }
                }
                else
                {
                    Module = ModuleManager.GetCurrentModule();
                }
            }

            if (loaded == false)
            {
                BeforeOnLoaded();
                loaded = true;
                OnLoaded();
                LoadViewArgs loadViewArgs = LoadViewArgs.New();
                OnLoaded(loadViewArgs);
                LoadViewArgs.Recycle(loadViewArgs);
            }
        }

        internal void Remove() {
            onViewRemove?.Invoke();
            OnRemove();
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
            foreach (var event_name_hash in _events.Keys)
            {
                EventManager.AddEvent(event_name_hash, _events[event_name_hash]);
            }
        }


        private void UnRegisterEvents()
        {
            foreach (var event_name_hash in _events.Keys)
            {
                EventManager.RemoveEvent(event_name_hash, _events[event_name_hash]);
            }
        }

        #endregion


    }
}
