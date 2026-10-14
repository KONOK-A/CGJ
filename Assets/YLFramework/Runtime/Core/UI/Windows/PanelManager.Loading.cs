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

    }
}
