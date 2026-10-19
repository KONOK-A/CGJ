using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using YLFramework.Events;
using YLFramework.Core.Singleton;
using UpdateManager = YLFramework.Foundation.UpdateManager;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    /// <summary>协调 UI 请求与显示策略，不拥有 Module，也不维护第二套资源或实例池。</summary>
    public partial class PanelManager
    {
        private static PanelManager _instance;
        private IUnRegister lateSubscription;
        private bool released;
        private bool multiPanelListener = true;
        private GameObject _eventSystem;
        private bool inputConfigurationWarned;
        private PanelState pendingSelectionRelease;
        private GameObject releasedSelection;
        private EventSystem releasedSelectionSystem;
        private long sequence;
        private readonly HashSet<UIRootContext> registeredRoots = new HashSet<UIRootContext>();
        private readonly Dictionary<Panel, PanelState> states = new Dictionary<Panel, PanelState>();

        private sealed class PanelState
        {
            internal Panel Panel;
            internal object Session;
            internal UIRootContext Root;
            internal long Sequence, IntentSequence;
            internal int Order;
            internal bool WantsVisible = true;
            internal bool Active, Visible, Blocked = true;
            internal PanelInputState Input;
            internal GameObject Source;
            internal readonly HashSet<string> SourceNames = new HashSet<string>(StringComparer.Ordinal);
            internal List<CorePrefabLease> SourcePins => Panel.SourcePins;
        }

        static PanelManager()
        {
            SingletonSessionState.RegisterSessionReset(ResetSession);
            Application.quitting += OnExecutionEnded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }
        private static void OnExecutionEnded()
        {
            SingletonSessionState.StopExecution();
            ResetSession();
        }
#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) OnExecutionEnded();
        }
#endif
        private PanelManager() { }
        internal static PanelManager Instance => _instance ?? (_instance = new PanelManager());
        private static void ResetSession()
        {
            var manager = _instance;
            _instance = null;
            if (manager != null)
            {
                manager.released = true;
                manager.lateSubscription?.UnRegister();
                manager.lateSubscription = null;
                manager.ReleaseOwnedInput();
                foreach (var state in new List<PanelState>(manager.states.Values))
                {
                    state.Input.Restore(true);
                }
                manager.states.Clear();
                manager.registeredRoots.Clear();
            }
            onCreateCanvas = null;
        }

        private void SyncLateSubscription()
        {
            if (released || (states.Count == 0 && registeredRoots.Count == 0))
            { lateSubscription?.UnRegister(); lateSubscription = null; return; }
            if (lateSubscription == null)
                lateSubscription = UpdateManager.AddLateUpdateEvent(() => { if (!released && ReferenceEquals(_instance, this)) Reconcile(); });
        }

        /// <summary>是否启用同根完整覆盖停绘。关闭不影响 Modal 阻挡或 View 会话。</summary>
        public static bool MultiPanelListener
        {
            get => _instance == null || _instance.multiPanelListener;
            set { var manager = Instance; manager.multiPanelListener = value; manager.Reconcile(); }
        }
        /// <summary>宿主默认根首次准备完成的通知；自定义根登记不会触发，不改变根寿命。</summary>
        public static Action<Canvas> onCreateCanvas;

        private bool Current(PanelState state) => !released && state.Panel && state.Session != null &&
            ReferenceEquals(state.Panel.Session, state.Session) && !state.Panel.IsRemoving && state.Panel.Module != null &&
            state.Panel.Module.State != ModuleState.Closing && states.TryGetValue(state.Panel, out var current) && ReferenceEquals(current, state);
        private bool Active(PanelState state) => Current(state) && state.Panel.IsLoaded && state.Panel.isActiveAndEnabled &&
            state.Root && state.Root.HasValidStructure && state.Root.IsAvailable && ResourcesValid(state);
        private static int Compare(PanelState a, PanelState b)
            => PanelConfig.Compare(a.Panel.EffectiveSettings, a.Sequence, b.Panel.EffectiveSettings, b.Sequence);
        private static void Notify(Action action)
        {
            try { action(); }
            catch (Exception error) { Debug.LogException(error); }
        }
        internal static void ReleaseSources(List<CorePrefabLease> sources)
        {
            if (sources == null) return;
            var owned = sources.ToArray();
            sources.Clear();
            var errors = new List<Exception>();
            foreach (var source in owned) { try { source.Dispose(); } catch (Exception error) { errors.Add(error); } }
            ViewManager.ThrowCleanupErrors(errors);
        }
    }
}
