using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core
{
    public partial class PanelManager
    {
        private static readonly Unity.Profiling.ProfilerMarker ReconcileProfile = new Unity.Profiling.ProfilerMarker("YL.UI.Reconcile");
        private List<KeyValuePair<PanelState, int>> BuildOrders(Panel changed = null, PanelConfig? settings = null, long? accepted = null)
        {
            var list = new List<PanelState>();
            foreach (var state in states.Values) if (Current(state)) list.Add(state);
            PanelConfig Settings(PanelState state) => state.Panel == changed && settings.HasValue ? settings.Value : state.Panel.EffectiveSettings;
            long Sequence(PanelState state) => state.Panel == changed && accepted.HasValue ? accepted.Value : state.Sequence;
            foreach (var state in list) { state.Root.ValidateRoot(); state.Root.GetLayer(Settings(state).Layer); }
            list.Sort((a, b) => a.Root != b.Root ? a.Root.GetInstanceID().CompareTo(b.Root.GetInstanceID()) : PanelConfig.Compare(Settings(a), Sequence(a), Settings(b), Sequence(b)));
            var orders = new List<KeyValuePair<PanelState, int>>(list.Count);
            UIRootContext root = null;
            long order = 0;
            foreach (var state in list)
            {
                if (state.Root != root) { root = state.Root; order = root.Canvas.sortingOrder; }
                order += Settings(state).Modal ? 2 : 1;
                if (order < short.MinValue || order > short.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(settings), "该根的 Panel/模态遮罩耗尽 Canvas 排序空间。");
                orders.Add(new KeyValuePair<PanelState, int>(state, (int)order));
            }
            return orders;
        }

        private void ValidateOpen(UIRootContext root, PanelConfig settings, Panel existing = null)
        {
            settings.Validate();
            root.ValidateRoot();
            root.GetLayer(settings.Layer);
            if (existing) { BuildOrders(existing, settings); return; }
            long count = settings.Modal ? 2 : 1;
            foreach (var state in states.Values)
                if (Current(state) && state.Root == root) count += state.Panel.EffectiveSettings.Modal ? 2 : 1;
            if (root.Canvas.sortingOrder + count > short.MaxValue || root.Canvas.sortingOrder < short.MinValue)
                throw new ArgumentOutOfRangeException(nameof(settings), "该根的 Panel/模态遮罩耗尽 Canvas 排序空间。");
        }

        internal static void Prepare(Panel panel)
        {
            var manager = Instance;
            PanelInputState.ValidateHierarchy(panel);
            manager.ValidateOpen(panel.Root, panel.EffectiveSettings);
            var operation = panel.InitialOpen;
            var state = new PanelState { Panel = panel, Session = panel.Session, Root = panel.Root,
                Sequence = operation != null ? operation.Sequence : checked(++manager.sequence),
                IntentSequence = operation?.Sequence ?? 0, Source = operation?.Prefab, Input = new PanelInputState(panel) };
            if (operation?.Name != null) state.SourceNames.Add(operation.Name);
            manager.states.Add(panel, state);
            manager.registeredRoots.Add(state.Root);
            manager.Reconcile(panel);
            if (manager.Current(state)) panel.NotifySortingOrder();
        }

        private static bool TryDisplay(Panel panel, out PanelManager manager, out PanelState state)
        {
            panel.EnsureDisplayAllowed();
            manager = _instance;
            state = null;
            return manager != null && manager.states.TryGetValue(panel, out state) && manager.Current(state);
        }
        internal static void SetVisible(Panel panel, bool value)
        {
            if (!TryDisplay(panel, out var manager, out var state)) return;
            manager.BuildOrders();
            state.WantsVisible = value;
            manager.Reconcile(mutationTarget: panel);
        }
        internal static void BringToFront(Panel panel)
        {
            if (!TryDisplay(panel, out var manager, out var state)) return;
            long next = checked(manager.sequence + 1);
            manager.BuildOrders(panel, accepted: next);
            manager.sequence = next;
            state.Sequence = next;
            manager.Reconcile(mutationTarget: panel);
        }
        internal static void UpdateDisplay(Panel panel, PanelOverrides changes)
        {
            panel.EnsureDisplayAllowed();
            changes.ValidateDisplay();
            if (!TryDisplay(panel, out var manager, out var state)) return;
            var settings = changes.ApplyTo(panel.EffectiveSettings);
            manager.BuildOrders(panel, settings);
            if (!manager.CommitDisplay(state, settings, state.WantsVisible, null)) return;
            manager.Reconcile(mutationTarget: panel);
        }
        private bool CommitDisplay(PanelState state, PanelConfig settings, bool visible, long? sequence)
        {
            if (!Current(state)) return false;
            if (state.Panel.EffectiveSettings.Layer != settings.Layer)
            {
                // SetParent(false) 保留局部布局，不把 CoversRoot 当作拉伸指令。
                PanelInputGate.Reparent(state.Panel, state.Root.GetLayer(settings.Layer));
                if (!Current(state)) return false;
            }
            state.Panel.CommitSettings(settings);
            state.WantsVisible = visible;
            if (sequence.HasValue) state.Sequence = sequence.Value;
            return true;
        }

        private void Reconcile(Panel preparing = null, Panel mutationTarget = null)
        {
            using var profile = ReconcileProfile.Auto();
            if (released) return;
            var snapshot = new List<PanelState>(states.Values);
            foreach (var state in snapshot)
            {
                if (!Current(state)) continue;
                if (!state.Root || !state.Root.HasValidStructure || !state.Panel.Canvas ||
                    state.Panel.GetComponentInParent<UIRootContext>(true) != state.Root || !ResourcesValid(state))
                {
                    if (state.Panel.IsInitializing) throw new InvalidOperationException("Panel 准备期间根、Canvas 或资源责任失效。");
                    Notify(state.Panel.Close);
                }
            }
            var orders = BuildOrders();
            var sorting = new List<KeyValuePair<PanelState, int>>();
            foreach (var pair in orders)
            {
                var state = pair.Key;
                if (state.Order != pair.Value) sorting.Add(pair);
                state.Order = pair.Value;
            }
            var visibility = new List<KeyValuePair<PanelState, bool>>();
            foreach (var state in snapshot)
            {
                if (!Current(state)) continue;
                bool active = Active(state), visible = active && state.WantsVisible, blocked = !visible;
                foreach (var upper in snapshot)
                {
                    if (!Active(upper) || !upper.WantsVisible || upper.Root != state.Root || Compare(upper, state) <= 0) continue;
                    if (multiPanelListener && upper.Panel.EffectiveSettings.CoversRoot) visible = false;
                    if (upper.Panel.EffectiveSettings.Modal) blocked = true;
                }
                if (state.Visible != visible) visibility.Add(new KeyValuePair<PanelState, bool>(state, visible));
                state.Active = active;
                state.Visible = visible;
                state.Blocked = blocked || !visible;
                var canvas = state.Panel.Canvas;
                // 首次启用会重建原生 Canvas 关系；显示时先启用再提交，隐藏时在禁绘前保留排序。
                if (visible) canvas.enabled = true;
                if (!Current(state) || state.Visible != visible) continue;
                if (!canvas.overrideSorting) canvas.overrideSorting = true;
                if (!Current(state) || state.Visible != visible) continue;
                if (canvas.sortingLayerID != state.Root.Canvas.sortingLayerID) canvas.sortingLayerID = state.Root.Canvas.sortingLayerID;
                if (!Current(state) || state.Visible != visible) continue;
                if (canvas.sortingOrder != state.Order) canvas.sortingOrder = state.Order;
                if (!Current(state) || state.Visible != visible) continue;
                if (!visible) canvas.enabled = false;
            }
            foreach (var root in new List<UIRootContext>(registeredRoots)) if (root) root.RefreshMasks();
            RefreshInput();
            RefreshPanelInput(snapshot);
            SyncLateSubscription();
            foreach (var pair in sorting)
                if (pair.Key.Panel != preparing && Current(pair.Key) && pair.Key.Order == pair.Value)
                {
                    if (pair.Key.Panel == mutationTarget && pair.Key.Panel.IsInitializing) pair.Key.Panel.NotifySortingOrder();
                    else Notify(pair.Key.Panel.NotifySortingOrder);
                }
            foreach (var pair in visibility)
                if (Current(pair.Key) && pair.Key.Visible == pair.Value)
                {
                    if (pair.Key.Panel == preparing || (pair.Key.Panel == mutationTarget && pair.Key.Panel.IsInitializing)) pair.Key.Panel.NotifyVisible(pair.Value);
                    else Notify(() => pair.Key.Panel.NotifyVisible(pair.Value));
                }
        }
        internal static void ActivateView(View view) { if (view is Panel panel) _instance?.Reconcile(mutationTarget: panel); }
        internal static void DeactivateView(View view) { if (view is Panel panel) _instance?.Reconcile(mutationTarget: panel); }
        internal static void LateUpdate() => _instance?.Reconcile();
    }
}
