// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace YLFramework.BuffSystem
{

    [CustomEditor(typeof(EffectData))]
    public class EffectDataInspector : UnityEditor.Editor
    {

        private static EffectDataInspector _instance;

        public static EffectDataInspector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = CreateInstance<EffectDataInspector>();
                return _instance;
            }
        }

        private EffectData currentEffectData;

        private SerializedObject currentSerializedObject;

        private List<SerializedProperty> currentSerializeProperty;

        private Vector2 currentPosition;

        private EffectConfigsWindow window;

        private string effect_name;


        private GUIStyle style_dropdown;

        private GUIContent type_content;

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginDisabledGroup(true);

            base.OnInspectorGUI();

            EditorGUI.EndDisabledGroup();

            if (GUILayout.Button("编辑效果"))
            {
                EffectData effectData = (EffectData)target;

                EffectConfigsWindow window = EditorWindow.GetWindow<EffectConfigsWindow>();
                window.currentEffectData = effectData;
                window.RefreshList();
            }
        }

        internal void OnInspectorGUI( EffectConfigsWindow window, EffectData data)
        {
            this.window = window;

            if (data != null)
            {
                if (currentEffectData != data)
                {
                    currentEffectData = data;
                    currentSerializedObject = null;
                }

                if (currentSerializedObject == null)
                {
                    currentSerializedObject = new SerializedObject(this.currentEffectData);
                    currentSerializeProperty = currentSerializedObject.GetAllSerializedProperty();
                }


                currentSerializedObject.Update();

                currentPosition = GUILayout.BeginScrollView(currentPosition);



                for (int i = 0; i < currentSerializeProperty.Count; i++)
                {
                    SerializedProperty property = currentSerializeProperty[i];

                    //if (property.displayName == "Script")
                    //    continue;

                    if (property.name == "id")
                    {
                        OnInspectorId();
                        continue;
                    }

                    if (property.name == "effect_name")
                    {
                        OnInspectorName();
                        continue;
                    }

                    if (property.name == "type")
                    {
                        OnTypeInspector(property);
                        continue;

                    }

                    if (property.IsScript())
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.LabelField(string.Empty, "类型", "BoldLabel");
                        EditorGUILayout.PropertyField(property);
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(property);
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
                GUILayout.Label("空");
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
        }


        private void OnInspectorName()
        {
            effect_name = currentEffectData.effect_name;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(string.Empty, "名称", "BoldLabel");
            effect_name = EditorGUILayout.DelayedTextField("EffectName",effect_name);
            if (EditorGUI.EndChangeCheck() )
            {
                foreach (var item in BuffSystemSettings.Instance.AllEffectDatas)
                {
                    if (!item) continue;
                    if (item.effect_name.Equals(effect_name))
                    {
                        window.ShowNotification(new GUIContent("效果名称重复!"));
                        return;
                    }
                }

                currentEffectData.effect_name = effect_name;
                EditorUtility.SetDirty(currentEffectData);
                AssetDatabase.SaveAssets();
                AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(currentEffectData), effect_name);
                window.RefreshList();
            }
        }

        private void OnInspectorId()
        {
            int id = currentEffectData.id;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(string.Empty, "唯一标识", "BoldLabel");
            id = EditorGUILayout.DelayedIntField("Id", id);
            if (EditorGUI.EndChangeCheck() )
            {
                if (id == 0)
                {
                    window.ShowNotification(new GUIContent("id不能为0!"));
                }
                else
                {
                    if (IsContains(id))
                    {
                        window.ShowNotification(new GUIContent("效果Id重复!"));
                    }
                    else
                    {
                        currentEffectData.id = id;
                        EditorUtility.SetDirty(currentEffectData);
                        AssetDatabase.SaveAssets();
                        window.RefreshList();
                    }

                }

            }
        }

        private bool IsContains(int id)
        {
            foreach (var item in BuffSystemSettings.Instance.AllEffectDatas)
            {
                if (!item) continue;
                if (item.id.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }

        private void OnTypeInspector(SerializedProperty property)
        {

            if (style_dropdown == null)
            {
                style_dropdown = new GUIStyle("DropDown");
                style_dropdown.richText = true;
            }

            if (type_content == null)
                type_content = new GUIContent();

            EditorGUILayout.LabelField(string.Empty, "类型", "BoldLabel");

            type_content.text = BuffSystemSettings.Instance.GetType(property.intValue);

            GUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(string.Empty, "Type",GUILayout.Width(148));

            if (EditorGUILayout.DropdownButton(type_content, FocusType.Passive, style_dropdown))
            {
                GenericMenu menu = new GenericMenu();

                foreach (var type in BuffSystemSettings.Instance.AllEffectType)
                {
                    GUIContent content = new GUIContent(BuffSystemSettings.Instance.GetType(type.type, false));

                    menu.AddItem(content, property.intValue == type.type, () =>
                    {
                        property.intValue = type.type;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }

                if (BuffSystemSettings.Instance.AllEffectType.Count > 0)
                    menu.AddSeparator(string.Empty);

                menu.AddItem(new GUIContent("编辑"), false, () =>
                {
                    EffectListTree.OpenProjectSettings("YLFramework/Buff");
                });

                menu.ShowAsContext();
            }


            GUILayout.EndHorizontal();

        }


    }

}
