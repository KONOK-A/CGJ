using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Resource
{
    /// <summary>正式 Resource 工作台。保留原窗口脚本 GUID，不再提供旧标记/ResData 构建或 Runtime 模式开关。</summary>
    public class ResServiceEditorWindow : EditorWindow
    {
        [SerializeField] private ResourceProject project;
        [SerializeField] private string app = "app-v1";
        [SerializeField] private bool generate;
        [SerializeField] private bool force;
        private ResServiceView _resServiceView;
        private Vector2 scroll;
        private string selectionError;

        [MenuItem("YLFramework/Resource/Workbench %#r")]
        public static void OpenResServiceWindow()
        {
            var window = GetWindow<ResServiceEditorWindow>();
            window.titleContent = new GUIContent("YL Resource"); window.minSize = new Vector2(620, 480); window.Show();
            if (Selection.activeObject is ResourceProject selected) window.SetProject(selected);
        }

        public void SetProject(ResourceProject value)
        {
            if (value) ResourceEditorOperations.RequireEditableProject(value);
            _resServiceView?.SetProject(value); project = value; selectionError = null; Repaint();
        }

        private void OnEnable()
        {
            _resServiceView = new ResServiceView { EditorWindow = this };
            try { _resServiceView.SetProject(project); }
            catch (Exception error) { project = null; selectionError = error.Message; }
            EditorApplication.projectChanged += RefreshView;
            Undo.undoRedoPerformed += RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged += RefreshView;
            Selection.selectionChanged += Repaint;
        }
        private void RefreshView() { _resServiceView?.Invalidate(); Repaint(); }
        public void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshView; Undo.undoRedoPerformed -= RefreshView;
            EditorUserBuildSettings.activeBuildTargetChanged -= RefreshView; Selection.selectionChanged -= Repaint;
            _resServiceView?.OnDispose(); _resServiceView = null;
        }
        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var next = (ResourceProject)EditorGUILayout.ObjectField(project, typeof(ResourceProject), false);
            if (next != project) TrySelect(next);
            if (GUILayout.Button("项目列表", EditorStyles.toolbarDropDown, GUILayout.Width(90)))
            {
                var menu = new GenericMenu();
                foreach (var guid in AssetDatabase.FindAssets("t:ResourceProject"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
                    menu.AddItem(new GUIContent(asset.ProjectId + " / " + AssetDatabase.GetAssetPath(asset)), asset == project, () => TrySelect(asset));
                }
                menu.ShowAsContext();
            }
            if (GUILayout.Button("新建 Project", EditorStyles.toolbarButton, GUILayout.Width(110)))
            {
                var path = EditorUtility.SaveFilePanelInProject("创建自有 ResourceProject", "ResourceProject", "asset", "请选择明确属于新 YL 的配置位置；不要放入常量专用生成目录。");
                if (path.Length != 0)
                {
                    try { SetProject(ResourceEditorOperations.CreateProject(path)); }
                    catch (Exception error) { selectionError = error.Message; }
                }
            }
            EditorGUILayout.EndHorizontal();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (!string.IsNullOrEmpty(selectionError)) EditorGUILayout.HelpBox(selectionError, MessageType.Error);
            app = EditorGUILayout.TextField("App Content Compatibility", app);
            generate = EditorGUILayout.Toggle("发布后生成常量", generate);
            force = EditorGUILayout.Toggle("强制重建", force);
            _resServiceView.OnGUI(app, generate, force);
            EditorGUILayout.EndScrollView();
        }
        private void TrySelect(ResourceProject value)
        {
            try { SetProject(value); } catch (Exception error) { selectionError = error.Message; Repaint(); }
        }
    }

    /// <summary>仅持有窗口编辑与诊断快照，不成为地址、构建或发布的第二个权威。</summary>
    public sealed class ResServiceView
    {
        public EditorWindow EditorWindow { get; set; }
        private ResourceProject project;
        private SerializedObject serialized;
        private ResourceCatalog catalog;
        private ResourceEditorSimulation simulation;
        private string status;
        private string filter = "";
        private Object preview;
        private EditorApplication.CallbackFunction pending;
        public void SetProject(ResourceProject value)
        {
            if (value) ResourceEditorOperations.RequireEditableProject(value);
            CancelPending();
            serialized?.Dispose(); project = value;
            serialized = project ? new SerializedObject(project) : null;
            Invalidate(); status = null;
        }
        public void Invalidate() { catalog = null; simulation = null; preview = null; }
        public void RefreshPreview()
        {
            simulation = new ResourceEditorSimulation(new[] { project }); catalog = simulation.GetCatalog(project.ProjectId);
        }
        public void OnGUI(string app, bool generate, bool force)
        {
            if (!project) { EditorGUILayout.HelpBox("选择或创建自有 ResourceProject。工具不接管旧 Importer 标记、XFAB 项目或历史输出。", MessageType.Info); return; }
            try { ResourceEditorOperations.RequireEditableProject(project); }
            catch (Exception error) { CancelPending(); EditorGUILayout.HelpBox(error.Message, MessageType.Error); return; }
            var disabled = pending != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;
            using (new EditorGUI.DisabledScope(disabled || !AssetDatabase.IsOpenForEdit(project)))
            {
                DrawDefinition();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存定义")) RunOperation(() => { ResourceEditorOperations.SaveProject(project); return "Project 定义已保存。"; });
                if (GUILayout.Button("刷新模拟目录")) RunOperation(() => { RefreshPreview(); return "正式 Project 目录预览已更新。"; });
                if (GUILayout.Button("构建预检")) QueueOperation(() => ResourceEditorOperations.Preflight(project, app));
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("构建并发布")) QueueOperation(() =>
                {
                    var summary = ResourceEditorOperations.Preflight(project, app);
                    if (!EditorUtility.DisplayDialog("确认构建/本地发布", summary, "构建并发布", "取消")) return "操作已取消，未创建输出。";
                    var report = ResourceEditorOperations.BuildAndPublish(project, app, generate, forceRebuild: force);
                    // native build 可能卸载窗口引用的 Project SO；以既有 GUID 重新取得，而不是根据名称猜。
                    if (report.Build != null)
                    {
                        var current = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(report.Build.Map.ProjectGuid));
                        if (EditorWindow is ResServiceEditorWindow window) window.SetProject(current); else SetProject(current);
                    }
                    if (report.GenerationChanged) AssetDatabase.Refresh();
                    return report.Summary;
                });
                if (GUILayout.Button("恢复/读取当前发布")) QueueOperation(() =>
                {
                    var release = ResourcePublisher.Recover(project, EditorUserBuildSettings.activeBuildTarget, app);
                    return release == null ? "该 Project/平台尚无 owned 发布。" : "已验证发布：" + release.DirectoryPath;
                });
                if (GUILayout.Button("从发布集合生成常量")) QueueOperation(() =>
                {
                    var catalogs = ResourceEditorOperations.PublishedCatalogs(app);
                    if (!EditorUtility.DisplayDialog("确认常量输入集合", "当前平台/App 的全部 owned 已发布 Project：\n" + string.Join("\n", catalogs.Select(x => x.ProjectId)) +
                        "\n\n覆盖本工具已有常量集合，保留 .meta。程序集：YLFramework.Generated.ResourceNames。", "生成", "取消")) return "常量生成已取消。";
                    var changed = ResourceCodeGenerator.GenerateReferences(catalogs);
                    if (changed) AssetDatabase.Refresh();
                    return changed ? "Assets/YLFrameworkData/YLResourceReferences.cs 已更新；业务 asmdef 引用 YLFramework.Generated.ResourceNames，编译结果独立确认。" : "常量字节未变化。";
                });
                EditorGUILayout.EndHorizontal();
            }
            if (pending != null && GUILayout.Button("取消排队操作")) CancelPending();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.None);
            EditorGUILayout.HelpBox("平台：" + EditorUserBuildSettings.activeBuildTarget + "。常量共享一个程序集，输入为同平台/App 全部 owned 已发布 Project；有不兼容发布时明确拒绝。预览仅借用 AssetDatabase 对象，不执行场景加载或 Runtime 激活。构建/发布不支持 Undo，生成失败不会撤销已发布版本。", MessageType.Info);
            DrawCatalog();
        }

        private void DrawDefinition()
        {
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("ProjectId")); EditorGUILayout.PropertyField(serialized.FindProperty("DisplayName"));
            var root = serialized.FindProperty("AssetRootGuid");
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(AssetDatabase.GUIDToAssetPath(root.stringValue));
            var selected = (DefaultAsset)EditorGUILayout.ObjectField("资源根", folder, typeof(DefaultAsset), false);
            if (selected != folder)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (selected && (!AssetDatabase.IsValidFolder(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))) status = "资源根必须是 Assets 下的专用文件夹。";
                else root.stringValue = AssetDatabase.AssetPathToGUID(path);
            }
            var groups = serialized.FindProperty("Groups");
            for (var i = 0; i < groups.arraySize; i++)
            {
                var group = groups.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("box"); EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(group.FindPropertyRelative("Id"), new GUIContent("Group"));
                if (GUILayout.Button("删除 Group", GUILayout.Width(100))) { groups.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
                EditorGUILayout.EndHorizontal(); EditorGUILayout.PropertyField(group.FindPropertyRelative("Packing"));
                var selections = group.FindPropertyRelative("Selections");
                for (var s = 0; s < selections.arraySize; s++)
                {
                    var selection = selections.GetArrayElementAtIndex(s);
                    var guid = selection.FindPropertyRelative("Guid");
                    EditorGUILayout.BeginHorizontal();
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid.stringValue));
                    var next = EditorGUILayout.ObjectField(asset, typeof(Object), false);
                    if (next != asset) guid.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(next));
                    if (GUILayout.Button("移除", GUILayout.Width(55))) { selections.DeleteArrayElementAtIndex(s); EditorGUILayout.EndHorizontal(); break; }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.PropertyField(selection.FindPropertyRelative("Address"), new GUIContent("地址覆写"));
                    EditorGUILayout.PropertyField(selection.FindPropertyRelative("Aliases"), true);
                    EditorGUILayout.PropertyField(selection.FindPropertyRelative("Extensions"), true);
                }
                if (GUILayout.Button("加入 Project 窗口选择"))
                {
                    serialized.ApplyModifiedProperties();
                    try { ResourceEditorOperations.AddSelections(project, i, Selection.objects); }
                    catch (Exception error) { status = error.Message; }
                    serialized.Update(); Invalidate();
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("新增 Group"))
            {
                var index = groups.arraySize++; var item = groups.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("Id").stringValue = "group-" + (index + 1); item.FindPropertyRelative("Packing").enumValueIndex = 0;
                item.FindPropertyRelative("Selections").ClearArray();
            }
            if (serialized.ApplyModifiedProperties()) Invalidate();
        }

        private void DrawCatalog()
        {
            if (catalog == null) return;
            filter = EditorGUILayout.TextField("筛选地址 / 类型", filter);
            foreach (var entry in catalog.Entries.Where(x => x.Address.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || x.Location.AssetType.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(entry.Address.Path + (entry.Address.SubAsset == null ? "" : "#" + entry.Address.SubAsset) + " (" + entry.Location.AssetType.Name + ")");
                if (GUILayout.Button("选择", GUILayout.Width(50))) RunOperation(() =>
                {
                    preview = simulation.Load(entry.Address, entry.Location.AssetType); Selection.activeObject = preview;
                    return "取得 " + entry.AssetId + "；alias 数=" + entry.Aliases.Count + "。此对象属于 AssetDatabase。";
                });
                EditorGUILayout.EndHorizontal();
            }
            if (preview)
            {
                var texture = AssetPreview.GetAssetPreview(preview) ?? AssetPreview.GetMiniThumbnail(preview);
                if (texture) GUILayout.Label(texture, GUILayout.Width(96), GUILayout.Height(96));
            }
        }
        private void RunOperation(Func<string> operation)
        {
            try { status = operation(); } catch (Exception error) { status = "操作未完成：" + error; }
            if (EditorWindow) EditorWindow.Repaint();
        }
        private void QueueOperation(Func<string> operation)
        {
            if (pending != null) return;
            pending = () => { pending = null; RunOperation(operation); };
            EditorApplication.delayCall += pending;
        }
        private void CancelPending() { if (pending != null) EditorApplication.delayCall -= pending; pending = null; }
        public void OnDispose() { CancelPending(); serialized?.Dispose(); serialized = null; Invalidate(); }
    }
}
