using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;


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
                {
                    _instance = CreateInstance<BuffConfigDataInspector>();
                }

                return _instance;
            }
        }

        private BuffConfigData _currentBuffConfigData;

        private SerializedObject _currentSerializedObject;

        private List<SerializedProperty> _currentSerializeProperty;

        private Vector2 _currentPosition;

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
                BindSelection(data);

                _currentSerializedObject.Update();

                _currentPosition = GUILayout.BeginScrollView(_currentPosition);

                for (int i = 0; i < _currentSerializeProperty.Count; i++)
                {

                    SerializedProperty property = _currentSerializeProperty[i];

                    if (property.name == "parent" && property.objectReferenceValue == null)
                    {
                        property.objectReferenceValue = currentBuffConfig;
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
                        _currentSerializedObject.ApplyModifiedProperties();

                        if (property.name == "id")
                        {
                            window.SetSelection(property.intValue);
                        }

                        if (_currentBuffConfigData.parent != null)
                        {
                            _currentBuffConfigData.parent.ClearCache();
                        }

                        if (customPropertyGui != null && customPropertyGui.ReloadTreeViewOnValueChange)
                        {
                            window.BuffListTree.Reload();
                        }
                    }
                }

                GUILayout.EndScrollView();

                _currentSerializedObject.ApplyModifiedProperties();
            }
            else
            {
                ReleaseSelection();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label(string.Format("未查询到Buff,id:{0}!", window.CurrentShowBuffId));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
        }


        private Dictionary<string, CustomPropertyGui> _drawers = new();
        private static Dictionary<string, CustomPropertyGui> CreateDrawers() => new()
        {
            ["id"] = new CustomBuffConfigDataIdGui(),
            ["parent"] = new CustomBuffConfigDataParentGui(),
            ["icon"] = new YLFramework.Editor.Domain.DomainResourceIconGui(),
            ["effects"] = new CustomBuffConfigDataEffectsGui(),
            ["buffClassFullName"] = new CustomBuffConfigDataClassGui()
        };

        private Dictionary<SerializedProperty, Dictionary<Type, Attribute>> _attributes = new Dictionary<SerializedProperty, Dictionary<Type, Attribute>>();
        internal void BindSelection(BuffConfigData data)
        {
            if (_currentBuffConfigData == data && _currentSerializedObject != null)
            {
                return;
            }

            ReleaseSelection();
            if (!data)
            {
                return;
            }

            _currentBuffConfigData = data;
            _currentSerializedObject = new SerializedObject(data);
            _currentSerializeProperty = _currentSerializedObject.GetAllSerializedProperty();
            _drawers = CreateDrawers();
        }
        internal void ReleaseSelection()
        {
            _currentSerializedObject?.Dispose();
            _currentSerializedObject = null;
            _currentBuffConfigData = null;
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

            T customPropertyGui = property.GetAttribute<T>();

            _attributes[property].Add(typeof(T), customPropertyGui);
            return customPropertyGui;
        }

    }

}
