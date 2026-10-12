using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using YLFramework.Core.Singleton;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{
    public enum HttpRequestStatus { Pending, Succeeded, Failed, Canceled, Invalidated }

    /// <summary>主线程 HTTP 请求，独占原生传输；终态后 Abort 无副作用，yield/完成订阅沿用 Foundation。</summary>
    public sealed class SendWebRequest : YLFramework.Foundation.CustomAsyncOperation<SendWebRequest>
    {
        public HttpRequestStatus Status { get; private set; }
        /// <summary>构造、配置或发送的原始异常；协议/网络错误由 error 和 NativeResult 表达。</summary>
        public Exception Exception { get; private set; }
        public UnityWebRequest.Result NativeResult { get; private set; }
        public long ResponseCode { get; private set; }
        /// <summary>独立响应快照；协议失败也保留可读内容，取消/未发送时可能为 null。</summary>
        public string Text { get; private set; }
        public byte[] Data { get; private set; }
        public IReadOnlyDictionary<string, string> ResponseHeaders { get; private set; }
        public float ElapsedSeconds { get; private set; }

        private UnityWebRequest transport;
        private UnityWebRequestAsyncOperation nativeOperation;
        private Coroutine coroutine;
        private readonly HashSet<IDisposable> handlers = new();
        private readonly object session = SingletonSessionState.ExecutionSession;
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly double startedAt = Time.realtimeSinceStartupAsDouble;
        private bool terminal;

        internal SendWebRequest() => HttpManager.Register(this);

        internal T Own<T>(T handler) where T : IDisposable
        {
            if (terminal)
            {
                handler.Dispose();
                throw new InvalidOperationException("HTTP request has already ended.");
            }
            handlers.Add(handler);
            return handler;
        }

        internal void TakeOwnership(UnityWebRequest request)
        {
            if (terminal)
            {
                request?.Dispose();
                throw new InvalidOperationException("HTTP request has already ended.");
            }
            transport = request ?? throw new ArgumentNullException(nameof(request));
            CaptureHandlers();
        }

        private void CaptureHandlers()
        {
            if (transport.downloadHandler != null) handlers.Add(transport.downloadHandler);
            if (transport.uploadHandler != null) handlers.Add(transport.uploadHandler);
            if (transport.certificateHandler != null) handlers.Add(transport.certificateHandler);
        }

        private bool IsCurrent => ReferenceEquals(session, SingletonSessionState.ExecutionSession) && HttpManager.CanRun;

        internal void Start()
        {
            if (!IsCurrent) { Invalidate(); return; }
            var handle = CoroutineStarter.Start(Run());
            // 同步首步可取消/退出/完成；不能把返回的旧句柄重新挂到已收尾请求。
            if (terminal) CoroutineStarter.Stop(handle);
            else if (handle == null) Invalidate();
            else coroutine = handle;
        }

        private IEnumerator Run()
        {
            Exception failure = null;
            try
            {
                var before = HttpManager.Before;
                if (before != null)
                    foreach (Action<UnityWebRequest> callback in before.GetInvocationList())
                    {
                        if (terminal || !IsCurrent) break;
                        try { callback(transport); }
                        finally { if (!terminal) CaptureHandlers(); }
                    }
                if (!terminal && IsCurrent)
                {
                    nativeOperation = transport.SendWebRequest();
                    // 原生完成独立于协程宿主；宿主停止等待不能遗失 HTTP 的资源收尾。
                    nativeOperation.completed += OnNativeCompleted;
                }
            }
            catch (Exception exception) { failure = exception; }
            if (failure != null) Fail(failure);
            if (terminal) yield break;
            if (!IsCurrent) { Invalidate(); yield break; }
            yield return nativeOperation;
            OnNativeCompleted(nativeOperation);
        }

        private void OnNativeCompleted(AsyncOperation operation)
        {
            if (terminal) return;
            if (!IsCurrent) { Invalidate(); return; }
            try
            {
                var request = transport;
                var result = request.result;
                var code = request.responseCode;
                var responseHeaders = request.GetResponseHeaders();
                var nativeError = request.error;
                var handler = request.downloadHandler;
                string text = null;
                byte[] data = null;
                if (handler != null)
                {
                    // 自定义 Handler 的虚方法可同步取消/退出，不能边读取边写入可观察结果。
                    text = handler.text;
                    if (terminal) return;
                    if (!IsCurrent) { Invalidate(); return; }
                    data = handler.data;
                    if (terminal) return;
                    if (!IsCurrent) { Invalidate(); return; }
                    if (data != null) data = (byte[])data.Clone();
                }
                NativeResult = result;
                ResponseCode = code;
                ResponseHeaders = responseHeaders == null ? null : new ReadOnlyDictionary<string, string>(responseHeaders);
                Text = text;
                Data = data;
                Finish(result == UnityWebRequest.Result.Success ? HttpRequestStatus.Succeeded : HttpRequestStatus.Failed,
                    nativeError, null, true);
            }
            catch (Exception exception) { Fail(exception); }
        }

        internal void Fail(Exception exception)
        {
            if (terminal) return;
            string message;
            try { message = exception.ToString(); }
            catch { message = exception.GetType().FullName + " (exception formatting failed)."; }
            Finish(HttpRequestStatus.Failed, message, exception, false);
        }
        internal void Invalidate() => Finish(HttpRequestStatus.Invalidated, "HTTP execution session is unavailable.", null, false);

        /// <summary>只取消自己拥有的 HTTP 传输；不保证撤销服务器已执行的副作用。</summary>
        public override void Abort()
        {
            if (Thread.CurrentThread.ManagedThreadId != threadId)
                throw new InvalidOperationException("HTTP Abort must run on its accepting thread.");
            Finish(HttpRequestStatus.Canceled, "request abort!", null, false);
        }

        private void Finish(HttpRequestStatus status, string error, Exception exception, bool observe)
        {
            if (terminal) return;
            terminal = true;
            Status = status;
            Exception = exception;
            ElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
            HttpManager.Unregister(this);
            if (nativeOperation != null) nativeOperation.completed -= OnNativeCompleted;
            var handle = coroutine;
            coroutine = null;
            CoroutineStarter.Stop(handle);
            try
            {
                if (observe)
                {
                    var after = HttpManager.After;
                    if (after != null)
                        foreach (Action<UnityWebRequest, float> callback in after.GetInvocationList())
                        {
                            try { callback(transport, ElapsedSeconds); }
                            catch (Exception notificationError) { Debug.LogException(notificationError); }
                            finally { CaptureHandlers(); }
                        }
                }
            }
            finally
            {
                ReleaseTransport();
                // 基类保留 legacy -> registered 的历史多播/中断语义；异常只报告，不反写 HTTP 终态。
                try { Completed(error); }
                catch (Exception notificationError) { Debug.LogException(notificationError); }
            }
        }

        private void ReleaseTransport()
        {
            if (transport != null) CaptureHandlers();
            var request = transport;
            transport = null;
            nativeOperation = null;
            if (request != null)
            {
                try
                {
                    if (!request.isDone) request.Abort();
                }
                catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                finally
                {
                    // 显式释放所有接管过的 Handler，包含前置配置替换的旧对象；忽略外部修改的 Dispose 标志。
                    request.disposeDownloadHandlerOnDispose = false;
                    request.disposeUploadHandlerOnDispose = false;
                    request.disposeCertificateHandlerOnDispose = false;
                    try { request.Dispose(); }
                    catch (Exception cleanupError) { Debug.LogException(cleanupError); }
                }
            }
            foreach (var handler in handlers)
            {
                try { handler.Dispose(); }
                catch (Exception cleanupError) { Debug.LogException(cleanupError); }
            }
            handlers.Clear();
        }
    }
}
