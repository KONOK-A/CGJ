using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using YLFramework.Core.Singleton;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    /// <summary>主线程轻量 HTTP 入口；每笔请求独占传输，不管理认证、重试或共享资源生产者。</summary>
    public class HttpManager
    {
        private static readonly HashSet<SendWebRequest> pendingRequests = new();
        private static bool ending;

        static HttpManager()
        {
            SingletonSessionState.RegisterSessionReset(ResetSession);
            Application.quitting += OnExecutionEnded;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        private HttpManager() { }

        /// <summary>发送前同步配置。不得持有或释放借用的原生请求/Handler；异常使本请求失败。</summary>
        /// <remarks>
        /// 订阅者应在自己的生命周期结束时解除订阅；HTTP 会话重置时清空订阅。
        /// 初始及回调返回时附着的 Handler 归 HTTP；同一回调内创建并再次替换的中间 Handler 由配置者释放。
        /// </remarks>
        public static event Action<UnityWebRequest> PreWebRequest;

        /// <summary>原生完成后的同步借读观察（含协议/网络错误），第二参数为未缩放耗时（秒）。</summary>
        /// <remarks>终态已提交；异常仅报告，不改结果。完成通知应读取 SendWebRequest 的响应快照。</remarks>
        public static event Action<UnityWebRequest, float> PostWebRequest;

        internal static bool CanRun => !ending && SingletonSessionState.CanRunExecution;
        internal static void Register(SendWebRequest request) => pendingRequests.Add(request);
        internal static void Unregister(SendWebRequest request) => pendingRequests.Remove(request);
        internal static Action<UnityWebRequest> Before => PreWebRequest;
        internal static Action<UnityWebRequest, float> After => PostWebRequest;

        private static void OnExecutionEnded()
        {
            ending = true;
            // 请求自行先撤销在途身份再通知；通知重入不能向正在退出的 HTTP 会话增加工作。
            foreach (var request in new List<SendWebRequest>(pendingRequests)) request.Invalidate();
        }

        private static void ResetSession()
        {
            OnExecutionEnded();
            PreWebRequest = null;
            PostWebRequest = null;
            ending = false;
        }

#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode) OnExecutionEnded();
        }
#endif

        /// <summary>GET 参数保留原 query/fragment，键和值分别编码；null 值按空值，数值使用 InvariantCulture。</summary>
        public static SendWebRequest Get(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
            => Create(operation => Build(operation, QueryUrl(url, param), UnityWebRequest.kHttpVerbGET, null, null), headers);

        /// <summary>以 UTF-8 application/x-www-form-urlencoded 提交表单；null 字段值按空字符串。</summary>
        public static SendWebRequest PostForm(string url, Dictionary<string, string> param, Dictionary<string, string> headers)
            => Create(operation =>
            {
                var body = new StringBuilder();
                if (param != null)
                    foreach (var item in param)
                    {
                        if (body.Length != 0) body.Append('&');
                        body.Append(UnityWebRequest.EscapeURL(item.Key, Encoding.UTF8)).Append('=')
                            .Append(UnityWebRequest.EscapeURL(item.Value ?? string.Empty, Encoding.UTF8));
                    }
                return Build(operation, url, UnityWebRequest.kHttpVerbPOST, body.ToString(), "application/x-www-form-urlencoded; charset=utf-8");
            }, headers);

        /// <summary>将字典序列化为 JSON（null 字典为 JSON null），以 UTF-8 提交。</summary>
        public static SendWebRequest PostBody(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
            => Create(operation => Build(operation, url, UnityWebRequest.kHttpVerbPOST, JsonConvert.SerializeObject(param), "application/json; charset=utf-8"), headers);

        /// <summary>原样提交 JSON 字符串；null 字符串为参数错误，不进行 JSON 内容校验。</summary>
        public static SendWebRequest PostBody(string url, string json_param, Dictionary<string, string> headers)
            => Create(operation => Build(operation, url, UnityWebRequest.kHttpVerbPOST,
                json_param ?? throw new ArgumentNullException(nameof(json_param)), "application/json; charset=utf-8"), headers);

        /// <summary>以 PUT 提交字典的 UTF-8 JSON。</summary>
        public static SendWebRequest Put(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
            => Create(operation => Build(operation, url, UnityWebRequest.kHttpVerbPUT, JsonConvert.SerializeObject(param), "application/json; charset=utf-8"), headers);

        /// <summary>以 DELETE 提交字典的 UTF-8 JSON。</summary>
        public static SendWebRequest Delete(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
            => Create(operation => Build(operation, url, UnityWebRequest.kHttpVerbDELETE, JsonConvert.SerializeObject(param), "application/json; charset=utf-8"), headers);

        private static string QueryUrl(string url, Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0) return url;
            if (url == null) throw new ArgumentNullException(nameof(url));
            var fragmentIndex = url.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
            var address = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;
            var builder = new StringBuilder(address);
            var hasQuery = address.IndexOf('?') >= 0;
            foreach (var item in parameters)
            {
                if (!hasQuery) builder.Append('?');
                else if (builder[builder.Length - 1] != '?' && builder[builder.Length - 1] != '&') builder.Append('&');
                builder.Append(Uri.EscapeDataString(item.Key)).Append('=')
                    .Append(Uri.EscapeDataString(Convert.ToString(item.Value, CultureInfo.InvariantCulture) ?? string.Empty));
                hasQuery = true;
            }
            return builder.Append(fragment).ToString();
        }

        private static UnityWebRequest Build(SendWebRequest operation, string url, string method, string body, string contentType)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                throw new ArgumentException("HTTP requires an absolute http/https URI.", nameof(url));
            var request = new UnityWebRequest(url, method);
            operation.TakeOwnership(request);
            request.downloadHandler = operation.Own(new DownloadHandlerBuffer());
            if (body != null)
            {
                request.uploadHandler = operation.Own(new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)));
                request.SetRequestHeader("Content-Type", contentType);
                request.useHttpContinue = false;
            }
            return request;
        }

        private static SendWebRequest Create(Func<SendWebRequest, UnityWebRequest> factory, Dictionary<string, string> headers)
        {
            var operation = new SendWebRequest();
            try
            {
                var request = factory(operation);
                request.timeout = 5;
                if (headers != null)
                    foreach (var header in headers) request.SetRequestHeader(header.Key, header.Value);
                operation.Start();
            }
            catch (Exception exception) { operation.Fail(exception); }
            return operation;
        }

        // 内部生产入口接管全部原生资源；不提供外部重用同一 transport 的能力。
        internal static SendWebRequest SendWebRequest(UnityWebRequest request)
            => Create(operation => { operation.TakeOwnership(request); return request; }, null);
    }
}
