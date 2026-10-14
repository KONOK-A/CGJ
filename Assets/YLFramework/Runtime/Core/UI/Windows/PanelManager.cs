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

    public partial class PanelManager
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

    }
}
