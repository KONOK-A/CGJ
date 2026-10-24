using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Editor.Utilities;
using YLFramework.Equipment;
#if UNITY_6000_2_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

namespace YLFramework.Editor.Equipment
{
    public sealed class ConfigsWindow : SplitWindow
    {
        private const string SelectionKey = "YLFramework.Equipment.SelectedConfig";
        [SerializeField] private EquipmentsConfig currentEquipmentConfig;
        [SerializeField] private TreeViewState state = new();
        internal EquipmentListTree EquipmentListTree;
        private EquipmentConfigDataInspector _entryEditor;
        private Vector2 _scroll;

        [MenuItem("Window/YLFramework/EquipmentSystem", false, 10)]
        private static void Open() => GetWindow<ConfigsWindow>().Show();
        private void OnEnable()
        {
            titleContent = new GUIContent("装备配置");
            minSize = new Vector2(700, 400);
            var root = currentEquipmentConfig ? currentEquipmentConfig : AssetDatabase.LoadAssetAtPath<EquipmentsConfig>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(SelectionKey, "")));
            if (!root)
            {
                root = EquipmentsConfigController.GetAllEquipmentsConfig().FirstOrDefault();
            }

            if (root)
            {
                SetCurrentConfigs(root);
            }
        }
        private void OnDisable()
        {
            EquipmentListTree?.Dispose();
            EquipmentListTree = null;
            if (_entryEditor)
            {
                DestroyImmediate(_entryEditor);
            }
        }
        public void SetCurrentConfigs(EquipmentsConfig root)
        {
            EquipmentListTree?.Dispose();
            if (_entryEditor)
            {
                DestroyImmediate(_entryEditor);
            }

            currentEquipmentConfig = root;
            state = new TreeViewState();
            EquipmentListTree = root ? new EquipmentListTree(state, root) : null;
            if (root)
            {
                EditorPrefs.SetString(SelectionKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root)));
            }

            Repaint();
        }
        public void SetSelection(int id) => EquipmentListTree?.SelectEquipment(id);
        protected override void DrawGuiLeft(Rect rect)
        {
            GUILayout.BeginArea(rect);
            try
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button(currentEquipmentConfig ? (string.IsNullOrEmpty(currentEquipmentConfig.displayName) ? currentEquipmentConfig.name : currentEquipmentConfig.displayName) : "选择配表", EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        foreach (var root in EquipmentsConfigController.GetAllEquipmentsConfig())
                        {
                            var selected = root;
                            menu.AddItem(new GUIContent(root.name + " " + root.displayName), root == currentEquipmentConfig, () => SetCurrentConfigs(selected));
                        }
                        menu.ShowAsContext();
                    }
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(28)))
                        {
                            CreateRoot();
                        }

                        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(42)) && currentEquipmentConfig)
                        {
                            EquipmentsConfigController.RefreshEntries(currentEquipmentConfig);
                            ScriptBindController.RefreshBindings();
                            EquipmentListTree.Reload();
                        }
                    }
                }
                if (EquipmentListTree != null)
                {
                    EquipmentListTree.searchString = GUILayout.TextField(EquipmentListTree.searchString, EditorStyles.toolbarTextField);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        EquipmentListTree.OnGUI(new Rect(0, 43, rect.width, rect.height - 43));
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("创建配表后，右键列表创建基础或派生装备定义。", MessageType.Info);
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
            finally
            {
                GUILayout.EndArea();
            }
        }
        protected override void DrawGuiRight(Rect rect)
        {
            GUILayout.BeginArea(rect);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            try
            {
                var selected = EquipmentListTree?.Selected;
                if (!selected)
                {
                    EditorGUILayout.HelpBox("请选择装备定义。", MessageType.Info);
                    return;
                }
                if (!_entryEditor || _entryEditor.target != selected)
                {
                    if (_entryEditor)
                    {
                        DestroyImmediate(_entryEditor);
                    }

                    _entryEditor = (EquipmentConfigDataInspector)UnityEditor.Editor.CreateEditor(selected, typeof(EquipmentConfigDataInspector));
                }
                _entryEditor.OnInspectorGUI();
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
            }
            finally
            {
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
            }
        }
        private void CreateRoot()
        {
            var path = EditorUtility.SaveFilePanelInProject("新建装备配表", "EquipmentsConfig", "asset", "选择新配表位置");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var root = CreateInstance<EquipmentsConfig>();
            root.name = Path.GetFileNameWithoutExtension(path);
            root.displayName = root.name;
            root.startId = checked(EquipmentsConfigController.GetAllEquipmentsConfig().Select(c => c.startId).DefaultIfEmpty(0).Max() + 1000);
            AssetDatabase.CreateAsset(root, path);
            SetCurrentConfigs(root);
        }
    }
}
