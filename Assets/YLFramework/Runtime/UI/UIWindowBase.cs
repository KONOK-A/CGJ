using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace YLFramework.Services.UI
{
    public class UIWindowBase : MonoBehaviour
    {
        internal bool UIEnable { get; private set; }
        public IWindowLoader MyWindowLoader { get; set; }
        public int CurrentLayer { get; protected set; }
        public Type UIWindowType => GetType();
        public UIWindowData MyWindowData { get; private set; }

        public virtual void OnInit(params object[] args)
        {

        }

        internal UIWindowBase WithWindowData(UIWindowData data)
        {
            MyWindowData = data;
            return this;
        }

        internal void ShowUI(int layer, params object[] args)
        {
            CurrentLayer = layer;
            //UI处于关闭状态，则注册UI事件
            if (!UIEnable)
            {
                RegisterUIEvent();
            }
            OnOpen(args);
            UIEnable = true;
        }

        internal void CloseUI()
        {
            UIEnable = false;
            UnRegisterUIEvent();
            OnClose();
        }


        /// <summary>
        /// 显示时调用
        /// </summary>
        protected virtual void OnOpen(params object[] args)
        {
        }

        /// <summary>
        /// 关闭时调用
        /// </summary>
        protected virtual void OnClose()
        {
        }
        /// <summary>
        /// 销毁该物体之前的操作，父类中为卸载资源操作
        /// </summary>
        protected virtual void BeforeDestroy()
        {
            //卸载资源
            MyWindowLoader.UnLoad();
            UIManager.UISystemConfig.WindowLoaderPool.RecycleLoader(MyWindowLoader);
            MyWindowLoader = null;
            MyWindowData = null;
        }

        protected virtual void OnDestroy()
        {
            if (Application.isPlaying)
            {
                BeforeDestroy();
            }

        }

        /// <summary>
        /// 注册UI事件
        /// </summary>
        protected virtual void RegisterUIEvent()
        {

        }

        /// <summary>
        /// 取消UI事件
        /// </summary>
        protected virtual void UnRegisterUIEvent()
        {
        }

        #region 本地化

        #endregion
    }
}