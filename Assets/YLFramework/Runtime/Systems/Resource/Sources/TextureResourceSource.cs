using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace YLFramework.Services.Resource
{
    /// <summary>只拥有本来源新创建/解码的 Texture，不纳管外部 Texture。完整 locator 保真，共享与消费者取消由 ResourceSystem 管理。</summary>
    internal sealed class TextureResourceSource : IResourceSource
    {
        private const int MaximumLocalBytes = 32 * 1024 * 1024;
        private readonly bool local;
        public ResourceSourceId Id => local ? ResourceSourceId.LocalTexture : ResourceSourceId.UrlTexture;
        public bool SupportsSynchronousLoad => local;
        internal TextureResourceSource(bool local) => this.local = local;
        public void Validate(ResourceCatalogEntry entry)
        {
            if (entry.Location.AssetType != typeof(Texture2D) || entry.Address.SubAsset != null || entry.Dependencies.Count != 0)
                throw new ArgumentException("Texture sources require a Texture2D without explicit dependencies/subasset selection.");
            var locator = entry.Location.Locator;
            if (local)
            {
                if (!Path.IsPathFullyQualified(locator)) throw new ArgumentException("Local Texture requires an absolute file path.");
                ResourceLocalSnapshot.RejectLinks(locator);
            }
            else if (!Uri.TryCreate(locator, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https") ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("URL Texture requires an explicit HTTP(S) URL without user-info or fragment.");
        }
        public ResourceSourceResult Load(ResourceCatalogEntry entry, Type type)
        {
            if (!local) throw new ResourceException(ResourceError.Unsupported, "URL Texture cannot load synchronously.");
            var path = entry.Location.Locator;
            ResourceLocalSnapshot.RejectLinks(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= 0 || stream.Length > MaximumLocalBytes) throw new InvalidDataException("Local Texture byte limit exceeded or empty image.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var texture = new Texture2D(2, 2);
            try
            {
                if (!ImageConversion.LoadImage(texture, memory.ToArray(), true)) throw new InvalidDataException("Local Texture image decoding failed.");
                return new ResourceSourceResult(texture);
            }
            catch { Release(texture); throw; }
        }
        public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type)
        {
            if (local)
            {
                ResourceLocalSnapshot.RejectLinks(entry.Location.Locator);
                var file = new FileInfo(entry.Location.Locator);
                if (!file.Exists) throw new FileNotFoundException("Local Texture file is missing.", entry.Location.Locator);
                if (file.Length <= 0 || file.Length > MaximumLocalBytes) throw new InvalidDataException("Local Texture byte limit exceeded or empty image.");
            }
            return new Operation(local ? new Uri(entry.Location.Locator).AbsoluteUri : entry.Location.Locator);
        }
        public void Release(UnityEngine.Object asset)
        {
            if (!asset) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }

        private sealed class Operation : ResourceSourceOperation
        {
            private UnityWebRequest request;
            private UnityWebRequestAsyncOperation native;
            public override bool SupportsCancellation => true;
            public override float Progress => request == null ? base.Progress : request.downloadProgress;
            internal Operation(string url)
            {
                request = UnityWebRequestTexture.GetTexture(url, true);
                request.timeout = 30;
                try
                {
                    native = request.SendWebRequest();
                    native.completed += OnCompleted;
                    if (native.isDone) OnCompleted(native);
                }
                catch { request.Dispose(); request = null; throw; }
            }
            private void OnCompleted(AsyncOperation operation)
            {
                if (IsDone) return;
                native.completed -= OnCompleted;
                Texture2D texture = null;
                Exception error = null;
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new IOException($"Texture request failed ({request.responseCode}, {request.result}): {request.error}");
                    texture = DownloadHandlerTexture.GetContent(request);
                    if (!texture) throw new InvalidDataException("Texture response has no decoded image.");
                }
                catch (Exception cause) { error = cause; }
                Complete(texture, error);
            }
            public override void Cancel()
            {
                if (IsDone) return;
                var operation = native;
                request.Abort();
                if (!IsDone && operation.isDone) OnCompleted(operation);
            }
            public override void Dispose()
            {
                base.Dispose();
                request.Dispose();
                request = null;
                native = null;
            }
        }
    }
}
