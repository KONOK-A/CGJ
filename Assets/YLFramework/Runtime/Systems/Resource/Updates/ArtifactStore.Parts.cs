using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace YLFramework.Services.Resource
{
    public sealed partial class ArtifactStore
    {
        private void CleanupParts(ResourceProjectMount project, List<string> errors)
        {
            var root = System.IO.Path.Combine(ProjectRoot(project.ProjectId), "parts");
            ResourceLocalSnapshot.RejectLinks(root);
            if (File.Exists(root)) { errors.Add("Partial directory is occupied by an unknown file."); return; }
            if (!Directory.Exists(root)) return;
            foreach (var metadata in Directory.GetFiles(root, "*.json"))
                try
                {
                    var record = Read(metadata); ValidatePartial(record, metadata, project);
                    var part = System.IO.Path.ChangeExtension(metadata, ".part");
                    var next = metadata + ".next";
                    ValidatePartialFiles(metadata, record, project);
                    RequireOwner();
                    if (!JToken.DeepEquals(Read(metadata), record)) throw new InvalidDataException("Partial record changed during cleanup.");
                    if (File.Exists(part)) File.Delete(part);
                    if (File.Exists(next)) File.Delete(next);
                    File.Delete(metadata); // 最后删归属凭据；中途失败仍可安全重试。
                }
                catch (Exception error) when (IsFileFailure(error)) { errors.Add(System.IO.Path.GetFileName(metadata) + ": " + error.Message); }
            foreach (var next in Directory.GetFiles(root, "*.json.next"))
                try
                {
                    var metadata = next.Substring(0, next.Length - ".next".Length);
                    if (Exists(metadata) || Exists(System.IO.Path.ChangeExtension(metadata, ".part"))) continue;
                    ValidatePartial(Read(next), metadata, project); RequireOwner(); File.Delete(next);
                }
                catch (Exception error) when (IsFileFailure(error)) { errors.Add(System.IO.Path.GetFileName(next) + ": " + error.Message); }
            foreach (var unknown in Directory.GetFileSystemEntries(root)) errors.Add("Unknown download entry preserved: " + System.IO.Path.GetFileName(unknown));
        }

        private void ValidatePartial(JObject data, string path, ResourceProjectMount project)
        {
            ResourceCatalogCodec.Fields(data, "Version,OwnerId,State,ProjectId,Platform,CatalogDigest,File,Length,Sha256,Source,ETag");
            if (ResourceCatalogCodec.Integer(data, "Version") != 1 || ResourceCatalogCodec.Text(data, "OwnerId") != ownerId ||
                ResourceCatalogCodec.Text(data, "State") != "Partial" || ResourceCatalogCodec.Text(data, "ProjectId") != project.ProjectId ||
                ResourceCatalogCodec.Text(data, "Platform") != project.Platform || ResourceCatalogCodec.Integer(data, "Length") < 0)
                throw new InvalidDataException("Partial cleanup ownership mismatch.");
            var digest = ResourceCatalogCodec.Text(data, "CatalogDigest"); var sha = ResourceCatalogCodec.Text(data, "Sha256");
            ResourceCatalogCodec.Hex(digest, 64); ResourceCatalogCodec.Hex(sha, 64);
            var etag = ResourceCatalogCodec.Text(data, "ETag", true);
            if (etag != null && !ResourceDownloadResponse.StrongETag(etag)) throw new InvalidDataException("Invalid partial validator.");
            var file = ResourceCatalogCodec.Text(data, "File");
            if (data["Source"]?.Type != JTokenType.String || ((string)data["Source"]).Length > 4096) throw new InvalidDataException("Invalid partial source bound.");
            var source = (string)data["Source"];
            if (file == "." || file == ".." || file.Length > 128 || file.Any(x => !(x <= 127 && (char.IsLetterOrDigit(x) || x == '_' || x == '-' || x == '.'))) ||
                !Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https" || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || source != uri.AbsoluteUri || source.IndexOf('%') >= 0 ||
                !uri.AbsolutePath.EndsWith("/" + digest + "/" + file, StringComparison.Ordinal)) throw new InvalidDataException("Invalid partial target identity.");
            var token = ResourceCatalogCodec.Digest(Encoding.UTF8.GetBytes(source + ":" + sha)).Substring(0, 32);
            if (System.IO.Path.GetFileName(path) != token + ".json") throw new InvalidDataException("Partial record filename mismatch.");
        }

        private void ValidatePartialFiles(string metadata, JObject record, ResourceProjectMount project)
        {
            var part = System.IO.Path.ChangeExtension(metadata, ".part");
            ResourceLocalSnapshot.RejectLinks(part);
            if (Directory.Exists(part)) throw new InvalidDataException("Partial data path is occupied by an unknown directory.");
            var next = metadata + ".next";
            if (!Exists(next)) return;
            var candidate = Read(next); ValidatePartial(candidate, metadata, project);
            var current = (JObject)record.DeepClone(); current.Remove("ETag"); candidate.Remove("ETag");
            if (!JToken.DeepEquals(current, candidate)) throw new InvalidDataException("Partial candidate changed target ownership.");
        }

        /// <summary>parts只保存Partial记录。即使长度完整也必须实际hash；它从来不是可挂载版本或Complete凭据。</summary>
        internal sealed class DownloadPart
        {
            private readonly ArtifactStore store;
            private readonly JObject identity;
            private readonly string metadata;
            internal string Path { get; }
            internal string Validator { get; private set; }
            internal long Expected => (long)identity["Length"];
            internal long Length
            {
                get { Require(); return File.Exists(Path) ? new FileInfo(Path).Length : 0; }
            }

            internal DownloadPart(ArtifactStore store, ResourceRemoteRelease release, string file, long length, string sha256)
            {
                store.RequireOwner();
                this.store = store;
                ResourceCatalogCodec.Hex(sha256, 64);
                var source = release.Source.Resolve(release.CatalogDigest + "/" + file).AbsoluteUri;
                var token = ResourceCatalogCodec.Digest(Encoding.UTF8.GetBytes(source + ":" + sha256)).Substring(0, 32);
                Path = System.IO.Path.Combine(store.ProjectRoot(release.ProjectId), "parts", token + ".part");
                metadata = System.IO.Path.ChangeExtension(Path, ".json");
                identity = new JObject { ["Version"] = 1, ["OwnerId"] = store.ownerId, ["State"] = "Partial", ["ProjectId"] = release.ProjectId,
                    ["Platform"] = release.Platform, ["CatalogDigest"] = release.CatalogDigest, ["File"] = file, ["Length"] = length, ["Sha256"] = sha256, ["Source"] = source };
                ResourceLocalSnapshot.RejectLinks(Path); ResourceLocalSnapshot.RejectLinks(metadata);
                if (!File.Exists(metadata))
                {
                    if (Exists(Path) || Exists(metadata + ".next")) throw new InvalidDataException("Unregistered download partial; refusing adoption.");
                    store.Write(metadata, Record(null));
                }
                else
                {
                    var current = store.Read(metadata);
                    Validate(current);
                    Validator = (string)current["ETag"];
                    store.RemoveCandidate(metadata + ".next", Validate);
                }
                if (Length > Expected) Reset();
            }

            private JObject Record(string validator)
            { var data = (JObject)identity.DeepClone(); data["ETag"] = validator == null ? JValue.CreateNull() : new JValue(validator); return data; }

            private void Validate(JObject data)
            {
                var etag = ResourceCatalogCodec.Text(data, "ETag", true);
                if (etag != null && !ResourceDownloadResponse.StrongETag(etag) || !JToken.DeepEquals(data, Record(etag)))
                    throw new InvalidDataException("Download partial ownership or target identity mismatch.");
            }

            private void Require()
            {
                store.RequireOwner(); ResourceLocalSnapshot.RejectLinks(Path);
                if (!JToken.DeepEquals(store.Read(metadata), Record(Validator))) throw new InvalidDataException("Partial ownership changed during transfer.");
            }

            internal void Reset()
            {
                Require(); store.Write(metadata, Record(null)); Validator = null;
                ResourceLocalSnapshot.RejectLinks(Path);
                using var output = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.None);
                output.Flush(true);
            }

            internal FileStream OpenWrite(long offset, string validator)
            {
                Require();
                if (offset != 0 && (offset != Length || validator != Validator)) throw new InvalidDataException("Partial changed before append.");
                var strong = ResourceDownloadResponse.StrongETag(validator) ? validator : null;
                if (offset == 0) { store.Write(metadata, Record(null)); Validator = null; }
                ResourceLocalSnapshot.RejectLinks(Path);
                var output = new FileStream(Path, offset == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None);
                try
                {
                    if (Validator != strong) { store.Write(metadata, Record(strong)); Validator = strong; }
                    return output;
                }
                catch { output.Dispose(); throw; }
            }

            internal void Verify(CancellationToken cancellation = default)
            {
                Require();
                using var input = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                store.Checkpoint?.Invoke("PartShaStarted");
                try
                {
                    ResourceLocalSnapshot.VerifyContentFile(input, new ResourceCatalogFile(new JObject {
                        ["RelativeFile"] = identity["File"], ["Length"] = Expected, ["Sha256"] = identity["Sha256"] }), cancellation);
                }
                finally { store.Checkpoint?.Invoke("PartShaFinished"); }
            }
        }
    }
}
