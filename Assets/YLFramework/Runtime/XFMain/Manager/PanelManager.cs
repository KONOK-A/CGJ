// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/PanelManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using YLFramework.Core.Extensions;
using System; 
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{

    public enum UIType
    {
        /// <summary>
        /// 把UI创建在当前场景的Canvas下面，需要对当前模块的Canvas进行赋值
        /// </summary>
        UI = 1,
        /// <summary>
        /// 把UI创建到不被销毁的Canvas下面
        /// </summary>
        DontDestroyUI = 2
    }

    public class PanelManager
    {

        #region 常量

        // 界面与界面之间的层级差量
        internal const int OrderDelta = 10;
        internal const int UI_START_ORDER = 0;
        internal const int DONTDESTROYUI_START_ORDER = 10000;

        #endregion


        #region 单例

        private static PanelManager _instance;

        internal static PanelManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new PanelManager();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif

                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    SceneManager.sceneLoaded += OnSceneLoaded;

                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif
         
        private PanelManager()
        {
        }

        #endregion



        #region 字段
         
        internal  Dictionary<Panel,int> uiOrders = new Dictionary<Panel, int>();
        
        internal  Dictionary<Panel,int> dontDestroyUIOrders = new Dictionary<Panel, int>();
      
        private  List<Panel> panelQueues = new List<Panel>(); // 保存所有的依次打开的Panel

        private  Panel _currentPanel = null; // 当前显示的panel 
         
        private  GameObject _eventSystem;

        private bool multiPanelListener = true; // 默认是true

        public  Dictionary<UIType, Canvas> uiCanvas= new Dictionary<UIType, Canvas>();

        #endregion



        #region 静态属性


        /// <summary>
        /// 保存Panel的层级
        /// </summary>
        internal static Dictionary<Panel, int> UIOrders => Instance.uiOrders;
        /// <summary>
        /// 保存DontDestroyPanel的层级
        /// </summary>
        internal static Dictionary<Panel, int> DontDestroyUIOrders => Instance.dontDestroyUIOrders; 

        private static List<Panel> PanelQueues => Instance.panelQueues; // 保存所有的依次打开的Panel

        private static Panel CurrentPanel 
        {
            get {
                return Instance._currentPanel;
            }
            set {
                Instance._currentPanel = value;
            }
        }
           
        private static GameObject EventSystem
        {
            get {
                return Instance._eventSystem;
            }
            set {
                Instance._eventSystem = value;
            }
        }

        /// <summary>
        /// Canvas
        /// </summary>
        public static Dictionary<UIType, Canvas> UICanvas => Instance.uiCanvas;
 
        /// <summary>
        /// 多个界面的重叠监听，默认:true
        /// 当为true时，如果界面的类型为Panel,当该Panel被其他的Panel挡住时,将不会再渲染,以此来节省性能
        /// </summary>
        public static bool MultiPanelListener 
        { 
            get 
            {
                return Instance.multiPanelListener;        
            }
            set 
            {
                if (Instance.multiPanelListener == value) return;
                Instance.multiPanelListener = value;

                UpdateManager.RemoveLateUpdateEvent(LateUpdate);

                if (Instance.multiPanelListener)
                {
                    UpdateManager.AddLateUpdateEvent(LateUpdate);
                } 
            }
        }


        #endregion

        #region 事件

        /// <summary>
        /// 创建Canvas时触发
        /// </summary>
        public static Action<Canvas> onCreateCanvas;

        #endregion


        #region 加载Panel


        // 加载 Panel
        internal static Panel LoadPanel(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param)
        {
            ViewManager.EnsureOwner(module);

            Panel panel = null;

            parent = GetParent(module, parent, type);

            if (parent == null)
            {
                Debug.LogError("parent is null! ");
                return null;
            }

            panel = ViewManager.LoadView(module , asset_name, parent, param) as Panel;

            if (panel == null) return null;

            panel.transform.localPosition = Vector3.zero;

            // 设置层级
            panel.UIType = type;

            if (panel.IsMyselfAddCanvas)
            { 
                panel.SortingOrder = GetUIOrder(type); 
                AddOrder(type, panel, panel.SortingOrder);
                // 添加到panel队列(如果不是自己添加的Canvas，说明当前的UI是在其他的UI里面的，此时不需要考虑被遮挡的情况)
                AddPanelQueue(panel);
            }


            return panel;
        }

        // 异步加载 Panel
        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module , string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            ViewManager.EnsureOwner(module);
            LoadPanelRequest<T> loadPanel = new LoadPanelRequest<T>();
            CoroutineStarter.Start(loadPanel.LoadPanel(module,asset_name, type, parent, param));
            return loadPanel;
        }


        #endregion


        #region 加载Panel(With LoadViewArgs)


        // 加载 Panel
        internal static T LoadPanel<T>(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null)where T : Panel
        {
            ViewManager.EnsureOwner(module);

            T panel = null;

            parent = GetParent(module, parent, type);

            if (parent == null)
            {
                Debug.LogError("parent is null! ");
                return null;
            }

            panel = ViewManager.LoadView<T>(module, asset_name, parent, loadViewArgs);

            if (panel == null) return null;

            panel.transform.localPosition = Vector3.zero;

            // 设置层级
            panel.UIType = type;

            if (panel.IsMyselfAddCanvas)
            {
                panel.SortingOrder = GetUIOrder(type);
                AddOrder(type, panel, panel.SortingOrder);
                // 添加到panel队列(如果不是自己添加的Canvas，说明当前的UI是在其他的UI里面的，此时不需要考虑被遮挡的情况)
                AddPanelQueue(panel);
            }


            return panel;
        }

        // 异步加载 Panel
        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            ViewManager.EnsureOwner(module);
            LoadPanelRequest<T> loadPanel = new LoadPanelRequest<T>();
            CoroutineStarter.Start(loadPanel.LoadPanel(module, asset_name, type, parent, loadViewArgs));
            return loadPanel;
        }

        #endregion


        #region 方法


        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshEventSystem();
        }

        private static void RefreshEventSystem()
        {

            if (!ApplicationTool.isPlaying)
                return;

            if(UnityEngine.EventSystems.EventSystem.current != null) return;
            // 如果场景中没有 EventSystem 需要创建一个
            GameObject prefab = Resources.Load<GameObject>("YLFramework/Core/EventSystem");
            EventSystem = GameObject.Instantiate(prefab);
            EventSystem.name = "EventSystem(Created By YLFramework.Core)";
            EventSystem.gameObject.SetActive(true);
        }


        // 获取UI层级
        internal static int GetUIOrderMax(UIType type)
        {

            int layer = 0;

            List<Panel> TempUIOdersPanels = ClassObjectPoolManager.Load<List<Panel>>(); 
            TempUIOdersPanels.Clear();

            switch (type)
            {
                case UIType.UI:

                    foreach (var item in UIOrders.Keys)
                    {
                        if (!item || !item.gameObject.activeInHierarchy)
                        {
                            TempUIOdersPanels.Add(item);
                            continue;
                        }

                        if (UIOrders[item]>layer)
                            layer = UIOrders[item];
                    }

                    foreach (var item in TempUIOdersPanels)
                    {
                        UIOrders.Remove(item);
                    }

                    break;
                case UIType.DontDestroyUI:

                    foreach (var item in DontDestroyUIOrders.Keys)
                    {
                        if (!item || !item.gameObject.activeInHierarchy)
                        {
                            TempUIOdersPanels.Add(item);
                            continue;
                        }

                        if (DontDestroyUIOrders[item] > layer)
                            layer = DontDestroyUIOrders[item];
                    }

                    foreach (var item in TempUIOdersPanels)
                    {
                        DontDestroyUIOrders.Remove(item);
                    }

                    break;
            }

            ClassObjectPoolManager.Unload(TempUIOdersPanels);

            if (layer == 0)
                layer =  type == UIType.UI ? UI_START_ORDER : DONTDESTROYUI_START_ORDER;

            return layer;
        }

        internal static int GetUIOrder(UIType type)
        { 
            return GetUIOrderMax(type) + OrderDelta;
        }

        // 释放层级
        internal static void ReleaseOrder(Panel panel)
        {
            if (panel == null) return;
            if (UIOrders.ContainsKey(panel))
                UIOrders.Remove(panel);
            if (DontDestroyUIOrders.ContainsKey(panel))
                DontDestroyUIOrders.Remove(panel);
        }

        internal static void AddOrder(UIType type, Panel panel, int order) {
            switch (type)
            {
                case UIType.UI:
                    if(UIOrders.ContainsKey(panel))
                        UIOrders[panel] = order;
                    else
                        UIOrders.Add(panel, order);
                    break;
                case UIType.DontDestroyUI:
                    if (DontDestroyUIOrders.ContainsKey(panel))
                        DontDestroyUIOrders[panel] = order;
                    else
                        DontDestroyUIOrders.Add(panel, order);
                    break; 
            }

            // 更新所有在最前面的界面的层级
            UpdateAlwaysInFront(); 
        }

        internal static void UpdateAlwaysInFront()
        {
            // 找到所有始终显示在最上层的界面

            List<Panel> TempAddOrderPanels = ClassObjectPoolManager.Load<List<Panel>>(); 
            TempAddOrderPanels.Clear();

            foreach (var item in UIOrders.Keys)
            {
                if (item != null && item.AlwaysInFront)
                    TempAddOrderPanels.Add(item);
            }

            foreach (var item in TempAddOrderPanels)
            {
                item.SortingOrder = GetUIOrder(UIType.UI);
                item.transform.SetAsLastSibling(); // 节点设置到最后一个
            }

            TempAddOrderPanels.Clear();

            foreach (var item in DontDestroyUIOrders.Keys)
            {
                if (item != null && item.AlwaysInFront)
                    TempAddOrderPanels.Add(item);
            }

            foreach (var item in TempAddOrderPanels)
            {
                item.SortingOrder = GetUIOrder(UIType.DontDestroyUI);
                item.transform.SetAsLastSibling(); // 节点设置到最后一个
            }

            ClassObjectPoolManager.Unload(TempAddOrderPanels);

        }

        internal static void LateUpdate() {
            UpdatePanelQueue();
        }

        internal static void ReleaseView(View view)
        {
            var manager = _instance;
            var panel = view as Panel;
            if (manager == null || ReferenceEquals(panel, null)) return;
            // 必要登记先释放，不能依赖广播、开关或Visible业务回调成功。
            manager.uiOrders.Remove(panel);
            manager.dontDestroyUIOrders.Remove(panel);
            manager.panelQueues.RemoveAll(item => ReferenceEquals(item, panel));
            if (!ReferenceEquals(manager._currentPanel, panel)) return;
            manager._currentPanel = null;
            for (int i = manager.panelQueues.Count - 1; i >= 0; i--)
            {
                var previous = manager.panelQueues[i];
                if (!previous || !previous.IsLoaded || previous.Module == null || previous.Module.State == ModuleState.Closing)
                {
                    manager.panelQueues.RemoveAt(i);
                    continue;
                }
                manager._currentPanel = previous;
                break;
            }
            var current = manager._currentPanel;
            if (manager.multiPanelListener && current && current.Canvas && !current.Canvas.enabled)
            {
                current.Canvas.enabled = true;
                current.Visible();
            }
        }

        internal static Transform GetParent(Module module,Transform parent,UIType type)
        {
            RefreshEventSystem();
            
            if (parent != null) return parent;

            //try
            //{
                if (UICanvas.ContainsKey(type) && UICanvas[type])
                    return UICanvas[type].transform;
            //}
            //catch (System.Exception)
            //{ 
            //}


            UICanvas.Remove(type);

            GameObject canvasObj = GameObject.Instantiate(Resources.Load<GameObject>("YLFramework/Core/Canvas"));
            Canvas canvas = canvasObj.GetComponent<Canvas>();
 
            canvas.sortingOrder = type == UIType.UI ? UI_START_ORDER : DONTDESTROYUI_START_ORDER;

            if (type == UIType.DontDestroyUI)
                GameObject.DontDestroyOnLoad(canvasObj);

            UICanvas.Add(type, canvas);
            // 触发事件
            onCreateCanvas?.Invoke(canvas);
            return canvas.transform;
        }

        public static Panel GetTopPanel(UIType type)
        {

            Panel panel = null;

            switch (type)
            {
                case UIType.UI:

                    foreach (var item in UIOrders.Keys)
                    {
                        if (panel == null)
                        {
                            panel = item;
                            continue;
                        }
                        if (panel.SortingOrder < item.SortingOrder)
                            panel = item;
                    }

                    break;
                case UIType.DontDestroyUI:

                    foreach (var item in DontDestroyUIOrders.Keys)
                    {
                        if (panel == null)
                        {
                            panel = item;
                            continue;
                        }
                        if (panel.SortingOrder < item.SortingOrder)
                            panel = item;
                    }

                    break; 
            }

            return panel;
        }

        public static void GetAllPanels(UIType type,List<Panel> results) 
        {

            if (results == null)
                throw new Exception("result cannot be null!");

            results.Clear();

            switch (type)
            {
                case UIType.UI:

                    foreach (var item in UIOrders.Keys)
                    {
                        results.Add(item);
                    }

                    break;
                case UIType.DontDestroyUI:

                    foreach (var item in DontDestroyUIOrders.Keys)
                    {
                        results.Add(item);
                    }

                    break;
            }

        }
         
#endregion

        #region 自动管理多个Panel重叠问题

        internal static void AddPanelQueue(Panel panel)
        {
            // 判断是否开启该功能
            if (!MultiPanelListener) return;
            if (panel == null || panel.PanelType == PanelType.Window) return;

            // 检测当前的panel 是否已经在队列中 如果已经存在 需要移除
            if (PanelQueues.Contains(panel)) PanelQueues.Remove(panel);
            // 加入队列
            PanelQueues.Add(panel);

            if (PanelQueues.Count > 20) Debug.LogWarning("PanelManager.panel_queues队列数量过多,请检查是否存在异常!");

            CurrentPanel = panel;

            // 把前面的Panel位置进行修改
            for (int i = 0; i < PanelQueues.Count - 1; i++)
            {
                if (PanelQueues[i].Canvas.enabled)
                {
                    PanelQueues[i].Canvas.enabled = false;
                    PanelQueues[i].Invisible();
                }
            }

        }

        internal static void RemovePanelQueue(Panel panel)
        {
            // 判断是否开启该功能
            if (!MultiPanelListener) return;
            if (panel == null) return;
            PanelQueues.Remove(panel);

            if (panel == CurrentPanel) ReturnToPreviousPanel();
        }


        // 更新Panel队列
        internal static void UpdatePanelQueue()
        {
            // 判断是否开启该功能
            if (!MultiPanelListener) return;
            if (CurrentPanel == null) return;

            // 监听当前的页面是否被关掉 或者 销毁
            if (CurrentPanel.gameObject == null || CurrentPanel.gameObject.IsDestroy() || !CurrentPanel.gameObject.activeSelf)
            {
                while (PanelQueues.Count > 0)
                {
                    Panel last = PanelQueues[PanelQueues.Count - 1];
                    PanelQueues.RemoveAt(PanelQueues.Count - 1);
                    if (last == CurrentPanel) break;
                }

                // 把最后一个设置成当前的
                ReturnToPreviousPanel();
            }
        }

        // 返回到上一个界面
        internal static void ReturnToPreviousPanel()
        {
            if (PanelQueues.Count == 0) return;
            Panel last = PanelQueues[PanelQueues.Count - 1];
            if (last == null) return;
            if (last.Canvas.enabled == false)
            {
                last.Canvas.enabled = true;
                last.Visible();
            }
        }

        #endregion


    }
}
