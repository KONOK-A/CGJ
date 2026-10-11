using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YLFramework.Events;
using YLFramework.Core.Singleton;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using UpdateManager = YLFramework.Foundation.UpdateManager;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    public enum UIType
    {
        /// <summary>准确 Scene 下的 UI 根。</summary>
        UI = 1,
        /// <summary>持久 UI 根；不会转移 Module 业务所有权。</summary>
        DontDestroyUI = 2
    }

    public class PanelManager
    {
        internal const int OrderDelta = 10;
        internal const int UI_START_ORDER = 0;
        internal const int DONTDESTROYUI_START_ORDER = 10000;
        private static PanelManager _instance;
        private IUnRegister lateSubscription;
        private object lateRound;
        private bool released;
        private bool multiPanelListener = true;
        private GameObject _eventSystem;
        private readonly HashSet<Canvas> inputRoots = new HashSet<Canvas>();
        private bool inputConfigurationWarned;
        private readonly List<PanelState> inputSnapshot = new List<PanelState>();
        private PanelState pendingSelectionRelease;
        private GameObject releasedSelection;
        private EventSystem releasedSelectionSystem;
        private long sequence;

        public readonly Dictionary<UIType, Canvas> uiCanvas = new Dictionary<UIType, Canvas>();
        private readonly Dictionary<int, Canvas> sceneCanvases = new Dictionary<int, Canvas>();
        private readonly HashSet<Canvas> registeredRoots = new HashSet<Canvas>();
        private readonly Dictionary<Panel, PanelState> states = new Dictionary<Panel, PanelState>();

        private sealed class PanelState
        {
            internal Panel Panel;
            internal object Session;
            internal Canvas Root;
            internal int Scene;
            internal long Sequence;
            internal int Order;
            internal int Layer;
            internal bool Front;
            internal bool Modal;
            internal bool Active;
            internal bool Visible;
            internal bool Blocked;
            internal PanelInputState Input;
        }

        static PanelManager() => SingletonSessionState.RegisterSessionReset(ResetSession);
        private PanelManager() { }

        private static void ResetSession()
        {
            var manager = _instance;
            _instance = null;
            if (manager != null)
            {
                manager.released = true;
                manager.EndLateRound();
                manager.ReleaseOwnedInput();
                foreach (var state in manager.states.Values) state.Input.Restore(true);
                manager.states.Clear();
                manager.inputRoots.Clear();
                manager.registeredRoots.Clear();
                manager.sceneCanvases.Clear();
                manager.uiCanvas.Clear();
            }
            onCreateCanvas = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        internal static PanelManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new PanelManager();
                    SceneManager.sceneLoaded -= OnSceneLoaded;
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    SceneManager.sceneUnloaded -= OnSceneUnloaded;
                    SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        private static void EditorApplication_playModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode) ResetSession();
        }
#endif

        private void EndLateRound()
        {
            lateRound = null;
            var subscription = lateSubscription;
            lateSubscription = null;
            subscription?.UnRegister();
        }

        private void SyncLateSubscription()
        {
            if (released || (states.Count == 0 && inputRoots.Count == 0)) { EndLateRound(); return; }
            if (lateSubscription != null) return;
            var round = lateRound = new object();
            lateSubscription = UpdateManager.AddLateUpdateEvent(() =>
            {
                if (!released && ReferenceEquals(_instance, this) && ReferenceEquals(lateRound, round))
                    UpdatePanelQueueCore();
            });
        }

        /// <summary>缺省根入口。UI 项只代表当前 Scene；多根请传明确 parent 或使用 RegisterRoot。</summary>
        public static Dictionary<UIType, Canvas> UICanvas
        {
            get
            {
                var manager = Instance;
                var scene = SceneManager.GetActiveScene();
                if (!manager.uiCanvas.TryGetValue(UIType.UI, out var current) || !current || current.gameObject.scene != scene)
                {
                    if (manager.sceneCanvases.TryGetValue(scene.handle, out var root) && root) manager.uiCanvas[UIType.UI] = root;
                    else manager.uiCanvas.Remove(UIType.UI);
                }
                return manager.uiCanvas;
            }
        }

        /// <summary>是否启用同根全屏渲染遮挡。关闭时恢复显示，不改变 View 会话或模态状态。</summary>
        public static bool MultiPanelListener
        {
            get => Instance.multiPanelListener;
            set
            {
                var manager = Instance;
                manager.multiPanelListener = value;
                manager.Reconcile();
            }
        }

        /// <summary>缺省根创建通知，仅当前 Session 有效。</summary>
        public static Action<Canvas> onCreateCanvas;

        internal static Panel LoadPanel(Module module, string assetName, UIType type = UIType.UI, Transform parent = null, params object[] param)
            => LoadPanelLegacy<Panel>(module, assetName, type, parent, param);
        internal static T LoadPanelLegacy<T>(Module module, string assetName, UIType type, Transform parent, object[] param) where T : Panel
            => LoadPanelCore<T>(module, assetName, type, parent, null, param);
        internal static T LoadPanel<T>(Module module, string assetName, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
            => LoadPanelCore<T>(module, assetName, type, parent, loadViewArgs, null);

        private static T LoadPanelCore<T>(Module module, string assetName, UIType type, Transform parent, LoadViewArgs args, object[] param) where T : Panel
        {
            if (args != null) args.Claim();
            try
            {
                parent = ResolveParent(module, assetName, parent, type);
                return ViewManager.LoadViewClaimed<T>(module, assetName, parent, args, param, type);
            }
            finally { if (args != null) args.Release(); }
        }

        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module, string assetName, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
            => StartPanelAsync<T>(module, assetName, type, parent, null, param);
        internal static LoadPanelRequest<T> LoadPanelAsync<T>(Module module, string assetName, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
            => StartPanelAsync<T>(module, assetName, type, parent, loadViewArgs, null);

        private static LoadPanelRequest<T> StartPanelAsync<T>(Module module, string assetName, UIType type, Transform parent, LoadViewArgs args, object[] param) where T : Panel
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
            if (parent && parent.gameObject.scene.name == "DontDestroyOnLoad") return UIType.DontDestroyUI;
            return UIType.UI;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var manager = _instance;
            if (manager == null) return;
            foreach (var state in manager.states.Values)
                if (manager.Active(state) && (state.Panel.gameObject.scene == scene || state.Panel.UIType == UIType.DontDestroyUI))
                {
                    RefreshEventSystem();
                    return;
                }
        }
        private static void OnSceneUnloaded(Scene scene)
        {
            var manager = _instance;
            if (manager == null) return;
            manager.sceneCanvases.Remove(scene.handle);
            manager.registeredRoots.RemoveWhere(root => !root || root.gameObject.scene == scene);
            manager.Reconcile();
        }

        private static void RefreshEventSystem()
        {
            _instance?.RefreshInput(true);
        }

        private void ReleaseOwnedInput()
        {
            var owned = _eventSystem;
            _eventSystem = null;
            if (!owned) return;
            InputSystemBridge.DisableOwnedSystem(owned);
            if (Application.isPlaying) UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }

        private void RefreshInput(bool discover = false)
        {
            if (released || !ApplicationTool.isPlaying) return;
            inputRoots.RemoveWhere(root => !root);
            bool needed = false;
            foreach (var root in inputRoots) if (root.isActiveAndEnabled) { needed = true; break; }
            if (!needed) { ReleaseOwnedInput(); SyncLateSubscription(); return; }
            var current = EventSystem.current;
            if (current && !current.isActiveAndEnabled) current = null;
            if (_eventSystem && current && current.gameObject == _eventSystem)
            {
                var external = InputSystemBridge.FindExternalSystem(current);
                if (external) current = external;
            }
            if (discover || !current)
            {
                var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                int active = 0;
                bool inactive = false;
                foreach (var system in systems)
                {
                    if (!system.isActiveAndEnabled) { inactive = true; continue; }
                    active++;
                    if ((!current || current.gameObject == _eventSystem) && system.gameObject != _eventSystem) current = system;
                }
                if (!inputConfigurationWarned && (active > 1 || (!current && inactive)))
                {
                    inputConfigurationWarned = true;
                    Debug.LogWarning("[PanelManager] 多个 active 或仅 inactive EventSystem。保留外部对象；优先借用 active 系统，inactive 系统不会被激活。");
                }
            }
            if (current && current.gameObject != _eventSystem)
            {
                ReleaseOwnedInput();
                // 借用系统不补齐、不切换其模块，也不更改其配置与对象寿命。
                if (!inputConfigurationWarned)
                {
                    bool usable = false;
                    foreach (var module in current.GetComponents<BaseInputModule>())
                    {
#if !ENABLE_LEGACY_INPUT_MANAGER
                        if (module is StandaloneInputModule) continue;
#endif
                        if (module.isActiveAndEnabled && module.IsModuleSupported()) { usable = true; break; }
                    }
                    if (!usable)
                    {
                        inputConfigurationWarned = true;
                        Debug.LogWarning("[PanelManager] 借用的 EventSystem 没有可用输入模块，请由其 owner 配置；框架不会替换外部模块。");
                    }
                }
                return;
            }
            if (_eventSystem) return;
            var prefab = Resources.Load<GameObject>("YLFramework/Core/EventSystem");
            if (!prefab) throw new InvalidOperationException("Core EventSystem prefab is unavailable.");
            var created = UnityEngine.Object.Instantiate(prefab);
            try
            {
                created.name = "EventSystem(Created By YLFramework.Core)";
                InputSystemBridge.EnsureEventSystemInputModule(created.GetComponent<EventSystem>());
                UnityEngine.Object.DontDestroyOnLoad(created);
                _eventSystem = created;
                SyncLateSubscription();
            }
            catch
            {
                created.SetActive(false);
                UnityEngine.Object.Destroy(created);
                throw;
            }
        }

        /// <summary>将有效的现有根登记为所属 Scene 或 DDOL 的缺省根，不改变其寿命或组件配置。</summary>
        public static void RegisterRoot(Canvas root, UIType type = UIType.UI)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (!root.gameObject.scene.IsValid() || !root.gameObject.scene.isLoaded)
                throw new ArgumentException("UI root must belong to a loaded Scene, not an asset.", nameof(root));
            if (type != UIType.UI && type != UIType.DontDestroyUI) throw new ArgumentOutOfRangeException(nameof(type));
            var persistent = root.gameObject.scene.name == "DontDestroyOnLoad";
            if (persistent != (type == UIType.DontDestroyUI)) throw new ArgumentException("UI root lifetime does not match its Scene.", nameof(type));
            var manager = Instance;
            manager.registeredRoots.Add(root);
            if (ApplicationTool.isPlaying) manager.inputRoots.Add(root);
            if (type == UIType.UI) manager.sceneCanvases[root.gameObject.scene.handle] = root;
            manager.uiCanvas[type] = root;
            manager.Reconcile();
        }

        internal static Transform GetParent(Module module, Transform parent, UIType type)
        {
            if (parent)
            {
                var explicitRoot = ResolveParentRoot(parent);
                if (explicitRoot) Instance.inputRoots.Add(explicitRoot);
                RefreshEventSystem();
                return parent;
            }
            var manager = Instance;
            var scene = SceneManager.GetActiveScene();
            if (manager.uiCanvas.TryGetValue(type, out var legacy) && legacy &&
                (type == UIType.DontDestroyUI || legacy.gameObject.scene == scene))
            {
                manager.inputRoots.Add(legacy);
                RefreshEventSystem();
                return legacy.transform;
            }
            if (type == UIType.UI && manager.sceneCanvases.TryGetValue(scene.handle, out var existing) && existing)
            {
                manager.uiCanvas[type] = existing;
                manager.inputRoots.Add(existing);
                RefreshEventSystem();
                return existing.transform;
            }
            var obj = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("YLFramework/Core/Canvas"));
            var canvas = obj.GetComponent<Canvas>();
            canvas.sortingOrder = type == UIType.UI ? UI_START_ORDER : DONTDESTROYUI_START_ORDER;
            if (type == UIType.DontDestroyUI) UnityEngine.Object.DontDestroyOnLoad(obj);
            else SceneManager.MoveGameObjectToScene(obj, scene);
            manager.uiCanvas[type] = canvas;
            manager.registeredRoots.Add(canvas);
            if (type == UIType.UI) manager.sceneCanvases[scene.handle] = canvas;
            manager.inputRoots.Add(canvas);
            RefreshEventSystem();
            onCreateCanvas?.Invoke(canvas);
            return canvas.transform;
        }

        internal static Canvas ResolveParentRoot(Transform parent)
        {
            Canvas root = null;
            for (var current = parent; current; current = current.parent)
            {
                var canvas = current.GetComponent<Canvas>();
                if (!canvas) continue;
                root = canvas;
                if (_instance != null && _instance.registeredRoots.Contains(canvas)) break;
            }
            return root;
        }

        internal static void BringToFront(Panel panel)
        {
            var manager = _instance;
            if (manager == null) return;
            manager.Reconcile();
            if (!manager.states.TryGetValue(panel, out var state) || !manager.Current(state)) return;
            int max = state.Order;
            bool above = false;
            foreach (var other in manager.states.Values)
                if (manager.Current(other) && SameContext(state, other) && other.Panel.WindowLayer == panel.WindowLayer &&
                    other.Panel.Canvas.sortingLayerID == panel.Canvas.sortingLayerID &&
                    other.Panel.PolicyModal == panel.PolicyModal && other.Panel.AlwaysInFront == panel.AlwaysInFront &&
                    Compare(other, state) > 0)
                {
                    above = true;
                    max = Math.Max(max, other.Order);
                }
            if (above) SetOrder(panel, checked((int)((long)max + OrderDelta - panel.Module.UIStartSortOrder)));
        }

        private static Canvas ResolveRoot(Panel panel)
        {
            if (!panel) return null;
            var canvasToUse = panel.IsMyselfAddCanvas ? panel.Canvas : panel.FindBorrowedCanvas();
            if (!canvasToUse) return null;
            // inactive Canvas 的原生 rootCanvas 尚未可靠建立；以真实层级取得同一个根身份。
            var root = canvasToUse;
            if (_instance != null && _instance.registeredRoots.Contains(root)) return root;
            for (var parent = root.transform.parent; parent; parent = parent.parent)
            {
                var canvas = parent.GetComponent<Canvas>();
                if (canvas)
                {
                    root = canvas;
                    if (_instance != null && _instance.registeredRoots.Contains(root)) break;
                }
            }
            return root;
        }

        internal static Canvas GetRoot(Panel panel)
        {
            if (!panel || panel.Session == null) return null;
            return ResolveRoot(panel);
        }

        private bool Current(PanelState state) => !released && state.Panel && state.Session != null &&
            ReferenceEquals(state.Panel.Session, state.Session) && !state.Panel.IsRemoving &&
            (!state.Panel.IsMyselfAddCanvas || state.Panel.Canvas) && state.Panel.Module != null && state.Panel.Module.State != ModuleState.Closing &&
            states.TryGetValue(state.Panel, out var registered) && ReferenceEquals(registered, state);

        private bool Active(PanelState state) => Current(state) && state.Panel.IsLoaded && state.Panel.gameObject.activeInHierarchy;
        private static bool SameContext(PanelState a, PanelState b) => a.Root == b.Root && a.Scene == b.Scene;

        private static int CheckedOrder(Panel panel, int requested, int? offset = null)
        {
            var value = (long)requested + (offset ?? panel.Module.UIStartSortOrder);
            if (value < short.MinValue || value > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(requested), "Effective Canvas order must fit a signed 16-bit integer.");
            return (int)value;
        }

        internal static void ValidateModuleOrder(Module module, int offset)
        {
            var manager = _instance;
            if (manager == null) return;
            manager.BuildOrders(null, 0, module, offset);
        }

        // 每个根/排序层稳定排序；原始有效值尽量保留，平手及前置组只做必要提升。
        private List<KeyValuePair<PanelState, int>> BuildOrders(Panel changed = null, int requested = 0, Module module = null, int offset = 0)
        {
            var list = new List<PanelState>();
            foreach (var state in states.Values) if (Current(state) && state.Panel.IsMyselfAddCanvas) list.Add(state);
            var roots = new Dictionary<PanelState, Canvas>();
            foreach (var state in list) roots.Add(state, ResolveRoot(state.Panel));
            int Raw(PanelState state) => CheckedOrder(state.Panel, state.Panel == changed ? requested : state.Panel.RequestedOrder,
                state.Panel.Module == module ? (int?)offset : null);
            foreach (var state in list) Raw(state);
            list.Sort((a, b) =>
            {
                int comparison = roots[a].GetInstanceID().CompareTo(roots[b].GetInstanceID());
                if (comparison != 0) return comparison;
                comparison = a.Panel.gameObject.scene.handle.CompareTo(b.Panel.gameObject.scene.handle);
                if (comparison != 0) return comparison;
                comparison = SortingLayer.GetLayerValueFromID(a.Panel.Canvas.sortingLayerID).CompareTo(SortingLayer.GetLayerValueFromID(b.Panel.Canvas.sortingLayerID));
                if (comparison != 0) return comparison;
                comparison = a.Panel.WindowLayer.CompareTo(b.Panel.WindowLayer);
                if (comparison != 0) return comparison;
                comparison = (a.Panel.PolicyModal && a.Panel.PolicyType != PanelType.Overlay).CompareTo(b.Panel.PolicyModal && b.Panel.PolicyType != PanelType.Overlay);
                if (comparison != 0) return comparison;
                comparison = a.Panel.AlwaysInFront.CompareTo(b.Panel.AlwaysInFront);
                if (comparison != 0) return comparison;
                comparison = Raw(a).CompareTo(Raw(b));
                return comparison != 0 ? comparison : a.Sequence.CompareTo(b.Sequence);
            });
            var result = new List<KeyValuePair<PanelState, int>>(list.Count);
            PanelState previous = null;
            int previousOrder = 0;
            foreach (var state in list)
            {
                int order = Raw(state);
                if (previous != null && roots[previous] == roots[state] &&
                    previous.Panel.gameObject.scene == state.Panel.gameObject.scene &&
                    previous.Panel.Canvas.sortingLayerID == state.Panel.Canvas.sortingLayerID)
                    order = Math.Max(order, previousOrder + (state.Panel.PolicyModal ? 2 : 1));
                if (order > short.MaxValue) throw new ArgumentOutOfRangeException(nameof(requested), "Panel priority exhausts the Canvas order range.");
                var context = roots[state].GetComponent<UIRootContext>();
                if (order == short.MinValue && state.Panel.PolicyModal && state.Panel.PolicyType != PanelType.Overlay &&
                    context && context.HasMask(state.Panel.WindowLayer))
                    throw new ArgumentOutOfRangeException(nameof(requested), "Modal window requires a Canvas order below it for the mask.");
                result.Add(new KeyValuePair<PanelState, int>(state, order));
                previous = state;
                previousOrder = order;
            }
            return result;
        }

        internal static void Prepare(Panel panel)
        {
            var manager = Instance;
            var state = new PanelState { Panel = panel, Session = panel.Session, Root = ResolveRoot(panel), Input = new PanelInputState(panel),
                Scene = panel.gameObject.scene.handle, Sequence = ++manager.sequence };
            if (panel.IsMyselfAddCanvas)
            {
                int max = panel.UIType == UIType.UI ? UI_START_ORDER : DONTDESTROYUI_START_ORDER;
                foreach (var existing in manager.states.Values)
                    if (manager.Current(existing) && ResolveRoot(existing.Panel) == state.Root &&
                        existing.Panel.gameObject.scene.handle == state.Scene && existing.Panel.IsMyselfAddCanvas &&
                        existing.Panel.Canvas.sortingLayerID == panel.Canvas.sortingLayerID &&
                        existing.Panel.WindowLayer == panel.WindowLayer &&
                        existing.Panel.PolicyModal == panel.PolicyModal && existing.Panel.AlwaysInFront == panel.AlwaysInFront)
                        max = Math.Max(max, existing.Panel.RequestedOrder);
                panel.CommitOrder(checked(max + OrderDelta));
                CheckedOrder(panel, panel.RequestedOrder);
            }
            manager.states[panel] = state;
            if (state.Root) manager.inputRoots.Add(state.Root);
            manager.Reconcile(panel);
            manager.SyncLateSubscription();
            // 当前窗口自身的准备通知仍在 View 初始化事务内，不能按无关观察异常处理。
            if (manager.Current(state) && panel.IsMyselfAddCanvas) panel.NotifySortingOrder();
        }

        internal static void SetOrder(Panel panel, int value)
        {
            var manager = _instance;
            if (manager == null || !manager.states.TryGetValue(panel, out var state) || !manager.Current(state)) return;
            CheckedOrder(panel, value);
            manager.BuildOrders(panel, value);
            panel.CommitOrder(value);
            manager.Reconcile();
        }

        internal static void Refresh(Panel panel)
        {
            var manager = _instance;
            if (manager != null) manager.Reconcile();
        }

        internal static void RestoreCanvas(Panel panel)
        {
            if (_instance != null && _instance.states.TryGetValue(panel, out var state) && _instance.Current(state))
            {
                panel.Canvas.overrideSorting = true;
                panel.Canvas.sortingOrder = state.Order;
            }
        }

        private static int Compare(PanelState a, PanelState b)
        {
            int comparison = a.Layer.CompareTo(b.Layer);
            if (comparison != 0) return comparison;
            comparison = a.Order.CompareTo(b.Order);
            return comparison != 0 ? comparison : a.Sequence.CompareTo(b.Sequence);
        }

        private void Reconcile(Panel preparing = null)
        {
            if (released) return;
            var stale = new List<Panel>();
            foreach (var state in states.Values)
            {
                if (state.Panel)
                {
                    state.Panel.RefreshBorrowedCanvas();
                    state.Root = ResolveRoot(state.Panel);
                    state.Scene = state.Panel.gameObject.scene.handle;
                    if (state.Root) inputRoots.Add(state.Root);
                }
                if (!Current(state)) stale.Add(state.Panel);
            }
            foreach (var panel in stale)
            {
                var state = states[panel];
                state.Input.Restore(true);
                states.Remove(panel);
            }
            var orders = BuildOrders();
            var sorting = new List<KeyValuePair<PanelState, int>>();
            foreach (var entry in orders)
            {
                var state = entry.Key;
                if (state.Order != entry.Value || state.Front != state.Panel.AlwaysInFront ||
                    state.Layer != SortingLayer.GetLayerValueFromID(state.Panel.Canvas.sortingLayerID)) sorting.Add(entry);
                state.Order = entry.Value;
                state.Layer = SortingLayer.GetLayerValueFromID(state.Panel.Canvas.sortingLayerID);
                state.Front = state.Panel.AlwaysInFront;
                state.Panel.Canvas.overrideSorting = true;
                state.Panel.Canvas.sortingOrder = state.Order;
            }
            var snapshot = new List<PanelState>(states.Values);
            var visibility = new List<KeyValuePair<PanelState, bool>>();
            foreach (var state in snapshot)
            {
                if (!state.Panel.IsMyselfAddCanvas) continue;
                bool active = Active(state);
                bool visible = active;
                bool blocked = !active;
                state.Modal = state.Panel.PolicyModal && state.Panel.PolicyType != PanelType.Overlay && state.Panel.IsMyselfAddCanvas;
                if (active)
                {
                    foreach (var upper in snapshot)
                    {
                        if (ReferenceEquals(upper, state) || !Active(upper) || !SameContext(upper, state) ||
                            !upper.Panel.IsMyselfAddCanvas || Compare(upper, state) <= 0) continue;
                        if (multiPanelListener && state.Panel.PolicyType == PanelType.Panel && state.Panel.IsMyselfAddCanvas &&
                            upper.Panel.PolicyType == PanelType.Panel) visible = false;
                        if (upper.Panel.PolicyModal && upper.Panel.PolicyType != PanelType.Overlay) blocked = true;
                    }
                }
                if (active && (state.Active ? state.Visible != visible : !visible))
                    visibility.Add(new KeyValuePair<PanelState, bool>(state, visible));
                state.Active = active;
                state.Visible = visible;
                state.Blocked = blocked || !visible;
                if (state.Panel.IsMyselfAddCanvas) state.Panel.Canvas.enabled = !active || visible;
            }
            // 所有独立窗口提交后再计算借用控件，不能依赖 Dictionary 枚举顺序或上轮状态。
            foreach (var state in snapshot)
            {
                if (state.Panel.IsMyselfAddCanvas) continue;
                PanelState owner = null;
                for (var parent = state.Panel.transform.parent; parent; parent = parent.parent)
                {
                    var panel = parent.GetComponent<Panel>();
                    if (panel && panel.IsMyselfAddCanvas && panel.Canvas == state.Panel.Canvas &&
                        states.TryGetValue(panel, out var candidate) && Current(candidate)) { owner = candidate; break; }
                }
                bool active = Active(state);
                bool visible = active && owner != null && owner.Visible;
                if (active && (state.Active ? state.Visible != visible : !visible))
                    visibility.Add(new KeyValuePair<PanelState, bool>(state, visible));
                state.Active = active;
                state.Visible = visible;
                state.Blocked = !visible || owner == null || owner.Blocked;
            }
            SyncLateSubscription();
            foreach (var root in new List<Canvas>(registeredRoots))
                if (root && root.TryGetComponent<UIRootContext>(out var context)) context.RefreshMasks();
            RefreshInput();
            RefreshPanelInput(snapshot);
            // 结构、排序与 Canvas 先全部提交，业务重入后只通知仍匹配的旧记录。
            var initializationErrors = new List<Exception>();
            foreach (var entry in sorting)
                if (entry.Key.Panel != preparing && Current(entry.Key) && entry.Key.Order == entry.Value)
                {
                    if (entry.Key.Panel.IsInitializing && ReferenceEquals(ViewManager.InitializingView.Key, entry.Key.Panel) &&
                        ReferenceEquals(ViewManager.InitializingView.Value, entry.Key.Session))
                    {
                        try { entry.Key.Panel.NotifySortingOrder(); }
                        catch (Exception error) { initializationErrors.Add(error); }
                    }
                    else Notify(() => entry.Key.Panel.NotifySortingOrder());
                }
            foreach (var entry in visibility)
                if (Active(entry.Key) && entry.Key.Visible == entry.Value)
                    Notify(() => { if (entry.Value) entry.Key.Panel.Visible(); else entry.Key.Panel.Invisible(); });
            ViewManager.ThrowCleanupErrors(initializationErrors);
        }

        private static void Notify(Action action)
        {
            try { action(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void RefreshPanelInput(List<PanelState> snapshot)
        {
            var system = EventSystem.current;
            if (pendingSelectionRelease != null && system && !system.alreadySelecting)
            {
                var ended = pendingSelectionRelease;
                var target = releasedSelection;
                var targetSystem = releasedSelectionSystem;
                pendingSelectionRelease = null;
                releasedSelection = null;
                releasedSelectionSystem = null;
                if (system == targetSystem && system.currentSelectedGameObject == target &&
                    (!ended.Panel || ended.Panel.Session == null || ReferenceEquals(ended.Panel.Session, ended.Session)))
                    system.SetSelectedGameObject(null);
            }
            // 移出原窗口的组件先交还输入属性，再由新窗口决定覆盖；不能由枚举顺序决定新快照。
            foreach (var state in snapshot) if (Current(state)) state.Input.RestoreDeparted();
            foreach (var state in snapshot)
            {
                if (!Current(state)) continue;
                var input = state.Input;
                if (state.Blocked && !input.Blocked && system && input.Owns(system.currentSelectedGameObject))
                {
                    input.Selection = system.currentSelectedGameObject;
                    input.SelectionSystem = system;
                }
                input.Apply(state.Blocked);
            }
            if (!system || !system.isActiveAndEnabled || system.alreadySelecting) return;
            bool cleared = false;
            foreach (var state in snapshot)
                if (Current(state) && state.Blocked && state.Input.Owns(system.currentSelectedGameObject))
                {
                    system.SetSelectedGameObject(null);
                    cleared = true;
                    break;
                }
            // Select/Deselect 可关闭、销毁或重开窗口。只允许仍匹配的会话恢复，且不覆盖外部或新选择。
            if (released || EventSystem.current != system || system.currentSelectedGameObject) return;
            foreach (var state in snapshot)
            {
                if (!Active(state) || state.Blocked || GetFocusedPanel(state.Root) != state.Panel) continue;
                var input = state.Input;
                var selection = input.Selection;
                if (selection && input.SelectionSystem == system && input.Owns(selection) && selection.activeInHierarchy &&
                    selection.TryGetComponent<Selectable>(out var selectable) && selectable.isActiveAndEnabled && selectable.IsInteractable())
                {
                    input.Selection = null;
                    system.SetSelectedGameObject(selection);
                    return;
                }
                if ((cleared || state.Modal) && state.Modal)
                {
                    selection = input.FirstSelection();
                    if (selection) { system.SetSelectedGameObject(selection); return; }
                }
            }
        }

        internal static void ActivateView(View view)
        {
            if (view is Panel panel) { panel.RestoreCanvas(); _instance?.Reconcile(); }
        }
        internal static void DeactivateView(View view)
        {
            if (view is Panel panel && panel && !panel.gameObject.activeInHierarchy) _instance?.Reconcile();
        }
        internal static void ReleaseView(View view)
        {
            var manager = _instance;
            if (manager == null || !(view is Panel panel)) return;
            if (manager.states.TryGetValue(panel, out var state))
            {
                state.Input.Restore(true);
                manager.states.Remove(panel);
                var system = EventSystem.current;
                if (system && state.Input.Owns(system.currentSelectedGameObject))
                {
                    if (system.alreadySelecting)
                    {
                        manager.pendingSelectionRelease = state;
                        manager.releasedSelection = system.currentSelectedGameObject;
                        manager.releasedSelectionSystem = system;
                    }
                    else system.SetSelectedGameObject(null);
                }
            }
            manager.Reconcile();
        }

        internal static bool IsVisible(Panel panel) => _instance != null && panel &&
            _instance.states.TryGetValue(panel, out var state) && _instance.Active(state) && state.Visible;
        internal static bool IsInputBlocked(Panel panel) => !IsVisible(panel) || _instance.states[panel].Blocked;

        private static Panel Query(Canvas root, bool modal, bool focus, UIType? type = null)
        {
            var manager = _instance;
            if (manager == null || (!root && !type.HasValue)) return null;
            PanelState top = null;
            foreach (var state in manager.states.Values)
            {
                if (!manager.Active(state) || !state.Panel.IsMyselfAddCanvas || state.Panel.PolicyType == PanelType.Overlay ||
                    !state.Visible || (root && state.Root != root) || (type.HasValue && state.Panel.UIType != type.Value) ||
                    (modal && !state.Modal) || (focus && state.Blocked)) continue;
                if (top == null || Compare(state, top) > 0) top = state;
            }
            return top?.Panel;
        }

        /// <summary>指定实际根的普通窗口 Top，不创建 Manager 或宿主。</summary>
        public static Panel GetTopPanel(Canvas root) => Query(root, false, false);
        /// <summary>兼容寿命分类查询；多根业务应使用明确根重载。</summary>
        public static Panel GetTopPanel(UIType type) => Query(null, false, false, type);
        public static Panel GetModalPanel(Canvas root) => Query(root, true, false);
        public static Panel GetFocusedPanel(Canvas root) => Query(root, false, true);

        internal static Panel GetLayerModal(Canvas root, int layer)
        {
            if (_instance == null) return null;
            PanelState top = null;
            foreach (var state in _instance.states.Values)
                if (_instance.Active(state) && state.Visible && state.Root == root && state.Modal &&
                    state.Panel.WindowLayer == layer && (top == null || Compare(state, top) > 0)) top = state;
            return top?.Panel;
        }

        public static void GetAllPanels(UIType type, List<Panel> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();
            var manager = _instance;
            if (manager == null) return;
            foreach (var state in manager.states.Values)
                if (manager.Current(state) && state.Panel.UIType == type && state.Panel.IsMyselfAddCanvas) results.Add(state.Panel);
        }

        internal static void LateUpdate() => _instance?.UpdatePanelQueueCore();
        private void UpdatePanelQueueCore()
        {
            if (released) return;
            RefreshInput();
            inputSnapshot.Clear();
            inputSnapshot.AddRange(states.Values);
            RefreshPanelInput(inputSnapshot);
            foreach (var state in states.Values)
                if (!Current(state) || state.Active != Active(state) || state.Root != ResolveRoot(state.Panel) ||
                    (!state.Panel.IsMyselfAddCanvas && state.Panel.Canvas != state.Panel.FindBorrowedCanvas()) ||
                    state.Scene != state.Panel.gameObject.scene.handle)
                {
                    Reconcile();
                    return;
                }
        }
    }
}
