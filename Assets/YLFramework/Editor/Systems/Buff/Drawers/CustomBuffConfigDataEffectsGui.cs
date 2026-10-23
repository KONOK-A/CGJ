using YLFramework.Foundation;
using YLFramework.Foundation.Attributes;
using YLFramework.Core.Editor;
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityEngine;


namespace YLFramework.BuffSystem
{
    public class CustomBuffConfigDataEffectsGui : CustomPropertyGui
    {


        private bool _showEffects = true;
        private ReorderableList _effectReorderableList = null;

        private Dictionary<int, GUIContent> _contents = new Dictionary<int, GUIContent>();


        private GUIStyle _styleName;


        public override void OnPropertyGui(SerializedProperty effectsProperty)
        {

            GUILayout.Space(10);



            EditorGUILayout.LabelField(string.Empty, "Buff效果(当前Buff有哪些效果)", "BoldLabel");
            _showEffects = EditorGUILayout.Foldout(_showEffects, "Effects");
            if (_showEffects)
            {
                if (_effectReorderableList == null)
                {
                    _effectReorderableList = new ReorderableList(effectsProperty.serializedObject, effectsProperty, true, true, true, true);
                    _effectReorderableList.headerHeight = 20;
                    _effectReorderableList.drawElementCallback += DrawElementCallback;
                    _effectReorderableList.drawHeaderCallback += DrawHeaderCallback;
                    //effectReorderableList.onAddCallback += OnAddCallback;
                }

                //if(effectReorderableList != null)
                _effectReorderableList.DoLayoutList();
            }

        }

        private void DrawHeaderCallback(Rect rect)
        {
            float w = (rect.width - 15) / 6;

            rect.x += 15;
            rect.width = w * 0.7f;

            GUI.Label(rect, "ID");


            rect.x += rect.width;
            rect.width = w;
            GUI.Label(rect, "名称");

            rect.x += rect.width;

            GUI.Label(rect, "类型");

            rect.x += rect.width;
            rect.width = w * 1.5f;
            GUI.Label(rect, "效果分类");

            rect.x += rect.width;

            GUI.Label(rect, "描述");

        }

        private void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {

            if (index < 0 || index >= _effectReorderableList.serializedProperty.arraySize)
            {
                return;
            }

            SerializedProperty property = _effectReorderableList.serializedProperty.GetArrayElementAtIndex(index);

            EffectData data = property.objectReferenceValue as EffectData;

            float w = rect.width / 6;

            if (data != null)
            {
                rect.width = w * 0.7f;

                GUI.Label(rect, data.id.ToString());

                rect.x += rect.width;
                rect.width = w;
                GUI.Label(rect, data.effectName);

                string t = BuffSystemSettings.Instance.GetType(data.type);
                rect.x += rect.width;
                GUI.Label(rect, t);

                rect.x += rect.width;
                rect.width = w * 1.5f;


                if (!_contents.ContainsKey(index))
                {
                    _contents.Add(index, new GUIContent());
                }

                GUIContent content = _contents[index];

                content.text = GetEffectData(data);
                content.tooltip = content.text;

                GUI.Label(rect, content);


                rect.x += rect.width;
                rect.width = w;
                string des = data.description.Replace('\n', '\0');

                content.text = des;
                content.tooltip = content.text;

                GUI.Label(rect, content);
            }
            else
            {
                GUI.Label(rect, "空");

                rect.width = w * 0.7f;

                rect.x += rect.width;
                rect.width = w;

                rect.x += rect.width;
                rect.x += rect.width;
                rect.width = w * 1.5f;

                rect.x += rect.width;
                rect.width = w;
            }


            rect.x += rect.width;
            rect.width = w * 0.8f * 0.5f;

            if (_styleName == null)
            {
                _styleName = GUI.skin.GetStyle("DropDown");
                _styleName.richText = true;
            }

            if (GUI.Button(rect, "选择"))
            {
                PopupWindow.Show(rect, new SelectEffectPopWindow(property));
            }

            rect.x += rect.width;
            //rect.width = 35;

            EditorGUI.BeginDisabledGroup(data == null);

            if (GUI.Button(rect, "编辑"))
            {
                var window = EditorWindow.GetWindow<EffectConfigsWindow>();
                window.Show();
                window.CurrentEffectData = data;
                window.RefreshList();
            }

            EditorGUI.EndDisabledGroup();

        }

        private string GetEffectData(EffectData effect)
        {

            if (effect.GetType().GetCustomAttribute<EffectName>() != null)
            {
                return effect.GetType().GetCustomAttribute<EffectName>().Name;
            }

            return effect.GetType().FullName;
        }

    }
}

#endif
