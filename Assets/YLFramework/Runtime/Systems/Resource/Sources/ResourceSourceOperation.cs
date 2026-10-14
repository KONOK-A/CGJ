using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    // 具体来源的内部边界，不作为面向消费者的插件注册/路由平台。
    internal interface IResourceSource
    {
        ResourceSourceId Id { get; }
        bool SupportsSynchronousLoad { get; }
        void Validate(ResourceCatalogEntry entry);
        UnityEngine.Object Load(ResourceCatalogEntry entry, Type type);
        ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type);
        void Release(UnityEngine.Object asset);
    }

    /// <summary>producer 独占原生观察；未完成时不得 Dispose。取得结果后由唯一 record 负责 source.Release。</summary>
    internal abstract class ResourceSourceOperation : IDisposable
    {
        private UnityEngine.Object asset;
        public bool IsDone { get; private set; }
        public bool IsDisposed { get; private set; }
        public Exception Error { get; private set; }
        public virtual float Progress => IsDone ? 1 : 0;
        public event Action Completed;

        protected void Complete(UnityEngine.Object value, Exception error)
        {
            if (IsDone) return;
            asset = value;
            Error = error;
            IsDone = true;
            var callback = Completed;
            Completed = null;
            callback?.Invoke();
        }

        internal UnityEngine.Object TakeAsset()
        {
            if (!IsDone || IsDisposed) throw new InvalidOperationException("The source operation has no transferable result.");
            var value = asset;
            asset = null;
            return value;
        }

        public virtual void Dispose()
        {
            if (IsDisposed) return;
            if (!IsDone) throw new InvalidOperationException("An unfinished native producer must retain its cleanup owner.");
            IsDisposed = true;
            asset = null;
            Completed = null;
        }
    }
}
