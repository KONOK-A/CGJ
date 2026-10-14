using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>只从本构建内核的完整 Workspace 快照生成目录，不读取旧 ResData 或 XFAB 映射。</summary>
    public static class ResourceCatalogBuilder
    {
        public static ResourceCatalogDocument Create(ResourceBuildResult result, string appContentCompatibilityId)
        {
            using var locked = LockWorkspace(result);
            return CreateLocked(result, appContentCompatibilityId);
        }

        internal static ResourceCatalogDocument CreateLocked(ResourceBuildResult result, string app)
        {
            JObject FileRecord(ResourceBuiltFile file) => new JObject { ["RelativeFile"] = PublishedFile(result, file.Name), ["Length"] = file.Length, ["Sha256"] = file.Sha256 };
            JObject Asset(ResourceBuildAsset main, ResourceBuiltSubAsset sub = null) => new JObject
            {
                ["AssetId"] = main.Guid + "_" + (sub?.LocalId ?? main.LocalId).ToString(CultureInfo.InvariantCulture),
                ["Guid"] = main.Guid, ["LocalId"] = sub?.LocalId ?? main.LocalId, ["Address"] = main.Address.Path,
                ["SubAssetKey"] = sub?.LocalId.ToString(CultureInfo.InvariantCulture), ["BundleId"] = main.BundleName,
                ["Aliases"] = new JArray(main.Aliases.Select(x => x.Path)),
                ["LoadName"] = main.IsScene ? main.SourcePath : main.SourcePath.ToLowerInvariant(),
                ["AssetType"] = main.IsScene ? "Scene" : (sub?.AssetType ?? main.AssetType).FullName,
                ["AssetAssembly"] = main.IsScene ? null : (sub?.AssetType ?? main.AssetType).Assembly.GetName().Name,
                ["Kind"] = main.IsScene ? "Scene" : sub == null ? "Asset" : "SubAsset", ["SubAssetName"] = sub?.Name
            };
            var bundles = new JArray();
            foreach (var bundle in result.Bundles)
            {
                var data = FileRecord(result.Files.Single(x => x.Name == bundle.Name));
                data["LogicalId"] = bundle.Name; data["NativeName"] = bundle.Name; data["UnityHash"] = bundle.UnityHash;
                data["IsScene"] = result.Map.Bundles.Single(x => x.Name == bundle.Name).IsScene;
                data["Dependencies"] = new JArray(bundle.Dependencies);
                bundles.Add(data);
            }
            var payload = new JObject
            {
                ["SchemaVersion"] = 1, ["ProjectId"] = result.Map.ProjectId, ["Platform"] = result.Platform,
                ["UnityCompatibility"] = result.UnityCompatibility, ["AppContentCompatibilityId"] = app,
                ["BuildSettingsFingerprint"] = result.BuildSettingsFingerprint, ["ManifestFile"] = "manifest",
                ["Bundles"] = bundles,
                ["Assets"] = new JArray(result.Map.Assets.Select(x => Asset(x)).Concat(result.SubAssets.Select(x => Asset(x.MainAsset, x)))),
                ["Groups"] = new JArray(result.Map.Groups.Select(x => new JObject { ["Id"] = x.Key,
                    ["Members"] = new JArray(x.Value.Select(id => new JObject { ["AssetId"] = id, ["Required"] = true })) })),
                ["SupportFiles"] = new JArray(result.Files.Where(x => !result.Bundles.Any(b => b.Name == x.Name)).Select(FileRecord))
            };
            var bytes = ResourceCatalogCodec.Seal(Encoding.UTF8.GetBytes(payload.ToString(Formatting.None)));
            return ResourceCatalogCodec.Read(bytes, result.Map.ProjectId, result.Platform, result.UnityCompatibility, app);
        }

        // 物理名不是 native identity。短 token 防止摘要版本目录 + native 命名空间超过 Mono Windows 的文件路径限制。
        internal static string PublishedFile(ResourceBuildResult result, string workspaceFile)
        {
            var sidecar = workspaceFile.EndsWith(".manifest", StringComparison.Ordinal);
            var native = sidecar ? workspaceFile.Substring(0, workspaceFile.Length - 9) : workspaceFile;
            var file = native == result.ManifestBundleFile ? "manifest" : "b_" + ResourceBuildMap.Token(native);
            return file + (sidecar ? ".manifest" : "");
        }

        // 锁住可变缓存并逐文件复验；旧 result 不能悄悄改指向当前另一代 Workspace 产物。
        internal static FileStream LockWorkspace(ResourceBuildResult result)
        {
            if (result?.Map == null || result.Files == null || result.Bundles == null || result.SubAssets == null)
                throw new InvalidOperationException("A verified ResourceBuildResult is required.");
            var workspace = new ResourceBuildWorkspace(result.Map, result.BuildSettingsFingerprint, result.Platform);
            if (workspace.Output != result.WorkspacePath || workspace.Leaf != result.ManifestBundleFile)
                throw new InvalidOperationException("Build result workspace identity mismatch.");
            var record = workspace.Read();
            if (record == null || !record.Complete) throw new InvalidOperationException("Workspace is not complete.");
            var locked = workspace.AcquireLock(ref record);
            try
            {
                if (!record.Complete || record.Fingerprint != result.InputFingerprint ||
                    !result.Files.Select(x => x.Name).SequenceEqual(workspace.ExpectedFiles()) ||
                    !record.Files.Select(x => x.Name + ":" + x.Length + ":" + x.Sha256)
                        .SequenceEqual(result.Files.Select(x => x.Name + ":" + x.Length + ":" + x.Sha256)))
                    throw new InvalidOperationException("Workspace changed since the verified build result.");
                foreach (var file in result.Files)
                {
                    var path = Path.Combine(workspace.Output, file.Name);
                    using var input = File.OpenRead(path);
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    if (input.Length != file.Length || BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant() != file.Sha256)
                        throw new InvalidDataException("Workspace artifact integrity mismatch: " + file.Name);
                }
                return locked;
            }
            catch { locked.Dispose(); throw; }
        }
    }
}
