using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    /// <summary>构建、发布和生成的独立结果；失败首因不会被后续生成或 UI 操作覆盖。</summary>
    public sealed class ResourceEditorBuildReport
    {
        public ResourceBuildResult Build { get; internal set; }
        public ResourceRelease Release { get; internal set; }
        public Exception BuildError { get; internal set; }
        public Exception PublicationError { get; internal set; }
        public Exception GenerationError { get; internal set; }
        public bool GenerationRequested { get; internal set; }
        public bool GenerationChanged { get; internal set; }
        public IReadOnlyList<string> GeneratedProjects { get; internal set; } = Array.Empty<string>();
        public string Summary => (Build == null ? "构建未完成：" + BuildError?.Message : "构建成功：" + Build.WorkspacePath) + "\n" +
            (Release != null ? "发布成功：" + Release.DirectoryPath + "\nCatalogDigest=" + Release.Catalog.CatalogDigest :
                PublicationError != null ? "发布未确认：" + PublicationError.Message + "；如涉及提交点后的 IO，请显式恢复确认。" : "发布未执行。") + "\n" +
            (GenerationError != null ? "常量生成失败（资源版本不回滚）：" + GenerationError.Message :
                GenerationRequested && Release != null ? "常量集合=" + string.Join(", ", GeneratedProjects) + "；" + (GenerationChanged ? "源码已更新，Unity 编译结果独立确认。" : "源码未变化。") : "常量生成未执行。");
    }

    /// <summary>工作台/自动化共用编排，只调用正式 Builder/Publisher/常量生成器，不读取旧全局 Importer 标记。</summary>
    public static class ResourceEditorOperations
    {
        /// <summary>仅保存明确归属的 Assets 定义，不保存用户场景，不经过链接路径。</summary>
        public static void SaveProject(ResourceProject project)
        {
            RequireEditableProject(project); AssetDatabase.SaveAssetIfDirty(project);
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
        public static string Preflight(ResourceProject project, string appContentCompatibilityId)
        {
            ResourceBundleBuilder.ValidateEditor(EditorUserBuildSettings.activeBuildTarget);
            ResourceBundleBuilder.RejectLoadedBundles();
            RequireEditableProject(project);
            if (EditorUtility.IsDirty(project)) throw new InvalidOperationException("请先显式保存 ResourceProject 定义，再执行构建预检。");
            _ = new ResourceAddress(appContentCompatibilityId, "compatibility");
            if (appContentCompatibilityId.Length > 128) throw new ArgumentException("AppContentCompatibilityId exceeds its schema bound.");
            var map = ResourceBuildMap.Create(project);
            _ = ResourceBundleBuilder.DescribeSubAssets(map);
            var workspace = ResourceBundleBuilder.PreflightWorkspace(project, EditorUserBuildSettings.activeBuildTarget);
            var root = ResourcePublisher.Preflight(project, EditorUserBuildSettings.activeBuildTarget);
            return "ProjectId=" + map.ProjectId + " / GUID=" + map.ProjectGuid + "\n" +
                "平台=" + EditorUserBuildSettings.activeBuildTarget + " / Unity=" + Application.unityVersion + " / App=" + appContentCompatibilityId + "\n" +
                "资产=" + map.Assets.Count + " / Group=" + map.Groups.Count + " / Bundle=" + map.Bundles.Count + "\nWorkspace=" + workspace + "\n发布根=" + root +
                "\n仅本地/内部受控发布，不写 StreamingAssets，不执行 Runtime 激活；同步 native 构建期间不能中途取消。";
        }

        /// <summary>错误分阶段返回；生成失败不改变已成功的版本。取消仅影响本次构建/发布的提交前检查点。</summary>
        public static ResourceEditorBuildReport BuildAndPublish(ResourceProject project, string appContentCompatibilityId,
            bool generateReferences = false, string directory = "Assets/YLFrameworkData", bool forceRebuild = false, CancellationToken cancellation = default)
        {
            var report = new ResourceEditorBuildReport { GenerationRequested = generateReferences };
            try
            {
                cancellation.ThrowIfCancellationRequested();
                Preflight(project, appContentCompatibilityId);
                report.Build = ResourceBundleBuilder.Build(project, EditorUserBuildSettings.activeBuildTarget, forceRebuild, cancellation);
            }
            catch (Exception error) { report.BuildError = error; return report; }
            try { report.Release = ResourcePublisher.Publish(report.Build, appContentCompatibilityId, cancellation); }
            catch (Exception error) { report.PublicationError = error; return report; }
            if (generateReferences)
                try
                {
                    var catalogs = PublishedCatalogs(appContentCompatibilityId);
                    report.GeneratedProjects = catalogs.Select(x => x.ProjectId).ToList().AsReadOnly();
                    report.GenerationChanged = ResourceCodeGenerator.GenerateReferences(catalogs, directory);
                }
                catch (Exception error) { report.GenerationError = error; }
            return report;
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
            var root = AssetDatabase.GUIDToAssetPath(project.AssetRootGuid);
            if (!AssetDatabase.IsValidFolder(root)) throw new InvalidOperationException("请先选择项目资源根。");
            var paths = assets.Select(AssetDatabase.GetAssetPath).ToArray();
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path) || (path != root && !path.StartsWith(root + "/", StringComparison.Ordinal)) ||
                    new[] { ".cs", ".dll", ".asmdef", ".asmref", ".meta" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidOperationException("选择不属于本 Project 的可打包资源根：" + path);
                ResourceBuildOperation.ValidateAssetPath(path);
            }
            var guids = paths.Select(AssetDatabase.AssetPathToGUID).Distinct(StringComparer.Ordinal).ToArray();
            if (guids.Any(x => project.Groups.SelectMany(g => g.Selections).Any(s => s.Guid == x))) throw new InvalidOperationException("选择已登记；请编辑已有条目。");
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

        internal static void RequireEditableProject(ResourceProject project)
        {
            if (!project || !AssetDatabase.GetAssetPath(project).StartsWith("Assets/", StringComparison.Ordinal) || !AssetDatabase.IsOpenForEdit(project))
                throw new InvalidOperationException("需要明确保存于 Assets 的可编辑 ResourceProject。");
            ResourceBuildOperation.ValidateAssetPath(AssetDatabase.GetAssetPath(project));
            ResourceBuildOperation.RejectLinks(AssetDatabase.GetAssetPath(project) + ".meta");
        }
    }
}
