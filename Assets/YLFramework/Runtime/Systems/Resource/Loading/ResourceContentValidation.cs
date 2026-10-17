using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;

namespace YLFramework.Services.Resource
{
    // 获取应用 key 的调用边界与后台纯 IO/认证分开；输入句柄保持至提交或取消真正收尾。
    internal sealed class ResourceContentValidation : IDisposable
    {
        private sealed class Input
        {
            internal ResourceCatalogFile File;
            internal ResourceBundleContext Context;
            internal ResourceBundleEnvelopeHeader Header;
            internal FileStream Stream;
            internal byte[] Key;
        }

        private readonly List<Input> inputs = new();
        private readonly Dictionary<(string, string), byte[]> keys = new();
        private readonly bool representativesOnly;
        private ResourceContentReservation reservation;
        private bool disposed;
        private bool disposalStarted;
        private bool validated;
        internal bool HasEncryptedContent => inputs.Any(x => x.Header != null);
        internal bool HasContentFiles => inputs.Count != 0;

        private ResourceContentValidation(bool representativesOnly) => this.representativesOnly = representativesOnly;

        internal static ResourceContentValidation Capture(IEnumerable<ResourceLocalSnapshot> snapshots, ResourceDecryptionOptions options,
            bool representativesOnly = false, ResourceContentBudget budget = null)
        {
            var result = new ResourceContentValidation(representativesOnly);
            try
            {
                foreach (var snapshot in snapshots)
                    foreach (var file in ContentFiles(snapshot))
                    {
                        var input = new Input { File = file, Context = Context(snapshot, file), Stream = snapshot.OpenContentFile(file) };
                        result.inputs.Add(input);
                        var header = ResourceBundleEnvelope.Inspect(input.Stream, input.Context);
                        if (header == null)
                        {
                            if (representativesOnly)
                            {
                                input.Stream.Dispose();
                                input.Stream = null;
                                result.inputs.Remove(input);
                            }
                            continue;
                        }
                        input.Header = header;
                        if (input.Stream.Length != file.Length) throw new InvalidDataException("Content delivery length changed: " + file.RelativeFile);
                        var pair = (snapshot.Document.ProjectId, header.KeyId);
                        if (!result.keys.TryGetValue(pair, out var key))
                        {
                            key = CopyKey(options, pair.Item1, pair.Item2);
                            result.keys.Add(pair, key);
                        }
                        input.Key = key;
                        if (representativesOnly)
                        {
                            var previous = result.inputs.FirstOrDefault(x => !ReferenceEquals(x, input) && ReferenceEquals(x.Key, key));
                            if (previous != null)
                            {
                                var discard = previous.File.Length <= file.Length ? input : previous;
                                discard.Stream.Dispose();
                                discard.Stream = null;
                                result.inputs.Remove(discard);
                            }
                        }
                    }
                if (result.HasEncryptedContent)
                {
                    if (options.MaxBufferedBytes < ResourceDecryptStream.DataBufferBytes)
                        throw new ResourceException(ResourceError.ContentBufferUnavailable, "The configured content buffer budget cannot hold one authenticated input.");
                    if (budget != null) result.reservation = budget.Reserve(1)[0];
                }
                return result;
            }
            catch (Exception error)
            {
                try { result.Dispose(); }
                catch (Exception cleanup)
                {
                    throw new ResourceException(Failure(error, "Content validation capture failed.").Kind,
                        "Content validation capture and cleanup failed; unfinished ownership is retained in the cause.",
                        new ResourceContentCleanupException(result, new AggregateException(error, cleanup)));
                }
                throw Failure(error, "Content validation capture failed.");
            }
        }

        internal void Validate(CancellationToken cancellation = default, Action<string> checkpoint = null)
        {
            if (disposalStarted) throw new ObjectDisposedException(nameof(ResourceContentValidation));
            if (inputs.Count == 0) { validated = true; return; }
            reservation?.Allocate();
            foreach (var input in inputs)
            {
                cancellation.ThrowIfCancellationRequested();
                checkpoint?.Invoke("ContentShaStarted");
                try { ResourceLocalSnapshot.VerifyContentFile(input.Stream, input.File, cancellation); }
                finally { checkpoint?.Invoke("ContentShaFinished"); }
                if (input.Header == null) continue;
                checkpoint?.Invoke("ContentAuthenticationStarted");
                try
                {
                    if (representativesOnly)
                    {
                        using var probe = ResourceBundleEnvelope.OpenDecrypt(input.Stream, input.Context, input.Key, true, cancellation);
                    }
                    else ResourceBundleEnvelope.ValidateAll(input.Stream, input.Context, input.Key, cancellation);
                }
                catch (Exception error) { throw Failure(error, "Content authentication failed for " + input.File.RelativeFile + "."); }
                finally { checkpoint?.Invoke("ContentAuthenticationFinished"); }
            }
            validated = true;
        }

        internal void RequireStable()
        {
            if (disposalStarted || !validated) throw new InvalidOperationException("Content validation has not completed or was released.");
            foreach (var input in inputs)
                if (!input.Stream.CanRead || input.Stream.Length != input.File.Length)
                    throw new InvalidDataException("A validated content input changed before commit: " + input.File.RelativeFile);
        }

        internal static IEnumerable<ResourceCatalogFile> ContentFiles(ResourceLocalSnapshot snapshot) => snapshot.Document.Bundles
            .Cast<ResourceCatalogFile>().Concat(snapshot.Document.SupportFiles.Where(x => x.RelativeFile == snapshot.Document.ManifestFile));

        internal static ResourceBundleContext Context(ResourceLocalSnapshot snapshot, ResourceCatalogFile file) => new(
            snapshot.Document.ProjectId, snapshot.Document.Platform, snapshot.Document.UnityCompatibility,
            file is ResourceCatalogBundle ? "Bundle" : "Manifest", file is ResourceCatalogBundle bundle ? bundle.LogicalId : "manifest");

        internal static ResourceBundleEnvelopeHeader Inspect(ResourceLocalSnapshot snapshot, ResourceCatalogFile file)
        {
            try
            {
                using var stream = snapshot.OpenContentFile(file);
                return ResourceBundleEnvelope.Inspect(stream, Context(snapshot, file));
            }
            catch (Exception error) { throw Failure(error, "Content header validation failed for " + file.RelativeFile + "."); }
        }

        internal static byte[] CopyKey(ResourceDecryptionOptions options, string project, string keyId)
        {
            if (options?.KeyProvider == null)
                throw new ResourceException(ResourceError.ContentKeyUnavailable, "An explicit content key provider is required for " + project + "/" + keyId + ".");
            var key = new byte[32];
            try
            {
                bool found;
                try { found = options.KeyProvider.TryCopyKey(project, keyId, key); }
                catch (Exception error)
                {
                    throw new ResourceException(ResourceError.ContentKeyUnavailable, "The application content key provider failed for " + project + "/" + keyId + ".",
                        new ProviderFailure(error));
                }
                if (!found) throw new ResourceException(ResourceError.ContentKeyUnavailable, "Content key is unavailable for " + project + "/" + keyId + ".");
                return key;
            }
            catch { Array.Clear(key, 0, key.Length); throw; }
        }

        // 保留诊断因果对象，但常规Exception/历史日志不能串出应用provider的敏感原文。
        private sealed class ProviderFailure : Exception
        {
            internal Exception OriginalCause { get; }
            internal ProviderFailure(Exception cause) : base("Application key provider failure; sensitive cause retained privately.") => OriginalCause = cause;
        }

        internal static ResourceException Failure(Exception cause, string message)
        {
            if (cause is ResourceException resource) return resource;
            var kind = cause is PlatformNotSupportedException || cause is InvalidDataException || cause is OverflowException
                ? ResourceError.ContentFormatUnsupported : ResourceError.LoadFailed;
            for (var current = cause; current != null; current = current.InnerException)
                if (current is CryptographicException) { kind = ResourceError.ContentAuthenticationFailed; break; }
            if (cause is OperationCanceledException) kind = ResourceError.Canceled;
            return new ResourceException(kind, message, cause, source: ResourceSourceId.AssetBundle);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposalStarted = true;
            foreach (var key in keys.Values) Array.Clear(key, 0, key.Length);
            keys.Clear();
            List<Exception> failures = null;
            foreach (var input in inputs)
            {
                if (input.Stream == null) continue;
                try { input.Stream.Dispose(); input.Stream = null; }
                catch (Exception error) { (failures ??= new()).Add(error); }
            }
            if (failures != null)
            {
                if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
                throw new AggregateException("Content input cleanup failed; unresolved inputs and budget remain owned.", failures);
            }
            reservation?.Dispose();
            reservation = null;
            inputs.Clear();
            disposed = true;
        }
    }
}
