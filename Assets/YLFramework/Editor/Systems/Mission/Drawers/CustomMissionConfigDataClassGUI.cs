// Adapted from XF Mission. Original copyright and MIT License: Runtime/Systems/Mission/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace YLFramework.MissionSystem
{
    public class CustomMissionConfigDataClassGUI : CustomPropertyGUI
    {

        private string current_mission_type_class_fullname;
        private Type current_mission_type;
        private GUIContent classTypeContent;
        private SerializedProperty mission_type_guid_property;

        public override void OnPropertyGUI(SerializedProperty class_fullname_property)
        {
            GUILayout.Space(10);


            if (current_mission_type_class_fullname != class_fullname_property.stringValue)
            {
                current_mission_type = AssemblyTool.GetType(class_fullname_property.stringValue);
                current_mission_type_class_fullname = class_fullname_property.stringValue;

                mission_type_guid_property = class_fullname_property.serializedObject.FindProperty("mission_type_guid");
            }

            string message = current_mission_type == null ? "无" : current_mission_type.Name;

            if (current_mission_type != null && !string.IsNullOrEmpty(current_mission_type.Namespace))
                message = string.Format("{0}({1})", message, current_mission_type.Namespace);

            if (classTypeContent == null)
                classTypeContent = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));

            classTypeContent.text = message;

            GUILayout.BeginHorizontal();

            GUILayout.Label("任务脚本");

            if (EditorGUILayout.DropdownButton(classTypeContent, FocusType.Passive))
            {
                List<MonoScript> scripts = GetAllMissionTypes();

                GenericMenu generic = new GenericMenu();

                generic.AddItem(new GUIContent("无"), false, () =>
                {
                    mission_type_guid_property.stringValue = string.Empty;
                    class_fullname_property.stringValue = string.Empty;

                    class_fullname_property.serializedObject.ApplyModifiedProperties();
                });

                generic.AddSeparator(string.Empty);

                foreach (var item in scripts)
                {
                    GUIContent content = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));
                    content.text = item.GetClass().Name;
                    if (!string.IsNullOrEmpty(item.GetClass().Namespace))
                        content.text = string.Format("{0}({1})", content.text, item.GetClass().Namespace);

                    bool isOn = class_fullname_property.stringValue == item.GetClass().FullName;

                    generic.AddItem(content, isOn, () =>
                    {

                        mission_type_guid_property.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                        class_fullname_property.stringValue = item.GetClass().FullName;
                        class_fullname_property.serializedObject.ApplyModifiedProperties();

                        AssetDatabase.SaveAssets();
                    });
                }

                generic.ShowAsContext();
            }


            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty( mission_type_guid_property.stringValue));

            if (GUILayout.Button("编辑", GUILayout.Width(50)))
            {
                string guid = GetGUID(mission_type_guid_property.stringValue, class_fullname_property.stringValue);
                OpenAsset(guid);
            }

            if (GUILayout.Button("选中", GUILayout.Width(50)))
            {
                string guid = GetGUID(mission_type_guid_property.stringValue, class_fullname_property.stringValue);
                SelectAsset(guid);
            }

            EditorGUI.EndDisabledGroup();

            GUILayout.EndHorizontal();
        }

        public static List<MonoScript> GetAllMissionTypes()
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

                if (type.IsSubclassOf(typeof(MissionBase)))
                    states.Add(script);
            }

            return states;
        }

        private static string GetGUID(string guid, string class_full_name)
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
            if (string.IsNullOrEmpty(asset_path)) return false;

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(asset_path) == null)
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
