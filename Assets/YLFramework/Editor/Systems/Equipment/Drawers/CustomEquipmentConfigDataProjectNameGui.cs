using UnityEditor;
using YLFramework.Attributes;
using YLFramework.Equipment;
namespace YLFramework.Editor.Equipment
{
    internal sealed class CustomEquipmentConfigDataProjectNameGui : CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty property)
        {
            if (property.serializedObject.FindProperty("equipmentType").intValue == (int)EquipmentType.Asset)
            {
                EquipmentAuthoring.DrawProject(property);
            }
        }
    }
}
