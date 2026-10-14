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
    public partial class PanelManager
    {
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

        internal static void ActivateView(View view)
        {
            if (view is Panel panel) { panel.RestoreCanvas(); _instance?.Reconcile(); }
        }
        internal static void DeactivateView(View view)
        {
            if (view is Panel panel && panel && !panel.gameObject.activeInHierarchy) _instance?.Reconcile();
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
