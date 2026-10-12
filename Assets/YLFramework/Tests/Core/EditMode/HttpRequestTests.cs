using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.Networking;

namespace YLFramework.Core.Tests
{
    public class HttpRequestTests
    {
        private sealed class OwnedCertificate : CertificateHandler { }
        private sealed class BadParameter
        {
            public override string ToString() => throw new UnprintableException();
        }
        private sealed class UnprintableException : Exception
        {
            public override string ToString() => throw new InvalidOperationException("Exception formatting failed.");
        }
        [Test]
        public void RefusedStartCompletesInsteadOfLeavingUnownedTransportPending()
        {
            var request = HttpManager.PostBody("http://127.0.0.1:1/", "{}", null);
            Assert.That(request.isDone, Is.True, "Non-Play execution must reject and release its request.");
            Assert.That(request.error, Is.Not.Null.And.Not.Empty);
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            var error = request.error;
            request.Abort();
            Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            Assert.That(request.error, Is.EqualTo(error));
        }

        [Test]
        public void ExceptionFormattingFailureCannotLeaveConstructionPendingOrEscapeTheFactory()
        {
            var pending = (HashSet<SendWebRequest>)typeof(HttpManager)
                .GetField("pendingRequests", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var previous = new HashSet<SendWebRequest>(pending);
            try
            {
                SendWebRequest request = null;
                Assert.DoesNotThrow(() => request = HttpManager.Get("http://127.0.0.1:1/",
                    new Dictionary<string, object> { { "q", new BadParameter() } }, null));
                Assert.That(request.isDone, Is.True);
                Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
                Assert.That(request.Exception, Is.TypeOf<UnprintableException>());
                Assert.That(request.error, Is.Not.Empty);
                Assert.That(pending.Contains(request), Is.False);
            }
            finally
            {
                foreach (var request in new List<SendWebRequest>(pending))
                    if (!previous.Contains(request)) request.Abort();
            }
        }

        [Test]
        public void RefusedNativeStartReleasesRequestAndAllHandlersEvenWithAutomaticDisposalDisabled()
        {
            var native = new UnityWebRequest("http://127.0.0.1:1/", "POST");
            var download = new DownloadHandlerBuffer();
            var upload = new UploadHandlerRaw(new byte[] { 1, 2 });
            var certificate = new OwnedCertificate();
            native.downloadHandler = download;
            native.uploadHandler = upload;
            native.certificateHandler = certificate;
            native.disposeDownloadHandlerOnDispose = false;
            native.disposeUploadHandlerOnDispose = false;
            native.disposeCertificateHandlerOnDispose = false;
            try
            {
                Assert.That(Pointer(native), Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(download), Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(upload), Is.Not.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(certificate), Is.Not.EqualTo(IntPtr.Zero));
                var operation = (SendWebRequest)typeof(HttpManager).GetMethod("SendWebRequest", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { native });
                Assert.That(operation.isDone, Is.True);
                Assert.That(operation.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
                Assert.That(Pointer(native), Is.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(download), Is.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(upload), Is.EqualTo(IntPtr.Zero));
                Assert.That(Pointer(certificate), Is.EqualTo(IntPtr.Zero));
                operation.Abort();
                operation.Abort();
                Assert.That(operation.Status, Is.EqualTo(HttpRequestStatus.Invalidated));
            }
            finally
            {
                native.Dispose();
                download.Dispose();
                upload.Dispose();
                certificate.Dispose();
            }
        }

        [Test]
        public void ConstructionAndJsonArgumentErrorsReturnStableFailedRequests()
        {
            var invalidUri = HttpManager.Get("not an absolute URI", null, null);
            var nullJson = HttpManager.PostBody("http://127.0.0.1:1/", (string)null, null);
            foreach (var request in new[] { invalidUri, nullJson })
            {
                Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
                Assert.That(request.isDone, Is.True);
                Assert.That(request.Exception, Is.InstanceOf<ArgumentException>());
                var error = request.error;
                request.Abort();
                Assert.That(request.Status, Is.EqualTo(HttpRequestStatus.Failed));
                Assert.That(request.error, Is.EqualTo(error));
            }
        }

        private static IntPtr Pointer(object target)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField("m_Ptr", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return (IntPtr)field.GetValue(target);
            }
            throw new InvalidOperationException("Native pointer evidence is unavailable for " + target.GetType());
        }
    }
}
