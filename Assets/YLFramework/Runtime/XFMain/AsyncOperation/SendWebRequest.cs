using System.Collections; 
using UnityEngine;
using UnityEngine.Networking;


namespace YLFramework.Core
{
    // Fix 编码

    public class SendWebRequest : YLFramework.Foundation.CustomAsyncOperation<SendWebRequest>
    {
        /// <summary>
        /// 接口返回的内容
        /// </summary>
        public string Text { get;private set; }

        /// <summary>
        /// 接口返回的数据
        /// </summary>
        public byte[] Data { get; private set; }

        public IEnumerator SendRequest(UnityWebRequest request)
        {
            request.timeout = 5; // 超时时间5s
            request.disposeDownloadHandlerOnDispose = true;
            request.disposeUploadHandlerOnDispose = true;

            HttpManager.PreWebRequest?.Invoke(request);

            UnityWebRequestAsyncOperation asyncOperation = request.SendWebRequest();
            float time = Time.time;
            yield return asyncOperation;
            HttpManager.PostWebRequest?.Invoke(request,Time.time - time);

            if (string.IsNullOrEmpty(request.error)) 
            {
                this.Text = request.downloadHandler.text; 
                this.Data = request.downloadHandler.data;

            } 
            Completed(request.error);
            request.Dispose(); 
        }
    }

}

