using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    [CustomEditor(typeof(ResourceProject))]
    internal sealed class ResourceProjectInspector : UnityEditor.Editor
    {
        private string folderName = "GeneratedAtlases";
        private string generationParentGuid;
        private string message;
        private bool atlases;
        private Exception failure;
        private bool technical;

        public override void OnInspectorGUI()
        {
            var project = (ResourceProject)target;
            serializedObject.Update();
            GUILayout.Label("资源项目身份", EditorStyles.boldLabel);
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("DisplayName"), new GUIContent("显示名称"));
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("ProjectId"), new GUIContent("ProjectId"));
            GUILayout.Label("ProjectId 是运行时地址与输出归属，不是显示名称；修改不会迁移旧发布或缓存。", ResourceWorkbenchGUI.Secondary);
            GUILayout.Space(10);
            var rootError = ResourceWorkbenchGUI.ResourceDirectoriesFields(serializedObject);
            if (rootError != null) message = rootError;
            GUILayout.Label("Group用于组织资源并设置分包方式。同一个Group可以包含不同资源目录中的文件。", ResourceWorkbenchGUI.Secondary);
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("Groups"), new GUIContent("Group 与资源选择"));
            GUILayout.Space(10); GUILayout.Label("构建策略", EditorStyles.boldLabel);
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("Compression"), new GUIContent("Bundle 压缩"));
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("ContentEncryption"), new GUIContent("内容加密"));
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("ContentKeyId"), new GUIContent("KeyId（非密钥）"));
            GUILayout.Label("None 表示不加密，不影响压缩。加密密钥由外部显式提供，不保存在工程；轮换密钥需使用新的 KeyId。", ResourceWorkbenchGUI.Secondary);
            ResourceWorkbenchGUI.Property(serializedObject.FindProperty("AutomaticAtlases"), new GUIContent("自动图集配置"));
            serializedObject.ApplyModifiedProperties();
            ResourceWorkbenchGUI.DirectoryValidation(project);
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                if (GUILayout.Button("保存资源项目", ResourceWorkbenchGUI.Button))
                    try { ResourceEditorOperations.SaveProject(project); message = null; failure = null; }
                    catch (Exception error) { failure = error; message = "资源项目未能保存。请检查资产是否可编辑，再重试；此操作不会保存Unity场景。"; }
            atlases = ResourceWorkbenchGUI.Disclosure(atlases, "自动图集（可选）");
            if (atlases && project.AutomaticAtlases)
            {
                ResourceWorkbenchGUI.CopyLine("生成目录", ResourceProjectPaths.GeneratedDirectory(project), EditorGUIUtility.currentViewWidth - 32);
                EditorGUILayout.HelpBox("此目录保存自动生成的SpriteAtlas及其规则配置。构建会先生成并准备图集，再将输出加入指定Group；不会把整个生成目录当作普通来源收集。", MessageType.None);
                if (GUILayout.Button("编辑自动图集规则")) Selection.activeObject = project.AutomaticAtlases;
            }
            else if (atlases)
            {
                EditorGUILayout.HelpBox("不使用自动图集时，可以暂不设置生成目录。需要时先保存资源项目，再明确选择Assets中的已有父目录并新建子目录；不会接管父目录中的其他文件或已有同名目录。建议放在资源目录之外。", MessageType.None);
                var parent = AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetDatabase.GUIDToAssetPath(generationParentGuid));
                parent = ResourceWorkbenchGUI.ObjectField("生成目录的父目录", parent);
                generationParentGuid = parent ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(parent)) : "";
                if (parent) ResourceWorkbenchGUI.CopyLine("父目录路径", AssetDatabase.GetAssetPath(parent), EditorGUIUtility.currentViewWidth - 32);
                folderName = ResourceWorkbenchGUI.TextField("新输出子目录", folderName);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
                    if (GUILayout.Button("新建并登记生成目录"))
                        try { Selection.activeObject = ResourceAtlasGenerator.CreateConfiguration(project, AssetDatabase.GetAssetPath(parent), folderName); message = null; failure = null; }
                        catch (Exception error) { failure = error; message = "生成目录未能登记。请先保存资源项目，选择有效父目录并使用尚不存在的子目录名称。未确认属于本次操作的文件不会被覆盖或删除。"; }
            }
            if (message != null) EditorGUILayout.HelpBox(message, MessageType.Error);
            ResourceWorkbenchGUI.FailureDetails(failure, ref technical);
        }
    }

    [CustomEditor(typeof(ResourceAtlasConfiguration))]
    internal sealed class ResourceAtlasConfigurationEditor : UnityEditor.Editor
    {
        private ResourceAtlasPlan plan;
        private string message;
        private MessageType messageType;
        private Exception failure;
        private bool technical;

        public override void OnInspectorGUI()
        {
            var configuration = (ResourceAtlasConfiguration)target;
            var project = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(configuration.ProjectGuid));
            using (new EditorGUI.DisabledScope(true)) ResourceWorkbenchGUI.ObjectField("所属资源项目", project);
            EditorGUILayout.HelpBox("从已登记资源目录中选择Sprite纹理或文件夹，不能包含生成目录。规则ID用于保持输出身份；可以调整地址和设置，一条规则也可以生成多个图集。", MessageType.None);
            if (ResourceAtlasFields.Draw(serializedObject)) { plan = null; message = null; }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存规则", ResourceWorkbenchGUI.Button)) Run(() => { ResourceAtlasFields.Save(project, configuration); return "规则已保存；未生成或准备图集。"; });
                if (GUILayout.Button("预览生成计划", ResourceWorkbenchGUI.Button)) Run(() => { plan = ResourceAtlasGenerator.Preview(project); return "图集计划已计算，尚未生成或准备。下方显示各输出的状态和位置。"; });
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("生成 / 更新并准备图集")) Run(() =>
                {
                    ResourceAtlasGenerator.Generate(project);
                    plan = ResourceAtlasGenerator.Preview(project);
                    return "图集已生成并准备。此操作没有构建或发布Bundle；正式构建会继续检查资源依赖和产物。";
                });
                if (plan != null)
                    foreach (var change in plan.Changes)
                    {
                        var status = change.Kind switch { ResourceAtlasChangeKind.Create => "待创建", ResourceAtlasChangeKind.Update => "待更新", ResourceAtlasChangeKind.Current => "当前有效", _ => "已失效，不进入构建" };
                        EditorGUILayout.LabelField(status + " / " + change.Address, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.SelectableLabel(change.Path, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        if (change.Kind == ResourceAtlasChangeKind.Obsolete && GUILayout.Button("检查引用并删除失效输出：" + change.Key) &&
                            EditorUtility.DisplayDialog("删除已失效的自动图集？", change.Path + "\n只删除这个已登记输出，不删除源图片。执行前会重新检查文件记录和引用；仍被使用或已被外部修改时不会删除。", "检查并删除", "取消"))
                        {
                            Run(() => { ResourceAtlasGenerator.DeleteObsolete(project, change.Key); plan = null; return "已删除确认无引用的自有失效输出；源 Sprite/Texture 未修改。"; });
                            GUIUtility.ExitGUI();
                        }
                    }
            }
            if (message != null) EditorGUILayout.HelpBox(message, messageType);
            ResourceWorkbenchGUI.FailureDetails(failure, ref technical);
        }

        private void Run(Func<string> action)
        {
            try { message = action(); failure = null; messageType = MessageType.Info; }
            catch (Exception error) { plan = null; failure = error; message = "图集操作未完成。请查看原始错误，检查来源、规则及已登记输出后重试；不要手工覆盖未知文件。"; messageType = MessageType.Error; }
        }
    }
}
