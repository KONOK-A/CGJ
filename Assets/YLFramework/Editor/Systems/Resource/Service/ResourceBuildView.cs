using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceBuildView
    {
        private ResourceBuildMap preview;
        private ResourceEditorBuildReport report;
        private string preflight;
        private string preflightError;
        private string bundleSearch = "";
        private int bundle;
        private Vector2 bundleScroll;
        private Vector2 sourceScroll;
        private (ResourceBuildBundle Bundle, string[] Paths, string[] Dependencies)[] previewRows = Array.Empty<(ResourceBuildBundle, string[], string[])>();
        private (ResourceBuildBundle Bundle, string[] Paths, string[] Dependencies)[] visibleBundles = Array.Empty<(ResourceBuildBundle, string[], string[])>();
        private string appliedBundleSearch;
        private HashSet<string> mainPaths = new();
        private readonly Dictionary<string, string> bundleGroups = new(StringComparer.Ordinal);
        private GUIStyle rowStyle;
        private bool advanced;
        private bool evidence;
        private bool publication;
        private bool attemptsExpanded;
        private IReadOnlyList<ResourceRelease> releases;
        private ResourceRelease selectedRelease;
        private string[] releaseLabels = Array.Empty<string>();
        private Vector2 releaseScroll;
        private string generationStatus;
        private bool generationFailed;
        private Vector2 artifactScroll;
        private int artifact;
        private readonly List<ResourceEditorBuildReport> attempts = new();
        internal bool DefinitionChanged { get; set; }
        private ResourceBuildRecipe recipe;

        internal void Reset()
        {
            InvalidatePreview(); report = null; generationStatus = null;
            attempts.Clear(); bundle = 0; bundleSearch = ""; DefinitionChanged = false;
        }
        internal void SetPreview(ResourceBuildMap value, ResourceProject project)
        {
            preview = value; bundle = 0; appliedBundleSearch = null;
            previewRows = value.Bundles.Select(x => (x, x.AssetPaths, x.Dependencies)).ToArray();
            mainPaths = new HashSet<string>(value.Assets.Select(x => x.SourcePath));
            bundleGroups.Clear();
            var bundlesByAsset = value.Assets.ToDictionary(x => x.AssetId, x => x.BundleName, StringComparer.Ordinal);
            var owners = value.Bundles.ToDictionary(x => x.Name, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var definition in project.Groups)
                if (value.Groups.TryGetValue(definition.Id, out var members))
                    foreach (var name in members.Where(bundlesByAsset.ContainsKey).Select(x => bundlesByAsset[x]).Distinct())
                        owners[name].Add(definition.Id + " (" + definition.Packing + ")");
            foreach (var item in owners) bundleGroups[item.Key] = item.Value.Count == 0 ? "共享 / 私有依赖" : string.Join(", ", item.Value);
        }
        internal void InvalidatePreview()
        {
            preview = null; preflight = null; preflightError = null; DefinitionChanged = true;
            releases = null; selectedRelease = null;
            releaseLabels = Array.Empty<string>();
            previewRows = Array.Empty<(ResourceBuildBundle, string[], string[])>();
            visibleBundles = previewRows; appliedBundleSearch = null;
            bundleGroups.Clear();
        }

        internal void DrawPreview(Action refresh)
        {
            EditorGUILayout.HelpBox("分包预览 / 预估：来自当前 Project 与 AssetDatabase 依赖图。未运行 BuildPipeline，Bundle 数量、源文件字节和重复避免估计不是压缩产物大小或 native memory 实测。", MessageType.Info);
            if (GUILayout.Button("重新计算分包与依赖预览")) refresh();
            if (preview == null) { EditorGUILayout.HelpBox("尚未计算预览，或定义/导入已变化。刷新将显示第一处校验错误，不复用旧目录。", MessageType.Info); return; }
            GUILayout.Label("主资产 " + preview.Assets.Count + " · Groups " + preview.Groups.Count + " · 预估 Bundle " + preview.Bundles.Count +
                " · 环归并 " + preview.MergedBundleCount + " · 避免重复源字节（估计） " + preview.AvoidedDuplicateSourceBytes, EditorStyles.wordWrappedLabel);
            bundleSearch = EditorGUILayout.TextField("筛选 Bundle / 源路径", bundleSearch);
            if (appliedBundleSearch != bundleSearch)
            {
                visibleBundles = previewRows.Where(x => x.Bundle.Name.IndexOf(bundleSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    bundleGroups[x.Bundle.Name].IndexOf(bundleSearch, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    x.Paths.Any(p => p.IndexOf(bundleSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                appliedBundleSearch = bundleSearch; bundle = 0; bundleScroll = Vector2.zero;
            }
            var bundles = visibleBundles;
            var rect = GUILayoutUtility.GetRect(200, Mathf.Clamp(bundles.Length * 24 + 6, 44, 180), GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            bundleScroll = GUI.BeginScrollView(rect, bundleScroll, new Rect(0, 0, Mathf.Max(0, rect.width - 20), Math.Max(rect.height - 2, bundles.Length * 24 + 4)));
            rowStyle ??= new GUIStyle(EditorStyles.miniButton) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            var first = Mathf.Max(0, (int)(bundleScroll.y / 24));
            for (var i = first; i < Math.Min(bundles.Length, first + 10); i++)
            {
                var item = bundles[i].Bundle;
                var label = bundleGroups[item.Name] + "  |  " + (item.IsScene ? "Scene" : item.IsShared ? "Shared" : "Asset / Private") + "  |  " + item.AssetPaths.Length + " sources";
                if (GUI.Toggle(new Rect(4, i * 24, rect.width - 28, 23), bundle == i, new GUIContent(label, item.Name + "\n预估分配，不是 Manifest 实测"), rowStyle) && bundle != i)
                { bundle = i; sourceScroll = Vector2.zero; }
            }
            GUI.EndScrollView();
            if (bundles.Length == 0) { GUILayout.Label("没有匹配的预估 Bundle。", EditorStyles.miniLabel); return; }
            bundle = Mathf.Clamp(bundle, 0, bundles.Length - 1);
            var selected = bundles[bundle].Bundle;
            GUILayout.Label("逻辑 Group / Packing：" + bundleGroups[selected.Name], EditorStyles.wordWrappedLabel);
            EditorGUILayout.SelectableLabel(selected.Name, EditorStyles.textField, GUILayout.Height(18));
            GUILayout.Label("源文件与归属（预估）", EditorStyles.boldLabel);
            var paths = bundles[bundle].Paths;
            var files = GUILayoutUtility.GetRect(200, Mathf.Clamp(paths.Length * 23 + 6, 48, 165), GUILayout.ExpandWidth(true));
            GUI.Box(files, GUIContent.none, EditorStyles.helpBox);
            sourceScroll = GUI.BeginScrollView(files, sourceScroll, new Rect(0, 0, Mathf.Max(0, files.width - 20), Math.Max(files.height - 2, paths.Length * 23 + 4)));
            var fileStart = Mathf.Max(0, (int)(sourceScroll.y / 23));
            for (var i = fileStart; i < Math.Min(paths.Length, fileStart + 10); i++)
            {
                var path = paths[i];
                GUI.Label(new Rect(4, i * 23, 48, 22), mainPaths.Contains(path) ? "Main" : selected.IsShared ? "Shared" : "Private", EditorStyles.miniLabel);
                if (GUI.Button(new Rect(54, i * 23, files.width - 82, 22), new GUIContent(Path.GetFileName(path), path), EditorStyles.linkLabel))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
            }
            GUI.EndScrollView();
            GUILayout.Label("直接 Bundle 依赖（预估）", EditorStyles.boldLabel);
            if (bundles[bundle].Dependencies.Length == 0) GUILayout.Label("无跨包依赖。", EditorStyles.miniLabel);
            foreach (var edge in bundles[bundle].Dependencies) GUILayout.Label(new GUIContent(bundleGroups[edge], edge), EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("依赖者（预估）", EditorStyles.boldLabel);
            foreach (var source in previewRows.Where(x => x.Dependencies.Contains(selected.Name))) GUILayout.Label(new GUIContent(bundleGroups[source.Bundle.Name], source.Bundle.Name), EditorStyles.wordWrappedMiniLabel);
        }

        internal void Draw(ResourceProject project, ref string app, ref bool generate, ref bool force,
            Action<Func<string>> queue, Action<ResourceBuildResult> rebind)
        {
            recipe = (ResourceBuildRecipe)EditorGUILayout.ObjectField("可共享构建配方", recipe, typeof(ResourceBuildRecipe), false);
            using (new EditorGUI.DisabledScope(!recipe))
                if (GUILayout.Button("在 Inspector 查看 / 编辑 / 执行配方")) Selection.activeObject = recipe;
            EditorGUILayout.HelpBox("配方入口使用其明确 Project 与执行选项；下方保留原不使用配方的工作流和默认行为。", MessageType.None);
            GUILayout.Label("1  校验输入", EditorStyles.boldLabel);
            app = EditorGUILayout.DelayedTextField(new GUIContent("App Content Compatibility", "与运行端配置精确匹配。脚本布局或 Player 消费能力变化须更新。"), app);
            advanced = EditorGUILayout.Foldout(advanced, "高级构建与生成设置", true);
            if (advanced)
            {
                force = EditorGUILayout.Toggle("显式强制重建", force);
                using (var definition = new SerializedObject(project))
                {
                    definition.Update();
                    EditorGUILayout.PropertyField(definition.FindProperty("Compression"), new GUIContent("Bundle 压缩"));
                    EditorGUILayout.PropertyField(definition.FindProperty("ContentEncryption"), new GUIContent("内容交付编码"));
                    EditorGUILayout.PropertyField(definition.FindProperty("ContentKeyId"), new GUIContent("Content KeyId（非密钥）"));
                    definition.ApplyModifiedProperties();
                }
                EditorGUILayout.HelpBox("加密仅支持 Windows x64 Editor/Mono；工作台从明确的进程 secret YL_RESOURCE_CONTENT_KEY 读取 Base64 32B，或由CI显式传provider。轮换需新KeyId。签发时必须确认渠道支持加密并与旧客户端endpoint隔离。", MessageType.Info);
                generate = EditorGUILayout.Toggle("发布后生成常量集合", generate);
                if (GUILayout.Button("准备本 Project 的显式 SpriteAtlas")) queue(() =>
                    "已准备显式图集：" + ResourceEditorOperations.PrepareAtlases(project) + "。此步骤只更新已选择图集的打包缓存；随后再执行构建。");
                EditorGUILayout.HelpBox("平台必须已激活并安装且匹配当前Editor主机，工作台不会切换平台。LZ4/LZMA/Uncompressed均保留StrictMode；修改后须保存Project。无法冻结输入的build callbacks始终阻止整体跳构建；不会伪造incremental命中。常量只从同平台/App全部owned已发布Project收集。", MessageType.Info);
            }
            var appValue = app; var generateValue = generate; var forceValue = force;
            if (GUILayout.Button("校验定义、归属与发布完整性（只读）")) queue(() =>
            {
                ValidateInput(project, appValue);
                return "预检成功。尚未执行构建或发布。";
            });
            if (preflight != null) EditorGUILayout.HelpBox(preflight, MessageType.Info);
            if (preflightError != null) EditorGUILayout.HelpBox(preflightError, MessageType.Error);
            GUILayout.Space(8);
            GUILayout.Label("2  构建 → 本地发布", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("构建只写 owned Workspace；发布新增不可变版本并原子提交 Index，不写 StreamingAssets，不上传网络，不激活 Runtime。构建、发布、生成分别报告，生成失败不会撤销成功发布。", MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("仅构建（不发布）", GUILayout.Height(29))) queue(() =>
            {
                var summary = ValidateInput(project, appValue);
                if (!EditorUtility.DisplayDialog("确认构建 Workspace", summary + "\n\n本次不发布、不生成常量。", "仅构建", "取消")) return "构建已取消。";
                return RecordReport(ResourceEditorOperations.BuildOnly(project, appValue, forceValue), rebind);
            });
            using (new EditorGUI.DisabledScope(report?.Build == null || DefinitionChanged))
                if (GUILayout.Button("发布已验证构建", GUILayout.Height(29))) queue(() =>
                {
                    var input = report.Build;
                    if (!EditorUtility.DisplayDialog("确认本地发布", "发布明确的 BuildResult，Publisher 将重新验证当前输入和 Workspace。\n" + input.WorkspacePath +
                        "\nApp=" + appValue + "\n不上传网络，不激活 Runtime。", "本地发布", "取消")) return "发布已取消。";
                    return RecordReport(ResourceEditorOperations.PublishBuild(input, appValue, generateValue), rebind);
                });
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("预检、构建并本地发布", GUILayout.Height(29))) queue(() =>
            {
                var summary = ValidateInput(project, appValue);
                if (!EditorUtility.DisplayDialog("确认构建与本地发布", summary, "构建并发布", "取消")) return "已取消，未创建输出。";
                return RecordReport(ResourceEditorOperations.BuildAndPublish(project, appValue, generateValue, forceRebuild: forceValue), rebind);
            });
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("读取当前发布（只读）")) queue(() =>
            {
                var release = ResourcePublisher.ReadCurrent(project, EditorUserBuildSettings.activeBuildTarget, appValue);
                selectedRelease = release;
                return release == null ? "该平台/App 尚无 owned 发布。" : "已验证当前发布：" + release.DirectoryPath + "\nCatalogDigest=" + release.Catalog.CatalogDigest;
            });
            if (GUILayout.Button("显式恢复本地事务")) queue(() =>
            {
                if (!EditorUtility.DisplayDialog("恢复 owned 发布事务？", "只恢复当前 Project/平台的已登记 journal；不接管未知文件、不删除历史、不激活 Runtime。", "恢复并验证", "取消")) return "恢复已取消。";
                var release = ResourcePublisher.Recover(project, EditorUserBuildSettings.activeBuildTarget, appValue);
                return release == null ? "尚无可恢复的 owned 发布。" : "已验证发布：" + release.DirectoryPath;
            });
            EditorGUILayout.EndHorizontal();
            DrawReport();
            GUILayout.Space(8);
            GUILayout.Label("3  交付与常量", EditorStyles.boldLabel);
            DrawHistory(project, queue);
            if (selectedRelease != null)
                EditorGUILayout.HelpBox("选定导出版本：" + selectedRelease.Catalog.ProjectId + "\nCatalogDigest=" + selectedRelease.Catalog.CatalogDigest +
                    "\nApp=" + selectedRelease.Catalog.AppContentCompatibilityId + "。只选择磁盘版本，不改变 Runtime active。", MessageType.None);
            using (new EditorGUI.DisabledScope(selectedRelease == null))
                if (GUILayout.Button("导出选定完整版本到工程外新本地目录")) queue(() =>
                {
                    var release = selectedRelease;
                    var parent = EditorUtility.OpenFolderPanel("选择工程外本地导出父目录", "", "");
                    if (string.IsNullOrEmpty(parent)) return "版本导出已取消。";
                    var output = Path.Combine(parent, release.Catalog.CatalogDigest);
                    if (!EditorUtility.DisplayDialog("确认本地导出", "新增完整版本目录：\n" + output + "\nProject=" + release.Catalog.ProjectId +
                        "\n不会覆盖、合并或删除任何已有目录；失败时保留部分输出供人工检查。不写网络。", "导出并验证", "取消")) return "版本导出已取消。";
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    var bytes = ResourceEditorOperations.ExportRelease(release, output);
                    return "本地导出并完整验证成功：" + output + "\n实际复制 " + bytes + " bytes；含验证耗时 " + watch.Elapsed.TotalMilliseconds.ToString("F2") + " ms。未执行网络部署。";
                });
            if (GUILayout.Button("签名当前发布元数据并导出到明确本地路径")) ResourceReleaseSigningWindow.Open(project, appValue);
            if (GUILayout.Button("从全部 owned 已发布 Project 生成常量")) queue(() =>
            {
                try
                {
                    var catalogs = ResourceEditorOperations.PublishedCatalogs(appValue);
                    if (!EditorUtility.DisplayDialog("确认常量输入集合", string.Join("\n", catalogs.Select(x => x.ProjectId)) +
                        "\n\n独立程序集 YLFramework.Generated.ResourceNames。更新保留 .meta；Unity 编译结果独立确认。", "生成集合", "取消")) return "常量生成已取消。";
                    var changed = ResourceCodeGenerator.GenerateReferences(catalogs);
                    if (changed) AssetDatabase.Refresh();
                    generationFailed = false;
                    return generationStatus = changed ? "常量源码已更新。业务 asmdef 引用 YLFramework.Generated.ResourceNames；待 Unity 编译确认。" : "常量源码字节未变化，未重写。";
                }
                catch (Exception error) { generationFailed = true; return generationStatus = "独立常量生成失败，已发布资源不回滚：" + error.Message; }
            });
            if (generationStatus != null) EditorGUILayout.HelpBox(generationStatus, generationFailed ? MessageType.Error : MessageType.Info);
            GUILayout.Label("常量默认位置：Assets/YLFrameworkData/YLResourceReferences.cs。工作台不内置网络部署、不读取私钥到 Project、不写内部归属 JSON。", EditorStyles.wordWrappedMiniLabel);
            if (attempts.Count > 1)
            {
                attemptsExpanded = EditorGUILayout.Foldout(attemptsExpanded, "本窗口构建尝试（" + attempts.Count + "，最多保留 32 条）", true);
                if (attemptsExpanded)
                    for (var i = attempts.Count - 1; i >= 0; i--)
                        if (GUILayout.Button("#" + (i + 1) + "  " + (attempts[i].Build == null ? "构建失败" : attempts[i].Release == null ? "构建完成，发布未确认" : "已发布") +
                            "  " + attempts[i].StartedAtUtc.ToString("u") + "  " + (attempts[i].Release?.Catalog.CatalogDigest ?? ""), EditorStyles.miniButton))
                        { report = attempts[i]; artifact = 0; DefinitionChanged = true; }
            }
        }

        private string ValidateInput(ResourceProject project, string app)
        {
            try { preflight = ResourceEditorOperations.Preflight(project, app); preflightError = null; return preflight; }
            catch (Exception error) { preflightError = error.Message; preflight = null; throw; }
        }

        private string RecordReport(ResourceEditorBuildReport completed, Action<ResourceBuildResult> rebind)
        {
            rebind(completed.Build);
            report = completed; attempts.Add(completed);
            if (attempts.Count > 32) attempts.RemoveAt(0);
            DefinitionChanged = false; selectedRelease = completed.Release; artifact = 0;
            if (completed.GenerationChanged) AssetDatabase.Refresh();
            return completed.Summary;
        }

        private void DrawHistory(ResourceProject project, Action<Func<string>> queue)
        {
            if (GUILayout.Button("读取已登记 owned 版本历史（只读）")) queue(() =>
            {
                releases = ResourcePublisher.ReadHistory(project, EditorUserBuildSettings.activeBuildTarget);
                releaseLabels = releases.Select(x => x.Catalog.CatalogDigest + "\nApp=" + x.Catalog.AppContentCompatibilityId +
                    " · Bundles=" + x.Catalog.Bundles.Count + " · FileBytes=" + x.Catalog.Bundles.Sum(b => b.Length)).ToArray();
                selectedRelease = releases.FirstOrDefault();
                releaseScroll = Vector2.zero;
                return "已完整验证 " + releases.Count + " 个 owner 登记版本。未恢复事务或修改 Index；按摘要排序，不推测构建时间。";
            });
            if (releases == null) return;
            if (releases.Count == 0) { GUILayout.Label("该 Project / 平台尚无登记版本。", EditorStyles.miniLabel); return; }
            publication = EditorGUILayout.Foldout(publication, "Owned 版本（" + releases.Count + "，按摘要排序）", true);
            if (!publication) return;
            var rect = GUILayoutUtility.GetRect(200, 165, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            releaseScroll = GUI.BeginScrollView(rect, releaseScroll, new Rect(0, 0, Mathf.Max(0, rect.width - 20), Math.Max(163, releases.Count * 42 + 4)));
            var first = Mathf.Max(0, (int)(releaseScroll.y / 42));
            for (var i = first; i < Math.Min(releases.Count, first + 6); i++)
            {
                var release = releases[i];
                if (GUI.Toggle(new Rect(4, i * 42, rect.width - 28, 40), ReferenceEquals(selectedRelease, release),
                    new GUIContent(releaseLabels[i], release.DirectoryPath), EditorStyles.miniButton))
                    selectedRelease = release;
            }
            GUI.EndScrollView();
        }

        private void DrawReport()
        {
            if (report == null) return;
            GUILayout.Label("实际执行结果", EditorStyles.boldLabel);
            GUILayout.Label("UTC " + report.StartedAtUtc.ToString("u") + " · 本次操作 " + report.ElapsedMilliseconds.ToString("F2") + " ms", EditorStyles.miniLabel);
            EditorGUILayout.HelpBox(report.Build == null ? "BUILD FAILED: " + report.BuildError?.Message : "BUILD SUCCEEDED: " + report.Build.WorkspacePath,
                report.Build == null ? MessageType.Error : MessageType.Info);
            EditorGUILayout.HelpBox(report.Release != null ? "PUBLISH SUCCEEDED: " + report.Release.DirectoryPath :
                report.PublicationError != null ? "PUBLISH UNCONFIRMED: " + report.PublicationError.Message : "PUBLISH NOT RUN",
                report.PublicationError == null ? MessageType.None : MessageType.Error);
            if (report.GenerationRequested || report.GenerationError != null)
                EditorGUILayout.HelpBox(report.GenerationError != null ? "CODEGEN FAILED (发布不回滚): " + report.GenerationError.Message :
                    report.Release == null ? "CODEGEN NOT RUN" : report.GenerationChanged ? "CODEGEN SOURCE UPDATED；编译单独确认。" : "CODEGEN SOURCE UNCHANGED", report.GenerationError == null ? MessageType.None : MessageType.Error);
            if (report.Build == null) return;
            var build = report.Build;
            if (DefinitionChanged) EditorGUILayout.HelpBox("定义或工程导入已变化。下方为上次执行的历史实测，不是当前输入的验收结果。", MessageType.Warning);
            GUILayout.Label("Manifest 验证 Bundle " + build.Bundles.Count + " · raw 文件 " + build.Files.Count + " · raw 文件总字节 " + build.Files.Sum(x => x.Length) +
                "\nPreflight " + build.PreflightMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms · Build " + build.BuildMilliseconds.ToString("F2", CultureInfo.InvariantCulture) +
                " ms · Validation " + build.ValidationMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms\n输入哈希读取 " + build.SourceHashBytes +
                " bytes · 产物哈希读取 " + build.ArtifactHashBytes + " bytes · 发布复制 " + (report.Release == null ? "未执行 / 未确认" : report.Release.CopiedBytes + " bytes"), EditorStyles.wordWrappedLabel);
            GUILayout.Label("Builder elapsed " + build.ElapsedMilliseconds.ToString("F2") + " ms · BuildPipeline " + (build.UnityBuildInvoked ? build.BuildPipelineMilliseconds.ToString("F2") + " ms" : "未调用") +
                "\nMap（含后验） " + build.MapMilliseconds.ToString("F2") + " ms · 输入指纹（含后验） " + build.InputFingerprintMilliseconds.ToString("F2") +
                " ms · 产物 Hash " + build.ArtifactHashMilliseconds.ToString("F2") + " ms · Native 读回 " + build.ReadbackMilliseconds.ToString("F2") +
                " ms\nBundle FileBytes " + build.BundleFileBytes + " · Publisher " + (report.PublicationMilliseconds?.ToString("F2") ?? "未执行") +
                " ms · Codegen " + (report.GenerationMilliseconds?.ToString("F2") ?? "未执行") + " ms", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("Delivery " + build.ContentEncryption + " · KeyId=" + (build.ContentKeyId ?? "None") + " · 总字节 " + build.DeliveryBytes +
                "\n新编码 " + build.EncodedDeliveryBytes + " bytes · 已认证复用 " + build.ReusedDeliveryBytes + " bytes · 编码/交付验证 " +
                build.ContentEncodingMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms", EditorStyles.wordWrappedMiniLabel);
            if (report.Release != null)
                GUILayout.Label("发布复制 IO " + report.Release.CopyMilliseconds.ToString("F2") + " ms · 新 staging 验证 " + report.Release.StagingValidationMilliseconds.ToString("F2") +
                    " ms · 重复版本=" + report.Release.IsRepeat, EditorStyles.wordWrappedMiniLabel);
            foreach (var reason in build.ExecutionReasons) GUILayout.Label(reason, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("Bundle 内容对比：变化 " + build.BundleChanges.Count(x => x.ContentChanged == true) + " · 未变 " + build.BundleChanges.Count(x => x.ContentChanged == false) +
                " · 无可比基线 " + build.BundleChanges.Count(x => !x.ContentChanged.HasValue), EditorStyles.wordWrappedMiniLabel);
            if (build.CacheReuseBlockedBy.Count != 0)
                EditorGUILayout.HelpBox("安全强制构建：" + build.CacheReuseBlockedBy.Count + " 项无法冻结输入的 callbacks 阻止整体跳构建。重复产物摘要相同也不能绕过此门禁。", MessageType.Warning);
            evidence = EditorGUILayout.Foldout(evidence, "展开真实输入、Manifest 和产物证据", true);
            if (!evidence) return;
            EditorGUILayout.SelectableLabel("InputFingerprint=" + build.InputFingerprint + "\nBuildSettingsFingerprint=" + build.BuildSettingsFingerprint,
                EditorStyles.wordWrappedMiniLabel, GUILayout.Height(55));
            if (report.Release != null)
                EditorGUILayout.SelectableLabel("BuildId=" + report.Release.Catalog.BuildId + "\nCatalogDigest=" + report.Release.Catalog.CatalogDigest,
                    EditorStyles.wordWrappedMiniLabel, GUILayout.Height(55));
            foreach (var callback in build.CacheReuseBlockedBy) GUILayout.Label("Callback: " + callback, EditorStyles.wordWrappedMiniLabel);
            var rect = GUILayoutUtility.GetRect(200, 150, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, EditorStyles.helpBox);
            artifactScroll = GUI.BeginScrollView(rect, artifactScroll, new Rect(0, 0, Mathf.Max(0, rect.width - 20), Math.Max(148, build.Bundles.Count * 24 + 4)));
            var first = Mathf.Max(0, (int)(artifactScroll.y / 24));
            for (var i = first; i < Math.Min(build.Bundles.Count, first + 9); i++)
                if (GUI.Toggle(new Rect(4, i * 24, rect.width - 28, 23), artifact == i, build.Bundles[i].Name, EditorStyles.miniButton)) artifact = i;
            GUI.EndScrollView();
            if (build.Bundles.Count == 0) return;
            artifact = Mathf.Clamp(artifact, 0, build.Bundles.Count - 1);
            var item = build.Bundles[artifact];
            var file = build.Files.First(x => x.Name == item.Name);
            var delivery = build.DeliveryFiles.First(x => x.Name == item.Name);
            var change = build.BundleChanges.First(x => x.Name == item.Name);
            var text = item.Name + "\nUnityHash=" + item.UnityHash + "\nRaw SHA256=" + file.Sha256 + " / bytes=" + file.Length +
                "\nDelivery SHA256=" + delivery.Sha256 + " / bytes=" + delivery.Length +
                "\nPlacement=" + item.Placement + " / source files=" + item.SourceFileCount + " / source bytes=" + item.SourceBytes +
                "\nManifest dependents=" + string.Join(", ", item.Dependents) +
                "\nPrevious SHA256=" + (change.PreviousSha256 ?? "无兼容基线") + "\n" + change.Reason + "\nManifest dependencies=" + string.Join(", ", item.Dependencies);
            EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(110));
        }
    }
}
