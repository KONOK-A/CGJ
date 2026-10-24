using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using YLFramework.Resource;

namespace YLFramework.Editor.Resource
{
    public enum ResourceAtlasChangeKind
    {
        Create,
        Update,
        Current,
        Obsolete
    }

    /// <summary>脱离生成记录的只读预览。冲突、缺失或外部修改会明确抛错，不被伪装为可覆盖的 Update。</summary>
    public sealed class ResourceAtlasChange
    {
        public ResourceAtlasChangeKind Kind
        {
            get; internal set;
        }
        public string Key
        {
            get; internal set;
        }
        public string Path
        {
            get; internal set;
        }
        public string Guid
        {
            get; internal set;
        }
        public string GroupId
        {
            get; internal set;
        }
        public string Address
        {
            get; internal set;
        }
        public IReadOnlyList<string> Sources
        {
            get; internal set;
        }
        internal ResourceAtlasRule Rule;
        internal string InputHash;
    }

    /// <summary>只读规则展开及归属检查；不导入、保存、生成或准备图集。</summary>
    public sealed class ResourceAtlasPlan
    {
        public string DirectoryPath
        {
            get;
        }
        public IReadOnlyList<ResourceAtlasChange> Changes
        {
            get;
        }
        public bool NeedsGeneration => Changes.Any(x => x.Kind == ResourceAtlasChangeKind.Create || x.Kind == ResourceAtlasChangeKind.Update);
        public override string ToString() => DirectoryPath + "\n" + string.Join("\n", Changes.Select(x =>
            x.Kind + " " + x.Path + (x.Kind == ResourceAtlasChangeKind.Obsolete ? " (保留，未进入构建)" : " -> " + x.GroupId + " / " + x.Address)));

        internal ResourceAtlasPlan(ResourceProject project)
        {
            var configuration = project.automaticAtlases;
            DirectoryPath = ValidateOwnership(project);
            var changes = new List<ResourceAtlasChange>();
            var ruleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var addresses = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var allSources = new HashSet<string>(StringComparer.Ordinal);
            var directories = ResourceProjectPaths.Resolve(project, validateProjects: false);
            if (configuration.rules == null || configuration.rules.Count > 256)
            {
                throw new InvalidOperationException("Automatic Atlas rules must be bounded (0..256).");
            }

            foreach (var rule in configuration.rules)
            {
                if (rule == null)
                {
                    throw new InvalidOperationException("Null automatic Atlas rule.");
                }

                ResourceAddress.ValidateId(rule.id, nameof(rule.id));
                if (!ruleIds.Add(rule.id) || rule.id.Length > 64)
                {
                    throw new InvalidOperationException("Duplicate/case-folded or overlong Atlas rule Id: " + rule.id);
                }

                if (project.groups == null || project.groups.Count(x => x != null && x.id == rule.groupId) != 1)
                {
                    throw new InvalidOperationException("Atlas rule must target an existing unique Group: " + rule.id + " -> " + rule.groupId);
                }

                _ = new ResourceAddress(project.projectId, rule.address);
                if (!Enum.IsDefined(typeof(ResourceAtlasGrouping), rule.grouping) || !Enum.IsDefined(typeof(FilterMode), rule.filterMode) ||
                    !Enum.IsDefined(typeof(TextureImporterCompression), rule.compression) || !new[] { 2, 4, 8 }.Contains(rule.padding) ||
                    rule.maxTextureSize < 32 || rule.maxTextureSize > 8192 || (rule.maxTextureSize & (rule.maxTextureSize - 1)) != 0)
                {
                    throw new InvalidOperationException("Invalid automatic Atlas settings: " + rule.id);
                }

                var sources = CollectSources(rule, directories, DirectoryPath);
                foreach (var source in sources)
                {
                    if (!allSources.Add(source))
                    {
                        throw new InvalidOperationException("A Sprite texture is selected by multiple automatic Atlas rules: " + source);
                    }
                }

                var buckets = sources.GroupBy(path => rule.grouping == ResourceAtlasGrouping.Together ? "" :
                    System.IO.Path.GetDirectoryName(path).Replace('\\', '/'), StringComparer.Ordinal);
                foreach (var bucket in buckets.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    if (changes.Count >= 1024)
                    {
                        throw new InvalidOperationException("Automatic Atlas output limit exceeded (1024).");
                    }

                    var key = rule.id + (bucket.Key.Length == 0 ? "" : "/" + AssetDatabase.AssetPathToGUID(bucket.Key));
                    var fileName = key.Replace('/', '_') + ".spriteatlasv2";
                    var folderAddress = bucket.Key.Length == 0 ? "" : directories.DefaultAddress(bucket.Key);
                    var address = rule.address + (folderAddress.Length == 0 ? "" : "/" + folderAddress);
                    _ = new ResourceAddress(project.projectId, address);
                    var paths = bucket.OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (addresses.TryGetValue(address, out var previous))
                    {
                        throw new InvalidOperationException(
                        "自动图集输出地址冲突 (Automatic Atlas address conflict)：" + address + "\n" + string.Join("\n", previous) + "\n" + string.Join("\n", paths) +
                        "\n请为来源目录设置不同的地址前缀，或修改规则的输出地址。");
                    }

                    addresses.Add(address, paths);
                    var sprites = paths.SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()).ToArray();
                    if (sprites.Length == 0 || sprites.Any(x => string.IsNullOrEmpty(x.name)) || sprites.GroupBy(x => x.name, StringComparer.Ordinal).Any(x => x.Count() != 1))
                    {
                        throw new InvalidOperationException("Automatic Atlas requires nonempty sprites with unique names: " + rule.id);
                    }
                    // Unity 对象的 instanceID 跨导入/重载不稳定；指纹只记录规则值与源文件的持久身份/字节。
                    var input = new StringBuilder(string.Join("|", rule.id, rule.groupId, rule.address, rule.grouping, rule.allowRotation,
                        rule.tightPacking, rule.padding, rule.maxTextureSize, rule.readable, rule.mipMaps, rule.srgb, rule.filterMode, rule.compression))
                        .Append('\n').Append(EditorUserBuildSettings.activeBuildTarget);
                    foreach (var path in paths)
                    {
                        input.Append('\n').Append(path).Append('|').Append(HashFile(path)).Append('|').Append(HashFile(path + ".meta"));
                    }

                    var inputHash = HashText(input.ToString());
                    var record = configuration.outputs.SingleOrDefault(x => x.key == key);
                    changes.Add(new ResourceAtlasChange
                    {
                        Key = key,
                        Path = DirectoryPath + "/" + fileName,
                        Guid = record?.guid,
                        Address = address,
                        GroupId = rule.groupId,
                        Rule = rule,
                        InputHash = inputHash,
                        Sources = Array.AsReadOnly(paths),
                        Kind = record == null ? ResourceAtlasChangeKind.Create : record.inputHash == inputHash ? ResourceAtlasChangeKind.Current : ResourceAtlasChangeKind.Update
                    });
                }
            }
            foreach (var record in configuration.outputs.Where(x => changes.All(change => change.Key != x.key)))
            {
                changes.Add(new ResourceAtlasChange
                {
                    Key = record.key,
                    Path = DirectoryPath + "/" + record.fileName,
                    Guid = record.guid,
                    Kind = ResourceAtlasChangeKind.Obsolete,
                    Sources = Array.Empty<string>()
                });
            }

            if (changes.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != changes.Count)
            {
                throw new InvalidOperationException("Automatic Atlas output path conflict between rule identities.");
            }

            var outputPaths = new HashSet<string>(configuration.outputs.Select(x => DirectoryPath + "/" + x.fileName), StringComparer.OrdinalIgnoreCase);
            foreach (var change in changes.Where(x => x.Kind == ResourceAtlasChangeKind.Create))
            {
                if (outputPaths.Contains(change.Path) || File.Exists(change.Path) || Directory.Exists(change.Path) || File.Exists(change.Path + ".meta"))
                {
                    throw new InvalidOperationException("Atlas output identity conflict; refusing to adopt: " + change.Path);
                }
            }

            Changes = changes.AsReadOnly();
        }

        private static string[] CollectSources(ResourceAtlasRule rule, ResourceProjectPaths directories, string output)
        {
            var candidates = SourceCandidates(rule, directories, output);
            foreach (var source in rule.sources.Where(x => x is Texture2D))
            {
                if (((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source))).textureType != TextureImporterType.Sprite)
                {
                    throw new InvalidOperationException("Explicit automatic Atlas textures must already be imported as Sprite: " + AssetDatabase.GetAssetPath(source));
                }
            }

            return candidates.Where(path => AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType == TextureImporterType.Sprite).ToArray();
        }

        internal static string[] SourceCandidates(ResourceAtlasRule rule, ResourceProjectPaths directories, string output)
        {
            if (rule.sources == null || rule.sources.Length == 0 || rule.sources.Length > 16384)
            {
                throw new InvalidOperationException("Atlas rule requires a bounded nonempty source collection: " + rule.id);
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in rule.sources)
            {
                if (!source)
                {
                    throw new InvalidOperationException("Missing automatic Atlas source: " + rule.id);
                }

                var path = AssetDatabase.GetAssetPath(source);
                directories.RequireSource(path);
                if (ResourceProjectPaths.Overlaps(path, output))
                {
                    throw new InvalidOperationException("Atlas source must not overlap its output: " + path);
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var candidate in AssetDatabase.FindAssets("t:Texture2D", new[] { path }).Select(AssetDatabase.GUIDToAssetPath))
                    {
                        paths.Add(candidate);
                    }
                }
                else if (source is Texture2D && AssetImporter.GetAtPath(path) is TextureImporter)
                {
                    paths.Add(path);
                }
                else
                {
                    throw new InvalidOperationException("Atlas sources must be folders or Sprite-imported Texture2D assets: " + path);
                }

                if (paths.Count > 16384)
                {
                    throw new InvalidOperationException("Automatic Atlas source limit exceeded.");
                }
            }
            // 必须在筛选 Sprite 之前拒绝未保存状态，否则内存 Importer 与磁盘输入指纹可能描述不同闭包。
            foreach (var path in paths)
            {
                directories.RequireSource(path);
                if (EditorUtility.IsDirty(AssetImporter.GetAtPath(path)) || EditorUtility.IsDirty(AssetDatabase.LoadMainAssetAtPath(path)))
                {
                    throw new InvalidOperationException("Unsaved automatic Atlas source/importer; save or revert it explicitly: " + path);
                }
            }
            return paths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        internal static string ResolveGenerationParent(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException("自动图集缺少明确登记的生成父目录 GUID；不会从来源目录推断或接管生成范围。");
            }

            if (guid.Length != 32 || guid.Any(x => !Uri.IsHexDigit(x)))
            {
                throw new InvalidOperationException("图集生成父目录需要有效的 Unity GUID：" + guid);
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.IndexOf('\\') >= 0 || path.Split('/').Any(x => x.Length == 0 || x == "." || x == "..") ||
                !AssetDatabase.IsValidFolder(path) || AssetDatabase.AssetPathToGUID(path) != guid ||
                ResourceProjectPaths.Contains("Assets/StreamingAssets", path))
            {
                throw new InvalidOperationException("图集生成父目录失效或路径不受支持；请选择 Assets 根目录或现有子目录，不能使用 StreamingAssets 或 Packages：" + guid);
            }

            if (path == "Assets")
            {
                // 固定根只检查路径及祖先，避免把生成父目录解析变为全工程内容扫描。
                for (var directory = new DirectoryInfo(Application.dataPath); directory != null; directory = directory.Parent)
                {
                    if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("拒绝符号链接/junction 图集生成父目录：" + directory.FullName);
                    }
                }
            }
            else
            {
                ResourceBuildOperation.ValidateAssetPath(path);
            }

            return path;
        }

        internal static string ValidateOwnership(ResourceProject project)
        {
            ResourceEditorOperations.RequireEditableProject(project);
            var configuration = project.automaticAtlases;
            if (!ResourceAtlasGenerator.IsConfigured(project))
            {
                throw new InvalidOperationException("No registered automatic Atlas configuration.");
            }

            ResourceProjectPaths.Resolve(project);
            var parent = ResolveGenerationParent(configuration.generationParentGuid);
            var configurationPath = AssetDatabase.GetAssetPath(configuration);
            var directory = System.IO.Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
            if (!File.Exists(configurationPath) || !File.Exists(configurationPath + ".meta") ||
                configuration.configurationMetaHash != HashFile(configurationPath + ".meta"))
            {
                throw new InvalidOperationException("Missing or externally modified automatic Atlas configuration/meta: " + configurationPath);
            }

            if (configuration.projectGuid != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) ||
                !AssetDatabase.IsValidFolder(parent) || !ResourceProjectPaths.Contains(parent, directory) ||
                System.IO.Path.GetDirectoryName(directory)?.Replace('\\', '/') != parent ||
                configurationPath != directory + "/AutomaticAtlases.asset" || configuration.configurationGuid != AssetDatabase.AssetPathToGUID(configurationPath) ||
                configuration.directoryGuid != AssetDatabase.AssetPathToGUID(directory) || configuration.directoryMetaHash != HashFile(directory + ".meta"))
            {
                throw new InvalidOperationException("Automatic Atlas configuration/root ownership conflict: " + configurationPath);
            }

            ResourceBuildOperation.ValidateAssetPath(directory);
            ResourceBuildOperation.RejectLinks(directory + ".meta");
            if (!AssetDatabase.IsOpenForEdit(configuration))
            {
                throw new InvalidOperationException("Automatic Atlas configuration is not editable.");
            }

            if (configuration.outputs == null || configuration.outputs.Count > 1024 || configuration.outputs.Any(x => x == null) ||
                configuration.outputs.Select(x => x.key).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.outputs.Count ||
                configuration.outputs.Select(x => x.fileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != configuration.outputs.Count)
            {
                throw new InvalidOperationException("Invalid automatic Atlas output ownership records.");
            }

            var allowed = new HashSet<string>(StringComparer.Ordinal) { configurationPath, configurationPath + ".meta" };
            foreach (var record in configuration.outputs)
            {
                if (string.IsNullOrEmpty(record.key) || string.IsNullOrEmpty(record.fileName) || System.IO.Path.GetFileName(record.fileName) != record.fileName ||
                    !record.fileName.EndsWith(".spriteatlasv2", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Invalid Atlas output record.");
                }

                var path = directory + "/" + record.fileName;
                allowed.Add(path);
                allowed.Add(path + ".meta");
                if (!File.Exists(path) || !File.Exists(path + ".meta"))
                {
                    throw new InvalidOperationException("Missing generated Atlas or meta; restore the registered identity before continuing: " + path);
                }

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (!atlas || record.guid != AssetDatabase.AssetPathToGUID(path) || record.assetHash != HashFile(path) || record.metaHash != HashFile(path + ".meta") ||
                    EditorUtility.IsDirty(atlas) || EditorUtility.IsDirty(AssetImporter.GetAtPath(path)))
                {
                    throw new InvalidOperationException("Generated Atlas was externally modified; refusing to overwrite: " + path);
                }
            }
            foreach (var entry in Directory.GetFileSystemEntries(directory).Select(x => x.Replace('\\', '/')))
            {
                if (!allowed.Contains(entry))
                {
                    throw new InvalidOperationException("Unknown asset or meta in the registered Atlas directory: " + entry);
                }
            }

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
