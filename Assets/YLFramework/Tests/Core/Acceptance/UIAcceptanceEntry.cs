using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace YLFramework.Core.Tests
{
    /// <summary>真实 Play / Mono Player 共用的定向验收入口；不参与框架分发。</summary>
    public sealed class UIAcceptanceEntry : MonoBehaviour
    {
        public GameObject RootPrefab;
        public GameObject ColorPrefab;
        public static int Round;
        public static string Result;
        private static GameObject previousRoot;
        private static ColorSelectorWindow previousWindow;
        private static LoadPanelRequest<ColorSelectorWindow> previousPending;
        private static UITipHandle previousTip;
        private static int oldHookCalls;
        private static int oldColorCalls;
        private static int oldTipCalls;
        private static int expectedTipCalls;
        private static bool exitAcquisitionRejected;
        private readonly AcceptanceModule owner = new AcceptanceModule();
        private WindowCatalog catalog;
        private GameObject rootObject;
        private UIRootContext root;
        private ColorSelectorWindow first;
        private ColorSelectorWindow second;
        private UITipsPresenter tips;
        private Mouse mouse;
        private Keyboard keyboard;
        private InputActionAsset actions;
        private UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? previousDevices;
        private int changes;

        private IEnumerator Start()
        {
            Result = "RUNNING";
            var routine = Run();
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception error)
                {
                    Result = "FAILED: " + error;
                    Debug.LogError("UI34 " + Result);
                    ReleaseDevices();
                    if (!Application.isEditor) Application.Quit(1);
                    yield break;
                }
                if (!next) break;
                yield return routine.Current;
            }
        }

        private IEnumerator Run()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-ui34-game") >= 0)
            {
                DontDestroyOnLoad(gameObject);
                var game = GameAcceptanceFlow.Run(ColorPrefab);
                try { while (game.MoveNext()) yield return game.Current; }
                finally { (game as IDisposable)?.Dispose(); }
                Result = "GAME_PLAYER_PASS";
                Debug.Log("UI34 " + Result);
                Application.Quit(0);
                yield break;
            }
            Round++;
            Check(!previousRoot && !previousWindow, "previous Play objects destroyed");
            Check(PanelManager.onCreateCanvas == null, "previous static canvas hook cleared");
            Check(ModuleManager.GetModule(typeof(AcceptanceModule)) == null, "previous owner not registered");
            if (previousPending != null)
            {
                Check(previousPending.isDone && previousPending.Status == LoadViewStatus.Invalidated, "exit invalidates pending request");
                Check(previousTip.IsDone && oldTipCalls == expectedTipCalls, "exit completes old Tips exactly once");
                Check(oldHookCalls == 0 && oldColorCalls == 0, "old closures never invoked by next Play");
                Check(exitAcquisitionRejected, "pending completion cannot acquire UI during exit");
            }
            var startup = ModuleManager.StartUpModule(owner);
            yield return startup;
            Check(startup.Exception == null && owner.State == ModuleState.Running, "explicit owner started");
            rootObject = Instantiate(RootPrefab);
            rootObject.name = "UI34.OwnedRoot";
            root = rootObject.GetComponentInChildren<UIRootContext>();
            tips = rootObject.GetComponentInChildren<UITipsPresenter>();
            Check(root && tips, "formal template root and Tips bindings");
            root.ConfigureCanvas(RenderMode.ScreenSpaceOverlay);
            root.ConfigureResolution(new Vector2(1280, 720), false, 0.5f);
            catalog = new WindowCatalog();
            catalog.Register(new WindowDefinition("color", ColorPrefab));
            catalog.Register(new WindowDefinition("second", ColorPrefab));
            catalog.Register(new WindowDefinition("modal", ColorPrefab) { Modal = true, Layer = 1 });
            first = catalog.Open<ColorSelectorWindow>(owner, "color", root.transform,
                ColorSelectorWindow.Arguments(new Color(1, 0, 0, 0.4f), _ => changes++));
            second = catalog.Open<ColorSelectorWindow>(owner, "second", root.transform, ColorSelectorWindow.Arguments(Color.blue));
            first.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 0);
            second.GetComponent<RectTransform>().anchoredPosition = new Vector2(250, 0);
            Check(first != second && first.CurrentColor.r == 1 && second.CurrentColor.b == 1, "independent initial colors");
            var system = EventSystem.current;
            yield return null;
            Check(system && system.currentInputModule is InputSystemUIInputModule, "Both selects real InputSystemUIInputModule");
            var input = (InputSystemUIInputModule)system.currentInputModule;
            // 仅过滤本验收场景自建输入模块的非持久 runtime actions；不触碰原游戏输入。
            actions = input.actionsAsset;
            mouse = InputSystem.AddDevice<Mouse>("UI34.Mouse");
            keyboard = InputSystem.AddDevice<Keyboard>("UI34.Keyboard");
            previousDevices = actions.devices;
            actions.devices = new InputDevice[] { mouse, keyboard };
            yield return null;
            Canvas.ForceUpdateCanvases();
            var selector = first.GetComponentInChildren<ColorSelector>();
            var wheel = selector.GetComponent<RectTransform>();
            QueueMouse(Point(wheel, new Vector2(-0.44f, 0.7621024f)), false);
            yield return null;
            QueueMouse(Point(wheel, new Vector2(-0.44f, 0.7621024f)), true);
            yield return null;
            Check(first.CurrentColor.g > 0.99f && changes == 1, "module-dispatched Overlay hue selection");
            root.ConfigureResolution(new Vector2(1000, 800), false, 0.25f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            QueueMouse(Point(wheel, new Vector2(-0.44f, -0.7621024f)), true);
            yield return null;
            Check(first.CurrentColor.b > 0.99f && Mathf.Abs(first.CurrentColor.a - 0.4f) < 0.001f && second.CurrentColor == Color.blue,
                "drag after adaptation preserves alpha and other instance");
            QueueMouse(Point(wheel, new Vector2(-0.44f, -0.7621024f)), false);
            yield return null;
            first.SortingOrder = 100;
            var focus = first.GetComponentInChildren<Button>();
            Check(PanelManager.GetFocusedPanel(root.Canvas) == first, "focus restoration target is the current navigable window");
            system.SetSelectedGameObject(focus.gameObject);
            var modal = catalog.Open<ColorSelectorWindow>(owner, "modal", root.transform, ColorSelectorWindow.Arguments(Color.white));
            yield return null;
            Check(first.IsInputBlocked && !focus.IsInteractable() && system.currentSelectedGameObject != focus.gameObject, "modal blocks navigation and focus");
            Check(UIPointer.CheckPositionOnUI(new Vector2(5, 5)), "formal mask blocks outside content");
            modal.Close();
            Check(!first.IsInputBlocked && focus.IsInteractable() && system.currentSelectedGameObject == focus.gameObject, "modal restores valid focus");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(first.Module == null, "real keyboard Submit closes focused window");
            int oldChanges = changes;
            first = catalog.Open<ColorSelectorWindow>(owner, "color", root.transform, ColorSelectorWindow.Arguments(Color.red));
            Check(first.CurrentColor == Color.red, "new session resets initial color");
            Check(changes == oldChanges, "initial synchronization does not invoke old callback");
            first.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 0);
            var camera = rootObject.GetComponentInChildren<Camera>(true);
            camera.gameObject.SetActive(true);
            root.ConfigureCanvas(RenderMode.ScreenSpaceCamera, camera, 5);
            yield return null;
            Canvas.ForceUpdateCanvases();
            wheel = first.GetComponentInChildren<ColorSelector>().GetComponent<RectTransform>();
            QueueMouse(Point(wheel, new Vector2(-0.44f, 0.7621024f)), false);
            yield return null;
            QueueMouse(Point(wheel, new Vector2(-0.44f, 0.7621024f)), true);
            yield return null;
            QueueMouse(Point(wheel, new Vector2(-0.44f, 0.7621024f)), false);
            yield return null;
            Check(first.CurrentColor.g > 0.99f && changes == oldChanges, "Camera input on reused session has no old callback");
            CheckRender(first, second);
            var tip = tips.Show(owner, "UI34 Tips", duration: 0.05f, fade: false);
            var deadline = Time.realtimeSinceStartup + 3;
            while (!tip.IsDone && Time.realtimeSinceStartup < deadline) yield return null;
            Check(tip.IsDone && tip.Result == UITipResult.Completed && tips.ActiveCount == 0, "formal Tips completes and returns owned item");
            var close = first.GetComponentInChildren<Button>();
            var closePoint = RectTransformUtility.WorldToScreenPoint(root.UICamera, close.transform.position);
            QueueMouse(closePoint, false);
            yield return null;
            QueueMouse(closePoint, true);
            yield return null;
            QueueMouse(closePoint, false);
            yield return null;
            Check(first.Module == null, "module-dispatched close button");
            var pending = catalog.OpenAsync<ColorSelectorWindow>(owner, "color", root.transform);
            pending.Abort();
            Check(pending.Status == LoadViewStatus.Canceled, "pending Abort stable terminal state");
            pending = catalog.OpenAsync<ColorSelectorWindow>(owner, "color", root.transform);
            owner.Close();
            Check(pending.Status == LoadViewStatus.Invalidated && second.Module == null, "owner exit invalidates pending and both window sessions");
            var restart = ModuleManager.StartUpModule(owner);
            yield return restart;
            Check(owner.State == ModuleState.Running, "owner restarts without stale key");
            first = catalog.Open<ColorSelectorWindow>(owner, "color", root.transform, ColorSelectorWindow.Arguments(Color.magenta));
            Check(first.CurrentColor == Color.magenta, "window safely rebuilt after owner restart");
            ReleaseDevices();
            Result = "READY round=" + Round;
            Debug.Log("UI34 " + Result + " platform=" + Application.platform + " input=InputSystemUIInputModule");
            if (!Application.isEditor)
            {
                owner.Close();
                Destroy(rootObject);
                yield return null;
                yield return null;
                Check(!rootObject && !EventSystem.current, "last owned root releases input host in Player");
                var lastStartup = ModuleManager.StartUpModule(owner);
                yield return lastStartup;
                var quitting = catalog.OpenAsync<ColorSelectorWindow>(owner, "color");
                Check(!quitting.isDone, "Player quit probe is genuinely pending");
                quitting.AddCompleteEvent(request =>
                {
                    Check(request.Status == LoadViewStatus.Invalidated, "actual Player quit invalidates pending");
                    Result = "PLAYER_PASS";
                    Debug.Log("UI34 " + Result);
                    Debug.Log("UI34 QUIT_PENDING_PASS");
                });
                Result = "PLAYER_READY_FOR_QUIT";
                Debug.Log("UI34 " + Result);
                Application.Quit(0);
            }
        }

        /// <summary>在 Editor 已暂停的真实 Play 内调用，留下真正在途请求供退出路径处理。</summary>
        public void PrepareExit()
        {
            Check(Application.isPlaying && first.IsLoaded, "exit probe requires live session");
            first.Close();
            exitAcquisitionRejected = false;
            previousPending = catalog.OpenAsync<ColorSelectorWindow>(owner, "color", root.transform,
                ColorSelectorWindow.Arguments(Color.red, _ => oldColorCalls++));
            Check(!previousPending.isDone, "exit probe is genuinely pending");
            previousPending.AddCompleteEvent(_ =>
            {
                try { catalog.Open<ColorSelectorWindow>(owner, "color", root.transform); }
                catch (InvalidOperationException error) { exitAcquisitionRejected = error.Message == "Views cannot be acquired outside an active execution session."; }
            });
            previousTip = tips.Show(owner, "Exit-owned tip", duration: 60, completed: _ => oldTipCalls++);
            expectedTipCalls = oldTipCalls + 1;
            previousRoot = rootObject;
            previousWindow = second;
            PanelManager.onCreateCanvas = _ => oldHookCalls++;
            Debug.Log("UI34 EXIT_ARMED round=" + Round);
        }

        public static string VerifyExit()
        {
            Check(previousPending.isDone && previousPending.Status == LoadViewStatus.Invalidated, "real Play exit terminates pending");
            Check(previousTip.IsDone && oldTipCalls == expectedTipCalls, "real Play exit terminates Tips exactly once");
            Check(!previousRoot && !previousWindow && PanelManager.onCreateCanvas == null, "real Play exit destroys own root and clears hook");
            Check(exitAcquisitionRejected, "real Play exit rejects callback acquisition");
            return "EXIT_PASS round=" + Round;
        }

        private Vector2 Point(RectTransform wheel, Vector2 normalized)
            => RectTransformUtility.WorldToScreenPoint(root.UICamera, wheel.TransformPoint(wheel.rect.center + normalized * Mathf.Min(wheel.rect.width, wheel.rect.height) * 0.5f));

        private void QueueMouse(Vector2 position, bool pressed)
            => InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });

        private void CheckRender(ColorSelectorWindow green, ColorSelectorWindow blue)
        {
            var camera = root.UICamera;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                foreach (var window in new[] { green, blue })
                {
                    var wheel = window.GetComponentInChildren<ColorSelector>().GetComponent<RectTransform>();
                    var point = Point(wheel, new Vector2(0, 0.4f));
                    var pixel = texture.GetPixel((int)point.x, (int)point.y);
                    var color = window.CurrentColor;
                    Check(color.g > 0.9f ? pixel.g > pixel.r + 0.1f && pixel.g > pixel.b + 0.1f : pixel.b > pixel.r + 0.1f && pixel.b > pixel.g + 0.1f,
                        "native Camera render matches independent triangle hue: " + pixel);
                }
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                Destroy(texture);
                Destroy(target);
            }
        }

        private void ReleaseDevices()
        {
            if (actions) actions.devices = previousDevices;
            actions = null;
            if (mouse != null) InputSystem.RemoveDevice(mouse);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            mouse = null;
            keyboard = null;
        }

        private void OnDestroy() => ReleaseDevices();
        private static void Check(bool condition, string contract)
        {
            if (!condition) throw new InvalidOperationException("UI34 contract: " + contract);
            Debug.Log("UI34 CHECK " + contract);
        }

        private sealed class AcceptanceModule : Module
        {
            public override void OnStart() { }
        }
    }
}
