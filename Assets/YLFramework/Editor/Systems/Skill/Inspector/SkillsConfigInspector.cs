// Adapted from XF Skill. Original copyright and MIT License: Runtime/Systems/Skill/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using YLFramework.Excel;

namespace YLFramework.SkillSystem
{

    [CustomEditor(typeof(SkillsConfig))]
    public class SkillsConfigInspector : UnityEditor.Editor
    {

        private List<SerializedProperty> serializedProperties = null;

        //private SerializedProperty m_Script = null;

        private void OnEnable()
        {
            serializedProperties = serializedObject.GetAllSerializedProperty();
            //m_Script = serializedObject.FindProperty("m_Script");
        }



        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            //EditorGUI.BeginDisabledGroup(true);
            //EditorGUILayout.PropertyField(m_Script);
            //EditorGUI.EndDisabledGroup();

            foreach (var item in serializedProperties)
            {
                //if (item.name == "Script") continue;

                if (item.name == "type_script_guid") continue;

                EditorGUI.BeginDisabledGroup(item.name != "displayName");
                EditorGUILayout.PropertyField(item);
                EditorGUI.EndDisabledGroup();
            }

            OnGUIDataType();

            if (GUILayout.Button("编辑技能配表"))
            {
                OpenSkillsConfig(target as SkillsConfig);
            }

            serializedObject.ApplyModifiedProperties();

        }



        public static void OpenSkillsConfig(SkillsConfig skillsConfig)
        {
            if (skillsConfig == null) return;

            ConfigsWindow window = EditorWindow.GetWindow<ConfigsWindow>();
            window.Show();
            window.SetCurrentConfigs(skillsConfig);
        }



        #region 配置类型

        private GUIContent classTypeContent;

        private List<MonoScript> configDatas;

        private GUIStyle dropdown_style;

        private GUIStyle labelStyle;

        // 绘制数据类型
        private void OnGUIDataType()
        {
            SkillsConfig skillsConfig = target as SkillsConfig;

            if (skillsConfig == null) return;

            GUILayout.Space(10);

            Type type = null;

            if (!string.IsNullOrEmpty(skillsConfig.type_script_guid))
            {
                string asset_path = AssetDatabase.GUIDToAssetPath(skillsConfig.type_script_guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(asset_path);
                Type t = script ? script.GetClass() : null;
                if(t != null && (t.IsSubclassOf(typeof(SkillConfigData)) || t == typeof(SkillConfigData)))
                    type = t;
            }

            string message = type == null ? "空" : GetDisplayName(type);

            if (classTypeContent == null)
                classTypeContent = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));

            classTypeContent.text = message;

            GUILayout.BeginHorizontal();

            GUILayout.Label("数据类型");

            GUILayout.Space(50);

            if (dropdown_style == null)
            {
                dropdown_style = new GUIStyle("DropDown");
                dropdown_style.richText = true;
            }

            if (EditorGUILayout.DropdownButton(classTypeContent, FocusType.Passive, dropdown_style, GUILayout.MinWidth(0)))
            {
                List<MonoScript> scripts = GetAllConfigDatas();

                GenericMenu generic = new GenericMenu();

                generic.AddItem(new GUIContent("空"), false, () =>
                {
                    skillsConfig.type_script_guid = string.Empty;
                    EditorUtility.SetDirty(skillsConfig);
                    AssetDatabase.SaveAssets();
                });

                generic.AddSeparator(string.Empty);

                foreach (var item in scripts)
                {
                    GUIContent content = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));
                    content.text = item.GetClass().Name;
                    if (!string.IsNullOrEmpty(item.GetClass().Namespace))
                        content.text = string.Format("{0}({1})", content.text, item.GetClass().Namespace);

                    string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));

                    bool isOn = skillsConfig.type_script_guid == guid;

                    generic.AddItem(content, isOn, () =>
                    {
                        skillsConfig.type_script_guid = guid;
                        EditorUtility.SetDirty(skillsConfig);
                        AssetDatabase.SaveAssets();
                    });
                }

                generic.ShowAsContext();

            }

            GUILayout.EndHorizontal();


            if (labelStyle == null)
            {
                labelStyle = new GUIStyle("WordWrappedLabel");
                labelStyle.normal.textColor = Color.grey;
                labelStyle.fontSize = 11;
            }

            GUILayout.Label("说明:如果数据类型为空,则表示该配表中可以创建任意类型的数据。如果不为空,该配表则仅能创建您指定的类型及其子类型的数据!", labelStyle);


            GUILayout.BeginHorizontal();


            EditorGUI.BeginDisabledGroup(type == null);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("编辑", GUILayout.Width(50)))
            {
                OpenAsset(skillsConfig.type_script_guid);
            }

            if (GUILayout.Button("选中", GUILayout.Width(50)))
            {
                SelectAsset(skillsConfig.type_script_guid);
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal ();

            GUILayout.Space(10);

        }

        private string GetDisplayName(Type type) {

            if(string.IsNullOrEmpty(type.Namespace))
                return type.Name;

            return string.Format("{0}<color=grey>({1})</color>",type.Name,type.Namespace);
        }

        private List<MonoScript> GetAllConfigDatas()
        {
            if( configDatas == null)
                configDatas = new List<MonoScript>();

            configDatas.Clear();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(typeof(SkillConfigData)) )
                {
                    configDatas.Add(script);
                }
            }

            return configDatas;
        }

        public static void OpenAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            string asset_path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(asset_path)) return;
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath(asset_path, typeof(UnityEngine.Object)));
        }

        public static void SelectAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return;
            string asset_path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(asset_path)) return;

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(asset_path, typeof(UnityEngine.Object));
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        #endregion

    }
}
