using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine.Networking;

namespace YLFramework.Services.Resource
{
    /// <summary>单个有界GET。由ResourceSystem现有主线程tick驱动；Dispose同步撤销私有UWR和文件句柄。</summary>
    internal sealed partial class ResourceTransfer : IEnumerator, IDisposable
    {
        private readonly ResourceRemoteSource source;
        private readonly Uri uri;
        private readonly long length;
        private readonly string digest;
        private readonly long maximum;
        private readonly ArtifactStore.DownloadPart part;
        private readonly bool deferPartVerification;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new();
        private UnityWebRequest request;
        private UnityWebRequestAsyncOperation native;
        private Sink sink;
        private int attempt;
        private double started;
        private double retryAt;
        private bool done;
        internal byte[] Bytes { get; private set; }
        internal float Progress => length <= 0 ? 0 : (float)(sink?.Written ?? part?.Length ?? 0) / length;
        public object Current => null;

        internal ResourceTransfer(ResourceRemoteSource source, Uri uri, long length = 0, string digest = null, long maximum = ResourceReleaseSignature.MaximumBytes,
            ArtifactStore.DownloadPart part = null, bool deferPartVerification = false)
        {
            this.source = source; this.uri = uri; this.length = length; this.digest = digest; this.maximum = length > 0 ? length : maximum; this.part = part;
            this.deferPartVerification = deferPartVerification;
            if (uri.Scheme != "https" || !source.Endpoint.IsBaseOf(uri) || maximum < 1 || length < 0) throw new ArgumentException("An owned bounded HTTPS transfer is required.");
        }

        public bool MoveNext()
        {
            if (done) return false;
            if (request == null)
            {
                if (clock.Elapsed.TotalSeconds < retryAt) return true;
                if (part != null && part.Length == length)
                {
                    if (deferPartVerification) { done = true; return false; }
                    try { part.Verify(); done = true; return false; }
                    catch (InvalidDataException) { part.Reset(); }
                }
                Start();
            }
            if (!native.isDone)
            {
                if (clock.Elapsed.TotalSeconds - started < source.TimeoutSeconds && clock.Elapsed.TotalSeconds - sink.LastData < source.IdleTimeoutSeconds) return true;
                request.Abort();
                return Failure(new IOException("Release GET timed out without completing."), true);
            }
            sink.Close();
            if (sink.Error is ResourceRangeException rangeError) { part?.Reset(); return Failure(rangeError, true); }
            if (sink.Error != null) { var error = sink.Error; Close(); done = true; throw error; }
            if (request.responseCode == 416 && part != null) { part.Reset(); return Failure(new ResourceRangeException(), true); }
            if (request.result != UnityWebRequest.Result.Success || request.responseCode != 200 && request.responseCode != 206)
                return Failure(new IOException("Release GET failed (HTTP " + request.responseCode + ", " + request.result + ")."),
                    request.result == UnityWebRequest.Result.ConnectionError && (request.responseCode == 0 || request.responseCode == 200 || request.responseCode == 206) ||
                    request.responseCode == 408 || request.responseCode == 429 || request.responseCode >= 500 && request.responseCode <= 599);
            try
            {
                sink.ValidateHeaders(); // 空body也不得绕过响应合同。
                sink.Close();
                if (length > 0 && sink.Written != length || sink.Written == 0) throw new IOException("Release GET ended with an incomplete length.");
                if (part != null) { if (!deferPartVerification) part.Verify(); }
                else
                {
                    Bytes = sink.Bytes();
                    if (digest != null && ResourceCatalogCodec.Digest(Bytes) != digest) throw new InvalidDataException("Release response digest mismatch.");
                }
                done = true; Close(); return false;
            }
            catch (ResourceRangeException error) { sink.Close(); part?.Reset(); return Failure(error, true); }
            catch (InvalidDataException) { Close(); part?.Reset(); done = true; throw; }
            catch (IOException error) { return Failure(error, true); }
        }

        private void Start()
        {
            attempt++;
            var offset = part?.Length ?? 0;
            var validator = part?.Validator;
            if (offset > 0 && !ResourceDownloadResponse.StrongETag(validator)) { part.Reset(); offset = 0; }
            request = new UnityWebRequest(uri.AbsoluteUri, "GET") { redirectLimit = 0, timeout = source.TimeoutSeconds };
            try
            {
                request.certificateHandler = source.CreateCertificateHandler();
                request.SetRequestHeader("Accept-Encoding", "identity");
                if (offset > 0)
                {
                    request.SetRequestHeader("Range", "bytes=" + offset.ToString(CultureInfo.InvariantCulture) + "-");
                    request.SetRequestHeader("If-Range", validator);
                }
                sink = new Sink(request, length, maximum, offset, validator, part, clock);
                request.downloadHandler = sink;
                started = clock.Elapsed.TotalSeconds;
                native = request.SendWebRequest();
            }
            catch { Close(); throw; }
        }

        private bool Failure(Exception error, bool retry)
        {
            var delay = Math.Min(8, .25 * Math.Pow(2, attempt - 1)) + random.NextDouble() * .1;
            var retryAfter = request?.GetResponseHeader("Retry-After");
            if (ResourceDownloadResponse.Number(retryAfter, out var seconds)) delay = Math.Max(delay, Math.Min(10, seconds));
            else if (DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                delay = Math.Max(delay, Math.Min(10, Math.Max(0, (date - DateTimeOffset.UtcNow).TotalSeconds)));
            Close();
            if (!retry || attempt >= source.Attempts) { done = true; throw error; }
            retryAt = clock.Elapsed.TotalSeconds + delay;
            return true;
        }

        private void Close()
        {
            try
            {
                if (request != null && native != null && !native.isDone) request.Abort();
                sink?.Close();
            }
            finally { request?.Dispose(); request = null; native = null; sink = null; }
        }
        public void Dispose() { done = true; Close(); }
        public void Reset() => throw new NotSupportedException();

        private sealed class Sink : DownloadHandlerScript
        {
            private readonly UnityWebRequest request;
            private readonly long expected;
            private readonly long maximum;
            private readonly long offset;
            private readonly string validator;
            private readonly ArtifactStore.DownloadPart part;
            private readonly Stopwatch clock;
            private Stream output;
            private readonly MemoryStream memory;
            private bool headers;
            private long discarded;
            internal Exception Error { get; private set; }
            internal long Written { get; private set; }
            internal double LastData { get; private set; }

            internal Sink(UnityWebRequest request, long expected, long maximum, long offset, string validator, ArtifactStore.DownloadPart part, Stopwatch clock)
                : base(new byte[64 * 1024])
            {
                this.request = request; this.expected = expected; this.maximum = maximum; this.offset = offset; this.validator = validator; this.part = part; this.clock = clock;
                if (part == null) memory = new MemoryStream();
                LastData = clock.Elapsed.TotalSeconds;
            }

            internal void ValidateHeaders()
            {
                if (headers) return;
                var start = ResourceDownloadResponse.Offset(request.responseCode, offset, expected, validator, request.GetResponseHeader);
                var size = request.GetResponseHeader("Content-Length");
                if (size != null && (!ResourceDownloadResponse.Number(size, out var count) || count > maximum - start))
                    throw new InvalidDataException("Release response exceeds its byte limit.");
                output = part == null ? memory : part.OpenWrite(start, request.GetResponseHeader("ETag"));
                Written = start;
                headers = true;
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                try
                {
                    LastData = clock.Elapsed.TotalSeconds;
                    if (request.responseCode != 200 && request.responseCode != 206)
                    { discarded += dataLength; return discarded <= ResourceReleaseSignature.MaximumBytes; }
                    ValidateHeaders();
                    if (dataLength < 0 || dataLength > maximum - Written) throw new InvalidDataException("Release response exceeded its byte limit.");
                    output.Write(data, 0, dataLength); Written += dataLength;
                    return true;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is InvalidDataException)
                { Error = error; return false; }
            }

            internal byte[] Bytes() => memory.ToArray();
            internal void Close()
            {
                if (output == null || ReferenceEquals(output, memory)) return;
                try { ((FileStream)output).Flush(true); }
                finally { output.Dispose(); output = null; }
            }
        }
    }
}
