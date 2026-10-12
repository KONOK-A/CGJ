using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using YLFramework.Foundation;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    public class HttpRequestPlayModeTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly List<CoroutineStarter> ownedHosts = new List<CoroutineStarter>();
        private readonly List<SendWebRequest> ownedRequests = new List<SendWebRequest>();
        private readonly List<Action<UnityWebRequest>> preSubscriptions = new List<Action<UnityWebRequest>>();
        private readonly List<Action<UnityWebRequest, float>> postSubscriptions = new List<Action<UnityWebRequest, float>>();
        private LoopbackServer server;
        private CultureInfo previousCulture;
        private CultureInfo previousUICulture;
        private float previousTimeScale;
        private bool isolated;
        private bool restoreSession;

        private static Type SessionType => typeof(CoroutineStarter).Assembly
            .GetType("YLFramework.Core.Singleton.SingletonSessionState", true);

        [SetUp]
        public void SetUp()
        {
            isolated = false;
            restoreSession = false;
            Assert.That(FindHosts(), Is.Empty, "Fixture must not own an existing Foundation host.");
            Assert.That(SessionType.GetProperty("CanRunExecution", PrivateStatic).GetValue(null), Is.True,
                "Fixture must not reopen a pre-existing closed execution gate.");
            Assert.That(PendingRequests(), Is.Empty, "Fixture must not interrupt another HTTP owner.");
            // 验证当前 Unity 的实际字段，不以反射写指针或逻辑完成代替 Dispose。
            foreach (var type in new[] { typeof(UnityWebRequest), typeof(DownloadHandler),
                         typeof(UploadHandler), typeof(CertificateHandler) })
                Assert.That(type.GetField("m_Ptr", PrivateInstance), Is.Not.Null, type.FullName);
            previousCulture = CultureInfo.CurrentCulture;
            previousUICulture = CultureInfo.CurrentUICulture;
            previousTimeScale = Time.timeScale;
            isolated = true;
            server = new LoopbackServer();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!isolated) yield break;
            foreach (var callback in preSubscriptions) HttpManager.PreWebRequest -= callback;
            foreach (var callback in postSubscriptions) HttpManager.PostWebRequest -= callback;
            preSubscriptions.Clear();
            postSubscriptions.Clear();
            try
            {
                foreach (var request in ownedRequests) request.Abort();
            }
            finally
            {
                ownedRequests.Clear();
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
                Time.timeScale = previousTimeScale;
                foreach (var host in ownedHosts) if (host) Object.Destroy(host.gameObject);
                ownedHosts.Clear();
                try { server?.Dispose(); }
                finally
                {
                    server = null;
                    if (restoreSession)
                    {
                        // Reset 清空 HTTP 钩子；保存其他订阅者，不将夹具的恢复变成订阅清理。
                        var pre = (Action<UnityWebRequest>)typeof(HttpManager)
                            .GetField("PreWebRequest", PrivateStatic).GetValue(null);
                        var post = (Action<UnityWebRequest, float>)typeof(HttpManager)
                            .GetField("PostWebRequest", PrivateStatic).GetValue(null);
                        try
                        {
                            // 仅恢复本夹具关闭的 gate，不是实际退出/再次进入 Play 的证据。
                            SessionType.GetMethod("ResetForNewSession", PrivateStatic).Invoke(null, null);
                        }
                        finally
                        {
                            HttpManager.PreWebRequest += pre;
                            HttpManager.PostWebRequest += post;
                            restoreSession = false;
                        }
                    }
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetPreservesQueryAndFragmentAndEncodesKeysValuesAndNullUsingInvariantCulture()
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr-FR");
            string configuredUrl = null;
            Pre(native => configuredUrl = native.url);
            var request = Start(() => HttpManager.Get(server.Url("/echo?existing=keep#client-fragment"),
                new Dictionary<string, object>
                {
                    { "key &=#中文", "value +&=#/雪" }, { "empty", "" }, { "nil", null }, { "number", 12.5m }
                }, Headers()));
            yield return WaitFor(request);
            AssertSucceeded(request);
            var echo = Echo(request);
            Assert.That(echo[0], Is.EqualTo("GET"));
            Assert.That(echo[1], Does.StartWith("/echo?existing=keep&"));
            Assert.That(echo[1], Does.Not.Contain("#"));
            var query = ParseForm(echo[1].Substring(echo[1].IndexOf('?') + 1));
            Assert.That(query.Count, Is.EqualTo(5));
            Assert.That(query["existing"], Is.EqualTo("keep"));
            Assert.That(query["key &=#中文"], Is.EqualTo("value +&=#/雪"));
            Assert.That(query["empty"], Is.Empty);
            Assert.That(query["nil"], Is.Empty);
            Assert.That(query["number"], Is.EqualTo("12.5"));
            Assert.That(configuredUrl, Does.EndWith("#client-fragment"));
            Assert.That(echo[3], Is.EqualTo("fixture-header"));

            request = Start(() => HttpManager.Get(server.Url("/echo?existing=keep#client-fragment"),
                new Dictionary<string, object>(), null));
            yield return WaitFor(request);
            Assert.That(Echo(request)[1], Is.EqualTo("/echo?existing=keep"));
            request = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
            yield return WaitFor(request);
            Assert.That(Echo(request)[1], Is.EqualTo("/echo"));
        }

        [UnityTest]
        public IEnumerator GetAddsQuerySeparatorWhenPathItselfEndsWithAmpersand()
        {
            var request = Start(() => HttpManager.Get(server.Url("/path&"),
                new Dictionary<string, object> { { "q", "v" } }, null));
            yield return WaitFor(request);
            Assert.That(Echo(request)[1], Is.EqualTo("/path&?q=v"));
        }

        [UnityTest]
        public IEnumerator CustomHandlerGetterCancellationCannotPublishSnapshotAfterCompletion()
        {
            for (var index = 0; index < 2; index++)
            {
                SendWebRequest request = null;
                NativeEvidence evidence = null;
                var handler = new SnapshotHandler();
                Action cancel = () => request.Abort();
                if (index == 0) handler.ReadText = cancel;
                else handler.ReadData = cancel;
                Action<UnityWebRequest> configure = native =>
                {
                    native.downloadHandler = handler;
                    evidence = new NativeEvidence(native);
                };
                Pre(configure);
                request = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
                string completedText = null;
                byte[] completedData = null;
                long completedCode = -1;
                var notified = false;
                request.AddCompleteEvent(_ =>
                {
                    notified = true;
                    completedText = request.Text;
                    completedData = request.Data;
                    completedCode = request.ResponseCode;
                    evidence.AssertReleased();
                });
                yield return WaitFor(request);
                Assert.That(notified, Is.True);
                Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Canceled));
                Assert.That(request.Text, Is.EqualTo(completedText));
                Assert.That(request.Data, Is.SameAs(completedData));
                Assert.That(request.ResponseCode, Is.EqualTo(completedCode));
                evidence.AssertReleased();
                HttpManager.PreWebRequest -= configure;
            }
        }

        [UnityTest]
        public IEnumerator CustomHandlerDataSnapshotSurvivesHandlerDisposalMutatingItsArray()
        {
            var handler = new SnapshotHandler { ClearOnDispose = true };
            NativeEvidence evidence = null;
            Pre(native => { native.downloadHandler = handler; evidence = new NativeEvidence(native); });
            var request = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
            yield return WaitFor(request);
            AssertSucceeded(request);
            evidence.AssertReleased();
            Assert.That(handler.Bytes, Is.EqualTo(new byte[] { 0, 0, 0 }));
            Assert.That(request.Data, Is.EqualTo(new byte[] { 10, 20, 30 }));
            Assert.That(request.Data, Is.Not.SameAs(handler.Bytes));
        }

        private sealed class SnapshotHandler : DownloadHandlerScript
        {
            public Action ReadText;
            public Action ReadData;
            public bool ClearOnDispose;
            public readonly byte[] Bytes = { 10, 20, 30 };
            protected override string GetText() { ReadText?.Invoke(); return "custom snapshot"; }
            protected override byte[] GetData() { ReadData?.Invoke(); return Bytes; }
            public override void Dispose()
            {
                if (ClearOnDispose) Array.Clear(Bytes, 0, Bytes.Length);
                base.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator FormAndJsonFactoriesSendCorrectMethodsBodiesContentTypesAndHeaders()
        {
            var form = Start(() => HttpManager.PostForm(server.Url("/echo"),
                new Dictionary<string, string> { { "key &=", "a +&=" }, { "empty", "" } }, Headers()));
            yield return WaitFor(form);
            AssertSucceeded(form);
            var echo = Echo(form);
            Assert.That(echo[0], Is.EqualTo("POST"));
            Assert.That(echo[2], Does.StartWith("application/x-www-form-urlencoded"));
            Assert.That(echo[3], Is.EqualTo("fixture-header"));
            var fields = ParseForm(echo[4]);
            Assert.That(fields.Count, Is.EqualTo(2));
            Assert.That(fields["key &="], Is.EqualTo("a +&="));
            Assert.That(fields["empty"], Is.Empty);

            var body = new Dictionary<string, object> { { "number", 12.5 }, { "text", "a +&=雪" } };
            var factories = new Func<SendWebRequest>[]
            {
                () => HttpManager.PostBody(server.Url("/echo"), body, Headers()),
                () => HttpManager.PostBody(server.Url("/echo"), "{\"number\":12.5,\"text\":\"a +&=雪\"}", Headers()),
                () => HttpManager.Put(server.Url("/echo"), body, Headers()),
                () => HttpManager.Delete(server.Url("/echo"), body, Headers())
            };
            var methods = new[] { "POST", "POST", "PUT", "DELETE" };
            for (var i = 0; i < factories.Length; i++)
            {
                var request = Start(factories[i]);
                yield return WaitFor(request);
                AssertSucceeded(request);
                echo = Echo(request);
                Assert.That(echo[0], Is.EqualTo(methods[i]));
                Assert.That(echo[2], Does.StartWith("application/json"));
                Assert.That(echo[3], Is.EqualTo("fixture-header"));
                Assert.That(echo[4], Is.EqualTo("{\"number\":12.5,\"text\":\"a +&=雪\"}"));
            }

            var overrides = Headers();
            overrides["Content-Type"] = "application/vnd.fixture+json";
            var overridden = Start(() => HttpManager.PostBody(server.Url("/echo"), "{}", overrides));
            yield return WaitFor(overridden);
            Assert.That(Echo(overridden)[2], Is.EqualTo("application/vnd.fixture+json"));
            overridden = Start(() => HttpManager.PostForm(server.Url("/echo"),
                new Dictionary<string, string> { { "a", "b" } }, overrides));
            yield return WaitFor(overridden);
            Assert.That(Echo(overridden)[2], Is.EqualTo("application/vnd.fixture+json"));
        }

        [UnityTest]
        public IEnumerator SuccessCommitsSnapshotBeforePostAndReleasesNativeBeforeCompletionAndLateNotification()
        {
            NativeEvidence evidence = null;
            SendWebRequest request = null;
            var calls = new List<string>();
            Pre(native =>
            {
                Assert.That(native.timeout, Is.EqualTo(5));
                evidence = new NativeEvidence(native);
                evidence.AssertLive();
            });
            Post((native, seconds) =>
            {
                calls.Add("post");
                Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Succeeded));
                Assert.That(request.ResponseCode, Is.EqualTo(200));
                Assert.That(request.Text, Is.EqualTo(native.downloadHandler.text));
                Assert.That(seconds, Is.EqualTo(request.ElapsedSeconds));
                Assert.That(seconds, Is.GreaterThanOrEqualTo(0));
                evidence.AssertLive();
            });
            request = Start(() => HttpManager.Get(server.Url("/echo"), null, Headers()));
#pragma warning disable CS0618
            request.completed += _ => { evidence.AssertReleased(); calls.Add("legacy"); };
#pragma warning restore CS0618
            request.AddCompleteEvent(value => { evidence.AssertReleased(); value.Abort(); calls.Add("registered"); });
            yield return WaitFor(request);
            AssertSucceeded(request);
            Assert.That(request.ResponseHeaders["X-Fixture"], Is.EqualTo("loopback"));
            Assert.That(request.Data, Is.EqualTo(Encoding.UTF8.GetBytes(request.Text)));
            request.AddCompleteEvent(_ => { evidence.AssertReleased(); calls.Add("late"); });
            Assert.That(calls, Is.EqualTo(new[] { "post", "legacy", "registered", "late" }));
            var text = request.Text;
            var data = (byte[])request.Data.Clone();
            request.Abort();
            yield return null;
            AssertSucceeded(request);
            Assert.That(request.Text, Is.EqualTo(text));
            Assert.That(request.Data, Is.EqualTo(data));
            Assert.That(request.ResponseHeaders["X-Fixture"], Is.EqualTo("loopback"));
            Assert.That(calls.Count, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DestroyedCoroutineHostCannotOrphanNativeCompletionOrCreateReplacementHost()
        {
            NativeEvidence evidence = null;
            Pre(native => evidence = new NativeEvidence(native));
            var request = Start(() => HttpManager.PostBody(server.Url("/echo"), "{}", null));
            Assert.That(request.isDone, Is.False);
            var host = FindHosts()[0];
            Object.DestroyImmediate(host.gameObject);
            Assert.That(FindHosts(), Is.Empty);
            yield return WaitFor(request);
            AssertSucceeded(request);
            evidence.AssertReleased();
            Assert.That(FindHosts(), Is.Empty, "Native completion must not create a coroutine host.");
        }

        [UnityTest]
        public IEnumerator ProtocolFailureKeepsErrorBodyResponseCodeHeadersAndReleasesHandlers()
        {
            NativeEvidence evidence = null;
            Pre(native => evidence = new NativeEvidence(native));
            var request = Start(() => HttpManager.Get(server.Url("/error"), null, null));
            yield return WaitFor(request);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
            Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            Assert.That(request.ResponseCode, Is.EqualTo(503));
            Assert.That(request.Text, Is.EqualTo("fixture protocol failure"));
            Assert.That(request.Data, Is.EqualTo(Encoding.UTF8.GetBytes("fixture protocol failure")));
            Assert.That(request.ResponseHeaders["X-Fixture"], Is.EqualTo("loopback"));
            evidence.AssertReleased();
        }

        [UnityTest]
        public IEnumerator TimeoutUsesPreConfiguredNativeTimeoutAndActuallyStopsSlowTransport()
        {
            Time.timeScale = 0;
            NativeEvidence evidence = null;
            Pre(native =>
            {
                Assert.That(native.timeout, Is.EqualTo(5));
                native.timeout = 1;
                evidence = new NativeEvidence(native);
            });
            var started = Time.realtimeSinceStartup;
            var request = Start(() => HttpManager.Get(server.Url("/slow"), null, null));
            yield return WaitFor(request, 8);
            Assert.That(server.RequestCount, Is.EqualTo(1), "The real loopback transport must have started.");
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
            Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            Assert.That(Time.realtimeSinceStartup - started, Is.GreaterThanOrEqualTo(0.5f).And.LessThan(8));
            Assert.That(request.ElapsedSeconds, Is.GreaterThanOrEqualTo(0.5f).And.LessThan(8));
            evidence.AssertReleased();
        }

        [UnityTest]
        public IEnumerator AbortIsIdempotentAndDoesNotCancelAnotherRequestOrAcceptLateNativeCompletion()
        {
            var evidence = new List<NativeEvidence>();
            Pre(native => evidence.Add(new NativeEvidence(native)));
            var canceled = Start(() => HttpManager.Get(server.Url("/slow"), null, null));
            yield return WaitUntil(() => server.RequestCount == 1, 5);
            var survivor = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
            var completions = 0;
            canceled.AddCompleteEvent(_ => { evidence[0].AssertReleased(); completions++; });
            Assert.That(canceled.Status, Is.EqualTo(HttpRequestStatus.Pending));
            Assert.That(survivor.Status, Is.EqualTo(HttpRequestStatus.Pending));
            canceled.Abort();
            var error = canceled.error;
            var text = canceled.Text;
            var data = canceled.Data;
            var headers = canceled.ResponseHeaders;
            var seconds = canceled.ElapsedSeconds;
            canceled.Abort();
            InvokeLateCompletion(canceled);
            yield return WaitFor(survivor);
            yield return null;
            Assert.That(canceled.isDone, Is.True);
            Assert.That(canceled.Status, Is.EqualTo(HttpRequestStatus.Canceled));
            Assert.That(canceled.error, Is.EqualTo(error));
            Assert.That(canceled.Text, Is.EqualTo(text));
            Assert.That(canceled.Data, Is.SameAs(data));
            Assert.That(canceled.ResponseHeaders, Is.SameAs(headers));
            Assert.That(canceled.ElapsedSeconds, Is.EqualTo(seconds));
            Assert.That(completions, Is.EqualTo(1));
            evidence[0].AssertReleased();
            evidence[1].AssertReleased();
            AssertSucceeded(survivor);
        }

        [UnityTest]
        public IEnumerator PreCanAbortRegisteredRequestBeforeFactoryReturns()
        {
            NativeEvidence original = null;
            NativeEvidence evidence = null;
            SendWebRequest inPre = null;
            var returned = false;
            Pre(native =>
            {
                Assert.That(returned, Is.False);
                original = new NativeEvidence(native);
                native.downloadHandler = new DownloadHandlerBuffer();
                native.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("replacement"));
                evidence = new NativeEvidence(native);
                var pending = PendingRequests();
                Assert.That(pending.Count, Is.EqualTo(1));
                inPre = pending[0];
                inPre.Abort();
                original.AssertReleased();
                evidence.AssertReleased();
            });
            var request = Start(() => HttpManager.PostBody(server.Url("/echo"), "{}", null));
            returned = true;
            Assert.That(request, Is.SameAs(inPre));
            Assert.That(request.isDone, Is.True);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Canceled));
            Assert.That(PendingRequests(), Is.Empty);
            InvokeLateCompletion(request);
            yield return null;
            Assert.That(server.RequestCount, Is.Zero);
            original.AssertReleased();
            evidence.AssertReleased();
        }

        [UnityTest]
        public IEnumerator ClosedGateRejectsBeforeStartWithoutInvokingHooksOrCreatingHost()
        {
            var preCalled = false;
            Pre(_ => preCalled = true);
            CloseExecution();
            var native = new UnityWebRequest(server.Url("/echo"), "POST",
                new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}")));
            native.certificateHandler = new FixtureCertificateHandler();
            var evidence = new NativeEvidence(native);
            evidence.AssertLive();
            var request = Start(() => SendOwnedNative(native));
            Assert.That(request.isDone, Is.True);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            Assert.That(preCalled, Is.False);
            Assert.That(FindHosts(), Is.Empty);
            Assert.That(PendingRequests(), Is.Empty);
            evidence.AssertReleased();
            yield return null;
            Assert.That(server.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PreExecutionEndInvalidatesRegisteredRequestAndRejectsReentrantNewRequest()
        {
            NativeEvidence evidence = null;
            NativeEvidence rejectedEvidence = null;
            SendWebRequest rejected = null;
            Pre(native =>
            {
                evidence = new NativeEvidence(native);
                var hosts = FindHosts();
                Assert.That(hosts.Length, Is.EqualTo(1));
                if (!ownedHosts.Contains(hosts[0])) ownedHosts.Add(hosts[0]);
                CloseExecution();
                InvokeExecutionEnded();
                var rejectedNative = UnityWebRequest.Get(server.Url("/echo"));
                rejectedEvidence = new NativeEvidence(rejectedNative);
                rejected = Start(() => SendOwnedNative(rejectedNative));
            });
            var request = Start(() => HttpManager.Get(server.Url("/slow"), null, null));
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(request.isDone, Is.True);
            Assert.That(rejected.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(rejected.isDone, Is.True);
            evidence.AssertReleased();
            rejectedEvidence.AssertReleased();
            Assert.That(PendingRequests(), Is.Empty);
            yield return null;
            Assert.That(server.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PreDeactivatingOwnedHostRejectsSynchronousStartAndReleasesNative()
        {
            NativeEvidence evidence = null;
            Pre(native =>
            {
                evidence = new NativeEvidence(native);
                var hosts = FindHosts();
                Assert.That(hosts.Length, Is.EqualTo(1));
                // 此宿主由本次 Start 创建；在同步首步立即记账，才能安全失活。
                if (!ownedHosts.Contains(hosts[0])) ownedHosts.Add(hosts[0]);
                hosts[0].gameObject.SetActive(false);
            });
            var request = Start(() => HttpManager.Get(server.Url("/slow"), null, null));
            Assert.That(request.isDone, Is.True);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            evidence.AssertReleased();
            Assert.That(PendingRequests(), Is.Empty);
            var native = UnityWebRequest.Get(server.Url("/echo"));
            var rejectedEvidence = new NativeEvidence(native);
            var rejected = Start(() => SendOwnedNative(native));
            Assert.That(rejected.isDone, Is.True);
            Assert.That(rejected.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            rejectedEvidence.AssertReleased();
            yield return null;
            Assert.That(FindHosts().Length, Is.EqualTo(1), "Inactive owned host must not be replaced.");
        }

        [UnityTest]
        public IEnumerator PreExceptionPreservesOriginalFailureAndReleasesReplacedAndCurrentHandlers()
        {
            NativeEvidence original = null;
            NativeEvidence replacement = null;
            var failure = new InvalidOperationException("http fixture pre failure");
            var laterPre = false;
            Pre(native =>
            {
                original = new NativeEvidence(native);
                native.downloadHandler = new DownloadHandlerBuffer();
                native.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("replacement"));
                native.certificateHandler = new FixtureCertificateHandler();
                native.disposeDownloadHandlerOnDispose = false;
                native.disposeUploadHandlerOnDispose = false;
                native.disposeCertificateHandlerOnDispose = false;
                replacement = new NativeEvidence(native);
                throw failure;
            });
            Pre(_ => laterPre = true);
            var nativeRequest = new UnityWebRequest(server.Url("/echo"), "POST",
                new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes("original")));
            nativeRequest.certificateHandler = new FixtureCertificateHandler();
            var request = Start(() => SendOwnedNative(nativeRequest));
            Assert.That(request.isDone, Is.True);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
            Assert.That(request.Exception, Is.SameAs(failure));
            Assert.That(request.error, Does.Contain("http fixture pre failure"));
            Assert.That(laterPre, Is.False, "Failed configuration must not continue with later pre hooks.");
            original.AssertReleased();
            replacement.AssertReleased();
            Assert.That(PendingRequests(), Is.Empty);
            yield return null;
            Assert.That(server.RequestCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PostExceptionIsReportedWithoutChangingSuccessOrSkippingCleanupAndCompletion()
        {
            NativeEvidence evidence = null;
            Pre(native => evidence = new NativeEvidence(native));
            Post((native, seconds) => throw new InvalidOperationException("http fixture post failure"));
            var laterPost = false;
            Post((native, seconds) => { evidence.AssertLive(); laterPost = true; });
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: http fixture post failure"));
            var request = Start(() => HttpManager.PostBody(server.Url("/echo"), "{}", null));
            var completed = false;
            request.AddCompleteEvent(_ => { evidence.AssertReleased(); completed = true; });
            yield return WaitFor(request);
            AssertSucceeded(request);
            Assert.That(request.Exception, Is.Null);
            Assert.That(completed, Is.True);
            Assert.That(laterPost, Is.True);
            evidence.AssertReleased();
        }

        [UnityTest]
        public IEnumerator CompletionExceptionKeepsLegacyOrderAndInterruptsLaterCallbacksButNotCleanup()
        {
            NativeEvidence evidence = null;
            Pre(native => evidence = new NativeEvidence(native));
            var calls = new List<string>();
            var request = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
#pragma warning disable CS0618
            request.completed += _ => { evidence.AssertReleased(); calls.Add("legacy"); };
#pragma warning restore CS0618
            request.AddCompleteEvent(_ =>
            {
                evidence.AssertReleased();
                calls.Add("throw");
                throw new InvalidOperationException("http fixture completion failure");
            });
            request.AddCompleteEvent(_ => calls.Add("must not run"));
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: http fixture completion failure"));
            yield return WaitFor(request);
            AssertSucceeded(request);
            Assert.That(request.Exception, Is.Null);
            evidence.AssertReleased();
            Assert.That(calls, Is.EqualTo(new[] { "legacy", "throw" }));
            request.AddCompleteEvent(_ => calls.Add("late"));
            request.Abort();
            InvokeLateCompletion(request);
            Assert.That(calls, Is.EqualTo(new[] { "legacy", "throw", "late" }));
        }

        [UnityTest]
        public IEnumerator LegacyCompletionExceptionInterruptsRegisteredEventsWithoutLeakingNative()
        {
            NativeEvidence evidence = null;
            Pre(native => evidence = new NativeEvidence(native));
            var request = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
            var registered = false;
#pragma warning disable CS0618
            request.completed += _ =>
            {
                evidence.AssertReleased();
                throw new InvalidOperationException("http fixture legacy failure");
            };
#pragma warning restore CS0618
            request.AddCompleteEvent(_ => registered = true);
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: http fixture legacy failure"));
            yield return WaitFor(request);
            AssertSucceeded(request);
            Assert.That(registered, Is.False);
            evidence.AssertReleased();
        }

        [UnityTest]
        public IEnumerator PostReentrantAbortDoesNotReleaseBorrowedNativeEarlyAndCanStartIndependentRequest()
        {
            var evidence = new List<NativeEvidence>();
            Pre(native => evidence.Add(new NativeEvidence(native)));
            SendWebRequest first = null;
            SendWebRequest nested = null;
            var firstPost = false;
            Post((native, seconds) =>
            {
                if (firstPost) return;
                firstPost = true;
                Assert.That(first.Status, Is.EqualTo(HttpRequestStatus.Succeeded));
                first.Abort();
                evidence[0].AssertLive();
                Assert.That(native.downloadHandler.text, Is.EqualTo(first.Text));
                nested = Start(() => HttpManager.Get(server.Url("/echo?nested=yes"), null, null));
                evidence[0].AssertLive();
            });
            first = Start(() => HttpManager.Get(server.Url("/echo"), null, null));
            var completed = false;
            first.AddCompleteEvent(_ => { evidence[0].AssertReleased(); completed = true; });
            yield return WaitFor(first);
            Assert.That(nested, Is.Not.Null);
            yield return WaitFor(nested);
            AssertSucceeded(first);
            AssertSucceeded(nested);
            Assert.That(completed, Is.True);
            evidence[0].AssertReleased();
            evidence[1].AssertReleased();
            Assert.That(Echo(nested)[1], Is.EqualTo("/echo?nested=yes"));
        }

        [UnityTest]
        public IEnumerator ExecutionEndInvalidatesInFlightRequestsAndLateCallbacksCannotChangeTheirSnapshots()
        {
            var evidence = new List<NativeEvidence>();
            Pre(native => evidence.Add(new NativeEvidence(native)));
            var first = Start(() => HttpManager.Get(server.Url("/slow"), null, null));
            var second = Start(() => HttpManager.PostBody(server.Url("/slow"), "{}", null));
            yield return WaitUntil(() => server.RequestCount == 2, 5);
            var completed = 0;
            first.AddCompleteEvent(_ => { evidence[0].AssertReleased(); completed++; });
            second.AddCompleteEvent(_ => { evidence[1].AssertReleased(); completed++; });
            CloseExecution();
            InvokeExecutionEnded();
            var error = first.error;
            var seconds = first.ElapsedSeconds;
            var data = first.Data;
            InvokeExecutionEnded();
            InvokeLateCompletion(first);
            InvokeLateCompletion(second);
            first.Abort();
            second.Abort();
            yield return null;
            Assert.That(first.isDone && second.isDone, Is.True);
            Assert.That(first.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(second.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(first.error, Is.EqualTo(error));
            Assert.That(first.ElapsedSeconds, Is.EqualTo(seconds));
            Assert.That(first.Data, Is.SameAs(data));
            Assert.That(first.ResponseCode, Is.Zero);
            Assert.That(completed, Is.EqualTo(2));
            Assert.That(PendingRequests(), Is.Empty);
            evidence[0].AssertReleased();
            evidence[1].AssertReleased();
        }

        private SendWebRequest Start(Func<SendWebRequest> factory)
        {
            var before = FindHosts();
            foreach (var host in before)
                Assert.That(ownedHosts.Contains(host), Is.True, "Do not adopt a foreign host.");
            try
            {
                var request = factory();
                ownedRequests.Add(request);
                return request;
            }
            finally
            {
                foreach (var host in FindHosts())
                    if (Array.IndexOf(before, host) < 0 && !ownedHosts.Contains(host)) ownedHosts.Add(host);
            }
        }

        private void Pre(Action<UnityWebRequest> callback)
        {
            preSubscriptions.Add(callback);
            HttpManager.PreWebRequest += callback;
        }

        private void Post(Action<UnityWebRequest, float> callback)
        {
            postSubscriptions.Add(callback);
            HttpManager.PostWebRequest += callback;
        }

        private IEnumerator WaitFor(SendWebRequest request, float timeout = 5)
        {
            yield return WaitUntil(() => request.isDone, timeout);
            Assert.That(request.keepWaiting, Is.False);
            Assert.That(request.progress, Is.EqualTo(1));
            Assert.That(PendingRequests().Contains(request), Is.False);
        }

        private IEnumerator WaitUntil(Func<bool> condition, float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Bounded realtime wait expired.");
            server.AssertHealthy();
        }

        private void CloseExecution()
        {
            restoreSession = true;
            SessionType.GetMethod("StopExecution", PrivateStatic).Invoke(null, null);
        }

        private static void InvokeExecutionEnded() => typeof(HttpManager)
            .GetMethod("OnExecutionEnded", PrivateStatic).Invoke(null, null);

        private static void InvokeLateCompletion(SendWebRequest request) => typeof(SendWebRequest)
            .GetMethod("OnNativeCompleted", PrivateInstance).Invoke(request, new object[] { null });

        private static SendWebRequest SendOwnedNative(UnityWebRequest native) => (SendWebRequest)typeof(HttpManager)
            .GetMethod("SendWebRequest", PrivateStatic).Invoke(null, new object[] { native });

        private static List<SendWebRequest> PendingRequests()
        {
            var pending = typeof(HttpManager).GetField("pendingRequests", PrivateStatic);
            Assert.That(pending, Is.Not.Null);
            return new List<SendWebRequest>((HashSet<SendWebRequest>)pending.GetValue(null));
        }

        private static CoroutineStarter[] FindHosts() => Object.FindObjectsByType<CoroutineStarter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static Dictionary<string, string> Headers() =>
            new Dictionary<string, string> { { "X-Test", "fixture-header" } };

        private static void AssertSucceeded(SendWebRequest request)
        {
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Succeeded), request.error);
            Assert.That(request.isDone, Is.True);
            Assert.That(request.error, Is.Null.Or.Empty);
            Assert.That(request.ResponseCode, Is.EqualTo(200));
        }

        private static string[] Echo(SendWebRequest request)
        {
            AssertSucceeded(request);
            var result = request.Text.Split(new[] { '\n' }, 5);
            Assert.That(result.Length, Is.EqualTo(5));
            return result;
        }

        private static Dictionary<string, string> ParseForm(string text)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in text.Split('&'))
            {
                var separator = pair.IndexOf('=');
                Assert.That(separator, Is.GreaterThanOrEqualTo(0));
                result.Add(Uri.UnescapeDataString(pair.Substring(0, separator).Replace("+", " ")),
                    Uri.UnescapeDataString(pair.Substring(separator + 1).Replace("+", " ")));
            }
            return result;
        }

        private sealed class FixtureCertificateHandler : CertificateHandler { }

        private sealed class NativeEvidence
        {
            private readonly UnityWebRequest request;
            private readonly DownloadHandler download;
            private readonly UploadHandler upload;
            private readonly CertificateHandler certificate;

            public NativeEvidence(UnityWebRequest request)
            {
                this.request = request;
                download = request.downloadHandler;
                upload = request.uploadHandler;
                certificate = request.certificateHandler;
            }

            public void AssertLive()
            {
                Assert.That(Pointer(request), Is.Not.EqualTo(IntPtr.Zero));
                if (download != null) Assert.That(Pointer(download), Is.Not.EqualTo(IntPtr.Zero));
                if (upload != null) Assert.That(Pointer(upload), Is.Not.EqualTo(IntPtr.Zero));
                if (certificate != null) Assert.That(Pointer(certificate), Is.Not.EqualTo(IntPtr.Zero));
            }

            public void AssertReleased()
            {
                Assert.That(Pointer(request), Is.EqualTo(IntPtr.Zero), "UnityWebRequest native allocation");
                if (download != null) Assert.That(Pointer(download), Is.EqualTo(IntPtr.Zero), "DownloadHandler native allocation");
                if (upload != null) Assert.That(Pointer(upload), Is.EqualTo(IntPtr.Zero), "UploadHandler native allocation");
                if (certificate != null) Assert.That(Pointer(certificate), Is.EqualTo(IntPtr.Zero), "CertificateHandler native allocation");
            }

            private static IntPtr Pointer(object value)
            {
                for (var type = value.GetType(); type != null; type = type.BaseType)
                {
                    var field = type.GetField("m_Ptr", PrivateInstance | BindingFlags.DeclaredOnly);
                    if (field != null) return (IntPtr)field.GetValue(value);
                }
                throw new InvalidOperationException("Unity native m_Ptr field was not found.");
            }
        }

        // 后台仅处理 .NET socket/HTTP 字节；Unity 对象和测试断言都留在测试主线程。
        private sealed class LoopbackServer : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly object sync = new object();
            private readonly List<TcpClient> clients = new List<TcpClient>();
            private readonly List<Task> workers = new List<Task>();
            private readonly List<Exception> failures = new List<Exception>();
            private readonly Task acceptTask;
            private readonly int port;
            private int requestCount;
            private bool disposed;

            public int RequestCount => Volatile.Read(ref requestCount);

            public LoopbackServer()
            {
                listener.Start();
                port = ((IPEndPoint)listener.LocalEndpoint).Port;
                acceptTask = Task.Run(AcceptLoop);
            }

            public string Url(string path) => "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + path;

            public void AssertHealthy()
            {
                lock (sync) Assert.That(failures, Is.Empty, "Unexpected loopback fixture failure.");
            }

            private async Task AcceptLoop()
            {
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        lock (sync)
                        {
                            if (cancellation.IsCancellationRequested) { client.Dispose(); break; }
                            clients.Add(client);
                            workers.Add(Task.Run(() => Serve(client)));
                        }
                    }
                }
                catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
                catch (SocketException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exception) { lock (sync) failures.Add(exception); }
            }

            private async Task Serve(TcpClient client)
            {
                try
                {
                    using (client)
                    using (var stream = client.GetStream())
                    {
                        var headerBytes = new List<byte>();
                        var single = new byte[1];
                        while (headerBytes.Count < 32768)
                        {
                            if (await stream.ReadAsync(single, 0, 1, cancellation.Token).ConfigureAwait(false) == 0) return;
                            headerBytes.Add(single[0]);
                            var count = headerBytes.Count;
                            if (count >= 4 && headerBytes[count - 4] == 13 && headerBytes[count - 3] == 10 &&
                                headerBytes[count - 2] == 13 && headerBytes[count - 1] == 10) break;
                        }
                        if (headerBytes.Count >= 32768) throw new InvalidDataException("Request headers exceed fixture limit.");
                        var lines = Encoding.ASCII.GetString(headerBytes.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                        var first = lines[0].Split(' ');
                        if (first.Length != 3) throw new InvalidDataException("Malformed request line.");
                        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 1; i < lines.Length && lines[i].Length != 0; i++)
                        {
                            var separator = lines[i].IndexOf(':');
                            if (separator <= 0) throw new InvalidDataException("Malformed request header.");
                            headers[lines[i].Substring(0, separator)] = lines[i].Substring(separator + 1).Trim();
                        }
                        if (headers.TryGetValue("Expect", out var expect) &&
                            expect.IndexOf("100-continue", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var interim = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                            await stream.WriteAsync(interim, 0, interim.Length, cancellation.Token).ConfigureAwait(false);
                        }
                        var length = headers.TryGetValue("Content-Length", out var contentLength)
                            ? int.Parse(contentLength, CultureInfo.InvariantCulture) : 0;
                        if (length < 0 || length > 65536) throw new InvalidDataException("Request body exceeds fixture limit.");
                        if (headers.ContainsKey("Transfer-Encoding")) throw new InvalidDataException("Fixture expects Content-Length.");
                        var body = new byte[length];
                        var offset = 0;
                        while (offset < length)
                        {
                            var read = await stream.ReadAsync(body, offset, length - offset, cancellation.Token).ConfigureAwait(false);
                            if (read == 0) return;
                            offset += read;
                        }
                        Interlocked.Increment(ref requestCount);
                        if (first[1].StartsWith("/slow", StringComparison.Ordinal))
                            await Task.Delay(15000, cancellation.Token).ConfigureAwait(false);
                        var error = first[1].StartsWith("/error", StringComparison.Ordinal);
                        headers.TryGetValue("Content-Type", out var contentType);
                        headers.TryGetValue("X-Test", out var testHeader);
                        var text = error ? "fixture protocol failure" : first[0] + "\n" + first[1] + "\n" +
                            (contentType ?? "") + "\n" + (testHeader ?? "") + "\n" + Encoding.UTF8.GetString(body);
                        var responseBody = Encoding.UTF8.GetBytes(text);
                        var responseHeader = Encoding.ASCII.GetBytes("HTTP/1.1 " + (error ? "503 Service Unavailable" : "200 OK") +
                            "\r\nContent-Type: text/plain; charset=utf-8\r\nX-Fixture: loopback\r\nContent-Length: " +
                            responseBody.Length.ToString(CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(responseHeader, 0, responseHeader.Length, cancellation.Token).ConfigureAwait(false);
                        await stream.WriteAsync(responseBody, 0, responseBody.Length, cancellation.Token).ConfigureAwait(false);
                    }
                }
                // 客户端取消/超时会关闭连接；仅网络断开是允许的，解析错误仍报告。
                catch (IOException exception) when (!(exception is InvalidDataException)) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) when (cancellation.IsCancellationRequested) { }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception exception) { lock (sync) failures.Add(exception); }
                finally { lock (sync) clients.Remove(client); }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                cancellation.Cancel();
                listener.Stop();
                lock (sync) foreach (var client in clients) client.Dispose();
                var acceptStopped = acceptTask.Wait(5000);
                Task[] running;
                lock (sync) running = workers.ToArray();
                var workersStopped = Task.WaitAll(running, 5000);
                cancellation.Dispose();
                Assert.That(acceptStopped && workersStopped, Is.True, "Own loopback tasks must stop before fixture ends.");
                AssertHealthy();
            }
        }
    }
}
