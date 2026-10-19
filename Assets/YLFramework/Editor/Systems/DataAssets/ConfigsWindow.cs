// Adapted from ScriptableObjectSystem 1.1.4 (MIT). Copyright (c) 2024 弦风课堂.
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.DataAssets.Editor
{
    public sealed class ConfigsWindow : SplitWindow
    {
        [SerializeField] private ScriptableObjectConfigs current;
        [SerializeField] private TreeViewState treeState = new TreeViewState();
        private ScriptableObjectListTree tree;
        private UnityEditor.Editor dataEditor;
        private ScriptableObjectData selected;
        private Vector2 rightScroll;
        private string search;
        private static string PreferenceKey => "YLFramework.DataAssets.SelectedRoot." + Hash128.Compute(Application.dataPath);

        [MenuItem("Window/YLFramework/Data Assets")]
        public static void Open() => CreateWindow<ConfigsWindow>().Show();

        public static void Open(ScriptableObjectConfigs root)
        {
            var window = GetWindow<ConfigsWindow>();
            window.SetCurrentConfigs(root);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("Data Assets");
            minSize = new Vector2(640, 300);
            if (!current)
                current = AssetDatabase.LoadAssetAtPath<ScriptableObjectConfigs>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(PreferenceKey, string.Empty)));
            Undo.undoRedoPerformed += Refresh;
            EditorApplication.projectChanged += Refresh;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
            EditorApplication.projectChanged -= Refresh;
            if (current) EditorPrefs.SetString(PreferenceKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(current)));
            ReleaseEditors();
        }

        public void SetCurrentConfigs(ScriptableObjectConfigs root)
        {
            ReleaseEditors();
            current = root;
            treeState = new TreeViewState();
            Repaint();
        }

        private void ReleaseEditors()
        {
            tree?.Dispose();
            tree = null;
            SetSelected(null);
        }

        private void SetSelected(ScriptableObjectData data)
        {
            if (selected == data && dataEditor) return;
            GUIUtility.keyboardControl = 0;
            EditorGUIUtility.editingTextField = false;
            if (dataEditor) DestroyImmediate(dataEditor);
            dataEditor = null;
            selected = data;
            if (selected) dataEditor = UnityEditor.Editor.CreateEditor(selected);
        }

        private void Refresh()
        {
            if (current) current.ClearCache();
            tree?.RefreshKeepingSelection();
            if (selected && (!current || current.configs == null || !current.configs.Contains(selected))) SetSelected(null);
            Repaint();
        }

        protected override void OnGUI()
        {
            if (!current)
            {
                EditorGUILayout.HelpBox("Select a YL configuration root or create one. The Excel adapter is optional.", MessageType.Info);
                var root = (ScriptableObjectConfigs)EditorGUILayout.ObjectField("Root", null, typeof(ScriptableObjectConfigs), false);
                if (root) SetCurrentConfigs(root);
                if (GUILayout.Button("Create Root")) CreateRoot();
                return;
            }
            if (current.configs == null)
            {
                EditorGUILayout.HelpBox("The entry list is missing. Repair it explicitly before opening the tree.", MessageType.Error);
                if (GUILayout.Button("Initialize Empty List"))
                {
                    Undo.RegisterCompleteObjectUndo(current, "Repair DataAssets list");
                    current.configs = new System.Collections.Generic.List<ScriptableObjectData>();
                    EditorUtility.SetDirty(current);
                }
                return;
            }
            if (tree == null)
            {
                tree = new ScriptableObjectListTree(current, treeState);
                tree.Selected = SetSelected;
                tree.Changed = Refresh;
            }
            base.OnGUI();
        }

        protected override void DrawGUILeft(Rect rect)
        {
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button(string.IsNullOrEmpty(current.displayName) ? current.name : current.displayName, EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();
                foreach (var root in ScriptableObjectConfigsController.GetAllConfigs())
                {
                    var target = root;
                    menu.AddItem(new GUIContent(root.name), root == current, () => SetCurrentConfigs(target));
                }
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Create Root"), false, CreateRoot);
                menu.ShowAsContext();
            }
            if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(24))) tree.ShowCreateMenu();
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(42))) ScriptableObjectConfigsController.Save(current);
            GUILayout.EndHorizontal();
            search = EditorGUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarSearchField);
            tree.searchString = search;
            GUILayout.EndArea();
            tree.OnGUI(new Rect(rect.x, rect.y + 43, rect.width, Mathf.Max(0, rect.height - 43)));
        }

        protected override void DrawGUIRight(Rect rect)
        {
            GUILayout.BeginArea(rect);
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            if (selected && dataEditor) dataEditor.OnInspectorGUI();
            else
            {
                EditorGUILayout.LabelField(current.name, EditorStyles.boldLabel);
                ScriptableObjectConfigsInspector.DrawDiagnostics(current);
                if (GUILayout.Button("Select Root")) Selection.activeObject = current;
                if (GUILayout.Button("Delete Root..."))
                {
                    if (EditorUtility.DisplayDialog("Delete DataAssets Root", "Delete this root and its owned standalone entries? This operation cannot be undone. External assets and unknown files are preserved.", "Delete", "Cancel"))
                    {
                        if (ScriptableObjectConfigsController.DeleteRoot(current, true)) SetCurrentConfigs(null);
                        GUIUtility.ExitGUI();
                    }
                }
            }
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void CreateRoot()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create DataAssets Root", "DataConfigs", "asset", "Choose the root asset path.");
            if (string.IsNullOrEmpty(path)) return;
            var root = CreateInstance<ScriptableObjectConfigs>();
            AssetDatabase.CreateAsset(root, path);
            SetCurrentConfigs(root);
            EditorGUIUtility.PingObject(root);
        }
    }
}
