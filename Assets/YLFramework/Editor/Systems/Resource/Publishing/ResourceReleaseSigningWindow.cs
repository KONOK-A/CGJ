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
        private string status;

        internal static void Open(ResourceProject project, string app)
        {
            var window = GetWindow<ResourceReleaseSigningWindow>(true, "签名发布元数据");
            window.project = project; window.app = app; window.channelSupportsContentEncryption = false; window.minSize = new Vector2(460, 300);
        }

        private bool channelSupportsContentEncryption;

        private void OnGUI()
        {
            EditorGUILayout.ObjectField("已发布项目", project, typeof(ResourceProject), false);
            EditorGUILayout.LabelField("App", app ?? "");
            channel = EditorGUILayout.TextField("Channel", channel);
            keyId = EditorGUILayout.TextField("Signing KeyId", keyId);
            sequence = EditorGUILayout.LongField("Release Sequence", sequence);
            validDays = EditorGUILayout.IntField("有效天数 (1-31)", validDays);
            channelSupportsContentEncryption = EditorGUILayout.ToggleLeft("已确认此渠道仅供支持内容加密的客户端，且与旧客户端 endpoint 隔离", channelSupportsContentEncryption);
            EditorGUILayout.HelpBox("只签名当前已发布且重新验证的制品，不构建、不上传、不激活。外部PFX路径由 YL_RESOURCE_SIGNING_PFX 提供，密码由 YL_RESOURCE_SIGNING_PASSWORD 提供；私钥不得位于仓库。公钥必须由应用另行固定。", MessageType.Info);
            using (new EditorGUI.DisabledScope(!project || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("验证制品并导出签名元数据"))
                {
                    try
                    {
                        if (validDays < 1 || validDays > 31) throw new InvalidOperationException("有效天数必须为1-31。");
                        var release = ResourcePublisher.ReadCurrent(project, EditorUserBuildSettings.activeBuildTarget, app) ?? throw new InvalidOperationException("当前平台/App尚未发布。");
                        var path = EditorUtility.SaveFilePanel("新增签名发布元数据（不覆盖已有文件）", "", "release", "json");
                        if (string.IsNullOrEmpty(path)) return;
                        var bytes = ResourceReleaseSigner.CreateFromEnvironment(release, channel, sequence, keyId, DateTimeOffset.UtcNow.AddDays(validDays), channelSupportsContentEncryption);
                        ResourceReleaseSigner.Export(path, bytes);
                        status = "已导出：" + path + "\n将元数据部署为endpoint/release.json；将完整版本目录按CatalogDigest部署在同一endpoint下：\n" + release.DirectoryPath +
                            "\n发布序号必须递增。构建、本地发布和生产部署是独立步骤。";
                    }
                    catch (Exception)
                    {
                        // 凭据提供者的异常可能包含敏感细节；UI不回显证书、环境变量或私钥内容。
                        status = "签名/导出失败。检查项目发布、Channel/KeyId/序号/有效期、工程外PFX与密码，以及新增输出路径。未执行上传或激活。";
                    }
                }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
        }
    }
}
