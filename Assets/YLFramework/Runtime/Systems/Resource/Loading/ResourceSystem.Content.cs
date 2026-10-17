using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        private static readonly List<ResourceSystem> contentCleanup = new();
        private readonly List<ContentWork> contentWork = new();
        private readonly List<(IDisposable Owner, Exception Error)> retainedContentCleanup = new();
        private readonly ConcurrentQueue<(BundleRecord Record, Exception Error)> contentFaults = new();
        private readonly ResourceContentBudget contentBudget;
        private ContentWork initializationWork;
        private int liveContentInputs;

        internal sealed class ContentWork
        {
            internal Task Task;
            internal CancellationTokenSource Cancellation;
            internal Action Complete;
            internal bool Completing;
            internal ResourceLocalSnapshot[] Snapshots;
            internal void Cancel() => Cancellation?.Cancel();
        }

        // 只在主线程登记/交割；worker不访问Unity、应用provider或Current。
        private ContentWork StartContentWork<T>(Func<CancellationToken, T> work, Action<T, Exception> completed,
            CancellationToken cancellation = default, IEnumerable<ResourceLocalSnapshot> snapshots = null)
        {
            CheckThread();
            var job = new ContentWork { Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation),
                Snapshots = snapshots?.Where(x => x != null).Distinct().ToArray() ?? Array.Empty<ResourceLocalSnapshot>() };
            var token = job.Cancellation.Token;
            var task = new Task<T>(() => work(token), CancellationToken.None, TaskCreationOptions.DenyChildAttach);
            job.Task = task;
            job.Complete = () =>
            {
                T result = default;
                Exception failure = null;
                try { result = task.GetAwaiter().GetResult(); }
                catch (Exception error) { failure = error; }
                completed(result, failure);
            };
            contentWork.Add(job);
            try { RegisterContentCleanup(); task.Start(TaskScheduler.Default); }
            catch { contentWork.Remove(job); job.Cancellation.Dispose(); throw; }
            return job;
        }

        private void RegisterContentCleanup()
        {
            if (!contentCleanup.Contains(this)) contentCleanup.Add(this);
            InstallInstanceCleanup();
        }

        private static void TickContentWork()
        {
            for (var ownerIndex = contentCleanup.Count - 1; ownerIndex >= 0; ownerIndex--)
            {
                if (ownerIndex >= contentCleanup.Count) continue;
                var owner = contentCleanup[ownerIndex];
                for (var i = owner.contentWork.Count - 1; i >= 0; i--)
                {
                    if (i >= owner.contentWork.Count) continue;
                    var job = owner.contentWork[i];
                    if (job.Completing || !job.Task.IsCompleted) continue;
                    job.Completing = true;
                    // 完成回调及其输入交割也属于in-flight，重入不能先把旧主体判为已排空。
                    try { job.Complete(); }
                    catch (Exception error) { Debug.LogException(error); }
                    finally
                    {
                        job.Cancellation.Dispose();
                        job.Cancellation = null;
                        job.Complete = null;
                        owner.contentWork.Remove(job);
                    }
                }
                for (var i = 0; i < 8 && owner.contentFaults.TryDequeue(out var fault); i++)
                    if (fault.Record.State != BundleState.Released)
                        fault.Record.Error ??= ResourceContentValidation.Failure(fault.Error, "An authenticated Bundle input faulted; new dependency-closure acquisitions are refused.");
                if (owner.contentWork.Count == 0 && owner.retainedContentCleanup.Count == 0 && owner.liveContentInputs == 0 && owner.contentFaults.IsEmpty)
                    contentCleanup.Remove(owner);
                owner.CheckDrain();
            }
        }

        private void CancelContentWork()
        {
            foreach (var job in contentWork) job.Cancel();
        }

        private void RetainContentCleanup(IDisposable owner, Exception error)
        {
            if (retainedContentCleanup.Any(x => ReferenceEquals(x.Owner, owner))) return;
            retainedContentCleanup.Add((owner, error));
            RegisterContentCleanup();
            Debug.LogException(new ResourceException(ResourceError.LoadFailed,
                "Content cleanup failed; ownership is retained and no unconditional native retry is performed.", error));
        }

        private void RetainContentFailureOwners(Exception error)
        {
            if (error == null) return;
            if (error is ResourceContentCleanupException cleanup) RetainContentCleanup(cleanup.Owner, cleanup);
            if (error is AggregateException aggregate)
                foreach (var inner in aggregate.InnerExceptions) RetainContentFailureOwners(inner);
            else RetainContentFailureOwners(error.InnerException);
        }

        internal void RequireContentAdmission(ResourceCatalogEntry entry)
        {
            if (entry.Snapshot != null && entry.Descriptor != null)
                RequireHealthyBundle(entry.Snapshot, entry.Descriptor.BundleId);
        }

        private void RequireHealthyBundle(ResourceLocalSnapshot snapshot, string bundleId)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(bundleId);
            while (pending.Count != 0)
            {
                var id = pending.Pop();
                if (!visited.Add(id)) continue;
                if (bundleRecords.TryGetValue((snapshot.Document.CatalogDigest, id), out var record))
                {
                    if (record.Collecting) throw new ResourceException(ResourceError.InProgress, "A Bundle input is being unloaded.");
                    if (record.CleanupError != null) throw new ResourceException(ResourceError.LoadFailed, "A Bundle input has an unresolved cleanup failure.", record.CleanupError);
                    var error = record.Error ?? (record.Input?.Fault == null ? null :
                        ResourceContentValidation.Failure(record.Input.Fault, "A Bundle dependency input is faulted: " + id));
                    if (error != null) throw error;
                }
                foreach (var dependency in snapshot.Bundles[id].Dependencies) pending.Push(dependency);
            }
        }

        internal void RequireHealthyBundle(BundleLease lease)
            => RequireHealthyBundle(lease.Record.Snapshot, lease.Record.Descriptor.LogicalId);

        private void FinishInitialization(Exception error)
        {
            initializationWork = null;
            if (error == null && !IsSessionActive) error = new ResourceException(ResourceError.SessionUnavailable, "Initialization session ended before authentication completed.");
            if (error == null && initialization.Count == 0) error = new OperationCanceledException("Initialization no longer has any waiting consumer.");
            var joined = initialization.ToArray();
            initialization.Clear();
            if (error == null)
            {
                State = ResourceSystemState.Ready;
                foreach (var item in joined) item.SetSuccess(this);
            }
            else
            {
                RetainContentFailureOwners(error);
                var failure = error as ResourceException ?? ResourceContentValidation.Failure(error, "Resource initialization failed.");
                if (State != ResourceSystemState.Invalidated) State = ResourceSystemState.Failed;
                if (ReferenceEquals(authority, this)) authority = null;
                if (contentWork.Count != 0 || retainedContentCleanup.Count != 0) draining.Add(this);
                foreach (var item in joined) item.SetFailure(failure.Kind == ResourceError.Canceled ? ResourceRequestStatus.Canceled : ResourceRequestStatus.Failed, failure);
            }
            foreach (var item in joined) item.Notify();
        }

        private void JoinInitialization(ResourceRequest<ResourceSystem> request)
        {
            initialization.Add(request);
            request.Attach(() => { initialization.Remove(request); if (initialization.Count == 0) initializationWork?.Cancel(); }, () => 0);
        }

        private void ContinueInitialization(IReadOnlyList<ResourceLocalSnapshot> mounted)
        {
            ResourceContentValidation validation = null;
            try
            {
                if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "Initialization session ended.");
                try { ValidateConfiguration(mounted); }
                catch (Exception error)
                {
                    throw error as ResourceException ?? new ResourceException(ResourceError.InvalidConfiguration, "Resource configuration validation failed.", error);
                }
                var snapshots = catalogs.Values.Select(x => x.Snapshot).Where(x => x != null).Distinct().ToArray();
                validation = ResourceContentValidation.Capture(snapshots, configuration.Decryption, true, contentBudget);
                if (!IsSessionActive) throw new ResourceException(ResourceError.SessionUnavailable, "Initialization was invalidated by the key provider.");
                if (!validation.HasEncryptedContent) { validation.Dispose(); FinishInitialization(null); return; }
                var owned = validation;
                initializationWork = StartContentWork(token => { owned.Validate(token); return true; }, (_, error) =>
                {
                    if (error == null)
                        try { owned.RequireStable(); } catch (Exception cause) { error = cause; }
                    try { owned.Dispose(); }
                    catch (Exception cleanup) { RetainContentCleanup(owned, cleanup); error = error == null ? cleanup : new AggregateException(error, cleanup); }
                    FinishInitialization(error);
                }, snapshots: snapshots);
            }
            catch (Exception error)
            {
                if (validation != null)
                    try { validation.Dispose(); } catch (Exception cleanup) { RetainContentCleanup(validation, cleanup); error = new AggregateException(error, cleanup); }
                FinishInitialization(error);
            }
        }
    }
}
