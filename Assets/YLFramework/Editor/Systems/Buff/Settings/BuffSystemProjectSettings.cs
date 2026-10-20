// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;

using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.BuffSystem
{

    public class BuffSettingsProvider : SettingsProvider
    {

        private static BuffSettingsProvider instance;

        public static BuffSettingsProvider Instance
        {
            get
            {
                if (instance == null)
                    instance = new BuffSettingsProvider("YLFramework/Buff", SettingsScope.Project);

                return instance;
            }
        }

        public BuffSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) : base(path, scopes, keywords)
        {
        }

        private SerializedObject serializedObject;

        private List<SerializedProperty> serializedProperties;

        public override void OnGUI(string searchContext)
        {
            //base.OnGUI(searchContext);

            if (serializedObject == null)
                serializedObject = new SerializedObject(BuffSystemSettings.Instance);

            if (serializedProperties == null)
                serializedProperties = serializedObject.GetAllSerializedProperty();


            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            foreach (var item in serializedProperties)
            {
                if (item.name == "allEffectDatas")
                    continue;

                if (item.name == "allEffectTypes")
                {
                    OnTypeGUI(item);
                    continue;
                }

                EditorGUI.BeginDisabledGroup(true);

                EditorGUILayout.PropertyField(item);

                EditorGUI.EndDisabledGroup();
            }


            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                BuffSystemSettings.Save();
            }
        }

        private ReorderableList reorderableListType;



        private void OnTypeGUI(SerializedProperty property)
        {

            if (reorderableListType == null)
            {
                reorderableListType = new ReorderableList(property.serializedObject, property);
                reorderableListType.draggable = false;
                reorderableListType.drawElementCallback += ElementCallbackDelegate;
                reorderableListType.onAddCallback += AddCallbackDelegate;
                reorderableListType.drawHeaderCallback += HeaderCallbackDelegate;
            }

            reorderableListType.DoLayoutList();
        }


        private void ElementCallbackDelegate(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= reorderableListType.serializedProperty.arraySize)
                return;

            SerializedProperty property = reorderableListType.serializedProperty.GetArrayElementAtIndex(index);

            SerializedProperty type = property.FindPropertyRelative("type");
            SerializedProperty name = property.FindPropertyRelative("name");

            rect.width = rect.width / 2;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.IntField(rect, type.intValue);
            EditorGUI.EndDisabledGroup();

            rect.x+=rect.width;
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
