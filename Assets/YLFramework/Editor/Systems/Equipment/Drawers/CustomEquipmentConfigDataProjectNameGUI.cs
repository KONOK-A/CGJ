using UnityEditor;
using YLFramework.Foundation.Attributes;
namespace YLFramework.EquipmentSystem
{
    internal sealed class CustomEquipmentConfigDataProjectNameGUI : CustomPropertyGUI
    {
        public override void OnPropertyGUI(SerializedProperty property)
        {
            if (property.serializedObject.FindProperty("equipmentType").intValue == (int)EquipmentType.Asset) EquipmentAuthoring.DrawProject(property);
        }
    }
}
