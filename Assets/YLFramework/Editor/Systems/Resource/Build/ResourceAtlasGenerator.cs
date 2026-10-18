using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    /// <summary>只维护显式登记的自动 Atlas；最终选择、构建及 Runtime 所有权继续由既有链负责。</summary>
    public static class ResourceAtlasGenerator
    {
        private static bool generating;

        /// <summary>在明确选择的 Assets 根目录或现有子目录中新建并登记自有子目录；不会接管父目录或已有内容。</summary>
        public static ResourceAtlasConfiguration CreateConfiguration(ResourceProject project, string generationParentPath, string folderName)
        {
            ResourceEditorOperations.RequireEditableProject(project);
            ResourceBundleBuilder.ValidateEditor(EditorUserBuildSettings.activeBuildTarget);
            if (generating) throw new InvalidOperationException("Automatic Atlas generation is already running.");
            if (IsConfigured(project) || EditorUtility.IsDirty(project)) throw new InvalidOperationException("Save the Project and use its existing Atlas configuration, if registered.");
            ResourceProjectPaths.Resolve(project);
            if (string.IsNullOrEmpty(generationParentPath))
                throw new InvalidOperationException("请明确选择 Assets 根目录或现有子目录作为图集生成父目录；不能使用 StreamingAssets 或 Packages。");
            var parentGuid = AssetDatabase.AssetPathToGUID(generationParentPath);
            var parent = ResourceAtlasPlan.ResolveGenerationParent(parentGuid);
            if (parent != generationParentPath)
                throw new InvalidOperationException("图集生成父目录与其登记 GUID 不一致。");
            if (string.IsNullOrEmpty(folderName) || folderName.Length > 64 || folderName.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
                throw new InvalidOperationException("Atlas output folder must be a single explicit name (letters, digits, '_' or '-').");
            var directory = parent + "/" + folderName;
            ResourceBuildOperation.ValidateAssetPath(directory);
            ResourceBuildOperation.RejectLinks(directory + ".meta");
            ResourceProjectPaths.ValidateProjects(project, directory);
            if (Directory.Exists(directory) || File.Exists(directory) || File.Exists(directory + ".meta"))
                throw new InvalidOperationException("Refusing to adopt an existing Atlas output directory or meta: " + directory);
            var projectPath = AssetDatabase.GetAssetPath(project);
            var projectGuid = AssetDatabase.AssetPathToGUID(projectPath);
            if (!HasExpectedGuid(projectPath, projectGuid)) throw new InvalidOperationException("Resource Project meta identity was externally modified; not adopted.");
            var projectBytes = File.ReadAllBytes(projectPath);
            var projectJson = EditorJsonUtility.ToJson(project);
            var projectMetaHash = ResourceAtlasPlan.HashFile(projectPath + ".meta");
            var configuration = ScriptableObject.CreateInstance<ResourceAtlasConfiguration>();
            string boundProjectJson = null;
            string directoryGuid = null;
            string directoryMetaHash = null;
            string configurationGuid = null;
            string configurationMetaHash = null;
            byte[] configurationBytes = null;
            var createdDirectory = directory;
            var created = false;
            var createdConfiguration = false;
            generating = true;
            try
            {
                var guid = directoryGuid = AssetDatabase.CreateFolder(parent, folderName);
                if (string.IsNullOrEmpty(guid)) throw new IOException("Cannot create Atlas directory: " + directory);
                created = true;
                configuration.DirectoryGuid = guid;
                createdDirectory = AssetDatabase.GUIDToAssetPath(guid);
                if (createdDirectory != directory || !HasExpectedGuid(directory, guid)) throw new InvalidOperationException("New Atlas directory identity was externally modified; not adopted.");
                configuration.ProjectGuid = projectGuid;
                configuration.GenerationParentGuid = parentGuid;
                configuration.DirectoryMetaHash = directoryMetaHash = ResourceAtlasPlan.HashFile(directory + ".meta");
                var path = directory + "/AutomaticAtlases.asset";
                AssetDatabase.CreateAsset(configuration, path);
                createdConfiguration = true;
                configuration.ConfigurationGuid = configurationGuid = AssetDatabase.AssetPathToGUID(path);
                if (!HasExpectedGuid(path, configuration.ConfigurationGuid)) throw new InvalidOperationException("New Atlas configuration meta was externally modified; not adopted.");
                if (configuration.ProjectGuid != projectGuid || configuration.DirectoryGuid != guid ||
                    configuration.GenerationParentGuid != parentGuid || configuration.Rules == null || configuration.Rules.Count != 0 ||
                    configuration.Outputs == null || configuration.Outputs.Count != 0)
                    throw new InvalidOperationException("New Atlas configuration ownership changed during creation; not adopted.");
                configuration.ConfigurationMetaHash = configurationMetaHash = ResourceAtlasPlan.HashFile(path + ".meta");
                RequireRegistrationInputs();
                EditorUtility.SetDirty(configuration); AssetDatabase.SaveAssetIfDirty(configuration);
                configurationBytes = File.ReadAllBytes(path);
                RequireRegistrationInputs();
                if (!HasExpectedGuid(directory, directoryGuid) || ResourceAtlasPlan.HashFile(directory + ".meta") != directoryMetaHash ||
                    !HasExpectedGuid(path, configurationGuid) || ResourceAtlasPlan.HashFile(path + ".meta") != configurationMetaHash)
                    throw new InvalidOperationException("New Atlas ownership/meta changed during registration; not adopted.");
                ResourceProjectPaths.ValidateProjects(project, directory);
                project.AutomaticAtlases = configuration;
                project.AutomaticAtlasConfigurationGuid = configurationGuid;
                boundProjectJson = EditorJsonUtility.ToJson(project);
                EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
                if (EditorUtility.IsDirty(project) || EditorJsonUtility.ToJson(project) != boundProjectJson)
                    throw new InvalidOperationException("Resource Project changed during Atlas registration; changed content was preserved.");
                return configuration;
            }
            catch
            {
                // 只撤销尚未落盘且仍精确等于本次绑定的内存改动；绝不恢复旧字节覆盖回调保存的 Project。
                if (project && boundProjectJson != null && EditorJsonUtility.ToJson(project) == boundProjectJson &&
                    File.Exists(projectPath) && File.ReadAllBytes(projectPath).SequenceEqual(projectBytes) &&
                    File.Exists(projectPath + ".meta") &&
                    ResourceAtlasPlan.HashFile(projectPath + ".meta") == projectMetaHash)
                {
                    EditorJsonUtility.FromJsonOverwrite(projectJson, project);
                    EditorUtility.ClearDirty(project);
                }
                var referenced = project && (project.AutomaticAtlases == configuration || !string.IsNullOrEmpty(configurationGuid) &&
                    project.AutomaticAtlasConfigurationGuid == configurationGuid);
                if (!referenced && createdConfiguration && configurationBytes != null && File.Exists(directory + "/AutomaticAtlases.asset") &&
                    File.ReadAllBytes(directory + "/AutomaticAtlases.asset").SequenceEqual(configurationBytes) &&
                    AssetDatabase.AssetPathToGUID(directory + "/AutomaticAtlases.asset") == configurationGuid &&
                    HasExpectedGuid(directory + "/AutomaticAtlases.asset", configurationGuid) &&
                    ResourceAtlasPlan.HashFile(directory + "/AutomaticAtlases.asset.meta") == configurationMetaHash)
                    AssetDatabase.DeleteAsset(directory + "/AutomaticAtlases.asset");
                if (!referenced && created && Directory.Exists(createdDirectory) && AssetDatabase.AssetPathToGUID(createdDirectory) == directoryGuid &&
                    HasExpectedGuid(createdDirectory, directoryGuid) && ResourceAtlasPlan.HashFile(createdDirectory + ".meta") == directoryMetaHash &&
                    !Directory.EnumerateFileSystemEntries(createdDirectory).Any())
                    AssetDatabase.DeleteAsset(createdDirectory);
                if (!createdConfiguration && configuration) Object.DestroyImmediate(configuration);
                throw;
            }
            finally { generating = false; }

            void RequireRegistrationInputs()
            {
                if (!project || EditorUtility.IsDirty(project) || AssetDatabase.GetAssetPath(project) != projectPath ||
                    AssetDatabase.AssetPathToGUID(projectPath) != projectGuid || EditorJsonUtility.ToJson(project) != projectJson ||
                    !File.ReadAllBytes(projectPath).SequenceEqual(projectBytes) || ResourceAtlasPlan.HashFile(projectPath + ".meta") != projectMetaHash ||
                    ResourceAtlasPlan.ResolveGenerationParent(parentGuid) != parent)
                    throw new InvalidOperationException("Resource Project/output parent changed during Atlas registration; not overwritten.");
            }
        }

        public static ResourceAtlasPlan Preview(ResourceProject project) => new(project);

        /// <summary>执行当前已保存规则并准备自有图集。返回实际计划；普通异常/取消回滚本次写入，旧有效输出不被当作成功。</summary>
        public static ResourceAtlasPlan Generate(ResourceProject project, CancellationToken cancellation = default)
        {
            ResourceBundleBuilder.ValidateEditor(EditorUserBuildSettings.activeBuildTarget);
            ResourceBundleBuilder.RejectLoadedBundles();
            if (generating) throw new InvalidOperationException("Automatic Atlas generation is already running.");
            if (!project) throw new ArgumentNullException(nameof(project));
            if (!IsConfigured(project)) return null;
            var configuration = project.AutomaticAtlases;
            if (EditorUtility.IsDirty(project) || EditorUtility.IsDirty(configuration)) throw new InvalidOperationException("Save the Project and automatic Atlas rules before generation.");
            if (EditorSettings.spritePackerMode == SpritePackerMode.Disabled) throw new InvalidOperationException("Automatic Atlases require an enabled SpritePacker; global settings are not changed by this feature.");
            cancellation.ThrowIfCancellationRequested();
            generating = true;
            try
            {
                var directory = ResourceAtlasPlan.ValidateOwnership(project);
                var configPath = AssetDatabase.GetAssetPath(configuration);
                var configBytes = File.ReadAllBytes(configPath);
                var savedConfiguration = EditorJsonUtility.ToJson(configuration);
                var configurationMetaHash = ResourceAtlasPlan.HashFile(configPath + ".meta");
                var projectPath = AssetDatabase.GetAssetPath(project);
                var projectGuid = AssetDatabase.AssetPathToGUID(projectPath);
                var projectMetaHash = ResourceAtlasPlan.HashFile(projectPath + ".meta");
                var savedProject = ResourceBundleBuilder.CanonicalDefinition(project);
                var directoryIdentity = ResourceProjectPaths.SemanticIdentity(project);
                long definitionBytes = 0;
                var projectHash = ResourceBundleBuilder.ProjectFileHash(projectPath, ref definitionBytes);
                void RequireDefinition()
                {
                    definitionBytes = 0;
                    if (!project || !configuration || project.AutomaticAtlases != configuration ||
                        AssetDatabase.GetAssetPath(project) != projectPath || AssetDatabase.AssetPathToGUID(projectPath) != projectGuid ||
                        AssetDatabase.GetAssetPath(configuration) != configPath || !IsConfigured(project) ||
                        ResourceProjectPaths.SemanticIdentity(project) != directoryIdentity || EditorUtility.IsDirty(project) ||
                        ResourceBundleBuilder.CanonicalDefinition(project) != savedProject ||
                        ResourceBundleBuilder.ProjectFileHash(projectPath, ref definitionBytes) != projectHash ||
                        ResourceAtlasPlan.HashFile(projectPath + ".meta") != projectMetaHash ||
                        ResourceAtlasPlan.HashFile(configPath + ".meta") != configurationMetaHash ||
                        EditorUtility.IsDirty(configuration) || EditorJsonUtility.ToJson(configuration) != savedConfiguration ||
                        !File.ReadAllBytes(configPath).SequenceEqual(configBytes))
                        throw new InvalidOperationException("Automatic Atlas Project/configuration/source directories changed during generation; not overwritten.");
                }
                if (configuration.Rules == null || configuration.Rules.Count > 256 || configuration.Rules.Any(x => x == null))
                    throw new InvalidOperationException("Invalid automatic Atlas rules.");
                var directories = ResourceProjectPaths.Resolve(project, validateProjects: false);
                var candidates = configuration.Rules.SelectMany(rule => ResourceAtlasPlan.SourceCandidates(rule, directories, directory)).Distinct(StringComparer.Ordinal).ToArray();
                if (candidates.Length > 16384) throw new InvalidOperationException("Automatic Atlas source limit exceeded.");
                // 仅同步明确来源的正常 Unity 导入，不设置或保存其 Importer。否则原始 PNG 已变而导入缓存未变时可能打出旧像素。
                foreach (var path in candidates)
                {
                    cancellation.ThrowIfCancellationRequested();
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    RequireDefinition();
                }
                RequireDefinition();
                var plan = Preview(project);
                if (!plan.NeedsGeneration) return plan;
                var changes = plan.Changes.Where(x => x.Kind == ResourceAtlasChangeKind.Create || x.Kind == ResourceAtlasChangeKind.Update).ToArray();
                var oldRecords = configuration.Outputs;
                List<ResourceAtlasConfiguration.Output> writtenRecords = null;
                var writes = new AtlasWrites();
                try
                {
                    foreach (var change in changes)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        RequireDefinition();
                        RequireExpectedOutput(change, oldRecords.SingleOrDefault(x => x.Key == change.Key));
                        writes.Begin(change.Path);
                        WriteAtlas(change, () => { writes.Checkpoint(change.Path); RequireDefinition(); });
                    }
                    cancellation.ThrowIfCancellationRequested();
                    var atlases = changes.Select(x => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(x.Path)).ToArray();
                    SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
                    RequireDefinition();
                    foreach (var change in changes)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        writes.RequireCurrent(change.Path);
                        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(change.Path);
                        if (!atlas || atlas.spriteCount != change.Sources.Sum(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count()))
                            throw new InvalidOperationException("Automatic Atlas preparation failed: " + change.Path);
                    }
                    RequireDefinition();
                    var records = oldRecords.Where(record => changes.All(x => x.Key != record.Key)).ToList();
                    records.AddRange(changes.Select(x => new ResourceAtlasConfiguration.Output { Key = x.Key, FileName = Path.GetFileName(x.Path),
                        Guid = AssetDatabase.AssetPathToGUID(x.Path), InputHash = x.InputHash, AssetHash = ResourceAtlasPlan.HashFile(x.Path), MetaHash = ResourceAtlasPlan.HashFile(x.Path + ".meta") }));
                    writes.Begin(configPath);
                    configuration.Outputs = writtenRecords = records.OrderBy(x => x.Key, StringComparer.Ordinal).ToList();
                    var writtenConfiguration = EditorJsonUtility.ToJson(configuration);
                    EditorUtility.SetDirty(configuration); AssetDatabase.SaveAssetIfDirty(configuration);
                    if (EditorUtility.IsDirty(configuration) || EditorJsonUtility.ToJson(configuration) != writtenConfiguration)
                        throw new InvalidOperationException("Automatic Atlas configuration changed during output registration; not overwritten.");
                    savedConfiguration = writtenConfiguration;
                    configBytes = File.ReadAllBytes(configPath);
                    writes.Checkpoint(configPath);
                    RequireDefinition();
                    cancellation.ThrowIfCancellationRequested();
                    var after = Preview(project);
                    RequireDefinition();
                    if (after.NeedsGeneration) throw new InvalidOperationException("Automatic Atlas sources/rules changed during generation; outputs rolled back.");
                    foreach (var change in changes) change.Guid = AssetDatabase.AssetPathToGUID(change.Path);
                    return plan;
                }
                catch (Exception error)
                {
                    try
                    {
                        writes.Restore();
                        if (configuration && ReferenceEquals(configuration.Outputs, writtenRecords)) configuration.Outputs = oldRecords;
                    }
                    catch (Exception rollback) { throw new AggregateException("Atlas generation failed and rollback could not complete; changed/unknown content was preserved. Restore registered outputs before building.", error, rollback); }
                    throw;
                }
            }
            finally { generating = false; }
        }

        private static void RequireExpectedOutput(ResourceAtlasChange change, ResourceAtlasConfiguration.Output record)
        {
            if (record == null)
            {
                ResourceBuildOperation.RejectLinks(change.Path);
                ResourceBuildOperation.RejectLinks(change.Path + ".meta");
                if (File.Exists(change.Path) || Directory.Exists(change.Path) || File.Exists(change.Path + ".meta"))
                    throw new InvalidOperationException("Atlas Create path was occupied during generation; not adopted: " + change.Path);
            }
            else if (!File.Exists(change.Path) || !File.Exists(change.Path + ".meta") || AssetDatabase.AssetPathToGUID(change.Path) != record.Guid ||
                ResourceAtlasPlan.HashFile(change.Path) != record.AssetHash || ResourceAtlasPlan.HashFile(change.Path + ".meta") != record.MetaHash ||
                EditorUtility.IsDirty(AssetImporter.GetAtPath(change.Path)) || EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<SpriteAtlas>(change.Path)))
                throw new InvalidOperationException("Atlas Update path changed during generation; not overwritten: " + change.Path);
        }

        private static void WriteAtlas(ResourceAtlasChange change, Action checkpoint)
        {
            // 写 authoring 资产而不是只修改导入后的 SpriteAtlas；保存不删除或重建存续 .meta。
            var authoring = new SpriteAtlasAsset();
            try
            {
                authoring.Add(change.Sources.Select(AssetDatabase.LoadAssetAtPath<Texture2D>).Cast<Object>().ToArray());
                authoring.SetIncludeInBuild(false);
                SpriteAtlasAsset.Save(authoring, change.Path);
                checkpoint();
            }
            finally { Object.DestroyImmediate(authoring); }
            var authoringHash = ResourceAtlasPlan.HashFile(change.Path);
            AssetDatabase.ImportAsset(change.Path, ImportAssetOptions.ForceSynchronousImport);
            var guid = AssetDatabase.AssetPathToGUID(change.Path);
            if (ResourceAtlasPlan.HashFile(change.Path) != authoringHash || change.Guid != null && guid != change.Guid || !HasExpectedGuid(change.Path, guid))
                throw new InvalidOperationException("Atlas authoring identity changed during import; not overwritten: " + change.Path);
            checkpoint();
            if (AssetImporter.GetAtPath(change.Path) is not SpriteAtlasImporter importer) throw new InvalidOperationException("Cannot import generated Atlas: " + change.Path);
            var rule = change.Rule;
            importer.includeInBuild = false;
            var packing = importer.packingSettings;
            packing.enableRotation = rule.AllowRotation; packing.enableTightPacking = rule.TightPacking; packing.padding = rule.Padding;
            importer.packingSettings = packing;
            var texture = importer.textureSettings;
            texture.readable = rule.Readable; texture.generateMipMaps = rule.MipMaps; texture.sRGB = rule.SRgb; texture.filterMode = rule.FilterMode;
            importer.textureSettings = texture;
            var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = rule.MaxTextureSize; platform.textureCompression = rule.Compression;
            importer.SetPlatformSettings(platform);
            importer.SaveAndReimport();
            if (ResourceAtlasPlan.HashFile(change.Path) != authoringHash || AssetDatabase.AssetPathToGUID(change.Path) != guid || !HasExpectedGuid(change.Path, guid))
                throw new InvalidOperationException("Generated Atlas changed during importer save: " + change.Path);
            checkpoint();
        }

        private static bool HasExpectedGuid(string path, string guid)
        {
            ResourceBuildOperation.RejectLinks(path + ".meta");
            if (string.IsNullOrEmpty(guid) || !File.Exists(path + ".meta")) return false;
            var identities = File.ReadLines(path + ".meta").Where(line => line.StartsWith("guid:", StringComparison.Ordinal)).ToArray();
            return identities.Length == 1 && identities[0] == "guid: " + guid;
        }

        // 只记录实际开始写入的资产对；回滚前再次核对最后一个受控写入点，不覆盖后来的未知内容。
        private sealed class AtlasWrites
        {
            private readonly Dictionary<string, byte[]> originals = new(StringComparer.Ordinal);
            private readonly Dictionary<string, byte[]> expected = new(StringComparer.Ordinal);
            private readonly List<string> paths = new();

            internal void Begin(string path)
            {
                paths.Add(path);
                foreach (var file in new[] { path, path + ".meta" }) originals.Add(file, Read(file));
                Checkpoint(path);
            }

            internal void Checkpoint(string path)
            {
                expected[path] = Read(path);
                expected[path + ".meta"] = Read(path + ".meta");
            }

            internal void RequireCurrent(string path)
            {
                foreach (var file in new[] { path, path + ".meta" })
                {
                    var actual = Read(file);
                    if (expected[file] == null ? actual != null : actual == null || !actual.SequenceEqual(expected[file]))
                        throw new InvalidOperationException("Atlas write state changed; unknown content preserved: " + file);
                }
            }

            internal void Restore()
            {
                var restored = new List<string>();
                var errors = new List<Exception>();
                foreach (var path in paths.AsEnumerable().Reverse())
                    try
                    {
                        RequireCurrent(path);
                        foreach (var file in new[] { path, path + ".meta" })
                            if (originals[file] == null) File.Delete(file);
                            else File.WriteAllBytes(file, originals[file]);
                        restored.Add(path);
                    }
                    catch (Exception error) { errors.Add(error); }
                foreach (var path in restored.Where(File.Exists)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (errors.Count != 0) throw new AggregateException("Some Atlas writes could not be safely rolled back.", errors);
            }

            private static byte[] Read(string path)
            {
                ResourceBuildOperation.RejectLinks(path);
                if (Directory.Exists(path)) throw new InvalidOperationException("An Atlas file was replaced by a directory: " + path);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        }

        /// <summary>显式删除单个失效输出，先复验完整归属及当前引用；不会自动删除仍被使用或未知的资产。</summary>
        public static void DeleteObsolete(ResourceProject project, string outputKey)
        {
            ResourceBundleBuilder.ValidateEditor(EditorUserBuildSettings.activeBuildTarget);
            ResourceBundleBuilder.RejectLoadedBundles();
            if (generating) throw new InvalidOperationException("Automatic Atlas generation is already running.");
            if (EditorUtility.IsDirty(project) || EditorUtility.IsDirty(project.AutomaticAtlases)) throw new InvalidOperationException("Save the Atlas rules before removing obsolete outputs.");
            var plan = Preview(project);
            var change = plan.Changes.SingleOrDefault(x => x.Key == outputKey && x.Kind == ResourceAtlasChangeKind.Obsolete);
            if (change == null) throw new InvalidOperationException("Only a verified obsolete output can be removed: " + outputKey);
            var files = ReferenceCandidates().Where(path => path != change.Path).ToArray();
            foreach (var path in files)
                if (AssetDatabase.GetDependencies(path, false).Contains(change.Path, StringComparer.Ordinal))
                    throw new InvalidOperationException("Obsolete Atlas is still referenced; not deleted: " + path);
            foreach (var guid in AssetDatabase.FindAssets("t:ResourceProject"))
            {
                var definition = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var selection in definition.Groups.Where(x => x != null).SelectMany(x => x.Selections))
                {
                    if (selection == null) continue;
                    var selected = AssetDatabase.GUIDToAssetPath(selection.Guid);
                    var registered = definition.AutomaticAtlases ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition.AutomaticAtlases))?.Replace('\\', '/') : null;
                    var excluded = registered != null && ResourceAtlasPlan.Within(registered, change.Path) && ResourceAtlasPlan.Within(selected, registered);
                    var includedExtension = selection.Extensions == null || selection.Extensions.Length == 0 || selection.Extensions.Contains(".spriteatlasv2", StringComparer.OrdinalIgnoreCase);
                    if (selection.Guid == change.Guid || !excluded && includedExtension && AssetDatabase.IsValidFolder(selected) && ResourceAtlasPlan.Within(selected, change.Path))
                        throw new InvalidOperationException("Obsolete Atlas is still selected by a ResourceProject: " + definition.ProjectId);
                }
            }
            var configuration = project.AutomaticAtlases;
            var configPath = AssetDatabase.GetAssetPath(configuration);
            var writes = new AtlasWrites();
            var oldRecords = configuration.Outputs;
            try
            {
                RequireExpectedOutput(change, oldRecords.Single(x => x.Key == change.Key));
                writes.Begin(change.Path);
                if (!AssetDatabase.DeleteAsset(change.Path)) throw new IOException("Cannot delete owned obsolete Atlas: " + change.Path);
                if (File.Exists(change.Path) || File.Exists(change.Path + ".meta")) throw new InvalidOperationException("Deleted Atlas path was occupied by new content; not adopted.");
                writes.Checkpoint(change.Path);
                writes.Begin(configPath);
                configuration.Outputs = oldRecords.Where(x => x.Key != change.Key).ToList();
                EditorUtility.SetDirty(configuration); AssetDatabase.SaveAssetIfDirty(configuration);
                writes.Checkpoint(configPath);
            }
            catch { configuration.Outputs = oldRecords; writes.Restore(); throw; }
        }

        private static IEnumerable<string> ReferenceCandidates() => AssetDatabase.GetAllAssetPaths().Where(path =>
            (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) && !AssetDatabase.IsValidFolder(path));

        internal static IReadOnlyList<ResourceAtlasChange> CurrentOutputs(ResourceProject project)
        {
            if (!IsConfigured(project)) return Array.Empty<ResourceAtlasChange>();
            var plan = Preview(project);
            if (plan.NeedsGeneration) throw new InvalidOperationException("Automatic Atlases require generation/update before dependency analysis. Use Generate or the formal Build entry.\n" + plan);
            return plan.Changes.Where(x => x.Kind == ResourceAtlasChangeKind.Current).ToList().AsReadOnly();
        }

        internal static bool IsConfigured(ResourceProject project)
        {
            if (!project) throw new ArgumentNullException(nameof(project));
            if (!project.AutomaticAtlases)
            {
                if (!string.IsNullOrEmpty(project.AutomaticAtlasConfigurationGuid))
                    throw new InvalidOperationException("Registered automatic Atlas configuration is missing/disconnected; restore its original reference and GUID before building.");
                return false;
            }
            if (project.AutomaticAtlasConfigurationGuid != AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project.AutomaticAtlases)))
                throw new InvalidOperationException("Automatic Atlas configuration is not the one registered to this Project.");
            return true;
        }
    }
}
