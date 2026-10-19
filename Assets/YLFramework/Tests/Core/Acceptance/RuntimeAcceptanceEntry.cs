using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Services.Audio;
using YLFramework.Services.Http;
using YLFramework.Utilities;
using YLFramework.Utilities.Grid;
using YLFramework.Utilities.Json;

namespace YLFramework.Core.Tests
{
    /// <summary>5.5 真实 Play / Windows Mono 综合门禁；仅验收程序集使用，不随框架导出。</summary>
    public sealed class RuntimeAcceptanceEntry : MonoBehaviour
    {
        public GameObject ColorPrefab;
        public string ServerUrl;
        public bool GameMode;
        public static string Result;
        public static int Round;
        public static int PreparedRound { get; private set; }
        private static readonly object DomainIdentity = new object();
        private static object previousDomain, previousSession;
        private static SendWebRequest previousHttp;
        private static UnityWebRequest previousNative;
        private static DownloadHandler previousDownload;
        private static UploadHandler previousUpload;
        private static UnityWebRequestAsyncOperation previousOperation;
        private static Action<AsyncOperation> previousNotification;
        private static AudioPlayRequest previousAudio;
        private static Provider previousProvider;
        private static AudioHandle previousHandle;
        private static LoadPanelRequest<ColorSelectorWindow> previousPanel;
        private static ColorSelectorWindow previousWindow;
        private static int oldPreCalls, oldPostCalls, oldCompletedCalls;
        private static bool exitRejected;
        private UnityWebRequest native;
        private DownloadHandler download;
        private UploadHandler upload;
        private SendWebRequest request;
        private readonly Owner module = new Owner();
        private ColorSelectorWindow window;
        private AudioClip clip;
        private Provider provider;
        private AudioPlayRequest audio;
        private AudioHandle handle;
        private bool quitArmed;
        private Exception callbackFailure;
        private Action<UnityWebRequest> pre;
        private Action<UnityWebRequest, float> post;
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;

        private sealed class Owner : Module { public override void OnStart() { } }
        private sealed class LeaseOwner : IDisposable
        {
            internal int Releases;
            public void Dispose() { Releases++; }
        }
        private sealed class Provider : IAudioClipProvider
        {
            internal Action<AudioClipLease, Exception> Complete;
            internal readonly LeaseOwner Active = new LeaseOwner();
            internal AudioClip Clip;
            public bool CanCache => false;
            public AudioClipLease Load(string key) => new AudioClipLease(Clip, Active);
            public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
            { Complete = completed; progress(.25f); }
        }

        private static object Session => typeof(CoroutineStarter).Assembly
            .GetType("YLFramework.Core.Singleton.SingletonSessionState", true)
            .GetProperty("ExecutionSession", StaticPrivate).GetValue(null);
        private static int Pending => (int)typeof(HttpManager).GetField("pendingRequests", StaticPrivate)
            .GetValue(null).GetType().GetProperty("Count").GetValue(
                typeof(HttpManager).GetField("pendingRequests", StaticPrivate).GetValue(null));
        private static IntPtr Pointer(object value)
        {
            if (value == null) return IntPtr.Zero;
            var type = value.GetType();
            while (type != null)
            {
                var field = type.GetField("m_Ptr", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null) return (IntPtr)field.GetValue(value);
                type = type.BaseType;
            }
            throw new InvalidOperationException("Missing native pointer evidence.");
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Result = "RUNNING";
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-runtime55-url") ServerUrl = args[i + 1];
            var flow = Run();
            while (true)
            {
                bool next;
                try { next = flow.MoveNext(); }
                catch (Exception error)
                {
                    Result = "FAILED: " + error;
                    Debug.LogError("RUNTIME55 " + Result);
                    (flow as IDisposable)?.Dispose();
                    request?.Abort();
                    module.Close();
                    AudioKit.Shutdown();
                    if (clip) Destroy(clip);
                    if (!Application.isEditor) Application.Quit(1);
                    yield break;
                }
                if (!next) break;
                yield return flow.Current;
            }
        }

        private IEnumerator Run()
        {
            if (GameMode || Array.IndexOf(Environment.GetCommandLineArgs(), "-runtime55-game") >= 0)
            {
                var listener = GetComponent<AudioListener>();
                if (listener) listener.enabled = false;
                var game = AudioGameAcceptanceFlow.Run();
                try { while (game.MoveNext()) yield return game.Current; }
                finally { (game as IDisposable)?.Dispose(); }
                Result = "GAME_PASS";
                Debug.Log("RUNTIME55 GAME_PASS");
                if (!Application.isEditor) Application.Quit(0);
                yield break;
            }
            Round++;
            Check(!string.IsNullOrEmpty(ServerUrl), "controlled loopback service configured");
            Check(Pending == 0, "fresh HTTP pending table");
            if (previousHttp != null)
            {
                Check(ReferenceEquals(previousDomain, DomainIdentity), "same script domain across real Play");
                Check(!ReferenceEquals(previousSession, Session), "new Foundation execution session");
                VerifyExit();
                Check(oldPreCalls == 4 && oldPostCalls == 2 && oldCompletedCalls == 1,
                    "previous HTTP subscriptions cannot receive new session work");
            }
            pre = value => { native = value; download = value.downloadHandler; upload = value.uploadHandler; };
            post = (value, elapsed) =>
            {
                try { Check(Pointer(value) != IntPtr.Zero && elapsed >= 0, "post synchronously borrows live transport"); }
                catch (Exception error) { if (callbackFailure == null) callbackFailure = error; }
            };
            HttpManager.PreWebRequest += pre;
            HttpManager.PostWebRequest += post;
            var ownedOldPre = 0;
            var ownedOldPost = 0;
            var acceptingRound = Round;
            HttpManager.PreWebRequest += _ => { if (Round == acceptingRound) ownedOldPre++; else oldPreCalls++; };
            HttpManager.PostWebRequest += (_, __) => { if (Round == acceptingRound) ownedOldPost++; else oldPostCalls++; };

            request = HttpManager.Get(ServerUrl + "/ok", null, null);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Check(request.isDone && request.Status == HttpRequestStatus.Succeeded && request.Text == "success-body",
                "real UnityWebRequest success / stable snapshot");
            Released();
            request = HttpManager.Get(ServerUrl + "/error", null, null);
            deadline = Time.realtimeSinceStartup + 10;
            while (!request.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Check(request.isDone && request.Status == HttpRequestStatus.Failed && request.ResponseCode == 503 &&
                request.Text == "protocol-body", "real protocol failure retains response");
            Released();
            request = HttpManager.PostBody(ServerUrl + "/hold", "{}", null);
            yield return new WaitForSecondsRealtime(.25f);
            Check(!request.isDone && Pointer(native) != IntPtr.Zero && Pointer(upload) != IntPtr.Zero,
                "exclusive native transport and upload genuinely in flight");
            request.Abort(); request.Abort();
            Check(request.Status == HttpRequestStatus.Canceled, "idempotent physical cancellation");
            Released();

            var startup = ModuleManager.StartUpModule(module);
            yield return startup;
            Check(startup.Exception == null && module.State == ModuleState.Running, "representative Core owner startup");
            window = module.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.green), overrides: new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple });
            Check(window && window.Module == module && window.CurrentColor == Color.green, "formal View/Panel attaches to its owner");
            window.Close();
            var canceled = module.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab, overrides: new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple });
            Check(!canceled.isDone, "closed window creates genuinely pending Panel request");
            canceled.Abort();
            Check(canceled.Status == LoadViewStatus.Canceled, "Panel cancellation does not cancel shared producer");
            window = module.LoadPanel<ColorSelectorWindow>(ColorPrefab, ColorSelectorWindow.Arguments(Color.magenta), overrides: new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple });

            clip = AudioClip.Create("runtime55-owned", 44100, 1, 44100, false);
            // 验收只配置当前自有会话，不触发设置通知或写入游戏偏好键。
            AudioKit.Settings.IsSoundOn.SetValueWithoutCallback(true);
            AudioKit.Settings.IsSoundMuted.SetValueWithoutCallback(false);
            provider = new Provider { Clip = clip };
            handle = AudioKit.PlaySoundNamed(new AudioClipAddress(provider, "active"), AudioSoundOptions.MusicDefaults);
            audio = AudioKit.PlaySoundAsync(new AudioClipAddress(provider, "pending"));
            Check(handle.IsValid && !audio.IsDone, "Audio keeps independent voice / pending resource ownership: handle=" +
                handle.IsValid + " pending=" + audio.State + "/" + audio.EndReason);
            if (previousAudio != null)
            {
                var late = new LeaseOwner();
                previousProvider.Complete(new AudioClipLease(clip, late), null);
                previousHandle.Stop(); previousAudio.Cancel(); previousHttp.Abort();
                previousNotification(previousOperation);
                Check(late.Releases == 1 && handle.IsValid && previousHttp.Status == HttpRequestStatus.Invalidated,
                    "late old Audio lease and native HTTP notification cannot mutate new session");
                Check(oldPreCalls == 4 && oldPostCalls == 2 && oldCompletedCalls == 1, "old closures remain isolated");
            }
            Check(AssemblyTool.GetType(typeof(Owner).FullName) == typeof(Owner), "Foundation FullName lookup works in Player");
            Check(new CustomClassNameGUI("runtime55").GetName() == "runtime55", "Attributes runtime metadata usable");
            Check(!string.IsNullOrEmpty(StringTools.FormatByte(1L << 40)), "Runtime byte formatter usable");
            var grid = new NormalGrid<int>(2, 2);
            grid[1, 1] = 55;
            Check(grid[1, 1] == 55 && new JSONObject("{\"probe\":55}").HasField("probe"), "Grid / JSONObject distinct tools usable");

            previousWindow = window;
            request = HttpManager.PostBody(ServerUrl + "/hold", "{}", null);
            yield return new WaitForSecondsRealtime(.25f);
            previousPanel = module.LoadPanelAsync<ColorSelectorWindow>(ColorPrefab, overrides: new PanelOverrides { InstanceMode = PanelInstanceMode.Multiple });
            Check(!request.isDone && !previousPanel.isDone, "HTTP / Panel really pending before real exit");
            Check(window && window.Module == module && window.IsLoaded, "real exit begins with an active owned View");
            if (callbackFailure != null) throw new InvalidOperationException("HTTP observer assertion failed.", callbackFailure);
            previousHttp = request; previousNative = native; previousDownload = download; previousUpload = upload;
            previousAudio = audio; previousProvider = provider; previousHandle = handle;
            previousSession = Session; previousDomain = DomainIdentity;
            previousOperation = (UnityWebRequestAsyncOperation)typeof(SendWebRequest)
                .GetField("nativeOperation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(request);
            previousNotification = (Action<AsyncOperation>)Delegate.CreateDelegate(typeof(Action<AsyncOperation>), request,
                typeof(SendWebRequest).GetMethod("OnNativeCompleted", BindingFlags.NonPublic | BindingFlags.Instance));
            oldPreCalls = ownedOldPre; oldPostCalls = ownedOldPost; oldCompletedCalls = 0; exitRejected = false;
            request.AddCompleteEvent(_ =>
            {
                oldCompletedCalls++;
                int hosts = UnityEngine.Object.FindObjectsOfType<CoroutineStarter>(true).Length;
                var denied = HttpManager.Get(ServerUrl + "/ok", null, null);
                exitRejected = denied.Status == HttpRequestStatus.Invalidated &&
                    hosts == UnityEngine.Object.FindObjectsOfType<CoroutineStarter>(true).Length;
                Check(exitRejected, "real exit callback rejects new request / replacement host");
            });
            Application.quitting += OnQuit;
            quitArmed = true;
            PreparedRound = Round;
            Result = "READY round=" + Round;
            Debug.Log("RUNTIME55 " + Result + " domain=" + DomainIdentity.GetHashCode() + " platform=" + Application.platform);
            if (!Application.isEditor) Application.Quit(0);
        }

        private void Released()
        {
            if (callbackFailure != null) throw new InvalidOperationException("HTTP observer assertion failed.", callbackFailure);
            Check(Pointer(native) == IntPtr.Zero && Pointer(download) == IntPtr.Zero &&
                Pointer(upload) == IntPtr.Zero, "native request / download / upload released before completed observation");
        }

        private void OnQuit()
        {
            try
            {
                if (callbackFailure != null) throw new InvalidOperationException("HTTP observer assertion failed.", callbackFailure);
                // Application.quitting 在场景对象销毁之前通知；活跃 View 的解绑另在真实 Exit 后验证。
                VerifyExit(Round, false);
                Check(provider.Active.Releases == 1 && !handle.IsValid && audio.EndReason == AudioPlayEnd.Shutdown,
                    "real quit releases only Audio-owned lease / identities");
                Debug.Log("RUNTIME55 QUIT_PASS round=" + Round);
            }
            catch (Exception error)
            {
                Result = "FAILED: " + error;
                Debug.LogError("RUNTIME55 " + Result);
                if (!Application.isEditor) Environment.Exit(1);
            }
            finally { if (clip) Destroy(clip); }
        }

        public static void VerifyExit(int expectedRound = 0, bool afterSceneTeardown = true)
        {
            Check(PreparedRound > 0 && (expectedRound == 0 || PreparedRound == expectedRound), "exit evidence belongs to expected ready round");
            Check(previousHttp.isDone && previousHttp.Status == HttpRequestStatus.Invalidated && Pending == 0,
                "real exit invalidates old HTTP and empties pending table");
            Check(Pointer(previousNative) == IntPtr.Zero && Pointer(previousDownload) == IntPtr.Zero &&
                Pointer(previousUpload) == IntPtr.Zero, "real exit releases exclusive native transport and handlers");
            Check(previousPanel.isDone && previousPanel.Status == LoadViewStatus.Invalidated,
                "real quit invalidates pending Panel before scene teardown");
            if (afterSceneTeardown)
                Check(!previousWindow || previousWindow.Module == null, "real scene teardown releases previously active View owner");
            Check(previousAudio.IsDone && !previousHandle.IsValid && exitRejected && oldCompletedCalls == 1,
                "real exit cleans Audio / rejects HTTP callback reentry exactly once");
            Check(previousProvider.Active.Releases == 1 && previousAudio.EndReason == AudioPlayEnd.Shutdown,
                "real exit returns Audio-owned active lease exactly once");
        }

        private void OnDestroy()
        {
            if (quitArmed) Application.quitting -= OnQuit;
            HttpManager.PreWebRequest -= pre;
            HttpManager.PostWebRequest -= post;
            if (!quitArmed) { audio?.Cancel(); handle.Stop(); }
            if (clip) Destroy(clip);
        }

        private static void Check(bool condition, string contract)
        {
            if (!condition) throw new InvalidOperationException("RUNTIME55 contract: " + contract);
            Debug.Log("RUNTIME55 CHECK " + contract);
        }
    }
}
