using UnityEditor;
using YLFramework.Foundation.Attributes;
namespace YLFramework.EquipmentSystem
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
