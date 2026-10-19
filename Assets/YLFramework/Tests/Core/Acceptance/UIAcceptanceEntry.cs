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
    public sealed class UIPanelAcceptancePointerProbe : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        public readonly int[] Clicks = new int[3];
        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData data) => Clicks[(int)data.button]++;
    }

    public sealed class UIPanelAcceptanceDragProbe : MonoBehaviour, UnityEngine.EventSystems.IDragHandler
    {
        public int Drags;
        public void OnDrag(UnityEngine.EventSystems.PointerEventData data) => Drags++;
    }

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
            var routines = new System.Collections.Generic.Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count != 0)
            {
                bool next;
                object current;
                try
                {
                    var routine = routines.Peek();
                    next = routine.MoveNext();
                    current = next ? routine.Current : null;
                }
                catch (Exception error)
                {
                    Result = "FAILED: " + error;
                    Debug.LogError("UI34 " + Result);
                    ReleaseDevices();
                    if (!Application.isEditor) Application.Quit(1);
                    yield break;
                }
                if (!next) { routines.Pop(); continue; }
                if (current is IEnumerator nested && !(current is CustomYieldInstruction)) routines.Push(nested);
                else yield return current;
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
            root.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.Canvas.worldCamera = null;
            root.GetComponent<AutomaticAdaptationCanvasScaler>().Configure(new Vector2(1280, 720), false, 0.5f);
            first = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab,
                ColorSelectorWindow.Arguments(new Color(1, 0, 0, 0.4f), _ => changes++), root, Multiple);
            second = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.blue), root, Multiple);
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
            root.GetComponent<AutomaticAdaptationCanvasScaler>().Configure(new Vector2(1000, 800), false, 0.25f);
            yield return null;
            Canvas.ForceUpdateCanvases();
            QueueMouse(Point(wheel, new Vector2(-0.44f, -0.7621024f)), true);
            yield return null;
            Check(first.CurrentColor.b > 0.99f && Mathf.Abs(first.CurrentColor.a - 0.4f) < 0.001f && second.CurrentColor == Color.blue,
                "drag after adaptation preserves alpha and other instance");
            QueueMouse(Point(wheel, new Vector2(-0.44f, -0.7621024f)), false);
            yield return null;
            first.UpdateDisplay(new PanelOverrides { Priority = 100 });
            var focus = first.GetComponentInChildren<Button>();
            Check(PanelManager.GetFocusedPanel(root) == first, "focus restoration target is the current navigable window");
            system.SetSelectedGameObject(focus.gameObject);
            var modal = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.white), root,
                new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple, Modal = true, Layer = 1 });
            yield return null;
            Check(first.IsInputBlocked && !focus.IsInteractable() && system.currentSelectedGameObject != focus.gameObject, "modal blocks navigation and focus");
            Check(UIPointer.CheckPositionOnUI(new Vector2(5, 5)), "formal mask blocks outside content");
            modal.Close();
            Check(!first.IsInputBlocked && focus.IsInteractable() && system.currentSelectedGameObject == focus.gameObject, "modal restores valid focus");
            yield return VerifyNativeCaptureGate();
            system.SetSelectedGameObject(focus.gameObject);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Check(first.Module == null, "real keyboard Submit closes focused window");
            int oldChanges = changes;
            first = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.red), root, Multiple);
            Check(first.CurrentColor == Color.red, "new session resets initial color");
            Check(changes == oldChanges, "initial synchronization does not invoke old callback");
            first.GetComponent<RectTransform>().anchoredPosition = new Vector2(-250, 0);
            var camera = rootObject.GetComponentInChildren<Camera>(true);
            camera.gameObject.SetActive(true);
            root.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            root.Canvas.worldCamera = camera;
            root.Canvas.planeDistance = 5;
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
            var closePoint = RectTransformUtility.WorldToScreenPoint(EventCamera, close.transform.position);
            QueueMouse(closePoint, false);
            yield return null;
            QueueMouse(closePoint, true);
            yield return null;
            QueueMouse(closePoint, false);
            yield return null;
            Check(first.Module == null, "module-dispatched close button");
            var pending = owner.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab, root: root, overrides: Multiple);
            pending.Abort();
            Check(pending.Status == LoadViewStatus.Canceled, "pending Abort stable terminal state");
            pending = owner.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab, root: root, overrides: Multiple);
            owner.Close();
            Check(pending.Status == LoadViewStatus.Invalidated && second.Module == null, "owner exit invalidates pending and both window sessions");
            var restart = ModuleManager.StartUpModule(owner);
            yield return restart;
            Check(owner.State == ModuleState.Running, "owner restarts without stale key");
            first = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.magenta), root, Multiple);
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
                var quitting = owner.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab, overrides: Multiple);
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
            previousPending = owner.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab,
                ColorSelectorWindow.Arguments(Color.red, _ => oldColorCalls++), root, Multiple);
            Check(!previousPending.isDone, "exit probe is genuinely pending");
            previousPending.AddCompleteEvent(_ =>
            {
                try { owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, root: root, overrides: Multiple); }
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
            => RectTransformUtility.WorldToScreenPoint(EventCamera, wheel.TransformPoint(wheel.rect.center + normalized * Mathf.Min(wheel.rect.width, wheel.rect.height) * 0.5f));
        private Camera EventCamera => root.Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.Canvas.worldCamera;
        private static PanelOverrides Multiple => new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple };

        // 同一个入口在 Editor 和 Windows64 IL2CPP 中运行；私有指针布局不匹配必须明确失败。
        private IEnumerator VerifyNativeCaptureGate()
        {
            var container = new GameObject("UIAcceptance.NativeLayout", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            container.transform.SetParent(first.transform, false);
            ((RectTransform)container.transform).sizeDelta = new Vector2(180, 90);
            var layout = container.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childScaleWidth = layout.childScaleHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter;
            var control = new GameObject("UIAcceptance.NativeCapture", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup), typeof(UIPanelAcceptancePointerProbe));
            control.transform.SetParent(container.transform, false);
            var rect = (RectTransform)control.transform;
            rect.sizeDelta = new Vector2(120, 70);
            rect.localScale = new Vector3(.9f, 1.1f, 1);
            var business = control.GetComponent<CanvasGroup>();
            business.alpha = .65f;
            var probe = control.GetComponent<UIPanelAcceptancePointerProbe>();
            yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
            Check(rect.sizeDelta == new Vector2(120, 70) && control.GetComponents<CanvasGroup>().Length == 1,
                "native layout and readonly driven getter preserve the business control");
            var point = RectTransformUtility.WorldToScreenPoint(EventCamera, rect.position);
            for (int button = 0; button < 3; button++)
            {
                QueueNativeButton(point, (MouseButton)button, false); yield return null;
                QueueNativeButton(point, (MouseButton)button, true); yield return null;
                var modal = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.white), root,
                    new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple, Modal = true, Layer = 1 });
                yield return null;
                Check(control.GetComponent<Button>().interactable && control.GetComponent<Button>().enabled, "gate preserves business control fields");
                Check(business.interactable && business.blocksRaycasts && !business.ignoreParentGroups && business.alpha == .65f,
                    "gate preserves the original business CanvasGroup");
                Check(!control.GetComponent<Button>().IsInteractable(), "native selectable is blocked without disabling it");
                modal.Close();
                QueueNativeButton(point, (MouseButton)button, false); yield return null;
                Check(probe.Clicks[button] == 0, "old physical button cannot click after modal release: " + button);
                QueueNativeButton(point, (MouseButton)button, true); yield return null;
                QueueNativeButton(point, (MouseButton)button, false); yield return null;
                Check(probe.Clicks[button] == 1, "a fresh button gesture works: " + button);
            }
            Destroy(container);
            yield return null;
            foreach (var movement in new[] { ScrollRect.MovementType.Elastic, ScrollRect.MovementType.Clamped })
            {
                var viewport = new GameObject("UIAcceptance.NativeScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(UIPanelAcceptanceDragProbe));
                viewport.transform.SetParent(first.transform, false);
                ((RectTransform)viewport.transform).sizeDelta = new Vector2(220, 220);
                var content = new GameObject("Content", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                var contentRect = (RectTransform)content.transform;
                contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = Vector2.up;
                contentRect.sizeDelta = new Vector2(800, 800);
                var scroll = viewport.GetComponent<ScrollRect>();
                scroll.content = contentRect; scroll.viewport = (RectTransform)viewport.transform;
                scroll.movementType = movement; scroll.inertia = true;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                point = RectTransformUtility.WorldToScreenPoint(EventCamera, viewport.transform.position);
                QueueNativeButton(point, MouseButton.Left, false); yield return null;
                QueueNativeButton(point, MouseButton.Left, true); yield return null;
                QueueNativeButton(point + new Vector2(-30, 30), MouseButton.Left, true); yield return null;
                QueueNativeButton(point + new Vector2(-70, 70), MouseButton.Left, true); yield return null;
                yield return null; // 由真实 LateUpdate 采样速度，不直接赋值或调用内部更新。
                Check(contentRect.anchoredPosition.sqrMagnitude > 0 && scroll.velocity.sqrMagnitude > 0, "native drag and inertia are exercised");
                if (movement == ScrollRect.MovementType.Elastic)
                { QueueNativeButton(point + new Vector2(120, -120), MouseButton.Left, true); yield return null; }
                var modal = owner.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.white), root,
                    new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple, Modal = true, Layer = 1 });
                var stopped = contentRect.anchoredPosition;
                var dragProbe = viewport.GetComponent<UIPanelAcceptanceDragProbe>();
                int stoppedDrags = dragProbe.Drags;
                Check(scroll.velocity == Vector2.zero && scroll.enabled && scroll.inertia, "cancel stops inertia without disabling business state");
                yield return null; yield return null;
                Check(dragProbe.Drags == stoppedDrags, "blocked scroll cannot receive the old drag for " + movement);
                if (movement == ScrollRect.MovementType.Clamped) Check(contentRect.anchoredPosition == stopped, "clamped content has no remaining inertia");
                modal.Close();
                QueueNativeButton(point, MouseButton.Left, true); yield return null;
                Check(dragProbe.Drags == stoppedDrags, "the old held gesture cannot resume; elastic return is allowed");
                QueueNativeButton(point, MouseButton.Left, false); yield return null;
                Destroy(viewport); yield return null;
            }
            Debug.Log("UI_PANEL_NATIVE_CAPTURE_GATE_REACHED: runtime checks completed; no independent final acceptance claim");
        }
        private void QueueNativeButton(Vector2 point, MouseButton button, bool pressed)
            => InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(button, pressed));

        private void QueueMouse(Vector2 position, bool pressed)
            => InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });

        private void CheckRender(ColorSelectorWindow green, ColorSelectorWindow blue)
        {
            var camera = root.Canvas.worldCamera;
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
