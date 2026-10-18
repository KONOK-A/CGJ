using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    public enum ResourcePreflightState { StaticBlocked, StaticReady, AtlasPreparationRequired, Complete, Failed }

    /// <summary>同一正式预检流的只读结论；StaticReady不是完整输入、密钥或制品验证。</summary>
    public sealed class ResourcePreflightResult
    {
        public ResourcePreflightState State { get; }
        public string Summary { get; }
        public Exception Error { get; }
        public string ProjectGuid { get; }
        public string ProjectId { get; }
        public string Platform { get; }
        public string AppContentCompatibilityId { get; }
        internal ResourcePreflightResult(ResourcePreflightState state, string summary, Exception error,
            ResourceProject project, BuildTarget target, string app)
        {
            State = state; Summary = summary; Error = error; Platform = target.ToString(); AppContentCompatibilityId = app;
            ProjectGuid = project ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) : null;
            ProjectId = project ? project.ProjectId : null;
        }
    }

    /// <summary>构建、发布和生成的独立结果；失败首因不会被后续生成或 UI 操作覆盖。</summary>
    public sealed class ResourceEditorBuildReport
    {
        public string ProjectGuid { get; internal set; }
        public string ProjectId { get; internal set; }
        public string Platform { get; internal set; }
        public string AppContentCompatibilityId { get; internal set; }
        public string Operation { get; internal set; }
        public bool ForceRebuild { get; internal set; }
        public string ReferenceDirectory { get; internal set; }
        public ResourceBuildInvocation Invocation { get; internal set; }
        public ResourceBuildResult Build { get; internal set; }
        public ResourceRelease Release { get; internal set; }
        public Exception BuildError { get; internal set; }
        public Exception PublicationError { get; internal set; }
        public Exception GenerationError { get; internal set; }
        public bool GenerationRequested { get; internal set; }
        public bool GenerationChanged { get; internal set; }
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public double ElapsedMilliseconds { get; internal set; }
        public double? PreflightMilliseconds { get; internal set; }
        public double? PublicationMilliseconds { get; internal set; }
        public double? GenerationMilliseconds { get; internal set; }
        public IReadOnlyList<string> GeneratedProjects { get; internal set; } = Array.Empty<string>();
        public string Summary => (Invocation == null ? "" : Invocation + "\n") + (Build == null ? "构建未完成：" + BuildError?.Message : "构建成功：" + Build.WorkspacePath) + "\n" +
            (Release != null ? "发布成功：" + Release.DirectoryPath + "\nCatalogDigest=" + Release.Catalog.CatalogDigest :
                PublicationError != null ? "发布未确认：" + PublicationError.Message + "；如涉及提交点后的 IO，请显式恢复确认。" : "发布未执行。") + "\n" +
            (GenerationError != null ? "常量生成失败（资源版本不回滚）：" + GenerationError.Message :
                GenerationRequested && Release != null ? "常量集合=" + string.Join(", ", GeneratedProjects) + "；" + (GenerationChanged ? "源码已更新，Unity 编译结果独立确认。" : "源码未变化。") : "常量生成未执行。");
    }

    /// <summary>工作台/自动化共用编排，只调用正式 Builder/Publisher/常量生成器，不读取旧全局 Importer 标记。</summary>
    public static class ResourceEditorOperations
    {
        /// <summary>纯解析非秘密执行选项，不预检制品、取 key、构建、发布、生成或初始化 Runtime。</summary>
        public static ResourceBuildInvocation ResolveRecipe(ResourceBuildRecipe recipe)
        {
            RequireRecipe(recipe);
            RequireEditableProject(recipe.Project);
            ResourceAddress.ValidateId(recipe.Project.ProjectId, nameof(recipe.Project.ProjectId));
            _ = new ResourceAddress(recipe.AppContentCompatibilityId, "compatibility");
            if (recipe.AppContentCompatibilityId.Length > 128 || !Enum.IsDefined(typeof(BuildTarget), recipe.Target) ||
                !Enum.IsDefined(typeof(ResourceBuildAction), recipe.Action)) throw new ArgumentException("Invalid recipe target, action or compatibility id.");
            if (recipe.GenerateReferences && recipe.Action != ResourceBuildAction.BuildAndPublish)
                throw new ArgumentException("BuildOnly cannot request publication or reference generation.");
            if (recipe.GenerateReferences && (string.IsNullOrEmpty(recipe.ReferenceDirectory) ||
                !recipe.ReferenceDirectory.StartsWith("Assets/", StringComparison.Ordinal) || recipe.ReferenceDirectory.Contains('\\') ||
                recipe.ReferenceDirectory.Split('/').Any(x => x.Length == 0 || x == "." || x == "..")))
                throw new ArgumentException("ReferenceDirectory must be an explicit Assets-relative directory.");
            return new ResourceBuildInvocation(recipe);
        }

        /// <summary>只保存所选配方，不保存 Project、场景或其他资产。</summary>
        public static void SaveRecipe(ResourceBuildRecipe recipe)
        {
            RequireRecipe(recipe); AssetDatabase.SaveAssetIfDirty(recipe);
        }

        /// <summary>主线程无窗口入口；冻结执行选项并调用原服务，错误仍按构建/发布/生成分阶段返回。</summary>
        public static ResourceEditorBuildReport ExecuteRecipe(ResourceBuildRecipe recipe, CancellationToken cancellation = default,
            IResourceContentKeyProvider provider = null)
        {
            ResourceBuildInvocation input;
            try
            {
                ResourceBuildProgress.RejectReentry();
                cancellation.ThrowIfCancellationRequested();
                input = ResolveSavedRecipe(recipe);
            }
            catch (Exception error)
            {
                var knownProject = recipe ? recipe.Project : null;
                return Finish(new ResourceEditorBuildReport { BuildError = error, Operation = "ExecuteRecipe",
                    ProjectGuid = knownProject ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(knownProject)) : null,
                    ProjectId = knownProject ? knownProject.ProjectId : null, Platform = recipe ? recipe.Target.ToString() : null,
                    AppContentCompatibilityId = recipe ? recipe.AppContentCompatibilityId : null, ForceRebuild = recipe && recipe.ForceRebuild,
                    GenerationRequested = recipe && recipe.GenerateReferences, ReferenceDirectory = recipe ? recipe.ReferenceDirectory : null });
            }
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(input.ProjectGuid));
            var report = BuildOnlyForTarget(project, input.Target, input.AppContentCompatibilityId, input.ForceRebuild,
                cancellation, provider, input);
            report.Invocation = input;
            report.GenerationRequested = input.GenerateReferences;
            if (report.Build != null && input.Action == ResourceBuildAction.BuildAndPublish)
                PublishReport(report, input.AppContentCompatibilityId, input.GenerateReferences, input.ReferenceDirectory, cancellation, provider);
            return Finish(report);
        }

        private static ResourceBuildInvocation ResolveSavedRecipe(ResourceBuildRecipe recipe)
        {
            var input = ResolveRecipe(recipe);
            if (EditorUtility.IsDirty(recipe)) throw new InvalidOperationException("请先显式保存 ResourceBuildRecipe，再执行。");
            return input;
        }

        internal static void RequireRecipe(ResourceBuildRecipe recipe)
        {
            var path = recipe ? AssetDatabase.GetAssetPath(recipe) : "";
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(recipe))
                throw new InvalidOperationException("需要明确保存于 Assets 的可编辑 ResourceBuildRecipe。");
            ResourceBuildOperation.ValidateAssetPath(path);
            ResourceBuildOperation.RejectLinks(path + ".meta");
        }

        /// <summary>仅保存明确归属的 Assets 定义，不保存用户场景，不经过链接路径。</summary>
        public static void SaveProject(ResourceProject project)
        {
            RequireEditableProject(project);
            AssetDatabase.SaveAssetIfDirty(project);
        }

        /// <summary>编辑来源目录并支持Undo。允许保留待修正配置；统一解析/构建会报告失效范围，不删除条目或源文件。</summary>
        public static void SetSourceDirectories(ResourceProject project, IEnumerable<ResourceSourceDirectory> directories)
        {
            RequireEditableProject(project);
            var values = directories?.Select(x => new ResourceSourceDirectory { Guid = x?.Guid, AddressPrefix = x?.AddressPrefix ?? "" }).ToArray()
                ?? throw new ArgumentNullException(nameof(directories));
            if (values.Length > 256) throw new ArgumentException("资源目录不能超过256个。", nameof(directories));
            BeginEdit("修改资源目录");
            using var serialized = new SerializedObject(project);
            var entries = serialized.FindProperty("SourceDirectories"); entries.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("Guid").stringValue = values[index].Guid ?? "";
                entry.FindPropertyRelative("AddressPrefix").stringValue = values[index].AddressPrefix;
            }
            serialized.ApplyModifiedProperties();
        }

        /// <summary>显式准备本Project选择的Atlas。Unity会写回其打包缓存，须在正式构建冻结输入前调用；不处理全工程图集。</summary>
        public static int PrepareAtlases(ResourceProject project)
        {
            RequireEditableProject(project);
            ResourceBundleBuilder.ValidateEditor(EditorUserBuildSettings.activeBuildTarget);
            ResourceBundleBuilder.RejectLoadedBundles();
            ResourceAtlasGenerator.Generate(project);
            var map = ResourceBuildMap.Create(project);
            var generated = new HashSet<string>(ResourceAtlasGenerator.CurrentOutputs(project).Select(x => x.Guid), StringComparer.Ordinal);
            var atlases = map.Assets.Where(value => value.AssetType == typeof(UnityEngine.U2D.SpriteAtlas))
                .Where(value => !generated.Contains(value.Guid))
                .Select(value => AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(value.SourcePath)).ToArray();
            if (atlases.Length != 0) UnityEditor.U2D.SpriteAtlasUtility.PackAtlases(atlases, EditorUserBuildSettings.activeBuildTarget, false);
            return atlases.Length + generated.Count;
        }

        /// <summary>创建独立定义身份；目标必须在 Assets 的真实文件夹中且没有已有文件或孤立 meta。</summary>
        public static ResourceProject CreateProject(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(x => x.Length == 0 || x == "." || x == "..") || Path.GetExtension(path) != ".asset" ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')))
                throw new InvalidOperationException("ResourceProject 只能创建于 Assets 的明确真实文件夹。");
            ResourceBuildOperation.ValidateAssetPath(path); ResourceBuildOperation.RejectLinks(path + ".meta");
            if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("目标身份已占用，拒绝覆盖：" + path);
            var asset = ScriptableObject.CreateInstance<ResourceProject>(); asset.ProjectId = "resource-" + Guid.NewGuid().ToString("N");
            try { AssetDatabase.CreateAsset(asset, path); return asset; }
            catch { Object.DestroyImmediate(asset); throw; }
        }

        /// <summary>只读预检，包含源定义、已活动平台、Workspace/发布归属及已发布制品完整性；不保存场景或创建目录。</summary>
        public static string Preflight(ResourceProject project, string appContentCompatibilityId, IResourceContentKeyProvider provider = null)
            => PreflightForTarget(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId, provider);

        private static string PreflightForTarget(ResourceProject project, BuildTarget target, string appContentCompatibilityId, IResourceContentKeyProvider provider)
        {
            var result = InspectPreflightForTarget(project, target, appContentCompatibilityId, provider, true);
            if (result.Error != null) ExceptionDispatchInfo.Capture(result.Error).Throw();
            return result.Summary;
        }

        public static ResourcePreflightResult InspectPreflight(ResourceProject project, string appContentCompatibilityId, IResourceContentKeyProvider provider = null) =>
            InspectPreflightForTarget(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId, provider, true);

        /// <summary>只做低成本静态门禁；不取key、不建Map、不预览图集、不扫描Workspace/历史或做SHA。</summary>
        public static ResourcePreflightResult CheckReadiness(ResourceProject project, string appContentCompatibilityId) =>
            InspectPreflightForTarget(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId, null, false);

        public static ResourcePreflightResult InspectRecipePreflight(ResourceBuildRecipe recipe, bool full = false, IResourceContentKeyProvider provider = null)
        {
            try
            {
                var input = ResolveSavedRecipe(recipe);
                return InspectPreflightForTarget(recipe.Project, input.Target, input.AppContentCompatibilityId, provider, full);
            }
            catch (Exception error) { return new ResourcePreflightResult(full ? ResourcePreflightState.Failed : ResourcePreflightState.StaticBlocked,
                error.Message, error, recipe ? recipe.Project : null, recipe ? recipe.Target : EditorUserBuildSettings.activeBuildTarget,
                recipe ? recipe.AppContentCompatibilityId : null); }
        }

        private static ResourcePreflightResult InspectPreflightForTarget(ResourceProject project, BuildTarget target, string appContentCompatibilityId,
            IResourceContentKeyProvider provider, bool full)
        {
            try { return PreflightCore(project, target, appContentCompatibilityId, provider, full); }
            catch (Exception error) { return new ResourcePreflightResult(full ? ResourcePreflightState.Failed : ResourcePreflightState.StaticBlocked,
                error.Message, error, project, target, appContentCompatibilityId); }
        }

        private static ResourcePreflightResult PreflightCore(ResourceProject project, BuildTarget target, string appContentCompatibilityId,
            IResourceContentKeyProvider provider, bool full)
        {
            ResourceBundleBuilder.ValidateEditor(target);
            ResourceBundleBuilder.RejectLoadedBundles();
            RequireEditableProject(project);
            if (EditorUtility.IsDirty(project)) throw new InvalidOperationException("请先显式保存 ResourceProject 定义，再执行构建预检。");
            _ = new ResourceAddress(appContentCompatibilityId, "compatibility");
            if (appContentCompatibilityId.Length > 128) throw new ArgumentException("AppContentCompatibilityId exceeds its schema bound.");
            ResourceBundleBuilder.ValidateContentPolicy(project.ContentEncryption, project.ContentKeyId, target.ToString());
            var directoryIdentity = ResourceProjectPaths.SemanticIdentity(project);
            if (full && project.ContentEncryption != ResourceContentEncryption.None)
            {
                var copy = ResourceBundleBuilder.CopyContentKey(ContentProvider(project.ContentEncryption, project.ProjectId, project.ContentKeyId, provider), project.ProjectId, project.ContentKeyId);
                Array.Clear(copy, 0, copy.Length);
                if (ResourceProjectPaths.SemanticIdentity(project) != directoryIdentity)
                    throw new InvalidOperationException("资源目录或生成配置在密钥检查期间发生变化。本次操作已停止，请检查并重新执行；已保存的配置修改未被撤销。");
            }
            if (ResourceAtlasGenerator.IsConfigured(project))
            {
                if (EditorUtility.IsDirty(project.AutomaticAtlases)) throw new InvalidOperationException("请先保存自动图集规则，再预检。 ");
                if (full)
                {
                    var atlasPlan = ResourceAtlasGenerator.Preview(project);
                    if (atlasPlan.NeedsGeneration) return new ResourcePreflightResult(ResourcePreflightState.AtlasPreparationRequired,
                        "自动图集需要生成/更新；此预检未写入，尚未进行最终依赖/Workspace 检查。\n" + atlasPlan +
                        "\n正式 Build 会先执行生成与准备，再冻结输入并进行完整依赖分析；生成失败不会继续构建。", null, project, target, appContentCompatibilityId);
                }
            }
            if (!full) return new ResourcePreflightResult(ResourcePreflightState.StaticReady,
                "配置基础检查通过，尚未检查资源依赖、图集或构建输出，也未读取密钥。正式执行时会继续检查当前输入。", null, project, target, appContentCompatibilityId);
            var map = ResourceBuildMap.Create(project);
            _ = ResourceBundleBuilder.DescribeSubAssets(map);
            var workspace = ResourceBundleBuilder.PreflightWorkspace(project, target);
            var root = ResourcePublisher.Preflight(project, target);
            var summary = "ProjectId=" + map.ProjectId + " / GUID=" + map.ProjectGuid + "\n" +
                "平台=" + target + " / Unity=" + Application.unityVersion + " / App=" + appContentCompatibilityId + "\n" +
                "交付=" + project.ContentEncryption + (project.ContentEncryption == ResourceContentEncryption.None ? "" : " / KeyId=" + project.ContentKeyId) + "\n" +
                "资产=" + map.Assets.Count + " / Group=" + map.Groups.Count + " / Bundle=" + map.Bundles.Count + "\nWorkspace=" + workspace + "\n发布根=" + root +
                "\n仅本地/内部受控发布，不写 StreamingAssets，不执行 Runtime 激活；同步 native 构建期间不能中途取消。";
            return new ResourcePreflightResult(ResourcePreflightState.Complete, summary, null, project, target, appContentCompatibilityId);
        }

        /// <summary>错误分阶段返回；生成失败不改变已成功的版本。取消仅影响本次构建/发布的提交前检查点。</summary>
        public static ResourceEditorBuildReport BuildAndPublish(ResourceProject project, string appContentCompatibilityId,
            bool generateReferences = false, string directory = "Assets/YLFrameworkData", bool forceRebuild = false, CancellationToken cancellation = default,
            IResourceContentKeyProvider provider = null)
        {
            var report = BuildOnlyForTarget(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId, forceRebuild, cancellation, provider);
            report.Operation = "BuildAndPublish"; report.ReferenceDirectory = directory;
            report.GenerationRequested = generateReferences;
            if (report.Build != null) PublishReport(report, appContentCompatibilityId, generateReferences, directory, cancellation, provider);
            return Finish(report);
        }

        /// <summary>仅构建已验证 Workspace，不发布、不生成、不初始化 Runtime。</summary>
        public static ResourceEditorBuildReport BuildOnly(ResourceProject project, string appContentCompatibilityId,
            bool forceRebuild = false, CancellationToken cancellation = default, IResourceContentKeyProvider provider = null)
            => Finish(BuildOnlyForTarget(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId, forceRebuild, cancellation, provider));

        private static ResourceEditorBuildReport BuildOnlyForTarget(ResourceProject project, BuildTarget target, string appContentCompatibilityId,
            bool forceRebuild, CancellationToken cancellation, IResourceContentKeyProvider provider, ResourceBuildInvocation invocation = null)
        {
            var report = new ResourceEditorBuildReport { ProjectGuid = project ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) : null,
                ProjectId = project ? project.ProjectId : null, Platform = target.ToString(), AppContentCompatibilityId = appContentCompatibilityId,
                Operation = invocation?.Action.ToString() ?? "BuildOnly", ForceRebuild = forceRebuild, ReferenceDirectory = invocation?.ReferenceDirectory };
            var elapsed = Stopwatch.StartNew();
            try
            {
                ResourceBuildProgress.RejectReentry();
                cancellation.ThrowIfCancellationRequested();
                RequireEditableProject(project);
                var directoryIdentity = ResourceProjectPaths.SemanticIdentity(project);
                ResourceBuildProgress.Notify(ResourceBuildPhase.PreflightStarted, project, target);
                var watch = Stopwatch.StartNew();
                try { PreflightForTarget(project, target, appContentCompatibilityId, provider); }
                finally { report.PreflightMilliseconds = watch.Elapsed.TotalMilliseconds; }
                if (ResourceProjectPaths.SemanticIdentity(project) != directoryIdentity)
                    throw new InvalidOperationException("资源目录或生成配置在预检期间发生变化，未开始构建。请核对当前配置后重新执行。");
                if (invocation != null && (project.ProjectId != invocation.ProjectId || project.Compression != invocation.Compression ||
                    project.ContentEncryption != invocation.ContentEncryption ||
                    project.ContentEncryption != ResourceContentEncryption.None && project.ContentKeyId != invocation.ContentKeyId))
                    throw new InvalidOperationException("Project effective policy changed after recipe resolution; explicitly resolve and execute again.");
                var build = ResourceBundleBuilder.BuildWithContentKey(project, target,
                    ContentProvider(project.ContentEncryption, project.ProjectId, project.ContentKeyId, provider), forceRebuild, cancellation);
                if (invocation != null && (build.Map.ProjectGuid != invocation.ProjectGuid || build.Map.ProjectId != invocation.ProjectId ||
                    build.Compression != invocation.Compression || build.ContentEncryption != invocation.ContentEncryption || build.ContentKeyId != invocation.ContentKeyId))
                    throw new InvalidOperationException("Built policy no longer matches the frozen recipe input; publication was not started.");
                report.Build = build;
            }
            catch (Exception error) { report.BuildError = error; }
            finally { report.ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds; }
            return report;
        }

        /// <summary>发布一个明确的真实 BuildResult；Publisher 会重新验证源/Workspace/归属，不接受窗口预估。</summary>
        public static ResourceEditorBuildReport PublishBuild(ResourceBuildResult build, string appContentCompatibilityId,
            bool generateReferences = false, string directory = "Assets/YLFrameworkData", CancellationToken cancellation = default, IResourceContentKeyProvider provider = null)
        {
            if (build == null) throw new ArgumentNullException(nameof(build));
            var report = new ResourceEditorBuildReport { Build = build, GenerationRequested = generateReferences, ProjectGuid = build.Map.ProjectGuid,
                ProjectId = build.Map.ProjectId, Platform = build.Platform, AppContentCompatibilityId = appContentCompatibilityId,
                Operation = "PublishVerifiedBuild", ReferenceDirectory = directory };
            PublishReport(report, appContentCompatibilityId, generateReferences, directory, cancellation, provider);
            return Finish(report);
        }

        private static void PublishReport(ResourceEditorBuildReport report, string app, bool generate, string directory, CancellationToken cancellation, IResourceContentKeyProvider provider)
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                ResourceBuildProgress.RejectReentry();
                var watch = Stopwatch.StartNew();
                try
                {
                    ResourceBuildProgress.Notify(ResourceBuildPhase.PublicationStarted, report.ProjectGuid, report.ProjectId, report.Platform);
                    report.Release = ResourcePublisher.PublishWithContentKey(report.Build, app,
                    ContentProvider(report.Build.ContentEncryption, report.Build.Map.ProjectId, report.Build.ContentKeyId, provider), cancellation); }
                catch (Exception error) { report.PublicationError = error; return; }
                finally { report.PublicationMilliseconds = watch.Elapsed.TotalMilliseconds; }
                if (!generate) return;
                watch.Restart();
                try
                {
                    ResourceBuildProgress.Notify(ResourceBuildPhase.ReferenceGenerationStarted, report.ProjectGuid, report.ProjectId, report.Platform);
                    var catalogs = PublishedCatalogs(app);
                    report.GeneratedProjects = catalogs.Select(x => x.ProjectId).ToList().AsReadOnly();
                    report.GenerationChanged = ResourceCodeGenerator.GenerateReferences(catalogs, directory);
                }
                catch (Exception error) { report.GenerationError = error; }
                finally { report.GenerationMilliseconds = watch.Elapsed.TotalMilliseconds; }
            }
            finally { report.ElapsedMilliseconds += elapsed.Elapsed.TotalMilliseconds; }
        }

        private static ResourceEditorBuildReport Finish(ResourceEditorBuildReport report)
        {
            ResourceBuildProgress.Notify(ResourceBuildPhase.AttemptFinished, report.ProjectGuid, report.ProjectId, report.Platform);
            return report;
        }

        public const string ContentKeyEnvironmentVariable = "YL_RESOURCE_CONTENT_KEY";

        private static IResourceContentKeyProvider ContentProvider(ResourceContentEncryption mode, string projectId, string keyId, IResourceContentKeyProvider provider) =>
            mode == ResourceContentEncryption.None ? null : provider ?? ContentKeyFromEnvironment(projectId, keyId, ContentKeyEnvironmentVariable);

        /// <summary>显式绑定一个项目/KeyId和进程secret名称；只在取key时读取，不保存secret，不按KeyId猜来源。</summary>
        public static IResourceContentKeyProvider ContentKeyFromEnvironment(string projectId, string keyId, string secretEnvironmentVariable)
        {
            ResourceAddress.ValidateId(projectId, nameof(projectId));
            ResourceBundleEnvelope.ValidateKeyId(keyId);
            if (string.IsNullOrEmpty(secretEnvironmentVariable) || secretEnvironmentVariable.Length > 128 ||
                !(secretEnvironmentVariable[0] == '_' || char.IsLetter(secretEnvironmentVariable[0])) || secretEnvironmentVariable.Any(x =>
                    !(x >= 'a' && x <= 'z' || x >= 'A' && x <= 'Z' || x >= '0' && x <= '9' || x == '_')))
                throw new ArgumentException("An explicit ASCII process secret environment name is required.", nameof(secretEnvironmentVariable));
            return new EnvironmentContentKeyProvider(projectId, keyId, secretEnvironmentVariable);
        }

        private sealed class EnvironmentContentKeyProvider : IResourceContentKeyProvider
        {
            private readonly string projectId;
            private readonly string keyId;
            private readonly string variable;
            internal EnvironmentContentKeyProvider(string projectId, string keyId, string variable)
            { this.projectId = projectId; this.keyId = keyId; this.variable = variable; }

            public bool TryCopyKey(string project, string requestedKey, byte[] destination)
            {
                if (project != projectId || requestedKey != keyId) return false;
                if (destination == null || destination.Length != 32) throw new ArgumentException("Content key destination must be exactly 32 bytes.");
                var value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrEmpty(value)) return false;
                byte[] decoded = null;
                try
                {
                    if (value.Length != 44) throw new FormatException();
                    decoded = Convert.FromBase64String(value);
                    if (decoded.Length != 32 || Convert.ToBase64String(decoded) != value) throw new FormatException();
                    Buffer.BlockCopy(decoded, 0, destination, 0, 32);
                    return true;
                }
                catch (FormatException) { throw new InvalidOperationException("The configured content secret must be canonical Base64 of exactly 32 bytes."); }
                finally { if (decoded != null) Array.Clear(decoded, 0, decoded.Length); }
            }
        }

        /// <summary>显式导出完整 owned 版本到工程外新的本地摘要目录。失败保留部分目录供人工检查，绝不清理未知内容。</summary>
        public static long ExportRelease(ResourceRelease release, string destination)
        {
            var catalog = ResourcePublisher.VerifyExportInput(release);
            if (string.IsNullOrWhiteSpace(destination) || !Path.IsPathFullyQualified(destination))
                throw new InvalidOperationException("请选择工程外的绝对本地导出路径。");
            var full = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var repository = ResourceBuildMap.Absolute(".").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith("\\\\", StringComparison.Ordinal) || full.StartsWith(repository, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只允许显式工程外本地导出；不写网络路径或工程内容。");
            ResourceBuildOperation.RejectLinks(full);
            if (Directory.Exists(full) || File.Exists(full) || File.Exists(full + ".meta"))
                throw new InvalidOperationException("目标身份已存在，拒绝接管、合并或覆盖任何目录：" + full);
            if (!Directory.Exists(Path.GetDirectoryName(full)) || Path.GetFileName(full) != catalog.CatalogDigest)
                throw new InvalidOperationException("请选择已存在的本地父目录；新增版本子目录必须等于 CatalogDigest。");
            Directory.CreateDirectory(full);
            long copied = 0;
            var names = catalog.Bundles.Select(x => x.RelativeFile).Concat(catalog.SupportFiles.Select(x => x.RelativeFile))
                .Concat(new[] { "resource-catalog.json", ".ylr-complete.json" });
            foreach (var name in names)
            {
                ResourceBuildOperation.RejectLinks(full);
                var sourcePath = Path.Combine(release.DirectoryPath, name);
                ResourceBuildOperation.RejectLinks(sourcePath);
                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(Path.Combine(full, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                source.CopyTo(output); output.Flush(true); copied += source.Length;
            }
            // 导出是字节级归档，不挂载 Runtime；历史 Unity/App 版本仍按原 owner + Complete + Catalog 摘要完整验证。
            ResourcePublisher.VerifyExportInput(release, full);
            return copied;
        }

        /// <summary>
        /// 工作台共享一个常量程序集，必须明确收集当前平台/App 的全部 owned 已发布 Project，避免项目切换删除另一项目常量。
        /// 未发布项目不混入，存在已发布但不兼容的项目时明确失败，调用方可用 GenerateReferences 显式选择集合。
        /// </summary>
        public static IReadOnlyList<ResourceCatalogDocument> PublishedCatalogs(string appContentCompatibilityId)
        {
            var catalogs = new List<ResourceCatalogDocument>();
            foreach (var path in AssetDatabase.FindAssets("t:ResourceProject").Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal))
            {
                var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(path);
                var release = ResourcePublisher.ReadCurrent(project, EditorUserBuildSettings.activeBuildTarget, appContentCompatibilityId);
                if (release != null) catalogs.Add(release.Catalog);
            }
            if (catalogs.Count == 0) throw new InvalidOperationException("该平台/App 尚无明确归属的发布 Catalog。");
            return catalogs.AsReadOnly();
        }

        /// <summary>以 SerializedObject 编辑显式 GUID 选择，支持 Undo；调用方显式保存定义，不改 Importer。</summary>
        public static void AddSelections(ResourceProject project, int groupIndex, Object[] assets)
        {
            RequireEditableProject(project);
            if (groupIndex < 0 || groupIndex >= project.Groups.Count || assets == null || assets.Length == 0) throw new ArgumentException("请选择 Group 和有效资产。");
            var directories = ResourceProjectPaths.Resolve(project);
            var generated = ResourceProjectPaths.GeneratedDirectory(project);
            var paths = assets.Select(AssetDatabase.GetAssetPath).ToArray();
            foreach (var path in paths)
            {
                directories.RequireSource(path);
                if (ResourceProjectPaths.Contains(generated, path)) throw new InvalidOperationException("自动图集输出由规则加入Group，不能手工添加生成目录中的文件：" + path);
                if (new[] { ".cs", ".dll", ".asmdef", ".asmref", ".meta" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("这个文件不能作为资源条目添加：" + path);
                ResourceBuildOperation.ValidateAssetPath(path);
            }
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).Distinct(StringComparer.Ordinal).ToArray();
            if (guids.Any(x => project.Groups.SelectMany(g => g.Selections).Any(s => s.Guid == x))) throw new InvalidOperationException("选择已登记；请编辑已有条目。");
            BeginEdit("添加 Resource 条目");
            using var serialized = new SerializedObject(project);
            var selections = serialized.FindProperty("Groups").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("Selections");
            foreach (var guid in guids)
            {
                var index = selections.arraySize++;
                var item = selections.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("Guid").stringValue = guid;
                item.FindPropertyRelative("Address").stringValue = "";
                item.FindPropertyRelative("Aliases").ClearArray(); item.FindPropertyRelative("Extensions").ClearArray();
            }
            serialized.ApplyModifiedProperties();
        }

        /// <summary>新增空 Group 作为编辑草稿；构建仍要求 Group 至少选择一个可打包资产。</summary>
        public static int AddGroup(ResourceProject project, string id)
        {
            RequireEditableProject(project);
            ValidateGroupId(project, -1, id);
            using var serialized = new SerializedObject(project);
            var groups = serialized.FindProperty("Groups");
            if (groups.arraySize >= 16384) throw new InvalidOperationException("Group collection limit exceeded.");
            BeginEdit("新增 Resource Group");
            var index = groups.arraySize++;
            var group = groups.GetArrayElementAtIndex(index);
            group.FindPropertyRelative("Id").stringValue = id;
            group.FindPropertyRelative("Packing").enumValueIndex = 0;
            group.FindPropertyRelative("Selections").ClearArray();
            serialized.ApplyModifiedProperties();
            return index;
        }

        /// <summary>Group ID 是目录和生成常量的技术键；只改定义，不重命名已发布版本。</summary>
        public static void RenameGroup(ResourceProject project, int groupIndex, string id)
        {
            RequireEditableProject(project);
            RequireGroup(project, groupIndex);
            ValidateGroupId(project, groupIndex, id);
            BeginEdit("重命名 Resource Group");
            using var serialized = new SerializedObject(project);
            serialized.FindProperty("Groups").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("Id").stringValue = id;
            serialized.ApplyModifiedProperties();
        }

        public static void RemoveGroup(ResourceProject project, int groupIndex)
        {
            RequireEditableProject(project);
            RequireGroup(project, groupIndex);
            BeginEdit("移除 Resource Group");
            using var serialized = new SerializedObject(project);
            serialized.FindProperty("Groups").DeleteArrayElementAtIndex(groupIndex);
            serialized.ApplyModifiedProperties();
        }

        /// <summary>完整校验后一次提交批量移动；地址、alias、文件夹筛选与源 GUID 均保留。</summary>
        public static void MoveSelections(ResourceProject project, int sourceGroup, int[] indices, int targetGroup)
        {
            RequireEditableProject(project);
            RequireGroup(project, sourceGroup); RequireGroup(project, targetGroup);
            var selected = SelectionIndices(project, sourceGroup, indices);
            if (sourceGroup == targetGroup) throw new ArgumentException("请选择不同的目标 Group。");
            var values = selected.OrderBy(x => x).Select(x => project.Groups[sourceGroup].Selections[x]).ToArray();
            if (values.Any(x => x == null || project.Groups[targetGroup].Selections.Any(y => y.Guid == x.Guid)))
                throw new ArgumentException("目标 Group 已包含相同选择，拒绝部分移动。");
            BeginEdit("批量移动 Resource 条目");
            using var serialized = new SerializedObject(project);
            var groups = serialized.FindProperty("Groups");
            var source = groups.GetArrayElementAtIndex(sourceGroup).FindPropertyRelative("Selections");
            var target = groups.GetArrayElementAtIndex(targetGroup).FindPropertyRelative("Selections");
            foreach (var value in values)
            {
                var item = target.GetArrayElementAtIndex(target.arraySize++);
                item.FindPropertyRelative("Guid").stringValue = value.Guid;
                item.FindPropertyRelative("Address").stringValue = value.Address ?? "";
                SetStrings(item.FindPropertyRelative("Aliases"), value.Aliases);
                SetStrings(item.FindPropertyRelative("Extensions"), value.Extensions);
            }
            foreach (var index in selected) source.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
        }

        public static void RemoveSelections(ResourceProject project, int groupIndex, int[] indices)
        {
            RequireEditableProject(project);
            var selected = SelectionIndices(project, groupIndex, indices);
            BeginEdit("批量移除 Resource 条目");
            using var serialized = new SerializedObject(project);
            var entries = serialized.FindProperty("Groups").GetArrayElementAtIndex(groupIndex).FindPropertyRelative("Selections");
            foreach (var index in selected) entries.DeleteArrayElementAtIndex(index);
            serialized.ApplyModifiedProperties();
        }

        private static int[] SelectionIndices(ResourceProject project, int group, int[] indices)
        {
            RequireGroup(project, group);
            if (indices == null || indices.Length == 0 || indices.Distinct().Count() != indices.Length ||
                indices.Any(x => x < 0 || x >= project.Groups[group].Selections.Count))
                throw new ArgumentException("批量条目选择无效；未修改任何定义。");
            return indices.OrderByDescending(x => x).ToArray();
        }

        private static void RequireGroup(ResourceProject project, int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= project.Groups.Count) throw new ArgumentException("请选择有效的 Group。");
        }

        private static void ValidateGroupId(ResourceProject project, int index, string id)
        {
            ResourceAddress.ValidateId(id, nameof(id));
            if (project.Groups.Where((_, i) => i != index).Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Group ID 已存在或大小写冲突：" + id);
        }

        private static void SetStrings(SerializedProperty property, string[] values)
        {
            property.arraySize = values?.Length ?? 0;
            for (var i = 0; i < property.arraySize; i++) property.GetArrayElementAtIndex(i).stringValue = values[i];
        }

        private static void BeginEdit(string label)
        {
            // API/快捷键可在同一帧连续调用，不能让下一次批量操作吞并前一次编辑的 Undo。
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(label);
        }

        internal static void RequireEditableProject(ResourceProject project)
        {
            if (!project || !AssetDatabase.GetAssetPath(project).StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(project))
                throw new InvalidOperationException("需要明确保存于 Assets 的可编辑 ResourceProject。");
            ResourceBuildOperation.ValidateAssetPath(AssetDatabase.GetAssetPath(project));
            ResourceBuildOperation.RejectLinks(AssetDatabase.GetAssetPath(project) + ".meta");
        }
    }
}
