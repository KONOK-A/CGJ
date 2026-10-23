using UnityEditor;
using YLFramework.Foundation.Attributes;
namespace YLFramework.EquipmentSystem
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
