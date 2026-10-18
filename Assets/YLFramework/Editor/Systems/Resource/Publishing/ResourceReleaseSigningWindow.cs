using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceReleaseSigningWindow : EditorWindow
    {
        [SerializeField] private ResourceProject project;
        [SerializeField] private string app;
        [SerializeField] private string channel = "";
        [SerializeField] private string keyId = "";
        [SerializeField] private long sequence = 1;
        [SerializeField] private int validDays = 7;
        [SerializeField] private bool selectedMode;
        [SerializeField] private ResourceReleaseIdentity selected;
        [SerializeField] private string outputPath = "";
        private string status;
        private bool statusError;
        private Exception failure;
        private string failureStage;
        private Vector2 scroll;
        private bool technical;

        internal static void Open(ResourceProject project, string app)
        {
            var window = GetWindow<ResourceReleaseSigningWindow>(true, "签发发布元数据");
            window.project = project; window.app = app; window.selectedMode = false; window.selected = null;
            window.ResetPresentation();
        }

        internal static void OpenSelected(ResourceProject project, ResourceRelease release)
        {
            var window = GetWindow<ResourceReleaseSigningWindow>(true, "签发选定版本");
            window.SelectRelease(project, release); window.ResetPresentation();
        }

        internal void SelectRelease(ResourceProject owner, ResourceRelease release)
        {
            var identity = new ResourceReleaseIdentity(release);
            if (!owner || owner.ProjectId != identity.ProjectId || AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(owner)) != identity.ProjectGuid)
                throw new InvalidOperationException("所选版本的项目标识或配置 GUID 与当前资源项目不匹配。");
            project = owner; app = identity.App; selected = identity; selectedMode = true;
        }

        private void ResetPresentation()
        { channelSupportsContentEncryption = false; status = null; statusError = false; failure = null; failureStage = null; scroll = Vector2.zero; minSize = new Vector2(620, 440); Repaint(); }

        private ResourceRelease ResolveRelease()
        {
            if (selectedMode) return (selected ?? throw new InvalidOperationException("所选版本记录已丢失，请重新选择版本；不会改用发布索引中的版本。")).Resolve();
            return ResourcePublisher.ReadCurrent(project, EditorUserBuildSettings.activeBuildTarget, app) ?? throw new InvalidOperationException("当前平台和 App 尚无已登记的当前发布版本。");
        }

        private bool channelSupportsContentEncryption;

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Label(selectedMode ? "签发所选版本" : "签发发布索引当前版本", ResourceWorkbenchGUI.Title);
            using (new EditorGUI.DisabledScope(true)) ResourceWorkbenchGUI.ObjectField("资源项目", project);
            ResourceWorkbenchGUI.Metric("资源兼容标识（App）", app);
            if (selectedMode && selected == null) EditorGUILayout.HelpBox("所选版本记录不可用，请关闭并重新选择版本；不会改读发布索引。", MessageType.Error);
            else if (selectedMode) ResourceWorkbenchGUI.Metric("所选版本摘要", selected.CatalogDigest.Substring(0, 16), selected.CatalogDigest);
            else GUILayout.Label("执行时读取当前平台与 App 对应的发布索引，不使用历史列表中所选版本。", ResourceWorkbenchGUI.Secondary);
            GUILayout.Space(10);
            channel = ResourceWorkbenchGUI.TextField("发布渠道", channel, false, "Channel：由应用明确约定的更新渠道。");
            keyId = ResourceWorkbenchGUI.TextField("签名 KeyId", keyId, false, "用于选择应用信任的签名公钥，不是内容加密 KeyId 或私钥。");
            sequence = EditorGUI.LongField(EditorGUILayout.GetControlRect(true, ResourceWorkbenchGUI.FieldHeight), new GUIContent("发布序号", "Release Sequence：由发布方明确填写，须满足应用的递增策略。"), sequence, ResourceWorkbenchGUI.Field);
            validDays = EditorGUI.IntField(EditorGUILayout.GetControlRect(true, ResourceWorkbenchGUI.FieldHeight), "有效天数 (1-31)", validDays, ResourceWorkbenchGUI.Field);
            channelSupportsContentEncryption = ResourceWorkbenchGUI.Toggle("渠道支持内容加密", channelSupportsContentEncryption, "确认该渠道仅供支持内容加密的客户端使用，且更新地址与旧客户端隔离。");
            GUILayout.Label("签发加密版本前，须确认渠道只供支持加密的客户端使用，并与旧客户端的更新地址隔离。签发只新增元数据文件，不上传或激活资源。", ResourceWorkbenchGUI.Secondary);
            outputPath = ResourceWorkbenchGUI.TextField("元数据输出路径", outputPath, true, "填写新 JSON 文件的绝对路径；不覆盖已有文件。");
            if (GUILayout.Button("选择新增本地 JSON 路径...", ResourceWorkbenchGUI.Button))
            {
                var picked = EditorUtility.SaveFilePanel("新增签名发布元数据（不覆盖已有文件）", "", "release", "json");
                if (!string.IsNullOrEmpty(picked)) outputPath = picked;
            }
            using (new EditorGUI.DisabledScope(!project || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("验证并签发资源发布元数据...", ResourceWorkbenchGUI.Button))
                {
                    status = null; statusError = false; failure = null; failureStage = "验证签发参数与发布版本";
                    try
                    {
                        if (validDays < 1 || validDays > 31) throw new InvalidOperationException("有效天数必须为1-31。");
                        var release = ResolveRelease();
                        var path = outputPath;
                        if (string.IsNullOrEmpty(path)) path = EditorUtility.SaveFilePanel("新增签名发布元数据（不覆盖已有文件）", "", "release", "json");
                        if (string.IsNullOrEmpty(path)) { status = "已取消选择输出路径，未签发或导出。"; return; }
                        var expires = DateTimeOffset.UtcNow.AddDays(validDays);
                        if (!ResourceOperationConfirmation.Confirm("确认签发此发布版本", new ResourceReleaseIdentity(release) + "\nChannel=" + channel + " / Sequence=" + sequence +
                            " / Signing KeyId=" + keyId + "\nExpiresUtc=" + expires.ToString("u") + "\n新增元数据=" + path +
                            "\n已确认渠道加密能力与旧客户端更新地址隔离=" + channelSupportsContentEncryption + "\n读取工程外配置的 PFX，签发并新增元数据文件；不覆盖、不构建、不上传、不切换运行版本。", "签发并导出", new ResourceConfirmationSummary
                            {
                                Fields = new[] { ("资源项目", release.Catalog.ProjectId), ("平台", release.Catalog.Platform), ("所选版本", release.Catalog.CatalogDigest.Substring(0, 16)),
                                    ("发布渠道", channel), ("发布序号", sequence.ToString()), ("签名 KeyId", keyId), ("有效期（UTC）", expires.ToString("u")), ("输出", path) },
                                Effects = new[] { "验证该发布版本，再从明确的工程外凭据创建签名元数据。", "只新增输出文件，不覆盖已有文件、不构建、不上传、不改变运行版本。" },
                                Notice = "签发不会替应用安装信任公钥。请核对渠道、发布序号及有效期。",
                                Details = new ResourceConfirmationDetails(new ResourceReleaseIdentity(release) + "\nChannel=" + channel + "\nSequence=" + sequence +
                                    "\nSigning KeyId=" + keyId + "\nExpiresUtc=" + expires.ToString("u"),
                                    "只读发布来源=" + release.DirectoryPath + "\n新增元数据=" + path,
                                    "已确认渠道加密能力与旧客户端更新地址隔离=" + channelSupportsContentEncryption +
                                    "\n执行前重新验证本次版本。只读取工程外配置的 PFX 并新增导出；不显示凭据、不覆盖、不构建、不上传、不切换运行版本。失败后不会自动重试或恢复。")
                            })) { status = "签发已取消，未读取 PFX、签名或导出。"; return; }
                        failureStage = "重新验证本次发布版本";
                        release = selectedMode ? selected.Resolve() : ResourcePublisher.ReadVersion(project, EditorUserBuildSettings.activeBuildTarget, app, release.Catalog.CatalogDigest);
                        failureStage = "读取签发凭据并创建签名";
                        var bytes = ResourceReleaseSigner.CreateFromEnvironment(release, channel, sequence, keyId, expires, channelSupportsContentEncryption);
                        failureStage = "导出签名元数据";
                        ResourceReleaseSigner.Export(path, bytes);
                        outputPath = path; statusError = false;
                        status = "签名元数据已导出：" + path + "\n生产部署时，将此文件放到更新地址下的 release.json，并将完整版本目录按 CatalogDigest 放在同一更新地址下：\n" + release.DirectoryPath +
                            "\n发布序号必须递增。构建、本地发布和生产部署是独立步骤。";
                    }
                    catch (Exception error)
                    {
                        // 凭据提供者的异常可能包含敏感细节；UI不回显证书、环境变量或私钥内容。
                        failure = error;
                        status = "签发或导出未完成，失败步骤：" + failureStage + "。请检查发布版本、渠道、KeyId、序号、有效期、工程外 PFX 和密码，以及新文件输出路径。输出位置可能保留部分文件，请检查后再重试；未执行上传或激活。";
                        statusError = true;
                    }
                }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusError ? MessageType.Error : MessageType.Info);
            technical = ResourceWorkbenchGUI.Disclosure(technical, "技术详情：版本与凭据配置");
            if (technical)
            {
                if (selectedMode) ResourceWorkbenchGUI.CopyLine("所选版本完整身份", selected?.ToString() ?? "不可用", position.width - 28);
                if (failure != null) GUILayout.Label("本次失败步骤：" + failureStage + "\n异常类型：" + failure.GetType().FullName + "\n原始异常保留在当前窗口中。异常可能含凭据或环境信息，界面和日志不输出其消息或调用信息。", ResourceWorkbenchGUI.Secondary);
                GUILayout.Label("执行时重新验证版本。PFX 路径由 YL_RESOURCE_SIGNING_PFX 提供，密码由 YL_RESOURCE_SIGNING_PASSWORD 提供；凭据不得位于仓库、资源目录或日志，公钥由应用另行固定。", ResourceWorkbenchGUI.Secondary);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
