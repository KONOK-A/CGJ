using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Attributes;
using YLFramework.Editor.Utilities;
using YLFramework.Skill;


namespace YLFramework.Editor.Skill
{

    [CustomEditor(typeof(SkillConfigData), true)]
    public class SkillConfigDataInspector : UnityEditor.Editor
    {

        private static SkillConfigDataInspector _instance;

        public static SkillConfigDataInspector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<SkillConfigDataInspector>();
                }

                return _instance;
            }
        }

        private SkillConfigData _currentSkillConfigData;
        //private Type currentSkillConfigDataType;
        private SerializedObject _currentSerializedObject;
        private List<SerializedProperty> _currentSerializeProperty;
        //private Vector2 currentPosition;

        //private SerializedProperty script = null;

        //private Dictionary<string,CustomPropertyGui> custom_property = new Dictionary<string,CustomPropertyGui>();


        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);

            base.OnInspectorGUI();

            EditorGUI.EndDisabledGroup();

            //if (GUILayout.Button("编辑技能"))
            //{
            //    ConfigsWindow window = EditorWindow.GetWindow<ConfigsWindow>();
            //    window.Show();

            //    SkillConfigData currentSkill = target as SkillConfigData;
            //    if (currentSkill.parent != null)
            //    {
            //        string asset_path = AssetDatabase.GetAssetPath(currentSkill.parent);
            //        ConfigsWindow.SelectGUID = AssetDatabase.AssetPathToGUID(asset_path);
            //        window.currentSkillConfig = null;
            //    }
            //    window.SetSelection(currentSkill.id);
            //}
        }

        internal void OnInspectorGUI(SkillConfigData data, ConfigsWindow window, SkillsConfig skillsConfig)
        {
            if (data != null)
            {
                BindSelection(data);

                _currentSerializedObject.Update();

                //EditorGUI.BeginDisabledGroup(true);
                //EditorGUILayout.PropertyField(script);
                //EditorGUI.EndDisabledGroup();

                for (int i = 0; i < _currentSerializeProperty.Count; i++)
                {
                    SerializedProperty property = _currentSerializeProperty[i];

                    //if (property.displayName == "Script") continue;


                    if (property.name == "parent" && property.objectReferenceValue == null)
                    {
                        property.objectReferenceValue = skillsConfig;
                    }

                    if (GetPropertyCustomAttribute<HideInInspector>(property) != null)
                    {
                        continue;
                    }

                    CustomPropertyGui customPropertyGui = GetPropertyCustomAttribute<CustomPropertyGui>(property) ?? GetDomainDrawer(property.name);
                    EditorGUI.BeginChangeCheck();

                    if (customPropertyGui != null)
                    {
                        customPropertyGui.OnPropertyGui(property);
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(property.IsScript());
                        EditorGUILayout.PropertyField(property);
                        EditorGUI.EndDisabledGroup();
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        _currentSerializedObject.ApplyModifiedProperties();

                        if (property.name == "id")
                        {
                            window.SetSelection(property.intValue);
                        }

                        //EditorUtility.SetDirty(currentSkillConfigData);

                        if (_currentSkillConfigData.parent != null)
                        {
                            _currentSkillConfigData.parent.ClearCache();
                        }

                        if (customPropertyGui != null && customPropertyGui.ReloadTreeViewOnValueChange)
                        {
                            window.SkillListTree.Reload();
                        }
                    }
                }
            }
            else
            {
                ReleaseSelection();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("空!");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
        }


        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> _attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();
        private Dictionary<string, CustomPropertyGui> _drawers = new();
        private static Dictionary<string, CustomPropertyGui> CreateDrawers() => new()
        {
            ["id"] = new CustomSkillConfigDataIdGui(),
            ["parent"] = new CustomSkillConfigDataParentGui(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGui(),
            ["simultaneousSkillIds"] = new CustomSkillConfigDataSkillGui(),
            ["skillClassFullName"] = new CustomSkillConfigDataSkillClassGui()
        };
        private CustomPropertyGui GetDomainDrawer(string name) => _drawers.TryGetValue(name, out var drawer) ? drawer : null;

        internal void BindSelection(SkillConfigData data)
        {
            if (_currentSkillConfigData == data && _currentSerializedObject != null)
            {
                return;
            }

            ReleaseSelection();
            if (!data)
            {
                return;
            }

            _currentSkillConfigData = data;
            _currentSerializedObject = new SerializedObject(data);
            _currentSerializeProperty = _currentSerializedObject.GetAllSerializedProperty();
            _drawers = CreateDrawers();
        }

        internal void ReleaseSelection()
        {
            _currentSerializedObject?.Dispose();
            _currentSerializedObject = null;
            _currentSkillConfigData = null;
            _currentSerializeProperty = null;
            _attributes.Clear();
            _drawers.Clear();
        }
        private void OnDisable() => ReleaseSelection();

        private T GetPropertyCustomAttribute<T>(SerializedProperty property) where T : Attribute
        {
            if (!_attributes.ContainsKey(property))
            {
                _attributes.Add(property, new Dictionary<Type, Attribute>());
            }

            if (_attributes[property].ContainsKey(typeof(T)))
            {
                return _attributes[property][typeof(T)] as T;
            }

            var customPropertyGui = property.GetAttribute<T>();

            _attributes[property].Add(typeof(T), customPropertyGui);
            return customPropertyGui;
        }
    }

}
