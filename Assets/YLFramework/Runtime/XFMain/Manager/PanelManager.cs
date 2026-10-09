using YLFramework.Core.Extensions;
using System; 
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;

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
            => LoadPanelLegacy<Panel>(module, asset_name, type, parent, param);

        internal static T LoadPanelLegacy<T>(Module module, string assetName, UIType type, Transform parent, object[] param) where T : Panel
            => LoadPanelCore<T>(module, assetName, type, parent, null, param);

        // 异步加载 Panel
        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module , string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
            => StartPanelAsync<T>(module, asset_name, type, parent, null, param);


        #endregion


        #region 加载Panel(With LoadViewArgs)


        // 加载 Panel
        internal static T LoadPanel<T>(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null)where T : Panel
            => LoadPanelCore<T>(module, asset_name, type, parent, loadViewArgs, null);

        private static T LoadPanelCore<T>(Module module, string assetName, UIType type, Transform parent,
            LoadViewArgs args, object[] param) where T : Panel
        {
            if (args != null) args.Claim();
            try
            {
                parent = ResolveParent(module, assetName, parent, type);
                return ViewManager.LoadViewClaimed<T>(module, assetName, parent, args, param, type);
            }
            finally { if (args != null) args.Release(); }
        }

        // 异步加载 Panel
        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module, string asset_name, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
            => StartPanelAsync<T>(module, asset_name, type, parent, loadViewArgs, null);

        private static LoadPanelRequest<T> StartPanelAsync<T>(Module module, string assetName, UIType type, Transform parent,
            LoadViewArgs args, object[] param) where T : Panel
        {
            if (args != null) args.Claim();
            LoadViewRequest<T> child;
            try
            {
                parent = ResolveParent(module, assetName, parent, type);
                child = ViewManager.StartAsync<T>(module, assetName, parent, args, param, true, type);
            }
            catch (Exception error)
            {
                if (args != null)
                {
                    try { args.Release(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            // 从此输入归子View请求所有，Panel没有额外producer或pending状态。
            return new LoadPanelRequest<T>(child);
        }

        private static Transform ResolveParent(Module module, string assetName, Transform parent, UIType type)
        {
            ViewManager.EnsureOwner(module);
            if (string.IsNullOrWhiteSpace(assetName)) throw new ArgumentException("Panel asset name is required.", nameof(assetName));
            if (type != UIType.UI && type != UIType.DontDestroyUI) throw new ArgumentOutOfRangeException(nameof(type));
            if (!ReferenceEquals(parent, null) && !parent) throw new ArgumentException("Panel parent has been destroyed.", nameof(parent));
            module.viewTransactions++;
            try
            {
                var result = GetParent(module, parent, type);
                if (!result) throw new InvalidOperationException("Panel parent is unavailable.");
                return result;
            }
            finally { module.viewTransactions--; }
        }

        internal static UIType GetUIType(Transform parent)
        {
            for (var ancestor = parent; ancestor; ancestor = ancestor.parent)
            {
                var panel = ancestor.GetComponent<Panel>();
                if (panel && panel.Session != null) return panel.UIType;
            }
            var manager = _instance;
            if (parent && manager != null && manager.uiCanvas.TryGetValue(UIType.DontDestroyUI, out var canvas) &&
                canvas && parent.gameObject.scene == canvas.gameObject.scene) return UIType.DontDestroyUI;
            return UIType.UI;
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
                        if (!item || item.Session == null || item.IsRemoving)
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
                        if (!item || item.Session == null || item.IsRemoving)
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

        internal static void UpdateOrder(Panel panel, int order)
        {
            var manager = _instance;
            if (manager == null) return;
            if (manager.uiOrders.ContainsKey(panel)) manager.uiOrders[panel] = order;
            if (manager.dontDestroyUIOrders.ContainsKey(panel)) manager.dontDestroyUIOrders[panel] = order;
        }

        internal static void UpdateAlwaysInFront()
        {
            var panels = ClassObjectPoolManager.Load<List<KeyValuePair<Panel, object>>>();
            panels.Clear();
            bool IsCurrent(KeyValuePair<Panel, object> entry, UIType type) =>
                entry.Value != null && entry.Key && !entry.Key.IsRemoving && entry.Key.UIType == type &&
                ReferenceEquals(entry.Key.Session, entry.Value);

            void Update(UIType type, Dictionary<Panel, int> orders)
            {
                foreach (var panel in orders.Keys)
                    if (panel) panels.Add(new KeyValuePair<Panel, object>(panel, panel.Session));
                foreach (var entry in panels)
                {
                    var panel = entry.Key;
                    if (!IsCurrent(entry, type) || !panel.AlwaysInFront || !IsCurrent(entry, type)) continue;
                    panel.SortingOrder = GetUIOrder(type);
                    // 排序业务回调可以关闭并复用同一组件，不操作其新会话。
                    if (IsCurrent(entry, type)) panel.transform.SetAsLastSibling();
                }
                panels.Clear();
            }
            try
            {
                Update(UIType.UI, UIOrders);
                Update(UIType.DontDestroyUI, DontDestroyUIOrders);
            }
            finally { ClassObjectPoolManager.Unload(panels); }
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
            ReleaseQueue(manager, panel);
        }

        internal static void ActivateView(View view)
        {
            if (view is Panel panel)
            {
                panel.RestoreCanvas();
                if (panel.IsMyselfAddCanvas) AddPanelQueue(panel);
            }
        }

        internal static void DeactivateView(View view)
        {
            var manager = _instance;
            if (manager != null && view is Panel panel && panel && !panel.gameObject.activeInHierarchy)
                ReleaseQueue(manager, panel);
        }

        private static void ReleaseQueue(PanelManager manager, Panel panel)
        {
            manager.panelQueues.RemoveAll(item => ReferenceEquals(item, panel));
            if (!ReferenceEquals(manager._currentPanel, panel)) return;
            manager._currentPanel = null;
            for (int i = manager.panelQueues.Count - 1; i >= 0; i--)
            {
                var previous = manager.panelQueues[i];
                if (!previous || !previous.IsLoaded || !previous.gameObject.activeInHierarchy || previous.IsRemoving ||
                    previous.Module == null || previous.Module.State == ModuleState.Closing)
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
            if (panel == null || !panel.IsLoaded || !panel.gameObject.activeInHierarchy || panel.PanelType != PanelType.Panel) return;

            if (PanelQueues.Contains(panel)) return;
            panel.Canvas.enabled = true;
            // 加入队列
            PanelQueues.Add(panel);

            if (PanelQueues.Count > 20) Debug.LogWarning("PanelManager.panel_queues队列数量过多,请检查是否存在异常!");

            CurrentPanel = panel;

            // 把前面的Panel位置进行修改
            for (int i = 0; i < PanelQueues.Count - 1; i++)
            {
                if (PanelQueues[i] && PanelQueues[i].Canvas && PanelQueues[i].Canvas.enabled)
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
