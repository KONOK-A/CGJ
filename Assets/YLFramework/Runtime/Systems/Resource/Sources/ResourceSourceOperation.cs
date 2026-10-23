using System;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    // 具体来源的内部边界，不作为面向消费者的插件注册/路由平台。
    internal interface IResourceSource
    {
        ResourceSourceId Id
        {
            get;
        }
        bool SupportsSynchronousLoad
        {
            get;
        }
        void Validate(ResourceCatalogEntry entry);
        ResourceSourceResult Load(ResourceCatalogEntry entry, Type type);
        ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type);
        void Release(UnityEngine.Object asset);
    }

    internal readonly struct ResourceSourceResult
    {
        internal readonly UnityEngine.Object Asset;
        internal readonly IDisposable Retention;
        internal ResourceSourceResult(UnityEngine.Object asset, IDisposable retention = null)
        {
            Asset = asset;
            Retention = retention;
        }
    }

    /// <summary>producer 独占原生观察；未完成时不得 Dispose。取得结果后由唯一 record 负责 source.Release。</summary>
    internal abstract class ResourceSourceOperation : IDisposable
    {
        private UnityEngine.Object _asset;
        private IDisposable _retention;
        public bool IsDone
        {
            get; private set;
        }
        public bool IsDisposed
        {
            get; private set;
        }
        public Exception Error
        {
            get; private set;
        }
        public virtual bool SupportsCancellation => false;
        public virtual float Progress => IsDone ? 1 : 0;
        public event Action Completed;

        protected void Complete(UnityEngine.Object value, Exception error, IDisposable ownedRetention = null)
        {
            if (IsDone)
            {
                return;
            }

            _asset = value;
            _retention = ownedRetention;
            Error = error;
            IsDone = true;
            var callback = Completed;
            Completed = null;
            callback?.Invoke();
        }

        internal ResourceSourceResult TakeResult()
        {
            if (!IsDone || IsDisposed)
            {
                throw new InvalidOperationException("The source operation has no transferable result.");
            }

            var value = new ResourceSourceResult(_asset, _retention);
            _asset = null;
            _retention = null;
            return value;
        }

        // 默认为不可取消 native。可取消来源只结束自己拥有的 IO，仍通过 Complete 交割收尾。
        public virtual void Cancel()
        {
        }

        public virtual void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            if (!IsDone)
            {
                throw new InvalidOperationException("An unfinished native producer must retain its cleanup owner.");
            }

            IsDisposed = true;
            _asset = null;
            _retention = null;
            Completed = null;
        }
    }
}
