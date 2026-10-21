using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YLFramework.SkillSystem
{

    public class CustomSkillConfigDataSkillClassGUI : CustomPropertyGUI
    {


        //private SkillConfigData currentSkillConfigData;

        private string current_skill_class_full_name;

        private Type current_skill_class_type;

        private SerializedProperty serializedProperty = null;

        private GUIContent classTypeContent = null;

        public override void OnPropertyGUI(SerializedProperty skill_class_full_name_property)
        {
            if (this.serializedProperty != skill_class_full_name_property)
            {
                this.serializedProperty = skill_class_full_name_property;
            }



            if (current_skill_class_full_name != skill_class_full_name_property.stringValue)
            {
                current_skill_class_type = AssemblyTool.GetType(skill_class_full_name_property.stringValue);
                current_skill_class_full_name = skill_class_full_name_property.stringValue;
            }


            string message = current_skill_class_type == null ? "无" : current_skill_class_type.Name;

            if (current_skill_class_type != null && !string.IsNullOrEmpty(current_skill_class_type.Namespace))
                message = string.Format("{0}({1})", message, current_skill_class_type.Namespace);

            if (classTypeContent == null)
                classTypeContent = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));

            classTypeContent.text = message;

            GUILayout.BeginHorizontal();

            GUILayout.Label("技能脚本");

            if (EditorGUILayout.DropdownButton(classTypeContent, FocusType.Passive))
            {
                List<MonoScript> scripts = GetAllStatesType();

                if (scripts.Count != 0)
                {
                    GenericMenu generic = new GenericMenu();

                    foreach (var item in scripts)
                    {
                        GUIContent content = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));
                        content.text = item.GetClass().Name;
                        if (!string.IsNullOrEmpty(item.GetClass().Namespace))
                            content.text = string.Format("{0}({1})", content.text, item.GetClass().Namespace);

                        bool isOn = skill_class_full_name_property.stringValue == item.GetClass().FullName;

                        generic.AddItem(content, isOn, () =>
                        {
                            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));

                            if (string.IsNullOrEmpty(guid)) return;

                            SerializedProperty skill_class_guid_property = skill_class_full_name_property.serializedObject.FindProperty("skill_class_guid");

                            skill_class_guid_property.stringValue = guid;
                            skill_class_full_name_property.stringValue = item.GetClass().FullName;
                            skill_class_full_name_property.serializedObject.ApplyModifiedProperties();
                            AssetDatabase.SaveAssets();
                        });
                    }

                    generic.ShowAsContext();
                }
                else
                {
                    string m = string.Format("未在项目中发现技能脚本,请创建脚本并继承自{0}用来处理技能逻辑!", typeof(SkillBase).FullName);
                    Debug.LogWarning(m);
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(m));
                }

            }

            EditorGUI.BeginDisabledGroup(current_skill_class_type == null);

            if (GUILayout.Button("编辑", GUILayout.Width(50)))
            {
                SerializedProperty skill_class_guid_property = skill_class_full_name_property.serializedObject.FindProperty("skill_class_guid");


                string guid = GetGUID(skill_class_guid_property.stringValue, skill_class_full_name_property.stringValue);

                OpenAsset(guid);
            }

            if (GUILayout.Button("选中", GUILayout.Width(50)))
            {
                SerializedProperty skill_class_guid_property = skill_class_full_name_property.serializedObject.FindProperty("skill_class_guid");

                string guid = GetGUID(skill_class_guid_property.stringValue, skill_class_full_name_property.stringValue);
                SelectAsset(guid);
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();

        }

        public static List<MonoScript> GetAllStatesType()
        {
            List<MonoScript> states = new List<MonoScript>();

            string[] scripts = AssetDatabase.FindAssets("t:Script");

            foreach (var item in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(item);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script == null) continue;

                Type type = script.GetClass();

                if (type == null) continue;

                if (type.IsSubclassOf(typeof(SkillBase)))
                    states.Add(script);
            }

            return states;
        }


        private static string GetGUID(string guid,string class_full_name)
        {
            if (!IsValid(guid) && !string.IsNullOrEmpty(class_full_name))
            {
                string[] paths = AssetDatabase.FindAssets("t:Script");
                foreach (var item in paths)
                {
                    string asset_path = AssetDatabase.GUIDToAssetPath(item);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(asset_path);
                    if (script != null && script.GetClass() != null && script.GetClass().FullName.Equals(class_full_name))
                    {
                        return item;
                    }
                }
            }

            return guid;
        }


        private static bool IsValid(string guid)
        {
            string asset_path = AssetDatabase.GUIDToAssetPath(guid);
            if(string.IsNullOrEmpty(asset_path)) return false;

            if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset_path) == null)
                return false;

            return true;
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


    }

}

#endif
