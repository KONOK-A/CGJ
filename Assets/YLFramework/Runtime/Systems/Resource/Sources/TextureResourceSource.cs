using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace YLFramework.Services.Resource
{
    /// <summary>只拥有本来源新创建/解码的 Texture，不纳管外部 Texture。完整 locator 保真，共享与消费者取消由 ResourceSystem 管理。</summary>
    internal sealed class TextureResourceSource : IResourceSource
    {
        private readonly bool local;
        private readonly ResourceImageOptions options;
        public ResourceSourceId Id => local ? ResourceSourceId.LocalTexture : ResourceSourceId.UrlTexture;
        public bool SupportsSynchronousLoad => local;
        internal TextureResourceSource(bool local, ResourceImageOptions options = null)
        { this.local = local; this.options = options ?? ResourceImageOptions.Default; }
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
            if (stream.Length <= 0 || stream.Length > options.MaximumEncodedBytes) throw new InvalidDataException("Local Texture byte limit exceeded or empty image.");
            var bytes = new byte[checked((int)stream.Length)];
            var offset = 0;
            while (offset < bytes.Length)
            {
                var count = stream.Read(bytes, offset, bytes.Length - offset);
                if (count == 0) throw new EndOfStreamException("The image file changed while reading.");
                offset += count;
            }
            return new ResourceSourceResult(Decode(bytes, options));
        }
        public ResourceSourceOperation LoadAsync(ResourceCatalogEntry entry, Type type)
        {
            if (local)
            {
                ResourceLocalSnapshot.RejectLinks(entry.Location.Locator);
                var file = new FileInfo(entry.Location.Locator);
                if (!file.Exists) throw new FileNotFoundException("Local Texture file is missing.", entry.Location.Locator);
                if (file.Length <= 0 || file.Length > options.MaximumEncodedBytes) throw new InvalidDataException("Local Texture byte limit exceeded or empty image.");
            }
            return new Operation(local ? new Uri(entry.Location.Locator).AbsoluteUri : entry.Location.Locator, options);
        }
        public void Release(UnityEngine.Object asset)
        {
            if (!asset) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset);
            else UnityEngine.Object.DestroyImmediate(asset);
        }

        private static Texture2D Decode(byte[] bytes, ResourceImageOptions options)
        {
            // 在 native 解码/像素分配前读取 PNG/JPEG 尺寸，拒绝压缩炸弹和不受支持的格式。
            var (width, height) = Dimensions(bytes);
            if (width <= 0 || height <= 0 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize ||
                (long)width * height > options.MaximumPixels)
                throw new InvalidDataException("Image dimensions exceed the configured pixel budget or GPU texture limit.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new InvalidDataException("PNG/JPEG image decoding failed.");
                if (texture.width != width || texture.height != height) throw new InvalidDataException("Decoded image dimensions do not match its header.");
                return texture;
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static (int width, int height) Dimensions(byte[] bytes)
        {
            if (bytes.Length >= 33 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10 &&
                bytes[8] == 0 && bytes[9] == 0 && bytes[10] == 0 && bytes[11] == 13 &&
                bytes[12] == 73 && bytes[13] == 72 && bytes[14] == 68 && bytes[15] == 82)
                return (Read32(bytes, 16), Read32(bytes, 20));
            if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216)
            {
                var offset = 2;
                while (offset < bytes.Length)
                {
                    if (bytes[offset++] != 255) break;
                    while (offset < bytes.Length && bytes[offset] == 255) offset++;
                    if (offset >= bytes.Length) break;
                    var marker = bytes[offset++];
                    if (marker == 217 || marker == 218) break;
                    if (marker == 1 || marker >= 208 && marker <= 215) continue;
                    if (offset + 2 > bytes.Length) break;
                    var length = (bytes[offset] << 8) | bytes[offset + 1];
                    if (length < 2 || length > bytes.Length - offset) break;
                    if (marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204)
                    {
                        if (length < 8) break;
                        return ((bytes[offset + 5] << 8) | bytes[offset + 6], (bytes[offset + 3] << 8) | bytes[offset + 4]);
                    }
                    offset += length;
                }
            }
            throw new InvalidDataException("A valid PNG or JPEG image header is required.");
        }

        private static int Read32(byte[] bytes, int offset)
            => (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];

        private sealed class ImageDownload : DownloadHandlerScript
        {
            private readonly int maximumBytes;
            private readonly MemoryStream bytes = new();
            internal bool LimitExceeded { get; private set; }
            internal ImageDownload(int maximumBytes) : base(new byte[16 * 1024]) => this.maximumBytes = maximumBytes;
            protected override void ReceiveContentLengthHeader(ulong length) => LimitExceeded |= length > (ulong)maximumBytes;
            protected override bool ReceiveData(byte[] data, int count)
            {
                if (LimitExceeded || count > maximumBytes - bytes.Length) { LimitExceeded = true; return false; }
                if (data != null && count > 0) bytes.Write(data, 0, count);
                return true;
            }
            internal byte[] CopyBytes() => bytes.ToArray();
            public override void Dispose() { bytes.Dispose(); base.Dispose(); }
        }

        private sealed class Operation : ResourceSourceOperation
        {
            private UnityWebRequest request;
            private UnityWebRequestAsyncOperation native;
            private readonly ImageDownload download;
            private readonly ResourceImageOptions options;
            public override bool SupportsCancellation => true;
            public override float Progress => request == null ? base.Progress : request.downloadProgress;
            internal Operation(string url, ResourceImageOptions options)
            {
                this.options = options;
                download = new ImageDownload(options.MaximumEncodedBytes);
                request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, download, null);
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
                    if (download.LimitExceeded) throw new InvalidDataException("Image download exceeded the configured encoded byte budget.");
                    if (request.result != UnityWebRequest.Result.Success)
                        throw new IOException($"Texture request failed ({request.responseCode}, {request.result}): {request.error}");
                    texture = Decode(download.CopyBytes(), options);
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
                if (IsDisposed) return;
                base.Dispose();
                request.Dispose();
                request = null;
                native = null;
            }
        }
    }
}
