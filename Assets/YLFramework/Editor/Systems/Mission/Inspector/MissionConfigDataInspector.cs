using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;


namespace YLFramework.MissionSystem
{
    [CustomEditor(typeof(MissionConfigData))]
    public class MissionConfigDataInspector : UnityEditor.Editor
    {

        private static MissionConfigDataInspector _instance;

        public static MissionConfigDataInspector Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<MissionConfigDataInspector>();
                }

                return _instance;
            }
        }

        private MissionConfigData _currentMissionConfigData;

        private SerializedObject _missionConfigSerializedObject = null;

        private List<SerializedProperty> _serializedProperties;

        private Vector2 _currentPosition;

        //private SerializedProperty m_Script = null;


        public override void OnInspectorGUI()
        {

            EditorGUI.BeginDisabledGroup(true);
            base.OnInspectorGUI();
            EditorGUI.EndDisabledGroup();

        }


        internal void OnInspectorGUI(MissionConfigData data, ConfigsWindow window, MissionsConfig missionsConfig)
        {
            if (data == null)
            {
                ReleaseSelection();
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("空");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();

                return;
            }

            BindSelection(data);

            _currentPosition = GUILayout.BeginScrollView(_currentPosition);

            _missionConfigSerializedObject.Update();


            foreach (var property in _serializedProperties)
            {

                //if (property.displayName == "Script") continue;

                if (property.name == "parent" && property.objectReferenceValue == null)
                {
                    property.objectReferenceValue = missionsConfig;
                }

                if (GetPropertyCustomAttribute<HideInInspector>(property) != null)
                {
                    continue;
                }

                CustomPropertyGui customPropertyGui = GetPropertyCustomAttribute<CustomPropertyGui>(property) ?? (_drawers.TryGetValue(property.name, out var drawer) ? drawer : null);
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
                    _missionConfigSerializedObject.ApplyModifiedProperties();

                    if (property.name == "id")
                    {
                        window.SetSelection(property.intValue);
                    }

                    //EditorUtility.SetDirty(currentMissionConfigData);

                    if (_currentMissionConfigData.parent != null)
                    {
                        _currentMissionConfigData.parent.ClearCache();
                    }

                    if (customPropertyGui != null && customPropertyGui.ReloadTreeViewOnValueChange)
                    {
                        window.MissionListTree.Reload();
                    }
                }

            }

            GUILayout.EndScrollView();
        }

        private Dictionary<string, CustomPropertyGui> _drawers = new();
        private static Dictionary<string, CustomPropertyGui> CreateDrawers() => new()
        {
            ["id"] = new CustomMissionConfigDataIdGui(),
            ["parent"] = new CustomMissionConfigDataParentGui(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGui(),
            ["conditions"] = new CustomMissionConfigDataCompleteConditionGui(),
            ["missionTypeClassFullName"] = new CustomMissionConfigDataClassGui()
        };

        internal void BindSelection(MissionConfigData data)
        {
            if (_currentMissionConfigData == data && _missionConfigSerializedObject != null)
            {
                return;
            }

            ReleaseSelection();
            if (!data)
            {
                return;
            }

            _currentMissionConfigData = data;
            _missionConfigSerializedObject = new SerializedObject(data);
            _serializedProperties = _missionConfigSerializedObject.GetAllSerializedProperty();
            _drawers = CreateDrawers();
        }

        internal void ReleaseSelection()
        {
            _missionConfigSerializedObject?.Dispose();
            _missionConfigSerializedObject = null;
            _currentMissionConfigData = null;
            _serializedProperties = null;
            _attributes.Clear();
            _drawers.Clear();
        }
        private void OnDisable() => ReleaseSelection();

        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> _attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();

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

            T customPropertyGui = property.GetAttribute<T>();

            _attributes[property].Add(typeof(T), customPropertyGui);
            return customPropertyGui;
        }

    }

}
