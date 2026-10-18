using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    [Serializable]
    internal sealed class ResourceWorkbenchRowState
    {
        public string ProjectGuid;
        public string Group;
        public string Guid;
        public string Definition;
        public string Address;
    }

    [Serializable]
    internal sealed class ResourceWorkbenchLayout
    {
        public float GroupWidth = 180;
        public float DetailWidth = 340;
        public bool ShowGroups = true;
        public bool ShowDetails = true;
        public bool CompactDetails;
        public bool Settings;
        public bool Output;
        public float OutputHeight;
        public int Task;
        public MultiColumnHeaderState Columns;
        public string SelectionProjectGuid;
        public string SelectionGroup;
        public string EntrySearch;
        public ResourceWorkbenchRowState[] SelectedRows;
    }

    internal static class ResourceWorkbenchGUI
    {
        private sealed class DirectoryDisplay
        {
            internal int Revision = -1;
            internal string Identity;
            internal ResourceProjectPaths Paths;
            internal Exception Error;
        }
        private static readonly ConditionalWeakTable<ResourceProject, DirectoryDisplay> directoryDisplays = new();
        private static int directoryRevision;

        static ResourceWorkbenchGUI()
        {
            EditorApplication.projectChanged += InvalidateDirectoryDisplay;
            Undo.undoRedoPerformed += InvalidateDirectoryDisplay;
        }

        private static void InvalidateDirectoryDisplay() => directoryRevision++;

        internal static ResourceProjectPaths DisplayPaths(ResourceProject project)
        {
            var display = directoryDisplays.GetValue(project, _ => new DirectoryDisplay());
            var identity = project.ProjectId + "\n" + ResourceProjectPaths.SemanticIdentity(project);
            if (display.Revision != directoryRevision || display.Identity != identity)
            {
                display.Revision = directoryRevision; display.Identity = identity; display.Paths = null; display.Error = null;
                try { display.Paths = ResourceProjectPaths.Resolve(project); }
                catch (Exception error) { display.Error = error; }
            }
            if (display.Error != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(display.Error).Throw();
            return display.Paths;
        }
        private static bool dark;
        private static GUIStyle title;
        private static GUIStyle secondary;
        private static GUIStyle project;
        private static GUIStyle text;
        private static GUIStyle objectPath;
        private static GUIStyle identity;
        private static GUIStyle field;
        private static GUIStyle popup;
        private static GUIStyle button;
        private static GUIStyle toolbar;
        private static GUIStyle toolbarButton;
        private static GUIStyle foldout;
        private static GUIStyle menu;
        private static GUIStyle navigation;
        private static GUIStyle taskNavigation;
        private static GUIStyle row;
        private static GUIStyle selectedRow;
        private static GUIStyle objectBar;
        private static GUIStyle searchHint;
        internal const float FieldHeight = 26;
        internal const float RowHeight = 26;
        internal const float BarHeight = 30;
        internal static GUIStyle Title { get { EnsureStyles(); return title; } }
        internal static GUIStyle Secondary { get { EnsureStyles(); return secondary; } }
        internal static GUIStyle Project { get { EnsureStyles(); return project; } }
        internal static GUIStyle ObjectPath { get { EnsureStyles(); return objectPath; } }
        internal static GUIStyle Identity { get { EnsureStyles(); return identity; } }
        internal static GUIStyle Field { get { EnsureStyles(); return field; } }
        internal static GUIStyle Popup { get { EnsureStyles(); return popup; } }
        internal static GUIStyle Button { get { EnsureStyles(); return button; } }
        internal static GUIStyle Toolbar { get { EnsureStyles(); return toolbar; } }
        internal static GUIStyle ToolbarButton { get { EnsureStyles(); return toolbarButton; } }
        internal static GUIStyle Foldout { get { EnsureStyles(); return foldout; } }
        internal static GUIStyle Menu { get { EnsureStyles(); return menu; } }
        internal static GUIStyle NavigationStyle { get { EnsureStyles(); return navigation; } }
        internal static GUIStyle ObjectBar { get { EnsureStyles(); return objectBar; } }
        internal static Color Accent => EditorGUIUtility.isProSkin ? new Color(.34f, .65f, .95f) : new Color(.12f, .39f, .68f);
        internal static Color Divider => EditorGUIUtility.isProSkin ? new Color(.13f, .13f, .13f) : new Color(.65f, .65f, .65f);

        private static void EnsureStyles()
        {
            if (title != null && dark == EditorGUIUtility.isProSkin) return;
            dark = EditorGUIUtility.isProSkin;
            title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            secondary = new GUIStyle(EditorStyles.label) { wordWrap = true };
            secondary.normal.textColor = dark ? new Color(.7f, .7f, .7f) : new Color(.33f, .33f, .33f);
            searchHint = new GUIStyle(secondary) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 0, 0) };
            project = new GUIStyle(EditorStyles.toolbarDropDown) { fontStyle = FontStyle.Bold, fixedHeight = BarHeight, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            identity = new GUIStyle(secondary)
            {
                wordWrap = false, clipping = TextClipping.Clip, alignment = project.alignment,
                font = project.font, fontSize = project.fontSize, fixedHeight = project.fixedHeight,
                contentOffset = project.contentOffset,
                margin = new RectOffset(project.margin.left, project.margin.right, project.margin.top, project.margin.bottom),
                padding = new RectOffset(0, 0, project.padding.top, project.padding.bottom)
            };
            text = new GUIStyle(EditorStyles.textField) { wordWrap = true };
            objectPath = new GUIStyle(EditorStyles.objectField) { margin = new RectOffset(), clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, fixedHeight = FieldHeight };
            field = new GUIStyle(EditorStyles.textField) { fixedHeight = FieldHeight, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(5, 5, 3, 3) };
            popup = new GUIStyle(EditorStyles.popup) { fixedHeight = FieldHeight, alignment = TextAnchor.MiddleLeft };
            button = new GUIStyle(GUI.skin.button) { fixedHeight = BarHeight, alignment = TextAnchor.MiddleCenter };
            objectBar = new GUIStyle(EditorStyles.toolbar) { fixedHeight = BarHeight };
            toolbar = new GUIStyle(GUIStyle.none) { fixedHeight = BarHeight, padding = new RectOffset(3, 3, 0, 0) };
            toolbarButton = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = BarHeight, alignment = TextAnchor.MiddleCenter };
            foldout = new GUIStyle(EditorStyles.foldout) { fixedHeight = FieldHeight, alignment = TextAnchor.MiddleLeft };
            menu = new GUIStyle(EditorStyles.toolbarDropDown) { fixedHeight = BarHeight, alignment = TextAnchor.MiddleLeft };
            navigation = new GUIStyle(EditorStyles.label) { name = "Resource.Navigation", fixedHeight = BarHeight, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 0, 0), clipping = TextClipping.Clip };
            navigation.normal.textColor = EditorGUIUtility.isProSkin ? new Color(.77f, .79f, .82f) : new Color(.27f, .29f, .32f);
            navigation.onNormal.textColor = navigation.onHover.textColor = navigation.onActive.textColor = EditorGUIUtility.isProSkin ? Color.white : Accent;
            taskNavigation = new GUIStyle(navigation) { fixedHeight = 34, fontSize = 13 };
            row = new GUIStyle(EditorStyles.label) { fixedHeight = RowHeight, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 4, 0, 0), clipping = TextClipping.Clip };
            selectedRow = new GUIStyle(row) { fontStyle = FontStyle.Bold };
            selectedRow.normal.textColor = selectedRow.onNormal.textColor = EditorGUIUtility.isProSkin ? Color.white : new Color(.08f, .25f, .43f);
        }

        internal static int Navigation(int selected, string[] labels, bool tasks = false, params GUILayoutOption[] options)
        {
            EnsureStyles();
            var style = tasks ? taskNavigation : navigation;
            var rect = GUILayoutUtility.GetRect(GUIContent.none, style, options.Length == 0 ? new[] { GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.Height(style.fixedHeight) } : options);
            var surface = EditorGUIUtility.isProSkin ? new Color(.16f, .17f, .18f) : new Color(.88f, .89f, .90f);
            EditorGUI.DrawRect(rect, surface);
            if (selected >= 0 && selected < labels.Length)
            {
                var active = new Rect(rect.x + rect.width * selected / labels.Length, rect.y, rect.width / labels.Length, rect.height);
                EditorGUI.DrawRect(active, Color.Lerp(surface, Accent, tasks ? .12f : .22f));
                EditorGUI.DrawRect(new Rect(active.x + 6, active.yMax - 3, active.width - 12, 3), GUI.enabled ? Accent : Divider);
            }
            return GUI.Toolbar(rect, selected, labels, style);
        }

        internal static bool ToolbarToggle(bool selected, string label, float width, string help = null) =>
            EditorGUI.ToggleLeft(EditorGUILayout.GetControlRect(false, FieldHeight, GUILayout.Width(width)), new GUIContent(label, help), selected);

        internal static bool SelectableRow(Rect rect, bool selected, GUIContent content)
        {
            EnsureStyles();
            if (selected)
            {
                var surface = EditorGUIUtility.isProSkin ? new Color(.16f, .17f, .18f) : new Color(.94f, .94f, .94f);
                EditorGUI.DrawRect(rect, Color.Lerp(surface, Accent, .25f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), Accent);
            }
            return GUI.Toggle(rect, selected, content, selected ? selectedRow : row);
        }

        internal static bool PrimaryAction(string label, params GUILayoutOption[] options)
        {
            var previous = GUI.backgroundColor;
            GUI.backgroundColor = EditorGUIUtility.isProSkin ? new Color(.52f, 1.15f, 1.75f) : new Color(.55f, .78f, 1f);
            try { return GUILayout.Button(label, Button, options); }
            finally { GUI.backgroundColor = previous; }
        }

        internal static string TextField(string label, string value, bool delayed = false, string help = null)
        {
            var rect = EditorGUILayout.GetControlRect(true, FieldHeight, Field);
            return delayed ? EditorGUI.DelayedTextField(rect, new GUIContent(label, help), value, Field) : EditorGUI.TextField(rect, new GUIContent(label, help), value, Field);
        }

        internal static string SearchField(string value, string hint, params GUILayoutOption[] options)
        {
            var rect = EditorGUILayout.GetControlRect(false, FieldHeight, Field, options);
            var next = EditorGUI.TextField(rect, value ?? "", Field);
            if (string.IsNullOrEmpty(next) && Event.current.type == EventType.Repaint)
                GUI.Label(rect, new GUIContent(hint, hint), searchHint);
            return next;
        }

        internal static T ObjectField<T>(string label, T value, string help = null) where T : UnityEngine.Object =>
            (T)EditorGUI.ObjectField(EditorGUILayout.GetControlRect(true, FieldHeight), new GUIContent(label, help), value, typeof(T), false);

        internal static bool Toggle(string label, bool value, string help = null) =>
            EditorGUI.Toggle(EditorGUILayout.GetControlRect(true, FieldHeight), new GUIContent(label, help), value);

        internal static bool Disclosure(bool value, string label) => EditorGUI.Foldout(EditorGUILayout.GetControlRect(true, FieldHeight), value, label, true, Foldout);

        internal static void Property(SerializedProperty property, GUIContent label = null)
        {
            label ??= new GUIContent(PropertyLabel(property), property.tooltip);
            if (property.propertyType == SerializedPropertyType.Generic)
            {
                property.isExpanded = EditorGUI.Foldout(EditorGUILayout.GetControlRect(true, FieldHeight), property.isExpanded, label, true, Foldout);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                var child = property.Copy(); var end = child.GetEndProperty();
                if (child.NextVisible(true)) do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    Property(child);
                } while (child.NextVisible(false));
                EditorGUI.indentLevel--;
                return;
            }
            var rect = EditorGUILayout.GetControlRect(true, FieldHeight);
            switch (property.propertyType)
            {
                case SerializedPropertyType.String: property.stringValue = EditorGUI.TextField(rect, label, property.stringValue, Field); break;
                case SerializedPropertyType.Integer: property.intValue = EditorGUI.IntField(rect, label, property.intValue, Field); break;
                case SerializedPropertyType.ArraySize: property.intValue = Math.Max(0, EditorGUI.IntField(rect, label, property.intValue, Field)); break;
                case SerializedPropertyType.Float: property.floatValue = EditorGUI.FloatField(rect, label, property.floatValue, Field); break;
                case SerializedPropertyType.Enum:
                    var values = string.Join(",", property.enumNames);
                    var names = values == "Together,PerAsset" ? new[] { "合并分包 (Together)", "逐资源分包 (PerAsset)" } :
                        values == "Lz4,Lzma,Uncompressed" ? new[] { "LZ4压缩", "LZMA压缩", "不压缩 (Uncompressed)" } :
                        values == "None,Aes256GcmHkdfSegmentedV1" ? new[] { "不加密 (None)", "分段加密 (Aes256GcmHkdfSegmentedV1)" } : property.enumDisplayNames;
                    property.enumValueIndex = EditorGUI.Popup(rect, label, property.enumValueIndex, Array.ConvertAll(names, x => new GUIContent(x)), Popup); break;
                default: EditorGUI.PropertyField(rect, property, label, false); break;
            }
        }

        private static string PropertyLabel(SerializedProperty property)
        {
            if (property.displayName.StartsWith("Element ", StringComparison.Ordinal) && int.TryParse(property.displayName.Substring(8), out var index)) return "第" + (index + 1) + "项";
            return property.name switch
            {
                "size" => "数量", "Id" => "Group ID", "Packing" => "分包方式", "Selections" => "资源条目", "Guid" => "资源GUID",
                "Address" => "加载地址", "Aliases" => "地址别名", "Extensions" => "扩展名筛选", _ => property.displayName
            };
        }

        internal static void Metric(string label, string value, string help = null, float labelWidth = 146)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent(label, help), GUILayout.Width(labelWidth), GUILayout.Height(22));
            GUILayout.Label(new GUIContent(value ?? "未知", help), EditorStyles.wordWrappedLabel, GUILayout.MinWidth(0));
            EditorGUILayout.EndHorizontal();
        }

        internal static string Bytes(long? value) => !value.HasValue ? "未知" : value.Value < 1024 ? value + " B" :
            value.Value < 1024 * 1024 ? (value.Value / 1024d).ToString("F1") + " KiB" : (value.Value / (1024d * 1024)).ToString("F2") + " MiB";
        internal static string Duration(double? value) => !value.HasValue ? "未测量" : value.Value < 1000 ? value.Value.ToString("F1") + " ms" : (value.Value / 1000).ToString("F2") + " s";

        internal static string ResourceRootField(SerializedProperty root, string label = "资源目录")
        {
            var path = AssetDatabase.GUIDToAssetPath(root.stringValue);
            var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            EditorGUILayout.BeginHorizontal();
            var rect = EditorGUILayout.GetControlRect(true, FieldHeight, GUILayout.MinWidth(0));
            var next = (DefaultAsset)EditorGUI.ObjectField(rect, new GUIContent(label, path), folder, typeof(DefaultAsset), false);
            if (folder && Event.current.type == EventType.Repaint)
            {
                var value = EditorGUI.IndentedRect(rect); value.xMin += EditorGUIUtility.labelWidth; value.xMax -= 20;
                ObjectPath.Draw(value, new GUIContent(path, AssetDatabase.GetCachedIcon(path), path), false, false, false, false);
            }
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                if (GUILayout.Button(new GUIContent("复制", "复制完整Assets路径"), Button, GUILayout.Width(44), GUILayout.Height(FieldHeight))) EditorGUIUtility.systemCopyBuffer = path;
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(root.stringValue)))
                if (GUILayout.Button(new GUIContent("GUID", "复制目录GUID：" + root.stringValue), Button, GUILayout.Width(44), GUILayout.Height(FieldHeight))) EditorGUIUtility.systemCopyBuffer = root.stringValue;
            EditorGUILayout.EndHorizontal();
            if (!folder) GUILayout.Label(string.IsNullOrEmpty(root.stringValue) ? "尚未选择文件夹。" : "目录GUID无法解析为现有文件夹：" + root.stringValue, Secondary);
            else
            {
                var pathRect = GUILayoutUtility.GetRect(new GUIContent(path), Secondary, GUILayout.ExpandWidth(true));
                EditorGUI.SelectableLabel(pathRect, path, Secondary);
            }
            if (next == folder) return null;
            var selected = AssetDatabase.GetAssetPath(next);
            if (next && (!AssetDatabase.IsValidFolder(selected) || !selected.StartsWith("Assets/", StringComparison.Ordinal) || selected == "Assets/StreamingAssets" || selected.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal)))
                return "请选择Assets下的真实子目录，不能使用StreamingAssets或Packages。";
            root.stringValue = next ? AssetDatabase.AssetPathToGUID(selected) : "";
            return null;
        }

        internal static string ResourceDirectoriesFields(SerializedObject serialized)
        {
            var directories = serialized.FindProperty("SourceDirectories");
            var count = directories.arraySize;
            string error = null;
            GUILayout.Label("资源目录（" + count + "）", EditorStyles.boldLabel);
            GUILayout.Label("选择范围", EditorStyles.boldLabel);
            GUILayout.Label("添加本资源项目会使用的文件夹，可以添加多个，无需搬动资源。这里只设置选择范围；哪些文件或文件夹参与构建，仍在Group中选择。", Secondary);
            GUILayout.Space(6);
            GUILayout.Label("地址前缀", EditorStyles.boldLabel);
            GUILayout.Label("用于区分不同目录中的同名资源。默认加载地址由“前缀 + 目录内相对路径”组成；前缀可留空。", Secondary);
            GUILayout.Label("例如：前缀为audio，文件为Music/bgm.ogg，默认地址就是audio/Music/bgm.ogg。", Secondary);
            GUILayout.Label("修改前缀会改变下次构建的默认地址；手工填写的地址和历史发布不受影响。", Secondary);
            for (var index = 0; index < count; index++)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                var guid = directories.GetArrayElementAtIndex(index).FindPropertyRelative("Guid");
                error = ResourceRootField(guid) ?? error;
                EditorGUILayout.BeginHorizontal();
                Property(directories.GetArrayElementAtIndex(index).FindPropertyRelative("AddressPrefix"), new GUIContent("地址前缀", "留空使用目录内相对路径。修改会改变下次构建的默认地址，不会改写历史发布；显式地址不受影响。"));
                if (GUILayout.Button(new GUIContent("移除", "只移除目录登记，不删除源文件或Group中的资源条目；受影响条目会阻止构建。"), Button, GUILayout.Width(52)))
                {
                    directories.DeleteArrayElementAtIndex(index); count--; index--;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("添加资源目录", Button))
            {
                var item = directories.GetArrayElementAtIndex(directories.arraySize++);
                item.FindPropertyRelative("Guid").stringValue = "";
                item.FindPropertyRelative("AddressPrefix").stringValue = "";
            }
            GUILayout.Label("同一目录不能重复登记，目录之间也不能互相包含。移除或替换只修改配置，可撤销；不会自动移除资源条目。", Secondary);
            return error;
        }

        internal static void DirectoryValidation(ResourceProject project)
        {
            try
            {
                var paths = DisplayPaths(project);
                var affected = new System.Collections.Generic.List<string>();
                foreach (var group in project.Groups)
                    foreach (var selection in group.Selections)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(selection.Guid);
                        if (!paths.ContainsSource(path)) affected.Add("Group ‘" + group.Id + "’：" + (string.IsNullOrEmpty(path) ? selection.Guid : path));
                    }
                if (project.AutomaticAtlases)
                    foreach (var rule in project.AutomaticAtlases.Rules)
                        foreach (var source in rule.Sources ?? Array.Empty<UnityEngine.Object>())
                        {
                            var path = AssetDatabase.GetAssetPath(source);
                            if (!paths.ContainsSource(path)) affected.Add("图集规则 ‘" + rule.Id + "’：" + (string.IsNullOrEmpty(path) ? "来源缺失" : path));
                        }
                if (affected.Count != 0) EditorGUILayout.HelpBox("以下条目不在已登记的资源目录中，构建将被阻止。请恢复目录登记或明确调整条目；源文件未被删除。\n" + string.Join("\n", affected), MessageType.Error);
            }
            catch (Exception error) when (error is InvalidOperationException || error is System.IO.IOException || error is UnauthorizedAccessException)
            { EditorGUILayout.HelpBox("目录配置无法使用，构建将被阻止。请检查下列原因并修正配置；源文件不会因此被删除。\n" + error.Message, MessageType.Error); }
        }

        internal static void FailureDetails(Exception error, ref bool expanded)
        {
            if (error == null) return;
            expanded = Disclosure(expanded, "技术详情：查看原始错误");
            if (expanded) CopyLine("原始异常", ResourceEditorText.FailureDetails(error), Mathf.Max(160, EditorGUIUtility.currentViewWidth - 32));
        }

        internal static float GetOutputHeight(float requested, float windowHeight, bool hasOutput) =>
            Mathf.Clamp(requested > 0 ? requested : hasOutput ? 140 : 56, 48, Mathf.Max(48, Mathf.Min(360, windowHeight - 360)));

        internal static void HeightSplitter(Rect rect, ref float requested, float displayed, float windowHeight)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.center.y, rect.width, 1), Divider);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            var id = GUIUtility.GetControlID(FocusType.Passive, rect); var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 && rect.Contains(input.mousePosition))
            { requested = displayed; GUIUtility.hotControl = id; input.Use(); }
            else if (GUIUtility.hotControl == id && input.type == EventType.MouseDrag)
            { requested = GetOutputHeight(requested - input.delta.y, windowHeight, true); GUI.changed = true; input.Use(); }
            else if (GUIUtility.hotControl == id && input.type == EventType.MouseUp)
            { GUIUtility.hotControl = 0; input.Use(); }
        }

        internal static void CopyLine(string label, string value, float width)
        {
            EnsureStyles();
            value ??= "";
            GUILayout.Label(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal(GUILayout.MinWidth(0), GUILayout.MaxWidth(width));
            EditorGUILayout.SelectableLabel(value, text, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true), GUILayout.Height(Mathf.Max(FieldHeight, text.CalcHeight(new GUIContent(value), Mathf.Max(90, width - 62)))));
            if (GUILayout.Button(new GUIContent("复制", "复制完整原值，不包含显示省略号"), Button, GUILayout.Width(44), GUILayout.Height(FieldHeight)))
                EditorGUIUtility.systemCopyBuffer = value;
            EditorGUILayout.EndHorizontal();
        }

        internal static void Splitter(Rect rect, ref float value, float min, float max, int direction = 1)
        {
            EditorGUI.DrawRect(new Rect(rect.center.x, rect.y, 1, rect.height), Divider);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);
            var id = GUIUtility.GetControlID(FocusType.Passive, rect);
            var input = Event.current;
            if (input.type == EventType.MouseDown && input.button == 0 && rect.Contains(input.mousePosition))
            { GUIUtility.hotControl = id; input.Use(); }
            else if (GUIUtility.hotControl == id && input.type == EventType.MouseDrag)
            { value = Mathf.Clamp(value + input.delta.x * direction, min, max); GUI.changed = true; input.Use(); }
            else if (GUIUtility.hotControl == id && input.type == EventType.MouseUp)
            { GUIUtility.hotControl = 0; input.Use(); }
        }
    }
}
