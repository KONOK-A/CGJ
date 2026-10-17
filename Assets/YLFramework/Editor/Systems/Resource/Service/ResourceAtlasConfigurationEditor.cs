using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    [CustomEditor(typeof(ResourceProject))]
    internal sealed class ResourceProjectInspector : UnityEditor.Editor
    {
        private string folderName = "GeneratedAtlases";
        private string message;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("ContentEncryption=None 保留原始交付。加密覆盖所有 Bundle 与二进制 root manifest，KeyId 不是密钥；工作台仅从显式进程 secret YL_RESOURCE_CONTENT_KEY 读取 Base64 32B（或由CI传入provider）。密钥不写入资源、EditorPrefs或日志；轮换必须使用新KeyId。加密发布须签发到与旧客户端隔离的支持渠道。", MessageType.Info);
            var project = (ResourceProject)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("自动 SpriteAtlas", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("保存 Project 定义"))
                    try { ResourceEditorOperations.SaveProject(project); message = null; }
                    catch (Exception error) { message = error.Message; }
            if (project.AutomaticAtlases)
            {
                EditorGUILayout.HelpBox("在独立配置中编辑来源与规则。正式 Build 自动生成/更新并准备，输出直接进入规则指定的 Group。", MessageType.Info);
                if (GUILayout.Button("编辑自动图集规则")) Selection.activeObject = project.AutomaticAtlases;
            }
            else
            {
                EditorGUILayout.HelpBox("先保存 Project 并配置专用资源根。登记只会新建不存在的直接子目录，不接管已有目录/meta。", MessageType.Info);
                folderName = EditorGUILayout.TextField("新自有子目录", folderName);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                    if (GUILayout.Button("登记自动图集范围"))
                        try { Selection.activeObject = ResourceAtlasGenerator.CreateConfiguration(project, folderName); message = null; }
                        catch (Exception error) { message = error.Message; }
            }
            if (message != null) EditorGUILayout.HelpBox(message, MessageType.Error);
        }
    }

    [CustomEditor(typeof(ResourceAtlasConfiguration))]
    internal sealed class ResourceAtlasConfigurationEditor : UnityEditor.Editor
    {
        private ResourceAtlasPlan plan;
        private string message;
        private MessageType messageType;

        public override void OnInspectorGUI()
        {
            var configuration = (ResourceAtlasConfiguration)target;
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(configuration.ProjectGuid));
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("所属 Project", project, typeof(ResourceProject), false);
            EditorGUILayout.HelpBox("Sources 选择目录或 Sprite 类型 Texture2D；禁止包含输出范围。Id 是稳定身份，地址和设置可更新；PerFolder 按源文件夹 GUID 保持身份。Include in Build 始终关闭。", MessageType.Info);
            if (DrawDefaultInspector()) { plan = null; message = null; }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存规则")) Run(() => { AssetDatabase.SaveAssetIfDirty(configuration); return "规则已保存；未生成或准备图集。"; });
                if (GUILayout.Button("只读预览")) Run(() => { plan = ResourceAtlasGenerator.Preview(project); return plan.ToString(); });
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("生成 / 更新并准备图集")) Run(() =>
                {
                    var executed = ResourceAtlasGenerator.Generate(project);
                    plan = ResourceAtlasGenerator.Preview(project);
                    return "生成与准备成功；正式 Build 仍执行依赖/输入/产物验证。\n" + executed;
                });
                if (plan != null)
                    foreach (var change in plan.Changes)
                    {
                        EditorGUILayout.LabelField(change.Kind + " / " + change.Address, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.SelectableLabel(change.Path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (change.Kind == ResourceAtlasChangeKind.Obsolete && GUILayout.Button("检查引用并删除失效输出：" + change.Key) &&
                            EditorUtility.DisplayDialog("删除自有失效 Atlas？", change.Path + "\n将重新检查归属与当前引用；仍被使用或已被外部修改时拒绝。", "检查并删除", "取消"))
                        {
                            Run(() => { ResourceAtlasGenerator.DeleteObsolete(project, change.Key); plan = null; return "已删除确认无引用的自有失效输出；源 Sprite/Texture 未修改。"; });
                            GUIUtility.ExitGUI();
                        }
                    }
            }
            if (message != null) EditorGUILayout.HelpBox(message, messageType);
        }

        private void Run(Func<string> action)
        {
            try { message = action(); messageType = MessageType.Info; }
            catch (Exception error) { plan = null; message = error.Message; messageType = MessageType.Error; }
        }
    }
}
