using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceBuildView
    {
        private ResourceBuildMap preview;
        private bool previewStale;
        private ResourceEditorBuildReport report;
        private ResourcePreflightResult preflight;
        private ResourcePreflightResult readiness;
        private bool readinessDirty = true;
        private int recipeRevision;
        private string lastWindowApp;
        private string recipeGuid;
        private ResourceBuildInvocation effective;
        private Exception effectiveError;
        private Vector2 optionsScroll;
        private Vector2 resultScroll;
        private bool compactResult;
        private readonly ResourceAnalysisView analysis = new();
        private bool advanced;
        private bool executionHelp;
        private bool evidence;
        private bool attemptsExpanded;
        private readonly ResourceDeliveryView delivery = new();
        private Vector2 artifactScroll;
        private int artifact;
        private readonly List<ResourceEditorBuildReport> attempts = new();
        private ResourceEditorBuildReport projectedReport;
        private long projectedRawBytes;
        private string projectedCosts;
        private string projectedChanges;
        private Dictionary<string, ResourceBuiltFile> rawFiles;
        private Dictionary<string, ResourceBuiltFile> deliveryFiles;
        private Dictionary<string, ResourceBundleChange> bundleChanges;
        internal bool DefinitionChanged { get; set; }
        internal ResourceEditorBuildReport CurrentReport => report;
        internal bool IsHistoricalReport => report != null && !ReferenceEquals(report,
            attempts.LastOrDefault(x => (x.Build?.Map.ProjectGuid ?? x.ProjectGuid) == (report.Build?.Map.ProjectGuid ?? report.ProjectGuid)));
        internal bool ExactReportStale => DefinitionChanged || IsHistoricalReport;
        private void TrackWindowApp(string value)
        {
            if (lastWindowApp == value) return;
            lastWindowApp = value;
            if (HasRecipe) return;
            readinessDirty = true; readiness = null; preflight = null;
        }
        private ResourceBuildRecipe recipe;
        internal bool HasRecipe => !string.IsNullOrEmpty(recipeGuid);
        internal ResourceBuildRecipe Recipe => recipe;
        internal void SetRecipe(ResourceBuildRecipe value, string guid)
        {
            if (ReferenceEquals(recipe, value) && recipeGuid == guid) return;
            recipe = value; recipeGuid = guid; readinessDirty = true; readiness = null; preflight = null; DefinitionChanged = true;
        }
        internal void SaveRecipe()
        {
            if (!HasRecipe || !recipe) throw new InvalidOperationException("所选构建配方已丢失。请解除选择或重新选择；不会改为保存其他配方。");
            ResourceEditorOperations.SaveRecipe(recipe); readinessDirty = true;
        }

        internal void Reset()
        {
            InvalidatePreview(); report = null; delivery.Reset();
            preview = null; analysis.Reset(); DefinitionChanged = false;
        }
        internal void SetPreview(ResourceBuildMap value, ResourceProject project)
        {
            preview = value; previewStale = false; analysis.Error = null; analysis.AtlasPending = null;
        }
        internal void InvalidatePreview()
        {
            preflight = null; readiness = null; readinessDirty = true; previewStale = true; DefinitionChanged = true;
            delivery.Invalidate();
        }

        internal void DrawPreview(ResourceProject project, float width, Action refresh, Action<ResourceBuildSource> locate) =>
            analysis.Draw(project, preview, report, ExactReportStale, previewStale, width, refresh, locate);
        internal void SetPreviewError(Exception error, ResourceAtlasPlan pending = null) { previewStale = true; analysis.Error = error; analysis.AtlasPending = pending; }

        internal void Draw(ResourceProject project, ref string app, ref bool generate, ref bool force, ref string directory, float width,
            Action<Func<string>> queue, Action<Func<string>> run, Action<ResourceBuildResult> rebind,
            Action<ResourceBuildRecipe> selectRecipe, Action<ResourceProject> switchProject, Action showAnalysis)
        {
            TrackWindowApp(app);
            if (recipe && recipeRevision != EditorUtility.GetDirtyCount(recipe)) { recipeRevision = EditorUtility.GetDirtyCount(recipe); readinessDirty = true; preflight = null; DefinitionChanged = true; }
            if (width < 1000) compactResult = ResourceWorkbenchGUI.Navigation(compactResult ? 1 : 0, new[] { "执行配置", "构建结果" }) == 1;
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            var wide = width >= 1000;
            if (wide || !compactResult)
            {
                EditorGUILayout.BeginVertical(wide ? new[] { GUILayout.Width(width * .46f), GUILayout.ExpandHeight(true) } : new[] { GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true) });
                optionsScroll = EditorGUILayout.BeginScrollView(optionsScroll, GUILayout.ExpandHeight(true));
                if (wide) { GUILayout.Space(8); GUILayout.Label("执行选项", EditorStyles.boldLabel); GUILayout.Space(6); }
                EditorGUI.BeginChangeCheck();
                var picked = ResourceWorkbenchGUI.ObjectField("构建配方（可选）", recipe, "首次构建无需配方。配方用于保存和复用执行选项，不替代资源项目的分组、压缩或加密设置。");
                if (EditorGUI.EndChangeCheck()) selectRecipe(picked);
                if (HasRecipe)
                {
                    if (!recipe)
                    {
                        EditorGUILayout.HelpBox("所选构建配方已丢失。请解除选择或重新选择配方。\n配方 GUID：" + recipeGuid + "\n不会自动改用窗口选项。", MessageType.Error);
                        if (GUILayout.Button("解除丢失配方选择")) selectRecipe(null);
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Label(EditorUtility.IsDirty(recipe) ? "构建配方未保存 *" : "正在编辑所选配方", EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("保存配方", ResourceWorkbenchGUI.Button, GUILayout.Width(96))) run(() => { SaveRecipe(); return "所选构建配方已保存；未保存资源项目或场景。"; });
                        EditorGUILayout.EndHorizontal();
                        using (var serialized = new SerializedObject(recipe))
                            if (ResourceBuildRecipeFields.Draw(serialized, false)) { readinessDirty = true; preflight = null; DefinitionChanged = true; }
                        if (recipe.Project != project)
                        {
                            EditorGUILayout.HelpBox("配方属于另一个资源项目，不能执行。请切换到配方所属项目，或解除配方选择。\n当前：" + Identity(project) + "\n配方：" + Identity(recipe.Project), MessageType.Error);
                            EditorGUILayout.BeginHorizontal();
                            using (new EditorGUI.DisabledScope(!recipe.Project))
                                 if (GUILayout.Button("切换到配方所属项目", ResourceWorkbenchGUI.Button)) switchProject(recipe.Project);
                             if (GUILayout.Button("解除配方选择", ResourceWorkbenchGUI.Button)) selectRecipe(null);
                            EditorGUILayout.EndHorizontal();
                        }
                        else if (GUILayout.Button("解除配方，使用窗口选项", ResourceWorkbenchGUI.Button)) selectRecipe(null);
                    }
                }
                else
                {
                    EditorGUI.BeginChangeCheck();
                    app = ResourceWorkbenchGUI.TextField("资源兼容标识（App）", app, true, "由应用约定的资源兼容标识，不是游戏版本号。构建与后续使用必须匹配，例如 app-v1（仅示例）。");
                    if (EditorGUI.EndChangeCheck()) { readinessDirty = true; preflight = null; }
                    GUILayout.Label("不使用配方也可以构建。完成资源配置并保存后，直接点击“构建并本地发布”。", ResourceWorkbenchGUI.Secondary);
                }
                if (!HasRecipe || recipe)
                {
                    GUILayout.Space(6);
                    advanced = ResourceWorkbenchGUI.Disclosure(advanced, "附加构建选项");
                    if (advanced)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            if (HasRecipe)
                            {
                                using var serialized = new SerializedObject(recipe);
                                if (ResourceBuildRecipeFields.Draw(serialized, advancedOnly: true)) { readinessDirty = true; preflight = null; DefinitionChanged = true; }
                            }
                            else
                            {
                                EditorGUI.BeginChangeCheck();
                                force = ResourceWorkbenchGUI.Toggle("强制重建", force, "不使用可跳过构建的缓存结果；不放宽验证。");
                                generate = ResourceWorkbenchGUI.Toggle("生成资源地址常量", generate, "本地发布成功后，额外生成地址常量；仅构建不执行生成。");
                                if (generate) directory = ResourceWorkbenchGUI.TextField("常量目标目录", directory, true);
                                if (EditorGUI.EndChangeCheck()) { readinessDirty = true; preflight = null; }
                            }
                        }
                    }
                }
                var activeForce = HasRecipe ? recipe && recipe.ForceRebuild : force;
                var activeGenerate = HasRecipe ? recipe && recipe.GenerateReferences : generate;
                if (!advanced && (activeForce || activeGenerate)) EditorGUILayout.HelpBox("已启用：" + (activeForce ? "强制重建" : "") + (activeForce && activeGenerate ? "；" : "") +
                    (activeGenerate ? "发布后生成资源地址常量 → " + (HasRecipe ? recipe.ReferenceDirectory : directory) : ""), MessageType.None);
                if (activeGenerate) GUILayout.Label("地址常量面向当前平台所有已登记的发布项目，要求它们的 Unity 与 App 兼容标识一致。", ResourceWorkbenchGUI.Secondary);
                if (readinessDirty)
                {
                    effective = null; effectiveError = null;
                    try { if (HasRecipe && recipe) effective = ResourceEditorOperations.ResolveRecipe(recipe); }
                    catch (Exception error) { effectiveError = error; }
                    readiness = HasRecipe ? ResourceEditorOperations.InspectRecipePreflight(recipe) : ResourceEditorOperations.CheckReadiness(project, app);
                    readinessDirty = false;
                }
                GUILayout.Space(5);
                var policyProject = HasRecipe ? (recipe ? recipe.Project : null) : project;
                GUILayout.Label("资源项目的构建策略", EditorStyles.boldLabel);
                if (policyProject) GUILayout.Label(policyProject.Compression + " 压缩 · " + (policyProject.ContentEncryption == YLFramework.Services.Resource.ResourceContentEncryption.None ? "不加密 (None)" :
                    policyProject.ContentEncryption + " 加密 · KeyId=" + policyProject.ContentKeyId), EditorStyles.wordWrappedLabel);
                else GUILayout.Label("配方指定的资源项目不可用；不会改用其他项目的构建设置。", ResourceWorkbenchGUI.Secondary);
                if (effectiveError != null) EditorGUILayout.HelpBox("无法读取配方的执行选项，请展开执行说明查看错误原因。", MessageType.Error);
                if (readiness?.State == ResourcePreflightState.StaticBlocked) EditorGUILayout.HelpBox("开始前需要修正配置：\n" + ReadinessSummary(readiness), MessageType.Error);
                else if (readiness != null) GUILayout.Label("配置基础检查通过；执行前仍会检查资源与输出。", ResourceWorkbenchGUI.Secondary);
                if (preflight != null) EditorGUILayout.HelpBox(preflight.State == ResourcePreflightState.Complete ? "资源检查完成，尚未构建或发布。执行时会再次核对。" :
                    preflight.State == ResourcePreflightState.AtlasPreparationRequired ? "图集需要生成或更新。正式构建会先准备图集，再继续检查资源；也可以到‘资源配置 > 图集’先预览计划。" : "资源检查未通过，请展开执行说明查看错误原因。",
                    preflight.Error != null ? MessageType.Error : preflight.State == ResourcePreflightState.Complete ? MessageType.Info : MessageType.Warning);
                executionHelp = ResourceWorkbenchGUI.Disclosure(executionHelp, "执行说明与技术详情");
                if (executionHelp)
                {
                    GUILayout.Label("None 表示不加密，与压缩方式无关。加密构建只在显式检查或执行时读取外部密钥；KeyId 不是密钥。", ResourceWorkbenchGUI.Secondary);
                    GUILayout.Label("只使用当前已激活且可用的平台。操作排队时可以取消；构建开始后须等待完成。", ResourceWorkbenchGUI.Secondary);
                    if (effective != null) GUILayout.Label("本次配方参数：\n" + effective, ResourceWorkbenchGUI.Secondary);
                    if (effectiveError != null) GUILayout.Label(ResourceEditorText.FailureDetails(effectiveError), ResourceWorkbenchGUI.Secondary);
                    if (readiness != null) GUILayout.Label("配置检查详情：\n" + readiness.Summary + (readiness.Error == null ? "" : "\n" + ResourceEditorText.FailureDetails(readiness.Error)), ResourceWorkbenchGUI.Secondary);
                    if (preflight != null) GUILayout.Label("资源检查详情：\n" + preflight.Summary + (preflight.Error == null ? "" : "\n" + ResourceEditorText.FailureDetails(preflight.Error)), ResourceWorkbenchGUI.Secondary);
                }
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
            }
            if (wide)
            {
                var divider = GUILayoutUtility.GetRect(12, 12, 0, 100000, GUILayout.Width(12), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(new Rect(divider.center.x, divider.y + 6, 1, Mathf.Max(0, divider.height - 12)), ResourceWorkbenchGUI.Divider);
            }
            if (wide || compactResult)
            {
                EditorGUILayout.BeginVertical(GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.ExpandHeight(true));
                if (wide) { GUILayout.Space(8); GUILayout.Label("执行结果", EditorStyles.boldLabel); GUILayout.Space(6); }
                if (report == null) GUILayout.Label("构建结果会显示在这里。\n首次使用：添加资源 → 保存项目 → 构建并本地发布。\n本窗口的执行记录可在底部“输出”中查看；重载后不恢复临时构建报告。", ResourceWorkbenchGUI.Secondary);
                else
                {
                    DrawReport();
                    if (report.Build != null && GUILayout.Button("查看这次构建的分包", ResourceWorkbenchGUI.Button)) { analysis.UseExact(); showAnalysis(); }
                }
                EditorGUILayout.EndScrollView(); EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
            var appValue = app; var generateValue = generate; var forceValue = force; var directoryValue = directory;
            var selectedRecipeGuid = recipeGuid;
            var canExecute = readiness?.State == ResourcePreflightState.StaticReady && (!HasRecipe || recipe && recipe.Project == project);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!canExecute))
            {
                if (GUILayout.Button("检查资源", ResourceWorkbenchGUI.Button, GUILayout.Width(124))) queue(() =>
                {
                    var checkedInput = ValidateInput(project, appValue, selectedRecipeGuid);
                    return ResourceEditorText.Preflight(checkedInput) + "\n检查详情：\n" + checkedInput.Summary;
                });
                if (HasRecipe)
                {
                    if (ResourceWorkbenchGUI.PrimaryAction(effective?.Action == ResourceBuildAction.BuildAndPublish ? "按配方构建并本地发布..." : "按配方仅构建...")) queue(() =>
                    {
                        var chosen = RequireMatchingRecipe(project, selectedRecipeGuid);
                        var checkedInput = ValidateInput(project, appValue, selectedRecipeGuid);
                        var frozen = ResourceEditorOperations.ResolveRecipe(chosen);
                        if (!ResourceOperationConfirmation.Confirm("确认执行构建配方", frozen + "\n\n" + checkedInput.Summary + "\n按配方中的执行操作运行；不部署、不上传、不切换运行版本。", "执行构建配方",
                            ResourceConfirmationSummary.Build(checkedInput, frozen.Action == ResourceBuildAction.BuildAndPublish ? "构建并本地发布" : "仅构建", frozen.ForceRebuild, frozen.GenerateReferences, frozen.ReferenceDirectory))) return "构建配方执行已取消。";
                        chosen = RequireMatchingRecipe(project, selectedRecipeGuid);
                        return RecordReport(ResourceEditorOperations.ExecuteRecipe(chosen), rebind);
                    });
                }
                else
                {
                    if (GUILayout.Button("仅构建...", ResourceWorkbenchGUI.Button, GUILayout.Width(104))) queue(() =>
                    {
                        var checkedInput = ValidateInput(project, appValue, null);
                        if (!ResourceOperationConfirmation.Confirm("确认仅构建", checkedInput.Summary + "\nForceRebuild=" + forceValue + "\n本次不发布、不生成常量。", "仅构建",
                            ResourceConfirmationSummary.Build(checkedInput, "仅构建", forceValue, false, null))) return "构建已取消。";
                        return RecordReport(ResourceEditorOperations.BuildOnly(project, appValue, forceValue), rebind);
                    });
                    if (ResourceWorkbenchGUI.PrimaryAction("构建并本地发布...")) queue(() =>
                    {
                        var checkedInput = ValidateInput(project, appValue, null);
                        if (!ResourceOperationConfirmation.Confirm("确认构建与本地发布", checkedInput.Summary + "\nForceRebuild=" + forceValue + "\n生成=" + generateValue + " / 目录=" + directoryValue, "构建并本地发布",
                            ResourceConfirmationSummary.Build(checkedInput, "构建并本地发布", forceValue, generateValue, directoryValue))) return "已取消，未创建输出。";
                        return RecordReport(ResourceEditorOperations.BuildAndPublish(project, appValue, generateValue, directoryValue, forceValue), rebind);
                    });
                }
            }
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(report?.Build == null || DefinitionChanged || !canExecute))
                if (report?.Build != null && (report.Release == null || evidence) && GUILayout.Button("发布这次构建（不重新构建）...", ResourceWorkbenchGUI.Button))
                {
                    var input = report.Build;
                    queue(() =>
                    {
                        var publicationOptions = string.IsNullOrEmpty(selectedRecipeGuid) ? null : ResourceEditorOperations.ResolveRecipe(RequireMatchingRecipe(project, selectedRecipeGuid));
                        if (publicationOptions != null && EditorUtility.IsDirty(recipe)) throw new InvalidOperationException("请先保存所选构建配方。");
                        var publicationApp = publicationOptions?.AppContentCompatibilityId ?? appValue;
                        var publicationGenerate = publicationOptions?.GenerateReferences ?? generateValue;
                        var publicationDirectory = publicationOptions?.ReferenceDirectory ?? directoryValue;
                        if (string.IsNullOrEmpty(publicationApp)) throw new InvalidOperationException("尚未填写资源兼容标识（App）。请先修正配方，或解除配方选择后填写窗口选项。");
                        if (!ResourceOperationConfirmation.Confirm("确认发布这次构建", "资源项目=" + input.Map.ProjectId + "\n平台=" + input.Platform + "\n构建工作目录=" + input.WorkspacePath +
                            "\nInputFingerprint=" + input.InputFingerprint + "\nApp=" + publicationApp + "\n不重新构建；发布前重新验证源资源和工作目录，不部署、不上传、不切换运行版本。", "发布此构建", new ResourceConfirmationSummary
                            {
                                Fields = new[] { ("资源项目", input.Map.ProjectId), ("构建平台", input.Platform), ("资源兼容标识（App）", publicationApp),
                                    ("使用的构建", "本窗口当前选中的构建结果"), ("地址常量", publicationGenerate ? "发布后生成 → " + publicationDirectory : "不生成") },
                                Effects = new[] { "重新验证这次构建后，写入本地发布版本和发布索引。", "不重新构建，不部署、不上传，也不切换正在运行的版本。" },
                                Notice = "发布提交未确认时，请先恢复发布事务，不要直接重复发布。"
                            }, details:
                            new ResourceConfirmationDetails("ProjectId=" + input.Map.ProjectId + "\nProjectGuid=" + input.Map.ProjectGuid + "\nPlatform=" + input.Platform +
                                "\nApp=" + publicationApp + "\nInputFingerprint=" + input.InputFingerprint,
                                "Workspace=" + input.WorkspacePath + "\n地址常量目标=" + (publicationGenerate ? publicationDirectory : "不生成"),
                                "不重新构建；发布前重新验证源资源与 Workspace。\nGenerateReferences=" + publicationGenerate +
                                "\n发布事务未完成时，请使用“恢复本地发布事务”。不部署、不上传、不切换运行版本。"))) return "发布已取消。";
                        return RecordReport(ResourceEditorOperations.PublishBuild(input, publicationApp, publicationGenerate, publicationDirectory), rebind);
                    });
                }
        }

        private static string Identity(ResourceProject project) => project ? project.ProjectId + " / " + AssetDatabase.GetAssetPath(project) + " / GUID=" +
            AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) : "[项目不可用]";

        private ResourceBuildRecipe RequireMatchingRecipe(ResourceProject project, string guid)
        {
            if (string.IsNullOrEmpty(guid) || recipeGuid != guid) throw new InvalidOperationException("构建配方选择已变化，排队请求已失效。请按当前选择重新执行。");
            var current = AssetDatabase.LoadAssetAtPath<ResourceBuildRecipe>(AssetDatabase.GUIDToAssetPath(guid));
            if (!current || current.Project != project) throw new InvalidOperationException("配方中的资源项目与当前工作台不匹配；未自动修改或切换项目。");
            return current;
        }

        private ResourcePreflightResult ValidateInput(ResourceProject project, string app, string chosenRecipe)
        {
            preflight = string.IsNullOrEmpty(chosenRecipe) ? ResourceEditorOperations.InspectPreflight(project, app) :
                ResourceEditorOperations.InspectRecipePreflight(RequireMatchingRecipe(project, chosenRecipe), true);
            if (preflight.Error != null) throw preflight.Error;
            return preflight;
        }

        internal void DrawDelivery(ResourceProject project, ref string app, float width, Action<Func<string>> queue, Action<Func<string>> run) =>
            delivery.Draw(project, ref app, width, queue, run);

        internal bool DrawAttempts(ResourceProject project = null)
        {
            var selected = false;
            var visible = project ? GetAttempts(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project))) : attempts;
            if (visible.Count > 0)
            {
                attemptsExpanded = EditorGUILayout.Foldout(attemptsExpanded, "当前项目执行记录 " + visible.Count + " / 全窗口 " + attempts.Count + "（最多32条，重载后清空）", true);
                if (attemptsExpanded)
                    for (var i = visible.Count - 1; i >= 0; i--)
                    {
                        var attempt = visible[i];
                        var state = attempt.Build == null ? "构建失败" : attempt.PublicationError != null ? "发布未确认" : attempt.Release == null ? "构建完成，未发布" :
                            attempt.GenerationError != null ? "已发布 / 常量失败" : "已发布";
                        if (GUILayout.Button("#" + (i + 1) + " " + ResourceEditorText.Operation(attempt.Operation) + " / " + state + " / " + attempt.StartedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss") +
                            " / " + (attempt.ProjectId ?? attempt.Build?.Map.ProjectId) + " / " + (attempt.Release?.Catalog.CatalogDigest ?? ""), EditorStyles.miniButton))
                        { report = attempt; artifact = 0; selected = true; }
                    }
            }
            return selected;
        }

        internal IReadOnlyList<ResourceEditorBuildReport> GetAttempts(string projectGuid) =>
            attempts.Where(x => (x.Build?.Map.ProjectGuid ?? x.ProjectGuid) == projectGuid).ToArray();

        private string RecordReport(ResourceEditorBuildReport completed, Action<ResourceBuildResult> rebind)
        {
            rebind(completed.Build);
            report = completed; attempts.Add(completed);
            if (attempts.Count > 32) attempts.RemoveAt(0);
            if (completed.Build != null) DefinitionChanged = false;
            delivery.OfferRelease(completed.Release); artifact = 0;
            if (completed.GenerationChanged) AssetDatabase.Refresh();
            return completed.Summary;
        }

        private void DrawReport()
        {
            if (report == null) return;
            ProjectReport();
            var previousColor = GUI.contentColor;
            GUI.contentColor = report.Build == null ? new Color(1, .52f, .48f) : report.PublicationError != null || report.GenerationError != null ? new Color(1, .76f, .35f) : EditorGUIUtility.isProSkin ? new Color(.58f, .85f, .66f) : new Color(.12f, .43f, .24f);
            try { GUILayout.Label(Outcome(report), ResourceWorkbenchGUI.Title); }
            finally { GUI.contentColor = previousColor; }
            GUILayout.Label(report.StartedAtUtc.ToLocalTime().ToString("MM-dd HH:mm:ss") + " · 操作总耗时 " + ResourceWorkbenchGUI.Duration(report.ElapsedMilliseconds), ResourceWorkbenchGUI.Secondary);
            GUILayout.Space(6);
            ResourceWorkbenchGUI.Metric("资源构建", report.Build == null ? "未完成" : "已完成");
            ResourceWorkbenchGUI.Metric("本地发布", report.Release != null ? "已发布" : report.PublicationError != null ? "提交未确认" : "未执行");
            ResourceWorkbenchGUI.Metric("资源地址常量", report.GenerationError != null ? "生成失败" : !report.GenerationRequested ? "未启用" : report.Release == null ? "未执行" : report.GenerationChanged ? "源码已更新，待 Unity 编译" : "源码未变化");
            var failure = report.BuildError ?? report.PublicationError ?? report.GenerationError;
            if (failure != null) EditorGUILayout.HelpBox(report.Build == null ? "本次构建未完成。请展开技术详情查看错误原因。" : report.PublicationError != null ?
                "构建已完成，但发布提交结果尚未确认。请先恢复发布事务，再读取发布索引。" : "发布已完成，但地址常量生成失败。请展开技术详情查看错误原因。", report.Build == null ? MessageType.Error : MessageType.Warning);
            if (report.PublicationError != null) GUILayout.Label("发布索引可能已经变化。请到“版本交付 > 恢复与其他”恢复发布事务，再读取发布索引；不要直接重复发布。", ResourceWorkbenchGUI.Secondary);
            else if (report.Release != null && report.GenerationError != null) GUILayout.Label("发布版本仍有效，无需重发或回滚。处理上方问题后，可在“版本交付 > 恢复与其他”单独生成资源地址常量。", ResourceWorkbenchGUI.Secondary);
            else if (report.Build == null) GUILayout.Label("根据技术详情中的原因修正问题后，重新检查资源。已有发布版本不会因此失效。", ResourceWorkbenchGUI.Secondary);
            if (report.Release != null) ResourceWorkbenchGUI.Metric("版本摘要", report.Release.Catalog.CatalogDigest.Substring(0, 16), report.Release.Catalog.CatalogDigest);
            if (DefinitionChanged || IsHistoricalReport) EditorGUILayout.HelpBox(IsHistoricalReport ? "当前选择的是较早的执行记录，不代表现在的配置。" : "配置或资源已变化。当前显示上次执行结果，请重新检查或构建。", MessageType.Warning);
            evidence = ResourceWorkbenchGUI.Disclosure(evidence, "技术详情、完整参数与产物");
            if (!evidence) return;
            if (report.BuildError != null) GUILayout.Label("构建错误：\n" + ResourceEditorText.FailureDetails(report.BuildError), ResourceWorkbenchGUI.Secondary);
            if (report.PublicationError != null) GUILayout.Label("发布错误：\n" + ResourceEditorText.FailureDetails(report.PublicationError), ResourceWorkbenchGUI.Secondary);
            if (report.GenerationError != null) GUILayout.Label("常量生成错误：\n" + ResourceEditorText.FailureDetails(report.GenerationError), ResourceWorkbenchGUI.Secondary);
            GUILayout.Label(report.Invocation?.ToString() ?? "操作=" + report.Operation + " / Project=" + report.ProjectId + " / GUID=" + report.ProjectGuid +
                "\n平台=" + report.Platform + " / App=" + report.AppContentCompatibilityId + " / force=" + report.ForceRebuild +
                " / 生成=" + report.GenerationRequested + " / 目录=" + report.ReferenceDirectory, ResourceWorkbenchGUI.Secondary);
            if (report.Build == null) return;
            var build = report.Build;
            GUILayout.Label("Manifest 已验证 " + build.Bundles.Count + " 个 Bundle · 原始构建文件 " + build.Files.Count + " 个 · 总字节 " + projectedRawBytes +
                "\n预检 " + build.PreflightMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms · 构建 " + build.BuildMilliseconds.ToString("F2", CultureInfo.InvariantCulture) +
                " ms · 验证 " + build.ValidationMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms\n输入哈希读取 " + build.SourceHashBytes +
                " bytes · 产物哈希读取 " + build.ArtifactHashBytes + " bytes · 发布复制 " + (report.Release == null ? "未执行 / 未确认" : report.Release.CopiedBytes + " bytes"), EditorStyles.wordWrappedLabel);
            GUILayout.Label(projectedCosts, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label("各项计时有重叠，不能相加。ContentEncoding 包含编码、复用和验证，不是纯 AES 耗时；原生内存未测量，不能由 IO 或字节量推算。", ResourceWorkbenchGUI.Secondary);
            GUILayout.Label("交付编码 " + build.ContentEncryption + " · KeyId=" + (build.ContentKeyId ?? "无") + " · 总字节 " + build.DeliveryBytes +
                "\n新编码 " + build.EncodedDeliveryBytes + " bytes · 已验证复用 " + build.ReusedDeliveryBytes + " bytes · 编码/交付验证 " +
                build.ContentEncodingMilliseconds.ToString("F2", CultureInfo.InvariantCulture) + " ms", EditorStyles.wordWrappedMiniLabel);
            if (report.Release != null)
                GUILayout.Label("发布复制 IO " + report.Release.CopyMilliseconds.ToString("F2") + " ms · 新 staging 验证 " + report.Release.StagingValidationMilliseconds.ToString("F2") +
                    " ms · 重复版本=" + report.Release.IsRepeat, EditorStyles.wordWrappedMiniLabel);
            foreach (var reason in build.ExecutionReasons) GUILayout.Label("执行原因（原始记录）：" + reason, EditorStyles.wordWrappedMiniLabel);
            GUILayout.Label(projectedChanges, EditorStyles.wordWrappedMiniLabel);
            if (build.CacheReuseBlockedBy.Count != 0)
                EditorGUILayout.HelpBox(build.CacheReuseBlockedBy.Count + " 项构建回调的输入无法固定，因此本次仍需构建，不能直接复用整个构建结果。即使产物摘要相同也不跳过此检查。", MessageType.Warning);
            EditorGUILayout.SelectableLabel("InputFingerprint=" + build.InputFingerprint + "\nBuildSettingsFingerprint=" + build.BuildSettingsFingerprint,
                EditorStyles.wordWrappedMiniLabel, GUILayout.Height(55));
            if (report.Release != null)
                EditorGUILayout.SelectableLabel("BuildId=" + report.Release.Catalog.BuildId + "\nCatalogDigest=" + report.Release.Catalog.CatalogDigest,
                    EditorStyles.wordWrappedMiniLabel, GUILayout.Height(55));
            foreach (var callback in build.CacheReuseBlockedBy) GUILayout.Label("构建回调：" + callback, EditorStyles.wordWrappedMiniLabel);
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
            var file = rawFiles[item.Name];
            var delivery = deliveryFiles[item.Name];
            var change = bundleChanges[item.Name];
            var text = item.Name + "\nUnityHash=" + item.UnityHash + "\nRaw SHA256=" + file.Sha256 + " / bytes=" + file.Length +
                "\nDelivery SHA256=" + delivery.Sha256 + " / bytes=" + delivery.Length +
                "\nPlacement=" + item.Placement + " / source files=" + item.SourceFileCount + " / source bytes=" + item.SourceBytes +
                "\nManifest dependents=" + string.Join(", ", item.Dependents) +
                "\nPrevious SHA256=" + (change.PreviousSha256 ?? "无兼容基线") + "\n" + change.Reason + "\nManifest dependencies=" + string.Join(", ", item.Dependencies);
            EditorGUILayout.SelectableLabel(text, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(110));
        }

        internal static string ReadinessSummary(ResourcePreflightResult value) => value != null && value.State == ResourcePreflightState.StaticBlocked &&
            string.IsNullOrWhiteSpace(value.AppContentCompatibilityId) && value.Error is ArgumentException error && error.ParamName == "projectId"
            ? "资源兼容标识（App）尚未填写。请先输入应用约定的稳定标识；它不是游戏版本号。原始检查信息可在执行说明中查看。"
            : value?.Error != null ? ResourceEditorText.Preflight(value) : value?.Summary ?? "";

        internal static string Outcome(ResourceEditorBuildReport value) => value.Build == null ? "构建未完成。" :
            value.PublicationError != null ? "构建完成，发布提交未确认。" : value.Release == null ? "构建完成，尚未发布。" :
            value.GenerationError != null ? "资源已发布，地址常量生成失败。" : "资源已构建并本地发布。";

        private void ProjectReport()
        {
            if (ReferenceEquals(projectedReport, report)) return;
            projectedReport = report;
            if (report?.Build == null) return;
            var build = report.Build;
            projectedRawBytes = build.Files.Sum(x => x.Length);
            rawFiles = build.Files.ToDictionary(x => x.Name, StringComparer.Ordinal);
            deliveryFiles = build.DeliveryFiles.ToDictionary(x => x.Name, StringComparer.Ordinal);
            bundleChanges = build.BundleChanges.ToDictionary(x => x.Name, StringComparer.Ordinal);
            projectedCosts = "构建总耗时 " + build.ElapsedMilliseconds.ToString("F2") + " ms · BuildPipeline " + (build.UnityBuildInvoked ? build.BuildPipelineMilliseconds.ToString("F2") + " ms" : "未调用") +
                "\n分包计算（含构建后复核） " + build.MapMilliseconds.ToString("F2") + " ms · 输入指纹（含构建后复核） " + build.InputFingerprintMilliseconds.ToString("F2") +
                " ms · 产物哈希 " + build.ArtifactHashMilliseconds.ToString("F2") + " ms · 原生读回 " + build.ReadbackMilliseconds.ToString("F2") +
                " ms\nBundle 文件字节 " + build.BundleFileBytes + " · 本地发布 " + (report.PublicationMilliseconds?.ToString("F2") ?? "未执行") +
                " ms · 常量生成 " + (report.GenerationMilliseconds?.ToString("F2") ?? "未执行") + " ms";
            projectedChanges = "Bundle 内容对比：变化 " + build.BundleChanges.Count(x => x.ContentChanged == true) + " · 未变 " + build.BundleChanges.Count(x => x.ContentChanged == false) +
                " · 无可比基线 " + build.BundleChanges.Count(x => !x.ContentChanged.HasValue);
        }
    }
}
