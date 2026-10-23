using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using YLFramework.Core.Editor;
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;

namespace YLFramework.BuffSystem
{

    public class BuffSettingsProvider : SettingsProvider
    {

        private static BuffSettingsProvider _instance;

        public static BuffSettingsProvider Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new BuffSettingsProvider("YLFramework/Buff", SettingsScope.Project);
                }

                return _instance;
            }
        }

        public BuffSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        private SerializedObject _serializedObject;

        private List<SerializedProperty> _serializedProperties;

        public override void OnGUI(string searchContext)
        {
            //base.OnGUI(searchContext);

            if (_serializedObject == null)
            {
                _serializedObject = new SerializedObject(BuffSystemSettings.Instance);
            }

            if (_serializedProperties == null)
            {
                _serializedProperties = _serializedObject.GetAllSerializedProperty();
            }

            _serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            foreach (var item in _serializedProperties)
            {
                if (item.name == "allEffectDatas")
                {
                    continue;
                }

                if (item.name == "allEffectTypes")
                {
                    OnTypeGui(item);
                    continue;
                }

                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.PropertyField(item);

                EditorGUI.EndDisabledGroup();
            }


            _serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                BuffSystemSettings.Save();
            }
        }

        private ReorderableList _reorderableListType;



        private void OnTypeGui(SerializedProperty property)
        {

            if (_reorderableListType == null)
            {
                _reorderableListType = new ReorderableList(property.serializedObject, property);
                _reorderableListType.draggable = false;
                _reorderableListType.drawElementCallback += ElementCallbackDelegate;
                _reorderableListType.onAddCallback += AddCallbackDelegate;
                _reorderableListType.drawHeaderCallback += HeaderCallbackDelegate;
            }

            _reorderableListType.DoLayoutList();
        }


        private void ElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _reorderableListType.serializedProperty.arraySize)
            {
                return;
            }

            SerializedProperty property = _reorderableListType.serializedProperty.GetArrayElementAtIndex(index);

            SerializedProperty type = property.FindPropertyRelative("type");
            SerializedProperty name = property.FindPropertyRelative("name");

            rect.width = rect.width / 2;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.IntField(rect, type.intValue);
            EditorGUI.EndDisabledGroup();

            rect.x += rect.width;
            name.stringValue = EditorGUI.TextField(rect, name.stringValue);

        }


        private void AddCallbackDelegate(ReorderableList list)
        {
            int index = list.serializedProperty.arraySize;
            list.serializedProperty.InsertArrayElementAtIndex(index);
            SerializedProperty property = list.serializedProperty.GetArrayElementAtIndex(index);
            property.FindPropertyRelative("type").intValue = index + 1;
        }

        private void HeaderCallbackDelegate(Rect rect)
        {
            rect.width /= 2;
            GUI.Label(rect, "类型");

            rect.x += rect.width;
            GUI.Label(rect, "名称");
        }

    }



    public static class BuffSystemProjectSettings
    {

        [SettingsProvider]
        public static SettingsProvider Settings()
        {
            return BuffSettingsProvider.Instance;
        }

    }




}
