using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>窗口定义目录；活跃会话与在途请求只查询 Module 的既有 View 索引。</summary>
    public sealed class WindowCatalog
    {
        private readonly Dictionary<string, WindowDefinition> definitions = new Dictionary<string, WindowDefinition>(StringComparer.Ordinal);

        public void Register(WindowDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var snapshot = definition.Snapshot();
            definitions.Add(snapshot.Key, snapshot);
        }

        /// <summary>唯一窗口更新当前会话并置顶；同 key 在途或初始化重入明确拒绝。参数由本次入口释放。</summary>
        public T Open<T>(Module owner, string key, Transform parent = null, LoadViewArgs args = null) where T : Panel
        {
            args = args ?? LoadViewArgs.New();
            args.Claim();
            try
            {
                var definition = Resolve(owner, key, ref parent, args);
                var existing = Find<T>(owner, definition, args.WindowContext);
                if (existing) { Update(existing, args); return existing; }
                return ViewManager.LoadWindowClaimed<T>(owner, definition, parent, args);
            }
            finally { args.Release(); }
        }

        /// <summary>直接 Prefab 也使用同一个 View 请求。拒绝同 key 在途，不共享 Abort 权限。</summary>
        public LoadPanelRequest<T> OpenAsync<T>(Module owner, string key, Transform parent = null, LoadViewArgs args = null) where T : Panel
        {
            args = args ?? LoadViewArgs.New();
            args.Claim();
            try
            {
                var definition = Resolve(owner, key, ref parent, args);
                var existing = Find<T>(owner, definition, args.WindowContext);
                if (existing)
                {
                    Update(existing, args);
                    args.Release();
                    return new LoadPanelRequest<T>(LoadViewRequest<T>.FromResult(existing));
                }
                var child = ViewManager.StartAsync<T>(owner, definition.AssetName ?? definition.Prefab.name,
                    parent, args, null, true, definition.UIType, definition.Prefab);
                return new LoadPanelRequest<T>(child);
            }
            catch { args.Release(); throw; }
        }

        private WindowDefinition Resolve(Module owner, string key, ref Transform parent, LoadViewArgs args)
        {
            ViewManager.EnsureOwner(owner);
            if (owner.viewTransactions != 0) throw new InvalidOperationException("Window entry cannot reenter an unfinished View transaction.");
            if (key == null || !definitions.TryGetValue(key, out var definition)) throw new KeyNotFoundException("Window definition not registered: " + key);
            if (!ReferenceEquals(parent, null) && !parent) throw new ArgumentException("Window parent has been destroyed.");
            parent = PanelManager.GetParent(owner, parent, definition.UIType);
            var context = PanelManager.ResolveParentRoot(parent);
            if (!context) throw new InvalidOperationException("Window parent requires a Canvas context.");
            var root = context ? context.GetComponent<UIRootContext>() : null;
            if (root) parent = root.GetLayer(definition.Layer);
            else if (definition.Layer != 0) throw new InvalidOperationException("Window layers require a configured UIRootContext.");
            args.Window = definition;
            args.WindowContext = context;
            if (definition.Unique)
                foreach (var pending in owner.pendingViews)
                    if (pending.MatchesWindow(key, context)) throw new InvalidOperationException("Unique window request is already pending: " + key);
            return definition;
        }

        private static T Find<T>(Module owner, WindowDefinition definition, Canvas context) where T : Panel
        {
            if (!definition.Unique) return null;
            foreach (var view in owner.ownedViews.Values)
                if (view is Panel panel && panel && panel.Session != null && !panel.IsRemoving &&
                    panel.Window != null && panel.Window.Key == definition.Key && panel.RootCanvas == context)
                {
                    if (!panel.IsLoaded) throw new InvalidOperationException("Unique window is still initializing.");
                    if (!(panel is T typed)) throw new InvalidCastException("Window key has a different active Panel type.");
                    return typed;
                }
            return null;
        }

        private static void Update(Panel panel, LoadViewArgs args)
        {
            var session = panel.Session;
            panel.gameObject.SetActive(true);
            if (!panel || !ReferenceEquals(panel.Session, session) || !panel.IsLoaded)
                throw new InvalidOperationException("Window session ended during activation.");
            args.BeginRead();
            try { panel.OnWindowUpdated(args); }
            catch (Exception error)
            {
                if (panel && ReferenceEquals(panel.Session, session))
                {
                    panel.DiscardOnExit = true;
                    try { panel.Close(); }
                    catch (Exception cleanupError) { StartUpModuleRequest.AttachCleanupError(error, cleanupError); }
                }
                throw;
            }
            finally { args.EndRead(); }
            if (!panel || !ReferenceEquals(panel.Session, session) || !panel.IsLoaded)
                throw new InvalidOperationException("Window session ended during reopen.");
            PanelManager.BringToFront(panel);
            if (!panel || !ReferenceEquals(panel.Session, session) || !panel.IsLoaded)
                throw new InvalidOperationException("Window session ended during sorting notification.");
        }
    }
}
