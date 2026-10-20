using YLFramework.Core.Editor;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YLFramework.StateMachine
{

    [CustomPropertyDrawer(typeof(RuntimeFSMControllerLayer))]
    public class CustomRuntimeFSMControllerLayer : PropertyDrawer
    {

        private GUIContent empty;

        private int count;

        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            rect.height = base.GetPropertyHeight(property, label);

            var name = property.FindPropertyRelative("name");
            var controller = property.FindPropertyRelative("controller");

            FSMController fSMController = property.serializedObject.targetObject as FSMController;

            if (fSMController.RuntimeFSMControllerLayers.Count != count)
            {
                count = fSMController.RuntimeFSMControllerLayers.Count;

                // 检测是否有重复名称
                for (int i = count - 1; i >= 0; i--)
                {

                    bool is_repeat = false;

                    for (int j = 0; j < count; j++)
                    {
                        if (i == j) continue;
                        if (fSMController.RuntimeFSMControllerLayers[i].name == fSMController.RuntimeFSMControllerLayers[j].name)
                            is_repeat = true;
                    }

                    if (is_repeat)
                    {
                        var layers = property.serializedObject.FindProperty("_runtimeFSMControllerLayer");
                        var layer_name = layers.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                        layer_name.stringValue = GetLayerName(fSMController, i);
                        layer_name.serializedObject.ApplyModifiedProperties();
                    }

                }

            }

            // 设置默认值
            if (fSMController.RuntimeFSMControllerLayers.Count == 1)
            {
                if (string.IsNullOrEmpty(fSMController.RuntimeFSMControllerLayers[0].name))
                {
                    var layers = property.serializedObject.FindProperty("_runtimeFSMControllerLayer");
                    var layer_name = layers.GetArrayElementAtIndex(0).FindPropertyRelative("name");
                    layer_name.stringValue = "Layer 0";
                    layer_name.serializedObject.ApplyModifiedProperties();
                }
            }

            rect.width /= 4;

            string name_value = name.stringValue;
            EditorGUI.BeginChangeCheck();
            name_value = EditorGUI.DelayedTextField(rect, name_value);
            if (EditorGUI.EndChangeCheck())
            {
                // 名称改变
                if (IsContainName(fSMController,name_value))
                {
                    EditorWindow.focusedWindow.ShowNotification(new GUIContent(string.Format("名称:{0}已经存在!", name_value)));
                }
                else
                {
                    // 保存名称
                    name.stringValue = name_value;
                    name.serializedObject.ApplyModifiedProperties();
                }
            }

            rect.x += rect.width + 3;
            rect.width *= 3;
            rect.width -= 2;

            if (empty == null)
                empty = new GUIContent();
            EditorGUI.PropertyField(rect, controller, empty);

        }

        private bool IsContainName( FSMController controller, string name)
        {
            foreach (var item in controller.RuntimeFSMControllerLayers)
            {
                if (item.name == name) return true;
            }

            return false;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return base.GetPropertyHeight(property, label) + 1;
        }

        private string GetLayerName(FSMController controller, int index = 0)
        {
            string name = string.Empty;

            do
            {
                name = string.Format("Layer {0}", index);
                index++;
            } while (IsContainName(controller,name));

            return name;

        }

    }

}
