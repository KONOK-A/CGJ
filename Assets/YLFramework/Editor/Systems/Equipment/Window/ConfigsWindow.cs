// Adapted from XF EquipmentSystem 1.1.9 (MIT). See Runtime/Systems/Equipment/LICENSE.txt.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using YLFramework.Core.Editor;
#if UNITY_6000_2_OR_NEWER
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

namespace YLFramework.EquipmentSystem
{
    public sealed class ConfigsWindow : SplitWindow
    {
        private const string SelectionKey = "YLFramework.Equipment.SelectedConfig";
        [SerializeField] private EquipmentsConfig currentEquipmentConfig;
        [SerializeField] private TreeViewState state = new();
        internal EquipmentListTree equipmentListTree;
        private EquipmentConfigDataInspector entryEditor;
        private Vector2 scroll;

        [MenuItem("Window/YLFramework/EquipmentSystem", false, 10)]
        private static void Open() => GetWindow<ConfigsWindow>().Show();
        private void OnEnable()
        {
            titleContent = new GUIContent("装备配置"); minSize = new Vector2(700, 400);
            var root = currentEquipmentConfig ? currentEquipmentConfig : AssetDatabase.LoadAssetAtPath<EquipmentsConfig>(AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(SelectionKey, "")));
            if (!root) root = EquipmentsConfigController.GetAllEquipmentsConfig().FirstOrDefault();
            if (root) SetCurrentConfigs(root);
        }
        private void OnDisable()
        {
            equipmentListTree?.Dispose(); equipmentListTree = null;
            if (entryEditor) DestroyImmediate(entryEditor);
        }
        public void SetCurrentConfigs(EquipmentsConfig root)
        {
            equipmentListTree?.Dispose();
            if (entryEditor) DestroyImmediate(entryEditor);
            currentEquipmentConfig = root;
            state = new TreeViewState();
            equipmentListTree = root ? new EquipmentListTree(state, root) : null;
            if (root) EditorPrefs.SetString(SelectionKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(root)));
            Repaint();
        }
        public void SetSelection(int id) => equipmentListTree?.SelectEquipment(id);
        protected override void DrawGUILeft(Rect rect)
        {
            GUILayout.BeginArea(rect);
            try
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    if (GUILayout.Button(currentEquipmentConfig ? (string.IsNullOrEmpty(currentEquipmentConfig.displayName) ? currentEquipmentConfig.name : currentEquipmentConfig.displayName) : "选择配表", EditorStyles.toolbarDropDown))
                    {
                        var menu = new GenericMenu();
                        foreach (var root in EquipmentsConfigController.GetAllEquipmentsConfig()) { var selected = root; menu.AddItem(new GUIContent(root.name + " " + root.displayName), root == currentEquipmentConfig, () => SetCurrentConfigs(selected)); }
                        menu.ShowAsContext();
                    }
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        if (GUILayout.Button("+", EditorStyles.toolbarButton, GUILayout.Width(28))) CreateRoot();
                        if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(42)) && currentEquipmentConfig) { EquipmentsConfigController.RefreshEntries(currentEquipmentConfig); ScriptBindController.RefreshBindings(); equipmentListTree.Reload(); }
                    }
                }
                if (equipmentListTree != null)
                {
                    equipmentListTree.searchString = GUILayout.TextField(equipmentListTree.searchString, EditorStyles.toolbarTextField);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) equipmentListTree.OnGUI(new Rect(0, 43, rect.width, rect.height - 43));
                }
                else EditorGUILayout.HelpBox("创建配表后，右键列表创建基础或派生装备定义。", MessageType.Info);
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            finally { GUILayout.EndArea(); }
        }
        protected override void DrawGUIRight(Rect rect)
        {
            GUILayout.BeginArea(rect); scroll = EditorGUILayout.BeginScrollView(scroll);
            try
            {
                var selected = equipmentListTree?.Selected;
                if (!selected) { EditorGUILayout.HelpBox("请选择装备定义。", MessageType.Info); return; }
                if (!entryEditor || entryEditor.target != selected)
                {
                    if (entryEditor) DestroyImmediate(entryEditor);
                    entryEditor = (EquipmentConfigDataInspector)UnityEditor.Editor.CreateEditor(selected, typeof(EquipmentConfigDataInspector));
                }
                entryEditor.OnInspectorGUI();
            }
            catch (ExitGUIException) { throw; }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
            finally { EditorGUILayout.EndScrollView(); GUILayout.EndArea(); }
        }
        private void CreateRoot()
        {
            var path = EditorUtility.SaveFilePanelInProject("新建装备配表", "EquipmentsConfig", "asset", "选择新配表位置");
            if (string.IsNullOrEmpty(path)) return;
            var root = CreateInstance<EquipmentsConfig>(); root.name = Path.GetFileNameWithoutExtension(path);
            root.displayName = root.name;
            root.start_id = checked(EquipmentsConfigController.GetAllEquipmentsConfig().Select(c => c.start_id).DefaultIfEmpty(0).Max() + 1000);
            AssetDatabase.CreateAsset(root, path); SetCurrentConfigs(root);
        }
    }
}
