using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private readonly Dictionary<string, IRemoteJob> remoteJobs = new(StringComparer.Ordinal);
        private static readonly List<IRemoteJob> remoteTicks = new();

        /// <summary>只读取得并验证签名/Catalog，产生既有UpdatePlan；不创建store、partial、Prepared或active。</summary>
        public ResourceRequest<UpdatePlan> CheckRemoteUpdatesAsync(CancellationToken cancellation = default)
        {
            CheckThread();
            if (!CanStartRemote(out var error)) return Fail(new ResourceRequest<UpdatePlan>(), error.Kind, error.Message);
            var current = StoreSnapshots().ToArray();
            return RemoteRequest<UpdatePlan>("check", (done, alive) => configuration.ArtifactStore.CheckRemote(current, done, alive), cancellation);
        }

        private ResourceRequest<PreparedUpdate> PrepareRemoteUpdate(UpdatePlan plan, CancellationToken cancellation)
        {
            if (!CanStartRemote(out var error)) return Fail(new ResourceRequest<PreparedUpdate>(), error.Kind, error.Message);
            var key = "prepare:" + string.Join(";", plan.Projects.OrderBy(x => x.ProjectId, StringComparer.Ordinal).Select(x =>
                x.ProjectId + ":" + x.FromDigest + ":" + x.ToDigest + ":" + (x.Remote == null ? "" : ResourceCatalogCodec.Digest(x.Remote.Envelope))));
            return RemoteRequest<PreparedUpdate>(key, (done, alive) => configuration.ArtifactStore.PrepareRemote(plan, done, alive), cancellation);
        }

        private bool CanStartRemote(out ResourceException error)
        {
            error = !IsAvailable ? new ResourceException(ResourceError.SessionUnavailable, "ResourceSystem is not available.") :
                configuration.ArtifactStore == null ? new ResourceException(ResourceError.Unsupported, "No ArtifactStore is configured.") :
                updating ? new ResourceException(ResourceError.Busy, "A version transaction is in progress.") : null;
            return error == null;
        }

        private ResourceRequest<T> RemoteRequest<T>(string key, Func<Action<T>, Action, IEnumerator> routine, CancellationToken cancellation)
        {
            if (cancellation.IsCancellationRequested)
            {
                var canceled = new ResourceRequest<T>();
                canceled.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "The remote consumer canceled its wait."));
                return canceled;
            }
            if (remoteJobs.TryGetValue(key, out var existing)) return ((RemoteJob<T>)existing).Join(cancellation);
            // 一个store一次只驱动一个网络流水线；重复请求join，文件GET串行，避免另造并发HTTP调度平台。
            if (remoteJobs.Count != 0) return Fail(new ResourceRequest<T>(), ResourceError.Busy, "A different remote update is already in progress.");
            var job = new RemoteJob<T>(this, key, routine);
            remoteJobs.Add(key, job); remoteTicks.Add(job); InstallInstanceCleanup();
            return job.Join(cancellation);
        }

        private static void TickRemoteUpdates()
        {
            foreach (var job in remoteTicks.ToArray()) job.Tick();
        }

        private interface IRemoteJob { void Tick(); }

        private sealed class RemoteJob<T> : IRemoteJob
        {
            private readonly ResourceSystem owner;
            private readonly string key;
            private readonly Stack<IEnumerator> stack = new();
            private readonly Dictionary<ResourceRequest<T>, CancellationToken> waiters = new();
            private T result;
            private bool hasResult;
            private bool done;
            private bool executing;

            internal RemoteJob(ResourceSystem owner, string key, Func<Action<T>, Action, IEnumerator> routine)
            {
                this.owner = owner; this.key = key;
                stack.Push(routine(value => { result = value; hasResult = true; }, EnsureAlive));
            }

            private void EnsureAlive()
            {
                // 只在主线程观察消费者token；每块复制及发布边界重检共享所有权，不能将任一waiter的token直接交给producer。
                foreach (var waiter in waiters.ToArray()) if (waiter.Value.IsCancellationRequested) waiter.Key.Cancel();
                if (!owner.IsSessionActive || waiters.Count == 0) throw new OperationCanceledException("The remote producer no longer has an active consumer.");
            }

            internal ResourceRequest<T> Join(CancellationToken cancellation)
            {
                var request = new ResourceRequest<T>();
                owner.Track(request); waiters.Add(request, cancellation);
                request.Attach(() =>
                {
                    waiters.Remove(request);
                    if (waiters.Count == 0 && !executing) Finish(null);
                }, () => stack.Count != 0 && stack.Peek() is ResourceTransfer transfer ? transfer.Progress : 0);
                return request;
            }

            public void Tick()
            {
                if (done || executing) return;
                executing = true;
                Exception failure = null;
                var settled = false;
                try
                {
                    foreach (var waiter in waiters.ToArray()) if (waiter.Value.IsCancellationRequested) waiter.Key.Cancel();
                    if (!owner.IsSessionActive || waiters.Count == 0) settled = true;
                    else
                    {
                        for (var steps = 0; steps < 64 && stack.Count != 0 && waiters.Count != 0; steps++)
                        {
                            var routine = stack.Peek();
                            if (!routine.MoveNext()) { stack.Pop(); (routine as IDisposable)?.Dispose(); continue; }
                            if (routine.Current is IEnumerator child) { stack.Push(child); continue; }
                            break;
                        }
                        settled = stack.Count == 0 || waiters.Count == 0;
                        if (stack.Count == 0 && !hasResult) throw new InvalidOperationException("Remote update ended without a verified result.");
                    }
                }
                catch (Exception error) { failure = error; settled = true; }
                finally { executing = false; }
                if (settled) Finish(failure);
            }

            private void Finish(Exception failure)
            {
                if (done) return;
                done = true;
                while (stack.Count != 0)
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception cleanup) { failure = failure == null ? cleanup : new AggregateException(failure, cleanup); }
                owner.remoteJobs.Remove(key); remoteTicks.Remove(this);
                var completed = waiters.ToArray(); waiters.Clear();
                foreach (var waiter in completed)
                    if (!owner.IsSessionActive) waiter.Key.SetFailure(ResourceRequestStatus.Invalidated, new ResourceException(ResourceError.SessionUnavailable, "The remote update session ended."));
                    else if (waiter.Value.IsCancellationRequested) waiter.Key.SetFailure(ResourceRequestStatus.Canceled, new ResourceException(ResourceError.Canceled, "The remote consumer canceled its wait."));
                    else if (failure != null) waiter.Key.SetFailure(ResourceRequestStatus.Failed, AsUpdateError(failure));
                    else waiter.Key.SetSuccess(result);
                owner.CheckDrain();
                foreach (var waiter in completed) waiter.Key.Notify();
                if (failure != null && failure is not OperationCanceledException && completed.Length == 0) Debug.LogException(failure);
            }
        }
    }
}
