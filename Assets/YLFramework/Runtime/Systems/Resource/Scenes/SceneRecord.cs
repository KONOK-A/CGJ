using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace YLFramework.Services.Resource
{
    /// <summary>独立于 caller Lease 的真实场景责任。handle + 加载序号 + Catalog 快照共同标识一次加载。</summary>
    public sealed class SceneRecord
    {
        internal readonly ResourceSystem Owner;
        internal BundleLease Retention;
        internal AsyncOperation UnloadOperation;
        internal readonly List<ResourceRequest<bool>> UnloadWaiters = new();
        internal int UnloadedFrame = -1;
        public Scene Scene
        {
            get;
        }
        public int Handle => Scene.handle;
        public long LoadSequence
        {
            get;
        }
        public ResourceAddress Address
        {
            get;
        }
        public System.Guid SnapshotId
        {
            get;
        }
        public string CatalogDigest
        {
            get;
        }
        public string Path
        {
            get;
        }
        public LoadSceneMode Mode
        {
            get;
        }
        public bool IsAmbiguous
        {
            get;
        }
        public bool IsUnloaded
        {
            get; internal set;
        }
        public bool IsLoaded => !IsUnloaded && Scene.IsValid() && Scene.isLoaded && Scene.path == Path;

        internal SceneRecord(ResourceSystem owner, Scene scene, ResourceCatalog catalog, SceneRequest request,
            ResourceAddress address, string path, bool ambiguous, BundleLease retention)
        {
            Owner = owner;
            Scene = scene;
            LoadSequence = request.LoadSequence;
            Address = address;
            SnapshotId = catalog.SnapshotId;
            CatalogDigest = catalog.CatalogDigest;
            Path = path;
            Mode = request.Mode;
            IsAmbiguous = ambiguous;
            Retention = retention;
        }
    }
}
