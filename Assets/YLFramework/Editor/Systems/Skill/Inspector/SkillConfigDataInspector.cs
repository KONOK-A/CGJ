using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


namespace YLFramework.SkillSystem
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
                    _instance = CreateInstance<SkillConfigDataInspector>();
                return _instance;
            }
        }

        private SkillConfigData currentSkillConfigData;
        //private Type currentSkillConfigDataType;
        private SerializedObject currentSerializedObject;
        private List<SerializedProperty> currentSerializeProperty;
        //private Vector2 currentPosition;

        //private SerializedProperty script = null;

        //private Dictionary<string,CustomPropertyGUI> custom_property = new Dictionary<string,CustomPropertyGUI>();


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

        internal void OnInspectorGUI(SkillConfigData data, ConfigsWindow window,SkillsConfig skillsConfig )
        {
            if (data != null)
            {
                BindSelection(data);

                currentSerializedObject.Update();

                //EditorGUI.BeginDisabledGroup(true);
                //EditorGUILayout.PropertyField(script);
                //EditorGUI.EndDisabledGroup();

                for (int i = 0; i < currentSerializeProperty.Count; i++)
                {
                    SerializedProperty property = currentSerializeProperty[i];

                    //if (property.displayName == "Script") continue;


                    if (property.name == "parent" && property.objectReferenceValue == null)
                        property.objectReferenceValue = skillsConfig;


                    if (GetPropertyCustomAttribute<HideInInspector>(property) != null) continue;
                    CustomPropertyGUI customPropertyGUI = GetPropertyCustomAttribute<CustomPropertyGUI>(property) ?? GetDomainDrawer(property.name);
                    EditorGUI.BeginChangeCheck();

                    if (customPropertyGUI != null)
                    {
                        customPropertyGUI.OnPropertyGUI(property);
                    }
                    else
                    {
                        EditorGUI.BeginDisabledGroup(property.IsScript());
                        EditorGUILayout.PropertyField(property);
                        EditorGUI.EndDisabledGroup();
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        currentSerializedObject.ApplyModifiedProperties();

                        if (property.name == "id")
                            window.SetSelection(property.intValue);

                        //EditorUtility.SetDirty(currentSkillConfigData);

                        if(currentSkillConfigData.parent != null)
                            currentSkillConfigData.parent.ClearCache();

                        if (customPropertyGUI != null && customPropertyGUI.reloadTreeViewOnValueChange)
                            window.skillListTree.Reload();
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


        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();
        private Dictionary<string, CustomPropertyGUI> drawers = new();
        private static Dictionary<string, CustomPropertyGUI> CreateDrawers() => new()
        {
            ["id"] = new CustomSkillConfigDataIDGUI(),
            ["parent"] = new CustomSkillConfigDataParentGUI(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGUI(),
            ["canbe_released_at_the_same_time"] = new CustomSkillConfigDataSkillGUI(),
            ["skill_class_full_name"] = new CustomSkillConfigDataSkillClassGUI()
        };
        private CustomPropertyGUI GetDomainDrawer(string name) => drawers.TryGetValue(name, out var drawer) ? drawer : null;

        internal void BindSelection(SkillConfigData data)
        {
            if (currentSkillConfigData == data && currentSerializedObject != null) return;
            ReleaseSelection();
            if (!data) return;
            currentSkillConfigData = data;
            currentSerializedObject = new SerializedObject(data);
            currentSerializeProperty = currentSerializedObject.GetAllSerializedProperty();
            drawers = CreateDrawers();
        }

        internal void ReleaseSelection()
        {
            currentSerializedObject?.Dispose();
            currentSerializedObject = null;
            currentSkillConfigData = null;
            currentSerializeProperty = null;
            attributes.Clear();
            drawers.Clear();
        }
        private void OnDisable() => ReleaseSelection();

        private T GetPropertyCustomAttribute<T>(SerializedProperty property) where T : Attribute
        {
            if (!attributes.ContainsKey(property))
                attributes.Add(property, new Dictionary<Type, Attribute>());

            if (attributes[property].ContainsKey(typeof(T)))
                return attributes[property][typeof(T)] as T;

            var customPropertyGUI = property.GetAttribute<T>();

            attributes[property].Add(typeof(T), customPropertyGUI);
            return customPropertyGUI;
        }
    }

}
