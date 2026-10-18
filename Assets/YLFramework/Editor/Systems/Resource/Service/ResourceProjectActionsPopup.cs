using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    internal sealed class ResourceProjectActionsPopup : PopupWindowContent
    {
        private const float Width = 220;
        private const float RowHeight = 28;
        private readonly ResourceProject project;
        private readonly string id;
        private readonly string path;
        private readonly Action<string> reportError;
        private readonly GUIStyle itemStyle;
        private readonly string[] labels;
        private int highlighted = -1;

        internal ResourceProjectActionsPopup(ResourceProject selected, Action<string> error)
        {
            project = selected;
            id = selected ? selected.ProjectId : "";
            path = selected ? AssetDatabase.GetAssetPath(selected) : "";
            reportError = error;
            labels = selected ? new[] { "定位当前项目配置", "复制项目标识", "复制配置路径", "帮助（Resource 文档）" } : new[] { "帮助（Resource 文档）" };
            itemStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 0, 0), margin = new RectOffset()
            };
        }

        // 使用已知弹层宽度右对齐，而不是猜测原生系统菜单的实际宽度。
        internal static Rect GetAnchor(Rect button) => new(button.xMax - Width, button.y + 4, Width, button.height);
        public override Vector2 GetWindowSize() => new(Width, labels.Length * RowHeight + 12 + (labels.Length > 1 ? 9 : 0));
        public override void OnOpen() => editorWindow.wantsMouseMove = true;

        public override void OnGUI(Rect rect)
        {
            var input = Event.current;
            if (input.type == EventType.KeyDown)
            {
                if (input.keyCode == KeyCode.DownArrow || input.keyCode == KeyCode.UpArrow)
                {
                    highlighted = highlighted < 0 ? (input.keyCode == KeyCode.DownArrow ? 0 : labels.Length - 1) :
                        (highlighted + (input.keyCode == KeyCode.DownArrow ? 1 : labels.Length - 1)) % labels.Length;
                    input.Use(); editorWindow.Repaint();
                }
                else if ((input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter) && highlighted >= 0) Execute(highlighted);
                else if (input.keyCode == KeyCode.Escape) { editorWindow.Close(); GUIUtility.ExitGUI(); }
            }
            for (var index = 0; index < labels.Length; index++)
            {
                var row = new Rect(6, 6 + index * RowHeight + (index == 3 ? 9 : 0), rect.width - 12, RowHeight);
                if (input.type == EventType.MouseMove && row.Contains(input.mousePosition))
                { highlighted = index; editorWindow.Repaint(); }
                if (highlighted == index)
                    EditorGUI.DrawRect(row, EditorGUIUtility.isProSkin ? new Color(.25f, .39f, .53f) : new Color(.7f, .81f, .92f));
                if (index == 3) EditorGUI.DrawRect(new Rect(12, row.y - 5, rect.width - 24, 1), ResourceWorkbenchGUI.Divider);
                if (GUI.Button(row, labels[index], itemStyle)) Execute(index);
            }
        }

        private void Execute(int index)
        {
            editorWindow.Close();
            switch (labels.Length == 1 ? 3 : index)
            {
                case 0:
                    if (project) { Selection.activeObject = project; EditorGUIUtility.PingObject(project); }
                    break;
                case 1: EditorGUIUtility.systemCopyBuffer = id; break;
                case 2: EditorGUIUtility.systemCopyBuffer = path; break;
                case 3:
                    var document = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/YLFramework/Documentation/Modules/Resource.md");
                    if (document) AssetDatabase.OpenAsset(document);
                    else reportError("Resource.md 不在当前工程中。");
                    break;
            }
            GUIUtility.ExitGUI();
        }
    }
}
