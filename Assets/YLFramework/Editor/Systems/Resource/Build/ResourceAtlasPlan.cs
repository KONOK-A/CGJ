using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    public enum ResourceAtlasChangeKind { Create, Update, Current, Obsolete }

    /// <summary>脱离生成记录的只读预览。冲突、缺失或外部修改会明确抛错，不被伪装为可覆盖的 Update。</summary>
    public sealed class ResourceAtlasChange
    {
        public ResourceAtlasChangeKind Kind { get; internal set; }
        public string Key { get; internal set; }
        public string Path { get; internal set; }
        public string Guid { get; internal set; }
        public string GroupId { get; internal set; }
        public string Address { get; internal set; }
        public IReadOnlyList<string> Sources { get; internal set; }
        internal ResourceAtlasRule Rule;
        internal string InputHash;
    }

    /// <summary>只读规则展开及归属检查；不导入、保存、生成或准备图集。</summary>
    public sealed class ResourceAtlasPlan
    {
        public string DirectoryPath { get; }
        public IReadOnlyList<ResourceAtlasChange> Changes { get; }
        public bool NeedsGeneration => Changes.Any(x => x.Kind == ResourceAtlasChangeKind.Create || x.Kind == ResourceAtlasChangeKind.Update);
        public override string ToString() => DirectoryPath + "\n" + string.Join("\n", Changes.Select(x =>
            x.Kind + " " + x.Path + (x.Kind == ResourceAtlasChangeKind.Obsolete ? " (保留，未进入构建)" : " -> " + x.GroupId + " / " + x.Address)));

        internal ResourceAtlasPlan(ResourceProject project)
        {
            var configuration = project.AutomaticAtlases;
            DirectoryPath = ValidateOwnership(project);
            var changes = new List<ResourceAtlasChange>();
            var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var allSources = new HashSet<string>(StringComparer.Ordinal);
            var root = AssetDatabase.GUIDToAssetPath(configuration.RootGuid);
            if (configuration.Rules == null || configuration.Rules.Count > 256) throw new InvalidOperationException("Automatic Atlas rules must be bounded (0..256).");
            foreach (var rule in configuration.Rules)
            {
                if (rule == null) throw new InvalidOperationException("Null automatic Atlas rule.");
                ResourceAddress.ValidateId(rule.Id, nameof(rule.Id));
                if (!ruleIds.Add(rule.Id) || rule.Id.Length > 64) throw new InvalidOperationException("Duplicate/case-folded or overlong Atlas rule Id: " + rule.Id);
                if (project.Groups == null || project.Groups.Count(x => x != null && x.Id == rule.GroupId) != 1)
                    throw new InvalidOperationException("Atlas rule must target an existing unique Group: " + rule.Id + " -> " + rule.GroupId);
                _ = new ResourceAddress(project.ProjectId, rule.Address);
                if (!Enum.IsDefined(typeof(ResourceAtlasGrouping), rule.Grouping) || !Enum.IsDefined(typeof(FilterMode), rule.FilterMode) ||
                    !Enum.IsDefined(typeof(TextureImporterCompression), rule.Compression) || !new[] { 2, 4, 8 }.Contains(rule.Padding) ||
                    rule.MaxTextureSize < 32 || rule.MaxTextureSize > 8192 || (rule.MaxTextureSize & (rule.MaxTextureSize - 1)) != 0)
                    throw new InvalidOperationException("Invalid automatic Atlas settings: " + rule.Id);
                var sources = CollectSources(rule, root, DirectoryPath);
                foreach (var source in sources)
                    if (!allSources.Add(source)) throw new InvalidOperationException("A Sprite texture is selected by multiple automatic Atlas rules: " + source);
                var buckets = sources.GroupBy(path => rule.Grouping == ResourceAtlasGrouping.Together ? "" :
                    System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), StringComparer.Ordinal);
                foreach (var bucket in buckets.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (changes.Count >= 1024) throw new InvalidOperationException("Automatic Atlas output limit exceeded (1024).");
                    var key = rule.Id + (bucket.Key.Length == 0 ? "" : "/" + AssetDatabase.AssetPathToGUID(bucket.Key));
                    var fileName = key.Replace('/', '_') + ".spriteatlasv2";
                    var address = rule.Address + (bucket.Key.Length == 0 || bucket.Key == root ? "" : "/" + bucket.Key.Substring(root.Length + 1));
                    _ = new ResourceAddress(project.ProjectId, address);
                    if (!addresses.Add(address)) throw new InvalidOperationException("Automatic Atlas address conflict: " + address);
                    var paths = bucket.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    var sprites = paths.SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()).ToArray();
                    if (sprites.Length == 0 || sprites.Any(x => string.IsNullOrEmpty(x.name)) || sprites.GroupBy(x => x.name, StringComparer.Ordinal).Any(x => x.Count() != 1))
                        throw new InvalidOperationException("Automatic Atlas requires nonempty sprites with unique names: " + rule.Id);
                    // Unity 对象的 instanceID 跨导入/重载不稳定；指纹只记录规则值与源文件的持久身份/字节。
                    var input = new StringBuilder(string.Join("|", rule.Id, rule.GroupId, rule.Address, rule.Grouping, rule.AllowRotation,
                        rule.TightPacking, rule.Padding, rule.MaxTextureSize, rule.Readable, rule.MipMaps, rule.SRgb, rule.FilterMode, rule.Compression))
                        .Append('\n').Append(EditorUserBuildSettings.activeBuildTarget);
                    foreach (var path in paths) input.Append('\n').Append(path).Append('|').Append(HashFile(path)).Append('|').Append(HashFile(path + ".meta"));
                    var inputHash = HashText(input.ToString());
                    var record = configuration.Outputs.SingleOrDefault(x => x.Key == key);
                    changes.Add(new ResourceAtlasChange { Key = key, Path = DirectoryPath + "/" + fileName, Guid = record?.Guid,
                        Address = address, GroupId = rule.GroupId, Rule = rule, InputHash = inputHash, Sources = Array.AsReadOnly(paths),
                        Kind = record == null ? ResourceAtlasChangeKind.Create : record.InputHash == inputHash ? ResourceAtlasChangeKind.Current : ResourceAtlasChangeKind.Update });
                }
            }
            foreach (var record in configuration.Outputs.Where(x => changes.All(change => change.Key != x.Key)))
                changes.Add(new ResourceAtlasChange { Key = record.Key, Path = DirectoryPath + "/" + record.FileName, Guid = record.Guid,
                    Kind = ResourceAtlasChangeKind.Obsolete, Sources = Array.Empty<string>() });
            if (changes.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != changes.Count)
                throw new InvalidOperationException("Automatic Atlas output path conflict between rule identities.");
            var outputPaths = new HashSet<string>(configuration.Outputs.Select(x => DirectoryPath + "/" + x.FileName), StringComparer.OrdinalIgnoreCase);
            foreach (var change in changes.Where(x => x.Kind == ResourceAtlasChangeKind.Create))
                if (outputPaths.Contains(change.Path) || File.Exists(change.Path) || Directory.Exists(change.Path) || File.Exists(change.Path + ".meta"))
                    throw new InvalidOperationException("Atlas output identity conflict; refusing to adopt: " + change.Path);
            Changes = changes.AsReadOnly();
        }

        private static string[] CollectSources(ResourceAtlasRule rule, string root, string output)
        {
            var candidates = SourceCandidates(rule, root, output);
            foreach (var source in rule.Sources.Where(x => x is Texture2D))
                if (((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source))).textureType != TextureImporterType.Sprite)
                    throw new InvalidOperationException("Explicit automatic Atlas textures must already be imported as Sprite: " + AssetDatabase.GetAssetPath(source));
            return candidates.Where(path => AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType == TextureImporterType.Sprite).ToArray();
        }

        internal static string[] SourceCandidates(ResourceAtlasRule rule, string root, string output)
        {
            if (rule.Sources == null || rule.Sources.Length == 0 || rule.Sources.Length > 16384)
                throw new InvalidOperationException("Atlas rule requires a bounded nonempty source collection: " + rule.Id);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in rule.Sources)
            {
                if (!source) throw new InvalidOperationException("Missing automatic Atlas source: " + rule.Id);
                var path = AssetDatabase.GetAssetPath(source);
                if (!Within(root, path) || path == output || Within(output, path) || Within(path, output))
                    throw new InvalidOperationException("Atlas source must be within the dedicated root and must not overlap its output: " + path);
                ResourceBuildOperation.RejectLinks(path);
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var candidate in AssetDatabase.FindAssets("t:Texture2D", new[] { path }).Select(AssetDatabase.GUIDToAssetPath))
                        paths.Add(candidate);
                }
                else if (source is Texture2D && AssetImporter.GetAtPath(path) is TextureImporter) paths.Add(path);
                else throw new InvalidOperationException("Atlas sources must be folders or Sprite-imported Texture2D assets: " + path);
                if (paths.Count > 16384) throw new InvalidOperationException("Automatic Atlas source limit exceeded.");
            }
            // 必须在筛选 Sprite 之前拒绝未保存状态，否则内存 Importer 与磁盘输入指纹可能描述不同闭包。
            foreach (var path in paths)
            {
                ResourceBuildOperation.RejectLinks(path);
                if (EditorUtility.IsDirty(AssetImporter.GetAtPath(path)) || EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path)))
                    throw new InvalidOperationException("Unsaved automatic Atlas source/importer; save or revert it explicitly: " + path);
            }
            return paths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        internal static string ValidateOwnership(ResourceProject project)
        {
            ResourceEditorOperations.RequireEditableProject(project);
            var configuration = project.AutomaticAtlases;
            if (!ResourceAtlasGenerator.IsConfigured(project)) throw new InvalidOperationException("No registered automatic Atlas configuration.");
            var root = AssetDatabase.GUIDToAssetPath(project.AssetRootGuid);
            var configurationPath = AssetDatabase.GetAssetPath(configuration);
            var directory = System.IO.Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
            if (!File.Exists(configurationPath) || !File.Exists(configurationPath + ".meta") ||
                configuration.ConfigurationMetaHash != HashFile(configurationPath + ".meta"))
                throw new InvalidOperationException("Missing or externally modified automatic Atlas configuration/meta: " + configurationPath);
            if (configuration.ProjectGuid != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) ||
                configuration.RootGuid != project.AssetRootGuid || !AssetDatabase.IsValidFolder(root) ||
                !Within(root, directory) || System.IO.Path.GetDirectoryName(directory)?.Replace('\\', '/') != root ||
                configurationPath != directory + "/AutomaticAtlases.asset" || configuration.ConfigurationGuid != AssetDatabase.AssetPathToGUID(configurationPath) ||
                configuration.DirectoryGuid != AssetDatabase.AssetPathToGUID(directory) || configuration.DirectoryMetaHash != HashFile(directory + ".meta"))
                throw new InvalidOperationException("Automatic Atlas configuration/root ownership conflict: " + configurationPath);
            ResourceBuildOperation.ValidateAssetPath(directory);
            ResourceBuildOperation.RejectLinks(directory + ".meta");
            ResourceBuildMap.ValidateProjects(project, root);
            if (!AssetDatabase.IsOpenForEdit(configuration)) throw new InvalidOperationException("Automatic Atlas configuration is not editable.");
            if (configuration.Outputs == null || configuration.Outputs.Count > 1024 || configuration.Outputs.Any(x => x == null) ||
                configuration.Outputs.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.Outputs.Count ||
                configuration.Outputs.Select(x => x.FileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.Outputs.Count)
                throw new InvalidOperationException("Invalid automatic Atlas output ownership records.");
            var allowed = new HashSet<string>(StringComparer.Ordinal) { configurationPath, configurationPath + ".meta" };
            foreach (var record in configuration.Outputs)
            {
                if (string.IsNullOrEmpty(record.Key) || string.IsNullOrEmpty(record.FileName) || System.IO.Path.GetFileName(record.FileName) != record.FileName ||
                    !record.FileName.EndsWith(".spriteatlasv2", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid Atlas output record.");
                var path = directory + "/" + record.FileName;
                allowed.Add(path); allowed.Add(path + ".meta");
                if (!File.Exists(path) || !File.Exists(path + ".meta"))
                    throw new InvalidOperationException("Missing generated Atlas or meta; restore the registered identity before continuing: " + path);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (!atlas || record.Guid != AssetDatabase.AssetPathToGUID(path) || record.AssetHash != HashFile(path) || record.MetaHash != HashFile(path + ".meta") ||
                    EditorUtility.IsDirty(atlas) || EditorUtility.IsDirty(AssetImporter.GetAtPath(path)))
                    throw new InvalidOperationException("Generated Atlas was externally modified; refusing to overwrite: " + path);
            }
            foreach (var entry in Directory.GetFileSystemEntries(directory).Select(x => x.Replace('\\', '/')))
                if (!allowed.Contains(entry)) throw new InvalidOperationException("Unknown asset or meta in the registered Atlas directory: " + entry);
            return directory;
        }

        internal static bool Within(string root, string path) => !string.IsNullOrEmpty(root) && path != null && path.StartsWith(root + "/", StringComparison.Ordinal);
        internal static string HashFile(string path)
        {
            ResourceBuildOperation.RejectLinks(path);
            using var hash = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string HashText(string text)
        {
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
