using Newtonsoft.Json;
using System; 
using System.Collections.Generic;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Networking;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;

namespace YLFramework.Core
{
    public class HttpManager
    {


        #region 单例

        private static HttpManager _instance;

        internal static HttpManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new HttpManager();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif


        private HttpManager()
        {
        }

        #endregion


        #region 字段
         
        public Action<UnityWebRequest> preWebRequest;
         
        public Action<UnityWebRequest, float> postWebRequest;

        #endregion

        #region 属性


        /// <summary>
        /// 接口请求前触发
        /// </summary>
        public static Action<UnityWebRequest> PreWebRequest => Instance.preWebRequest;

        /// <summary>
        /// 接口请求后触发(参数一:请求对象，参数二:耗时)
        /// </summary>
        public static Action<UnityWebRequest,float> PostWebRequest => Instance.postWebRequest;
        #endregion


        /// <summary>
        /// 发起Get请求
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest Get(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
        {

            StringBuilder builderParam = new StringBuilder();
            if (param != null)
            {
                foreach (string key in param.Keys)
                {
                    if (builderParam.Length != 0)
                    {
                        builderParam.Append("&");
                    }
                    builderParam.Append(key).Append("=").Append(param[key]);
                }
            }

            if (builderParam.Length != 0)
            {
                url = string.Format("{0}?{1}", url, builderParam.ToString());
            }

            UnityWebRequest request = UnityWebRequest.Get(url);

            if (headers != null)
            {
                foreach (var key in headers.Keys)
                {
                    request.SetRequestHeader(key, headers[key]);
                }
            }
            return SendWebRequest(request);
        }

        /// <summary>
        /// 发起Post请求，参数以Form形式提交
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest PostForm(string url, Dictionary<string, string> param, Dictionary<string, string> headers)
        {
            WWWForm form = new WWWForm();
            if (param != null)
            {
                foreach (var key in param.Keys)
                {
                    form.AddField(key, param[key]);
                }
            }

            UnityWebRequest request = UnityWebRequest.Post(url, form);

            if (headers != null)
            {
                foreach (var item in headers.Keys)
                {
                    request.SetRequestHeader(item, headers[item]);
                }
            }


            return SendWebRequest(request);
        }


        /// <summary>
        /// 发起Post请求，参数以Body形式提交
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest PostBody(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
        {  

#if UNITY_2022_1_OR_NEWER
            UnityWebRequest request = UnityWebRequest.PostWwwForm(url, string.Empty);
#else
            UnityWebRequest request = UnityWebRequest.Post(url, string.Empty);
#endif

            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(param));
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.useHttpContinue = false;
            request.disposeUploadHandlerOnDispose = true;
            request.disposeDownloadHandlerOnDispose = true;
            if (headers != null && headers.Count != 0)
            {
                foreach (string key in headers.Keys)
                {
                    request.SetRequestHeader(key, headers[key]);
                }
            }
            return SendWebRequest(request);
        }


        /// <summary>
        /// 发起Post请求，参数以Body形式提交
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest PostBody(string url, string json_param, Dictionary<string, string> headers)
        {

#if UNITY_2022_1_OR_NEWER
            UnityWebRequest request = UnityWebRequest.PostWwwForm(url, string.Empty);
#else
            UnityWebRequest request = UnityWebRequest.Post(url, string.Empty);
#endif

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json_param);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.useHttpContinue = false;
            request.disposeUploadHandlerOnDispose = true;
            request.disposeDownloadHandlerOnDispose = true;
            if (headers != null && headers.Count != 0)
            {
                foreach (string key in headers.Keys)
                {
                    request.SetRequestHeader(key, headers[key]);
                }
            }
            return SendWebRequest(request);
        }

        /// <summary>
        /// 发起Put请求
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest Put(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
        {
            UnityWebRequest request = UnityWebRequest.Put(url, string.Empty);

            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(param));
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.useHttpContinue = false;
            request.disposeUploadHandlerOnDispose = true;
            request.disposeDownloadHandlerOnDispose = true;
            //webRequest.SetRequestHeader("Content-Type", "application/json;charset=utf-8");
            if (headers != null && headers.Count != 0)
            {
                foreach (string key in headers.Keys)
                {
                    request.SetRequestHeader(key, headers[key]);
                }
            }

            return SendWebRequest(request);
        }

        /// <summary>
        /// 发起Delete请求
        /// </summary>
        /// <param name="url">地址</param>
        /// <param name="param">参数</param>
        /// <param name="headers">请求头</param>
        /// <returns></returns>
        public static SendWebRequest Delete(string url, Dictionary<string, object> param, Dictionary<string, string> headers)
        {
            UnityWebRequest webRequest = UnityWebRequest.Delete(url);

            byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(param));
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.useHttpContinue = false;
            webRequest.disposeUploadHandlerOnDispose = true;
            webRequest.disposeDownloadHandlerOnDispose = true;
            if (headers != null && headers.Count != 0)
            {
                foreach (string key in headers.Keys)
                {
                    webRequest.SetRequestHeader(key, headers[key]);
                }
            }
            return SendWebRequest(webRequest);
        }

        internal static SendWebRequest SendWebRequest(UnityWebRequest request)
        {
            SendWebRequest send_web_request = new SendWebRequest();
            CoroutineStarter.Start(send_web_request.SendRequest(request));
            return send_web_request;
        }
    }

}
