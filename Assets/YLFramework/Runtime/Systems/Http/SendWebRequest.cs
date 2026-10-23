using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using YLFramework.Core.Singleton;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Services.Http
{
    public enum HttpRequestStatus
    {
        Pending,
        Succeeded,
        Failed,
        Canceled,
        Invalidated
    }

    /// <summary>主线程 HTTP 请求，独占原生传输；终态后 Abort 无副作用，yield/完成订阅沿用 Foundation。</summary>
    public sealed class SendWebRequest : YLFramework.Foundation.CustomAsyncOperation<SendWebRequest>
    {
        public HttpRequestStatus Status
        {
            get; private set;
        }
        /// <summary>构造、配置或发送的原始异常；协议/网络错误由 error 和 NativeResult 表达。</summary>
        public Exception Exception
        {
            get; private set;
        }
        public UnityWebRequest.Result NativeResult
        {
            get; private set;
        }
        public long ResponseCode
        {
            get; private set;
        }
        /// <summary>独立响应快照；协议失败也保留可读内容，取消/未发送时可能为 null。</summary>
        public string Text
        {
            get; private set;
        }
        public byte[] Data
        {
            get; private set;
        }
        public IReadOnlyDictionary<string, string> ResponseHeaders
        {
            get; private set;
        }
        public float ElapsedSeconds
        {
            get; private set;
        }

        private UnityWebRequest _transport;
        private UnityWebRequestAsyncOperation _nativeOperation;
        private Coroutine _coroutine;
        private readonly HashSet<IDisposable> _handlers = new();
        private readonly object _session = SingletonSessionState.ExecutionSession;
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly double _startedAt = Time.realtimeSinceStartupAsDouble;
        private bool _isTerminal;

        internal SendWebRequest() => HttpManager.Register(this);

        internal T Own<T>(T handler) where T : IDisposable
        {
            if (_isTerminal)
            {
                handler.Dispose();
                throw new InvalidOperationException("HTTP request has already ended.");
            }
            _handlers.Add(handler);
            return handler;
        }

        internal void TakeOwnership(UnityWebRequest request)
        {
            if (_isTerminal)
            {
                request?.Dispose();
                throw new InvalidOperationException("HTTP request has already ended.");
            }
            _transport = request ?? throw new ArgumentNullException(nameof(request));
            CaptureHandlers();
        }

        private void CaptureHandlers()
        {
            if (_transport.downloadHandler != null)
            {
                _handlers.Add(_transport.downloadHandler);
            }

            if (_transport.uploadHandler != null)
            {
                _handlers.Add(_transport.uploadHandler);
            }

            if (_transport.certificateHandler != null)
            {
                _handlers.Add(_transport.certificateHandler);
            }
        }

        private bool IsCurrent => ReferenceEquals(_session, SingletonSessionState.ExecutionSession) && HttpManager.CanRun;

        internal void Start()
        {
            if (!IsCurrent)
            {
                Invalidate();
                return;
            }
            var handle = CoroutineStarter.Start(Run());
            // 同步首步可取消/退出/完成；不能把返回的旧句柄重新挂到已收尾请求。
            if (_isTerminal)
            {
                CoroutineStarter.Stop(handle);
            }
            else if (handle == null)
            {
                Invalidate();
            }
            else
            {
                _coroutine = handle;
            }
        }

        private IEnumerator Run()
        {
            Exception failure = null;
            try
            {
                var before = HttpManager.Before;
                if (before != null)
                {
                    foreach (Action<UnityWebRequest> callback in before.GetInvocationList())
                    {
                        if (_isTerminal || !IsCurrent)
                        {
                            break;
                        }

                        try
                        {
                            callback(_transport);
                        }
                        finally
                        {
                            if (!_isTerminal)
                            {
                                CaptureHandlers();
                            }
                        }
                    }
                }

                if (!_isTerminal && IsCurrent)
                {
                    _nativeOperation = _transport.SendWebRequest();
                    // 原生完成独立于协程宿主；宿主停止等待不能遗失 HTTP 的资源收尾。
                    _nativeOperation.completed += OnNativeCompleted;
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            if (failure != null)
            {
                Fail(failure);
            }

            if (_isTerminal)
            {
                yield break;
            }

            if (!IsCurrent)
            {
                Invalidate();
                yield break;
            }
            yield return _nativeOperation;
            OnNativeCompleted(_nativeOperation);
        }

        private void OnNativeCompleted(AsyncOperation operation)
        {
            if (_isTerminal)
            {
                return;
            }

            if (!IsCurrent)
            {
                Invalidate();
                return;
            }
            try
            {
                var request = _transport;
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
                    if (_isTerminal)
                    {
                        return;
                    }

                    if (!IsCurrent)
                    {
                        Invalidate();
                        return;
                    }
                    data = handler.data;
                    if (_isTerminal)
                    {
                        return;
                    }

                    if (!IsCurrent)
                    {
                        Invalidate();
                        return;
                    }
                    if (data != null)
                    {
                        data = (byte[])data.Clone();
                    }
                }
                NativeResult = result;
                ResponseCode = code;
                ResponseHeaders = responseHeaders == null ? null : new ReadOnlyDictionary<string, string>(responseHeaders);
                Text = text;
                Data = data;
                Finish(result == UnityWebRequest.Result.Success ? HttpRequestStatus.Succeeded : HttpRequestStatus.Failed,
                    nativeError, null, true);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        internal void Fail(Exception exception)
        {
            if (_isTerminal)
            {
                return;
            }

            string message;
            try
            {
                message = exception.ToString();
            }
            catch
            {
                message = exception.GetType().FullName + " (exception formatting failed).";
            }
            Finish(HttpRequestStatus.Failed, message, exception, false);
        }
        internal void Invalidate() => Finish(HttpRequestStatus.Invalidated, "HTTP execution session is unavailable.", null, false);

        /// <summary>只取消自己拥有的 HTTP 传输；不保证撤销服务器已执行的副作用。</summary>
        public override void Abort()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
            {
                throw new InvalidOperationException("HTTP Abort must run on its accepting thread.");
            }

            Finish(HttpRequestStatus.Canceled, "request abort!", null, false);
        }

        private void Finish(HttpRequestStatus status, string error, Exception exception, bool observe)
        {
            if (_isTerminal)
            {
                return;
            }

            _isTerminal = true;
            Status = status;
            Exception = exception;
            ElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - _startedAt);
            HttpManager.Unregister(this);
            if (_nativeOperation != null)
            {
                _nativeOperation.completed -= OnNativeCompleted;
            }

            var handle = _coroutine;
            _coroutine = null;
            CoroutineStarter.Stop(handle);
            try
            {
                if (observe)
                {
                    var after = HttpManager.After;
                    if (after != null)
                    {
                        foreach (Action<UnityWebRequest, float> callback in after.GetInvocationList())
                        {
                            try
                            {
                                callback(_transport, ElapsedSeconds);
                            }
                            catch (Exception notificationError)
                            {
                                Debug.LogException(notificationError);
                            }
                            finally
                            {
                                CaptureHandlers();
                            }
                        }
                    }
                }
            }
            finally
            {
                ReleaseTransport();
                // 基类保留 legacy -> registered 的历史多播/中断语义；异常只报告，不反写 HTTP 终态。
                try
                {
                    Complete(error);
                }
                catch (Exception notificationError)
                {
                    Debug.LogException(notificationError);
                }
            }
        }

        private void ReleaseTransport()
        {
            if (_transport != null)
            {
                CaptureHandlers();
            }

            var request = _transport;
            _transport = null;
            _nativeOperation = null;
            if (request != null)
            {
                try
                {
                    if (!request.isDone)
                    {
                        request.Abort();
                    }
                }
                catch (Exception cleanupError)
                {
                    Debug.LogException(cleanupError);
                }
                finally
                {
                    // 显式释放所有接管过的 Handler，包含前置配置替换的旧对象；忽略外部修改的 Dispose 标志。
                    request.disposeDownloadHandlerOnDispose = false;
                    request.disposeUploadHandlerOnDispose = false;
                    request.disposeCertificateHandlerOnDispose = false;
                    try
                    {
                        request.Dispose();
                    }
                    catch (Exception cleanupError)
                    {
                        Debug.LogException(cleanupError);
                    }
                }
            }
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.Dispose();
                }
                catch (Exception cleanupError)
                {
                    Debug.LogException(cleanupError);
                }
            }
            _handlers.Clear();
        }
    }
}
