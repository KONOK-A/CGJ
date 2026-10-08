// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/Panel.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
 
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{


    public enum PanelType
    {
        /// <summary>
        /// 普通的UI界面 ( 不透明，占满全屏 ) 当前显示的如果是这种类型的界面, 框架会自动把后面的移出显示区,以此来节省性能
        /// </summary>
        Panel = 1,
        /// <summary>
        /// 弹窗界面 ( 可能会看到后面的界面 ) 默认是这种类型
        /// </summary>
        Window = 2,

        /// <summary>
        /// 悬浮控件(提示或按钮),Panel.IsTop 的方法会忽略该类型的UI,因为悬浮控件不是一个完整的UI界面! 
        /// </summary>
        Overlay = 3
    }

    public abstract class Panel : View
    {

        #region 字段

        private int sortingOrder;


        private bool isInitCanvas = false;

        #endregion


        #region 属性
        /// <summary>
        /// 当前Panel的Canvas
        /// </summary>
        public Canvas Canvas { get; private set; }
         
        /// <summary>
        /// 当前Panel的层级
        /// </summary>
        public int SortingOrder
        {
            get { return sortingOrder; }
            set
            {
                if (!IsMyselfAddCanvas) return; // 用的是别人的Canvas 所以不能对层级进行修改
                sortingOrder = value;
                RefreshSortingOrder();
            }
        }


        /// <summary>
        /// 当前UI真实的显示层级( SortingOrder + Module.UIStartSortOrder )
        /// </summary>
        public int DisplaySortingOrder
        {
            get { 
                return SortingOrder + Module.UIStartSortOrder;
            }
        }

        /// <summary>
        /// 当前Panel的类型
        /// </summary>
        public virtual PanelType PanelType => PanelType.Window;

        /// <summary>
        /// 用来判断 Canvas是不是当前页面自己添加的
        /// </summary>
        internal bool IsMyselfAddCanvas { get;private set; }

        /// <summary>
        /// 是否始终显示在最上层，默认:false
        /// </summary>
        public virtual bool AlwaysInFront => false;

        /// <summary>
        /// UIType UI的类型
        /// </summary>
        public UIType UIType { get;internal set; }
        #endregion

        #region 生命周期
        protected virtual void Awake()
        { 
        }

        #endregion


        #region 方法

        internal void InitCanvas() 
        {

            // 判断是否已经初始化 这里初始化一次就可以了
            if (isInitCanvas) return;
            isInitCanvas = true;

            Canvas c = transform.GetComponent<Canvas>();
            if (c) 
            {
                // 因为要动态控制UI层级 所以不能自己添加Canvas
                throw new System.Exception(string.Format("检测到游戏物体:{0}身上有Canvas组件,请移除!", gameObject.name));
            }

            Canvas parent_canvas = transform.GetComponentInParent<Canvas>();
            if (parent_canvas != null && parent_canvas.transform.parent != null)
            {
                Canvas = parent_canvas;
                IsMyselfAddCanvas = false; // 用的别人的
            }
            else
            { 
                // 自己添加Canvas
                Canvas = transform.gameObject.AddComponent<Canvas>();
                Canvas.overrideSorting = true;
                Canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
                gameObject.AddComponent<GraphicRaycaster>();

                IsMyselfAddCanvas = true;
            }
        }

        /// <summary>
        /// 当UI层级发生改变时触发
        /// </summary>
        /// <param name="sortingOrder">层级</param>
        protected virtual void OnSortingOrderChange(int sortingOrder) { }

        /// <summary>
        /// 变为不可见(当前页面被其他页面完全挡住时触发)
        /// </summary>
        public virtual void Invisible() { }

        /// <summary>
        /// 变为可见的(当页面被其他页面完全挡住 又重新显示出来时触发)
        /// </summary>
        public virtual void Visible() { }

        /// <summary>
        /// 判断当前页面是否在最上层
        /// </summary>
        /// <returns></returns>
        public bool IsTop() 
        {
            switch (UIType)
            {
                case UIType.UI:

                    foreach (var item in PanelManager.UIOrders.Keys)
                    {
                        if (item.PanelType == PanelType.Overlay) 
                            continue;

                        if (item == this) 
                            continue;

                        if (SortingOrder < PanelManager.UIOrders[item])
                            return false;
                    }

                    break;
                case UIType.DontDestroyUI:

                    foreach (var item in PanelManager.DontDestroyUIOrders.Keys)
                    {
                        if (item.PanelType == PanelType.Overlay)
                            continue;

                        if (item == this)
                            continue;

                        if (SortingOrder < PanelManager.DontDestroyUIOrders[item])
                            return false;
                    }

                    break; 
            }

            return true;
        }


        internal void RefreshSortingOrder() 
        {
            if (!IsMyselfAddCanvas) return; // 用的是别人的Canvas 所以不能对层级进行修改 
            Canvas.sortingOrder = sortingOrder + Module.UIStartSortOrder;
            OnSortingOrderChange(Canvas.sortingOrder);
        }


        internal override void BeforeOnLoaded()
        {
            base.BeforeOnLoaded();
            InitCanvas();
        }

        #endregion




    }
}