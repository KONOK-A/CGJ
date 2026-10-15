using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private CoreResourceProvider coreProvider;

        /// <summary>应用在显式初始化后、启动 Module 前装配。成功关闭或会话退出时仅解绑自己的绑定。</summary>
        public CoreResourceProvider BindCoreResourceProvider()
        {
            CheckThread();
            if (!IsAvailable) throw new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            coreProvider ??= new CoreResourceProvider(this);
            CoreResourceAccess.Bind(coreProvider);
            return coreProvider;
        }

        private void UnbindCoreResourceProvider()
        {
            CoreResourceAccess.Unbind(coreProvider);
            coreProvider = null;
        }
    }

    /// <summary>Core 的唯一正式资源后端；不读取 XF 设置，不持有第二份资源表或实例池。</summary>
    public sealed class CoreResourceProvider : ICoreResourceProvider
    {
        private readonly ResourceSystem owner;
        internal CoreResourceProvider(ResourceSystem owner) => this.owner = owner;

        public CoreResourceRequest<string> ReadTextAsync(string projectId, string address)
            => Convert(() => owner.AcquireAsync<TextAsset>(new ResourceAddress(projectId, address)), lease =>
            {
                using (lease) return lease.Asset.text;
            });

        public CorePrefabLease AcquirePrefab(string projectId, string address)
        {
            try { return new PrefabLease(owner, owner.Acquire<GameObject>(new ResourceAddress(projectId, address))); }
            catch (Exception error) { throw Translate(error); }
        }

        public CoreResourceRequest<CorePrefabLease> AcquirePrefabAsync(string projectId, string address)
            => Convert<AssetLease<GameObject>, CorePrefabLease>(
                () => owner.AcquireAsync<GameObject>(new ResourceAddress(projectId, address)),
                lease => new PrefabLease(owner, lease), lease => lease.Dispose());

        public CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string projectId, string address, LoadSceneMode mode)
            => Convert<SceneLease, CoreSceneResult>(
                () => owner.LoadSceneAsync(new ResourceAddress(projectId, address), mode), lease =>
                {
                    using (lease) return new CoreSceneResult(lease.Scene, true);
                });

        private CoreResourceRequest<TResult> Convert<TSource, TResult>(Func<ResourceRequest<TSource>> start,
            Func<TSource, TResult> convert, Action<TResult> release = null)
        {
            owner.CheckThread();
            var output = new ResourceRequest<TResult>(release);
            var core = new Request<TResult>(output);
            ResourceRequest<TSource> input = null;
            owner.Track(output);
            output.Attach(() => input?.Dispose(), () => input?.Progress ?? 0);
            try
            {
                input = start();
                input.Completed += done =>
                {
                    try
                    {
                        if (output.IsDone) return;
                        if (done.Status == ResourceRequestStatus.Succeeded) output.SetSuccess(convert(done.TakeResult()));
                        else output.SetFailure(done.Status, done.Error);
                    }
                    catch (Exception error)
                    {
                        if (!output.IsDone) output.SetFailure(ResourceRequestStatus.Failed, Failure(error));
                    }
                    finally { done.Dispose(); }
                    output.Notify();
                };
            }
            catch (Exception error)
            {
                input?.Dispose();
                if (!output.IsDone) output.SetFailure(ResourceRequestStatus.Failed, Failure(error));
                output.Notify();
            }
            return core;
        }

        private static ResourceException Failure(Exception error) => error as ResourceException ??
            new ResourceException(error is ArgumentException ? ResourceError.InvalidAddress : ResourceError.LoadFailed,
                "Core resource operation failed.", error);

        private static CoreResourceException Translate(Exception error)
        {
            if (error is CoreResourceException core) return core;
            var kind = error is ArgumentException ? CoreResourceError.InvalidInput : CoreResourceError.LoadFailed;
            if (error is ResourceException resource)
                kind = resource.Kind switch
                {
                    ResourceError.AddressNotFound => CoreResourceError.Missing,
                    ResourceError.InProgress => CoreResourceError.InProgress,
                    ResourceError.InvalidAddress => CoreResourceError.InvalidInput,
                    ResourceError.SessionUnavailable or ResourceError.SourceUnavailable or ResourceError.Unsupported => CoreResourceError.Unavailable,
                    _ => CoreResourceError.LoadFailed
                };
            return new CoreResourceException(kind, error.Message, error);
        }

        private sealed class Request<T> : CoreResourceRequest<T>
        {
            private readonly ResourceRequest<T> request;
            private Action<CoreResourceRequest<T>> observers;
            private CoreResourceException error;
            internal Request(ResourceRequest<T> request)
            {
                this.request = request;
                request.Completed += _ =>
                {
                    var snapshot = observers;
                    observers = null;
                    Notify(snapshot);
                };
            }
            public override CoreResourceStatus Status => request.Status switch
            {
                ResourceRequestStatus.Succeeded => CoreResourceStatus.Succeeded,
                ResourceRequestStatus.Failed => CoreResourceStatus.Failed,
                ResourceRequestStatus.Canceled => CoreResourceStatus.Canceled,
                ResourceRequestStatus.Invalidated => CoreResourceStatus.Invalidated,
                _ => CoreResourceStatus.Pending
            };
            public override float Progress => request.Progress;
            public override CoreResourceException Error => request.Error == null ? null : error ??= Translate(request.Error);
            public override bool IsDisposed => request.IsDisposed;
            public override event Action<CoreResourceRequest<T>> Completed
            {
                add
                {
                    if (IsDisposed) throw new ObjectDisposedException(nameof(CoreResourceRequest<T>));
                    if (request.IsDone) Notify(value);
                    else observers += value;
                }
                remove => observers -= value;
            }
            private void Notify(Action<CoreResourceRequest<T>> snapshot)
            {
                if (snapshot == null) return;
                foreach (Action<CoreResourceRequest<T>> observer in snapshot.GetInvocationList())
                    try { observer(this); }
                    catch (Exception failure) { Debug.LogException(failure); }
            }
            public override T TakeResult()
            {
                if (!IsDisposed && request.IsDone && request.Status != ResourceRequestStatus.Succeeded) throw Error;
                return request.TakeResult();
            }
            public override void Cancel() => request.Cancel();
            public override void Dispose()
            {
                try { request.Dispose(); }
                finally { observers = null; }
            }
        }

        private sealed class PrefabLease : CorePrefabLease
        {
            private readonly ResourceSystem owner;
            private readonly AssetLease<GameObject> lease;
            internal PrefabLease(ResourceSystem owner, AssetLease<GameObject> lease) { this.owner = owner; this.lease = lease; }
            public override GameObject Prefab
            {
                get { try { return lease.Asset; } catch (ResourceException error) { throw Translate(error); } }
            }
            public override bool IsValid => lease.IsValid;
            public override CoreInstanceLease Instantiate(Transform parent, Scene scene)
            {
                try { return new InstanceHandle(owner.Instantiate(lease, parent, scene)); }
                catch (Exception error) { throw Translate(error); }
            }
            public override void Dispose() => lease.Dispose();
        }

        private sealed class InstanceHandle : CoreInstanceLease
        {
            private readonly InstanceLease lease;
            internal InstanceHandle(InstanceLease lease) => this.lease = lease;
            public override GameObject Instance => lease.GameObject;
            public override bool IsValid => lease.IsValid;
            public override void Return() => lease.Return();
            public override void Discard() => lease.Discard();
        }
    }
}
