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
    public class CustomMissionConfigDataClassGui : CustomPropertyGui
    {

        private string _currentMissionTypeClassFullname;
        private Type _currentMissionType;
        private GUIContent _classTypeContent;
        private SerializedProperty _missionTypeGuidProperty;

        public override void OnPropertyGui(SerializedProperty classFullNameProperty)
        {
            _missionTypeGuidProperty = classFullNameProperty.serializedObject.FindProperty("missionTypeGuid");
            GUILayout.Space(10);


            if (_currentMissionTypeClassFullname != classFullNameProperty.stringValue)
            {
                _currentMissionType = AssemblyTool.GetType(classFullNameProperty.stringValue);
                _currentMissionTypeClassFullname = classFullNameProperty.stringValue;

            }

            string message = _currentMissionType == null ? "无" : _currentMissionType.Name;

            if (_currentMissionType != null && !string.IsNullOrEmpty(_currentMissionType.Namespace))
            {
                message = string.Format("{0}({1})", message, _currentMissionType.Namespace);
            }

            if (_classTypeContent == null)
            {
                _classTypeContent = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));
            }

            _classTypeContent.text = message;

            GUILayout.BeginHorizontal();

            GUILayout.Label("任务脚本");

            if (EditorGUILayout.DropdownButton(_classTypeContent, FocusType.Passive))
            {
                List<MonoScript> scripts = GetAllMissionTypes();

                GenericMenu generic = new GenericMenu();

                generic.AddItem(new GUIContent("无"), false, () =>
                {
                    _missionTypeGuidProperty.stringValue = string.Empty;
                    classFullNameProperty.stringValue = string.Empty;

                    classFullNameProperty.serializedObject.ApplyModifiedProperties();
                });

                generic.AddSeparator(string.Empty);

                foreach (var item in scripts)
                {
                    GUIContent content = new GUIContent(EditorGUIUtility.IconContent("d_cs Script Icon"));
                    content.text = item.GetClass().Name;
                    if (!string.IsNullOrEmpty(item.GetClass().Namespace))
                    {
                        content.text = string.Format("{0}({1})", content.text, item.GetClass().Namespace);
                    }

                    bool isOn = classFullNameProperty.stringValue == item.GetClass().FullName;

                    generic.AddItem(content, isOn, () =>
                    {

                        _missionTypeGuidProperty.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item));
                        classFullNameProperty.stringValue = item.GetClass().FullName;
                        classFullNameProperty.serializedObject.ApplyModifiedProperties();

                        AssetDatabase.SaveAssets();
                    });
                }

                generic.ShowAsContext();
            }


            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_missionTypeGuidProperty.stringValue));

            if (GUILayout.Button("编辑", GUILayout.Width(50)))
            {
                string guid = GetGuid(_missionTypeGuidProperty.stringValue, classFullNameProperty.stringValue);
                OpenAsset(guid);
            }

            if (GUILayout.Button("选中", GUILayout.Width(50)))
            {
                string guid = GetGuid(_missionTypeGuidProperty.stringValue, classFullNameProperty.stringValue);
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

                if (script == null)
                {
                    continue;
                }

                Type type = script.GetClass();

                if (type == null)
                {
                    continue;
                }

                if (type.IsSubclassOf(typeof(MissionBase)))
                {
                    states.Add(script);
                }
            }

            return states;
        }

        private static string GetGuid(string guid, string classFullName)
        {
            if (!IsValid(guid) && !string.IsNullOrEmpty(classFullName))
            {
                string[] paths = AssetDatabase.FindAssets("t:Script");
                foreach (var item in paths)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(item);
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                    if (script != null && script.GetClass() != null && script.GetClass().FullName.Equals(classFullName))
                    {
                        return item;
                    }
                }
            }

            return guid;
        }


        private static bool IsValid(string guid)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) == null)
            {
                return false;
            }

            return true;
        }

        public static void OpenAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object)));
        }


        public static void SelectAsset(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(assetPath, typeof(UnityEngine.Object));
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }


    }

}

#endif
