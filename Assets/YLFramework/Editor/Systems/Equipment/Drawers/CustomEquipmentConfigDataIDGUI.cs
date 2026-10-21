// Adapted from XF EquipmentSystem (MIT). See Runtime/Systems/Equipment/LICENSE.txt.
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Foundation.Attributes;
namespace YLFramework.EquipmentSystem
{
    internal sealed class CustomEquipmentConfigDataIDGUI : CustomPropertyGUI
    {
        private string error;
        public override void OnPropertyGUI(SerializedProperty property)
        {
            var id = EditorGUILayout.DelayedIntField("ID", property.intValue);
            if (id != property.intValue)
            {
                var target = property.serializedObject.targetObject;
                var duplicate = EquipmentsConfigController.GetAllEquipmentsConfig().SelectMany(c => c.Equipments).Any(d => d && d != target && d.id == id);
                error = duplicate ? "装备 ID 已占用: " + id : null;
                if (!duplicate) property.intValue = id;
            }
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
