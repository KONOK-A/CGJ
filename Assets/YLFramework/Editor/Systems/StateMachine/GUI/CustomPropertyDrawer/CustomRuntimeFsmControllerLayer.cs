using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{

    [CustomPropertyDrawer(typeof(RuntimeFsmControllerLayer))]
    public class CustomRuntimeFsmControllerLayer : PropertyDrawer
    {

        private GUIContent _empty;

        private int _count;

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect.height = base.GetPropertyHeight(property, label);

            var name = property.FindPropertyRelative("name");
            var controller = property.FindPropertyRelative("controller");

            FsmController fsmController = property.serializedObject.targetObject as FsmController;

            if (fsmController.RuntimeFsmControllerLayers.Count != _count)
            {
                _count = fsmController.RuntimeFsmControllerLayers.Count;

                // 检测是否有重复名称
                for (int i = _count - 1; i >= 0; i--)
                {

                    bool isRepeat = false;

                    for (int j = 0; j < _count; j++)
                    {
                        if (i == j)
                        {
                            continue;
                        }

                        if (fsmController.RuntimeFsmControllerLayers[i].name == fsmController.RuntimeFsmControllerLayers[j].name)
                        {
                            isRepeat = true;
                        }
                    }

                    if (isRepeat)
                    {
                        var layers = property.serializedObject.FindProperty("runtimeFsmControllerLayers");
                        var layerName = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                        layerName.stringValue = GetLayerName(fsmController, i);
                        layerName.serializedObject.ApplyModifiedProperties();
                    }

                }

            }

            // 设置默认值
            if (fsmController.RuntimeFsmControllerLayers.Count == 1)
            {
                if (string.IsNullOrEmpty(fsmController.RuntimeFsmControllerLayers[0].name))
                {
                    var layers = property.serializedObject.FindProperty("runtimeFsmControllerLayers");
                    var layerName = layers.GetArrayElementAtIndex(0).FindPropertyRelative("name");
                    layerName.stringValue = "Layer 0";
                    layerName.serializedObject.ApplyModifiedProperties();
                }
            }

            rect.width /= 4;

            string nameValue = name.stringValue;
            EditorGUI.BeginChangeCheck();
            nameValue = EditorGUI.DelayedTextField(rect, nameValue);
            if (EditorGUI.EndChangeCheck())
            {
                // 名称改变
                if (IsContainName(fsmController, nameValue))
                {
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(string.Format("名称:{0}已经存在!", nameValue)));
                }
                else
                {
                    // 保存名称
                    name.stringValue = nameValue;
                    name.serializedObject.ApplyModifiedProperties();
                }
            }

            rect.x += rect.width + 3;
            rect.width *= 3;
            rect.width -= 2;

            if (_empty == null)
            {
                _empty = new GUIContent();
            }

            EditorGUI.PropertyField(rect, controller, _empty);

        }

        private bool IsContainName(FsmController controller, string name)
        {
            foreach (var item in controller.RuntimeFsmControllerLayers)
            {
                if (item.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label) + 1;
        }

        private string GetLayerName(FsmController controller, int index = 0)
        {
            string name = string.Empty;

            do
            {
                name = string.Format("Layer {0}", index);
                index++;
            } while (IsContainName(controller, name));

            return name;

        }

    }

}
