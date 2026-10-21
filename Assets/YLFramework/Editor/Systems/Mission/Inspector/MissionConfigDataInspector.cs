using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


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
                    _instance = CreateInstance<MissionConfigDataInspector>();
                return _instance;
            }
        }

        private MissionConfigData currentMissionConfigData;

        private SerializedObject missionConfigSerializedObject = null;

        private List<SerializedProperty> serializedProperties;

        private Vector2 currentPosition;

        //private SerializedProperty m_Script = null;


        public override void OnInspectorGUI()
        {

            EditorGUI.BeginDisabledGroup(true);
            base.OnInspectorGUI();
            EditorGUI.EndDisabledGroup();

        }


        internal void OnInspectorGUI(MissionConfigData data, ConfigsWindow window,MissionsConfig missionsConfig)
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

            currentPosition = GUILayout.BeginScrollView(currentPosition);

            missionConfigSerializedObject.Update();


            foreach (var property in serializedProperties)
            {

                //if (property.displayName == "Script") continue;

                if (property.name == "parent" && property.objectReferenceValue == null)
                {
                    property.objectReferenceValue = missionsConfig;
                }

                if (GetPropertyCustomAttribute<HideInInspector>(property) != null) continue;
                CustomPropertyGUI customPropertyGUI = GetPropertyCustomAttribute<CustomPropertyGUI>(property) ?? (drawers.TryGetValue(property.name, out var drawer) ? drawer : null);
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
                    missionConfigSerializedObject.ApplyModifiedProperties();

                    if (property.name == "id")
                        window.SetSelection(property.intValue);

                    //EditorUtility.SetDirty(currentMissionConfigData);

                    if (currentMissionConfigData.parent != null)
                        currentMissionConfigData.parent.ClearCache();

                    if (customPropertyGUI != null && customPropertyGUI.reloadTreeViewOnValueChange)
                        window.missionListTree.Reload();

                }

            }

            GUILayout.EndScrollView();
        }

        private Dictionary<string, CustomPropertyGUI> drawers = new();
        private static Dictionary<string, CustomPropertyGUI> CreateDrawers() => new()
        {
            ["id"] = new CustomMissionConfigDataIDGUI(),
            ["parent"] = new CustomMissionConfigDataParentGUI(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGUI(),
            ["conditions"] = new CustomMissionConfigDataCompleteConditionGUI(),
            ["mission_type_class_fullname"] = new CustomMissionConfigDataClassGUI()
        };

        internal void BindSelection(MissionConfigData data)
        {
            if (currentMissionConfigData == data && missionConfigSerializedObject != null) return;
            ReleaseSelection();
            if (!data) return;
            currentMissionConfigData = data;
            missionConfigSerializedObject = new SerializedObject(data);
            serializedProperties = missionConfigSerializedObject.GetAllSerializedProperty();
            drawers = CreateDrawers();
        }

        internal void ReleaseSelection()
        {
            missionConfigSerializedObject?.Dispose();
            missionConfigSerializedObject = null;
            currentMissionConfigData = null;
            serializedProperties = null;
            attributes.Clear();
            drawers.Clear();
        }
        private void OnDisable() => ReleaseSelection();

        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();

        private T GetPropertyCustomAttribute<T>(SerializedProperty property) where T : Attribute
        {
            if (!attributes.ContainsKey(property))
                attributes.Add(property, new Dictionary<Type, Attribute>());

            if (attributes[property].ContainsKey(typeof(T)))
                return attributes[property][typeof(T)] as T;

            T customPropertyGUI = property.GetAttribute<T>();

            attributes[property].Add(typeof(T), customPropertyGUI);
            return customPropertyGUI;
        }

    }

}
