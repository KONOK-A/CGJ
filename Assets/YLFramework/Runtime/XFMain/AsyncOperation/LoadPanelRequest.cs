// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/AsyncOperation/LoadPanelRequest.cs
using System;
using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>直接转接View事务结果；Abort仅取消Core业务请求，不物理取消共享资源加载。</summary>
    public sealed class LoadPanelRequest<T> : YLFramework.Foundation.CustomAsyncOperation<LoadPanelRequest<T>> where T : Panel
    {
        public T panel { get; private set; }
        public LoadViewStatus Status { get; private set; }
        /// <summary>原始失败原因，附加清理错误沿用View请求的Data约定。</summary>
        public Exception Exception { get; private set; }

        private LoadViewRequest<T> child;

        internal LoadPanelRequest(LoadViewRequest<T> request)
        {
            child = request;
            request.AddCompleteEvent(Complete);
        }

        public override void Abort()
        {
            if (!isDone) child.Abort();
        }

        private void Complete(LoadViewRequest<T> request)
        {
            if (isDone) return;
            // 必要UI准备已由子请求事务完成，不存在成功后再补登记的提交窗口。
            panel = request.view;
            Status = request.Status;
            Exception = request.Exception;
            request.RemoveCompleteEvent(Complete);
            child = null;
            try { Completed(request.error); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }
    }
}
