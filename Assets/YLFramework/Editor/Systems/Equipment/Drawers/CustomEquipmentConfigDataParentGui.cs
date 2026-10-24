using UnityEditor;
using YLFramework.Attributes;
namespace YLFramework.Editor.Equipment
{
    internal sealed class CustomEquipmentConfigDataParentGui : CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty property)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(property);
            }
        }
    }
}
