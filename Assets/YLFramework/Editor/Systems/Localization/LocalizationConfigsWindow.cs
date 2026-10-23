using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Localization;

namespace YLFramework.Editor.Localization
{
    public class LocalizationConfigsWindow : EditorWindow
    {
        private const string SelectedRootKey = "YLFramework.Localization.Editor.SelectedRoot";
        [SerializeField] private MultiLanguageConfigs root;
        [SerializeField] private TreeViewState treeState = new();
        private LanguageTree _tree;
        private LanguageConfig _selected;
        private Vector2 _scroll;
        private string _search = string.Empty;
        private int _newId;
        private int _sourceId;
        private MultiLanguageType _newType;
        private string _failure;

        [MenuItem("YLFramework/Localization/多语言配置")]
        public static void Open() => GetWindow<LocalizationConfigsWindow>("YL Localization").Show();

        public static void Open(MultiLanguageConfigs config)
        {
            var window = GetWindow<LocalizationConfigsWindow>("YL Localization");
            window.Bind(config);
            window.Show();
        }

        private void OnEnable()
        {
            if (!root)
            {
                root = AssetDatabase.LoadAssetAtPath<MultiLanguageConfigs>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(SelectedRootKey)));
            }

            Bind(root);
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            _tree?.Dispose();
            _tree = null;
            Undo.undoRedoPerformed -= Repaint;
        }

        private void Bind(MultiLanguageConfigs config)
        {
            _tree?.Dispose();
            _tree = null;
            root = config;
            _selected = null;
            _failure = null;
            if (!root)
            {
                return;
            }

            EditorPrefs.SetString(SelectedRootKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root)));
            _tree = new LanguageTree(root, treeState, value =>
            {
                _selected = value;
                Repaint();
            });
            _tree.Reload();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var value = (MultiLanguageConfigs)EditorGUILayout.ObjectField(root, typeof(MultiLanguageConfigs), false);
                if (value != root)
                {
                    Bind(value);
                }

                if (GUILayout.Button("新建", EditorStyles.toolbarButton, GUILayout.Width(45)))
                {
                    var path = EditorUtility.SaveFilePanelInProject("Create YL localization root", "MultiLanguageConfigs", "asset", "Choose a new root asset path.");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var asset = CreateInstance<MultiLanguageConfigs>();
                        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
                        Undo.RegisterCreatedObjectUndo(asset, "Create YL localization root");
                        Bind(asset);
                    }
                }
            }
            if (!root)
            {
                EditorGUILayout.HelpBox("选择或新建YL多语言根。不会自动登记旧XF配置。", MessageType.Info);
                return;
            }
            if (_selected && !root.Languages.Contains(_selected))
            {
                _selected = null;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加语言"))
                {
                    ShowLanguages();
                }

                if (GUILayout.Button("验证 / 直接刷新"))
                {
                    Run(() =>
                    {
                        LocalizationAuthoring.RequireValid(root);
                        LocalizationManager.Refresh();
                    });
                }

                if (GUILayout.Button("保存目标资产"))
                {
                    Run(() =>
                {
                    LocalizationAuthoring.RequireValid(root);
                    AssetDatabase.SaveAssetIfDirty(root);
                    foreach (var language in root.Languages)
                    {
                        AssetDatabase.SaveAssetIfDirty(language);
                    }
                });
                }
            }
            if (!string.IsNullOrEmpty(_failure))
            {
                EditorGUILayout.HelpBox(_failure, MessageType.Error);
            }

            try
            {
                LocalizationAuthoring.RequireValid(root);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
                {
                    _tree?.OnGUI(GUILayoutUtility.GetRect(200, 220, GUILayout.ExpandHeight(true)));
                    if (_selected && GUILayout.Button("定位语言资产"))
                    {
                        EditorGUIUtility.PingObject(_selected);
                    }

                    if (_selected && GUILayout.Button("移除语言引用 (保留文件)") && EditorUtility.DisplayDialog("移除语言", "仅移除当前根的引用，不删除语言文件，可Undo。", "移除", "取消"))
                    {
                        Run(() =>
                        {
                            LocalizationAuthoring.RemoveLanguageReference(root, _selected);
                            _selected = null;
                            _tree.Reload();
                        });
                    }
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawEntries();
                }
            }
        }

        private void DrawEntries()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _newId = EditorGUILayout.IntField("目标ID", _newId);
                _newType = (MultiLanguageType)EditorGUILayout.EnumPopup(_newType);
                if (GUILayout.Button("全部语言新增"))
                {
                    Run(() => LocalizationAuthoring.AddEntry(root, _newId, _newType));
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                _sourceId = EditorGUILayout.IntField("源ID", _sourceId);
                if (GUILayout.Button("复制到目标ID"))
                {
                    Run(() => LocalizationAuthoring.DuplicateEntry(root, _sourceId, _newId));
                }

                if (GUILayout.Button("删除源ID") && EditorUtility.DisplayDialog("删除条目", $"从此根的所有语言删除ID {_sourceId}，可Undo。", "删除", "取消"))
                {
                    Run(() => LocalizationAuthoring.RemoveEntry(root, _sourceId));
                }
            }
            _search = EditorGUILayout.TextField("搜索ID/文本/备注", _search);
            if (!_selected)
            {
                EditorGUILayout.HelpBox("选择左侧语言；拖动可以调整显式回退顺序。", MessageType.Info);
                return;
            }
            var serialized = new SerializedObject(_selected);
            serialized.Update();
            EditorGUILayout.PropertyField(serialized.FindProperty("language"));
            EditorGUILayout.PropertyField(serialized.FindProperty("displayName"));
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var entries = serialized.FindProperty("languagesInfo");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var property = entries.GetArrayElementAtIndex(i);
                var entry = i < _selected.languagesInfo.Count ? _selected.languagesInfo[i] : null;
                if (!string.IsNullOrEmpty(_search) && entry != null && !($"{entry.id} {entry.str} {entry.notes}").ToLowerInvariant().Contains(_search.ToLowerInvariant()))
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, new GUIContent(entry == null ? "Missing" : entry.id + " / " + entry.type), true);
                if (entry?.type == MultiLanguageType.Image && entry.image != null)
                {
                    var content = EditorAssetTool.GetTextureGuiContent(entry.image.projectName, entry.image.imageName);
                    GUILayout.Label(content, GUILayout.Height(56));
                }
            }
            EditorGUILayout.EndScrollView();
            if (serialized.ApplyModifiedProperties())
            {
                _selected.ClearCache();
                LocalizationManager.Refresh();
            }
        }

        private void ShowLanguages()
        {
            var menu = new GenericMenu();
            var seen = new HashSet<SystemLanguage>();
            foreach (SystemLanguage language in Enum.GetValues(typeof(SystemLanguage)))
            {
                if (language == SystemLanguage.Unknown || !seen.Add(language))
                {
                    continue;
                }

                var chosen = language;
                var exists = root.Languages.Exists(item => item && item.language == chosen);
                if (exists)
                {
                    menu.AddDisabledItem(new GUIContent(chosen.ToString()));
                }
                else
                {
                    menu.AddItem(new GUIContent(chosen.ToString()), false, () => Run(() =>
                    {
                        _selected = LocalizationAuthoring.CreateLanguage(root, chosen);
                        _tree.Reload();
                    }));
                }
            }
            menu.ShowAsContext();
        }

        private void Run(Action action)
        {
            try
            {
                action();
                _failure = null;
            }
            catch (Exception error)
            {
                _failure = error.Message;
                Debug.LogException(error, root);
            }
            Repaint();
        }

        private sealed class LanguageTree : ReorderableTreeView<LanguageConfig>
        {
            private readonly MultiLanguageConfigs _owner;
            private readonly Action<LanguageConfig> _select;
            internal LanguageTree(MultiLanguageConfigs owner, TreeViewState state, Action<LanguageConfig> select) : base(owner, () => owner.Languages, state)
            {
                this._owner = owner;
                this._select = select;
            }
            protected override void OnBuildRoot(TreeViewItem root, IList<LanguageConfig> data)
            {
                for (var i = 0; i < data.Count; i++)
                {
                    root.AddChild(new TreeViewItem(i + 1, 0, data[i] ? data[i].language + " / " + data[i].displayName : "(Missing)"));
                }
            }
            protected override void SelectionChanged(IList<int> ids)
            {
                if (ids.Count > 0 && ids[0] > 0 && ids[0] <= _owner.Languages.Count)
                {
                    _select(_owner.Languages[ids[0] - 1]);
                }
            }
        }
    }

    [CustomEditor(typeof(MultiLanguageConfigs))]
    internal class MultiLanguageConfigsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("打开多语言作者窗口"))
            {
                LocalizationConfigsWindow.Open((MultiLanguageConfigs)target);
            }

            try
            {
                LocalizationAuthoring.RequireValid((MultiLanguageConfigs)target);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
        }
    }

    [CustomEditor(typeof(LocalizeBase), true)]
    internal class LocalizeInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("直接刷新显示"))
            {
                ((LocalizeBase)target).Refresh();
            }

            EditorGUILayout.HelpBox("Other/TMP：填写完整组件名及公开string成员(如text)，IL2CPP需保留该成员。图片由ResourceImage持有请求和Lease。", MessageType.Info);
        }
    }
}
