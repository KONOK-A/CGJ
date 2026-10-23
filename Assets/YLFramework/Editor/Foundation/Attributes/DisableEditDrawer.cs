using UnityEditor;
using UnityEngine;


namespace YLFramework.Core.Editor
{
    [CustomPropertyDrawer(typeof(YLFramework.Foundation.Attributes.DisableEdit))]
    public class DisableEditDrawer : PropertyDrawer
    {


        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

    }
}
