// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;


namespace YLFramework.BuffSystem
{

    [CustomEditor(typeof(BuffConfigData), true)]
    public class BuffConfigDataInspector : UnityEditor.Editor
    {

        private static BuffConfigDataInspector _instance;

        public static BuffConfigDataInspector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = CreateInstance<BuffConfigDataInspector>();
                return _instance;
            }
        }

        private BuffConfigData currentBuffConfigData;

        private SerializedObject currentSerializedObject;

        private List<SerializedProperty> currentSerializeProperty;

        private Vector2 currentPosition;

        //private SerializedProperty script = null;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);

            base.OnInspectorGUI();

            EditorGUI.EndDisabledGroup();
        }

        internal void OnInspectorGUI(BuffConfigData data, BuffConfigsWindow window, BuffsConfig currentBuffConfig)
        {

            if (data != null)
            {
                if (currentBuffConfigData != data)
                {
                    currentSerializedObject?.Dispose();
                    attributes.Clear();
                    currentBuffConfigData = data;
                    currentSerializedObject = null;
                }

                if (currentSerializedObject == null)
                {
                    currentSerializedObject = new SerializedObject(currentBuffConfigData);
                    currentSerializeProperty = currentSerializedObject.GetAllSerializedProperty();
                }

                currentSerializedObject.Update();

                currentPosition = GUILayout.BeginScrollView(currentPosition);

                for (int i = 0; i < currentSerializeProperty.Count; i++)
                {

                    SerializedProperty property = currentSerializeProperty[i];

                    if (property.name == "parent" && property.objectReferenceValue == null) {
                        property.objectReferenceValue = currentBuffConfig;
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
                        currentSerializedObject.ApplyModifiedProperties();

                        if (property.name == "buff_id")
                            window.SetSelection(property.intValue);

                        if(currentBuffConfigData.parent != null)
                            currentBuffConfigData.parent.ClearCache();

                        if (customPropertyGUI != null && customPropertyGUI.reloadTreeViewOnValueChange)
                            window.buffListTree.Reload();
                    }
                }

                GUILayout.EndScrollView();

                currentSerializedObject.ApplyModifiedProperties();
            }
            else
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("未查询到Buff,id:{0}!", window.currentShowBuffId));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
        }


        private readonly Dictionary<string, CustomPropertyGUI> drawers = new()
        {
            ["buff_id"] = new CustomBuffConfigDataIDGUI(),
            ["parent"] = new CustomBuffConfigDataParentGUI(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGUI(),
            ["effects"] = new CustomBuffConfigDataEffectsGUI(),
            ["buff_class_full_name"] = new CustomBuffConfigDataClassGUI()
        };

        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();
        internal void ReleaseSelection()
        {
            currentSerializedObject?.Dispose();
            currentSerializedObject = null;
            currentBuffConfigData = null;
            currentSerializeProperty = null;
            attributes.Clear();
        }
        private void OnDisable() => ReleaseSelection();

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
