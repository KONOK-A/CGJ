using System;
using System.Collections.Generic;
using System.Linq;

namespace YLFramework.Services.Resource
{
    public sealed partial class ResourceSystem
    {
        /// <summary>枚举此 Bundle 在正式 Catalog 中的可赋值 typed 资产（含子资产），等待各自 producer 完成后交割；不取得未登记内容。</summary>
        public ResourceRequest<AssetBatchLease<T>> AcquireAllAsync<T>(string projectId, string bundleId) where T : UnityEngine.Object
        {
            CheckThread();
            if (!IsAvailable) return BatchFailure<T>(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            if (IsProjectBlocked(projectId)) return BatchFailure<T>(ResourceError.Busy, "The project is inside its activation barrier.");
            if (projectId == null || !catalogs.TryGetValue(projectId, out var catalog) || catalog.Snapshot == null ||
                bundleId == null || !catalog.Snapshot.Bundles.TryGetValue(bundleId, out var descriptor))
                return BatchFailure<T>(ResourceError.AddressNotFound, "Unknown mounted project/Bundle identity.");
            if (descriptor.IsScene) return BatchFailure<T>(ResourceError.Unsupported, "Scene Bundle preparation is not an asset batch or Scene load.");
            return AcquireBatch<T>(catalog.Entries.Where(x => x.Descriptor?.BundleId == bundleId && typeof(T).IsAssignableFrom(x.Location.AssetType))
                .Select(x => (x, true)).ToArray(), "typed Bundle batch '" + bundleId + "'");
        }

        /// <summary>按 canonical AssetId 取得 Catalog Group；required 失败归还整个批次，optional 失败保留 cause。不创建实例、不加载 Scene。</summary>
        public ResourceRequest<AssetBatchLease<UnityEngine.Object>> AcquireGroupAsync(string projectId, string groupId)
        {
            CheckThread();
            if (!IsAvailable) return BatchFailure<UnityEngine.Object>(ResourceError.SessionUnavailable, "ResourceSystem is not available.");
            if (IsProjectBlocked(projectId)) return BatchFailure<UnityEngine.Object>(ResourceError.Busy, "The project is inside its activation barrier.");
            if (projectId == null || !catalogs.TryGetValue(projectId, out var catalog))
                return BatchFailure<UnityEngine.Object>(ResourceError.AddressNotFound, "Unknown resource project.");
            var group = catalog.Groups.FirstOrDefault(x => x.Id == groupId);
            if (group == null) return BatchFailure<UnityEngine.Object>(ResourceError.AddressNotFound, "Unknown Catalog Group.");
            var entries = catalog.Entries.ToDictionary(x => x.AssetId, StringComparer.Ordinal);
            return AcquireBatch<UnityEngine.Object>(group.Members.Select(x => (entries.TryGetValue(x.AssetId, out var entry) ? entry : null, x.Required)).ToArray(),
                "Group '" + groupId + "'");
        }

        private static ResourceRequest<AssetBatchLease<T>> BatchFailure<T>(ResourceError kind, string message) where T : UnityEngine.Object =>
            Fail(new ResourceRequest<AssetBatchLease<T>>(value => value.Dispose()), kind, message);

        private ResourceRequest<AssetBatchLease<T>> AcquireBatch<T>((ResourceCatalogEntry Entry, bool Required)[] entries, string label) where T : UnityEngine.Object
        {
            var request = new ResourceRequest<AssetBatchLease<T>>(value => value.Dispose());
            var children = new List<(ResourceRequest<AssetLease<T>> Request, bool Required)>();
            var starting = true;
            Track(request);
            request.Attach(() => { foreach (var child in children.ToArray()) child.Request.Dispose(); },
                () => children.Count == 0 ? 0 : children.Sum(x => x.Request.Progress) / children.Count);
            void Finish()
            {
                if (starting || request.IsDone || children.Any(x => !x.Request.IsDone)) return;
                var errors = children.Where(x => x.Request.Status != ResourceRequestStatus.Succeeded).Select(x => x.Request.Error).ToList();
                if (children.Any(x => x.Required && x.Request.Status != ResourceRequestStatus.Succeeded))
                {
                    request.SetFailure(ResourceRequestStatus.Failed, new ResourceException(ResourceError.LoadFailed, label + " failed; required member ownership was rolled back.",
                        new AggregateException(errors)));
                    foreach (var child in children) child.Request.Dispose();
                }
                else
                {
                    var leases = new List<AssetLease<T>>();
                    try
                    {
                        foreach (var child in children)
                            if (child.Request.Status == ResourceRequestStatus.Succeeded) leases.Add(child.Request.TakeResult());
                        request.SetSuccess(new AssetBatchLease<T>(leases, errors));
                    }
                    catch
                    {
                        foreach (var lease in leases) lease.Dispose();
                        throw;
                    }
                    finally { foreach (var child in children) child.Request.Dispose(); }
                }
                request.Notify();
            }
            foreach (var entry in entries)
            {
                if (request.IsDone) break;
                var child = entry.Entry == null ? Fail(new ResourceRequest<AssetLease<T>>(value => value.Dispose()), ResourceError.Unsupported,
                    "Scene/group member cannot be acquired as an ordinary asset in this Source.") : AcquireAsync<T>(entry.Entry.Address);
                children.Add((child, entry.Required));
                if (request.IsDone) child.Dispose();
                else child.Completed += _ => Finish();
            }
            starting = false;
            Finish();
            return request;
        }
    }
}
