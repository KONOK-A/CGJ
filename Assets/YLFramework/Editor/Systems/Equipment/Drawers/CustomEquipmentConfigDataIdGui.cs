using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Attributes;
namespace YLFramework.Editor.Equipment
{
    internal sealed class CustomEquipmentConfigDataIdGui : CustomPropertyGui
    {
        private string _error;
        public override void OnPropertyGui(SerializedProperty property)
        {
            var id = EditorGUILayout.DelayedIntField("ID", property.intValue);
            if (id != property.intValue)
            {
                var target = property.serializedObject.targetObject;
                var duplicate = EquipmentsConfigController.GetAllEquipmentsConfig().SelectMany(c => c.Equipments).Any(d => d && d != target && d.id == id);
                _error = duplicate ? "装备 ID 已占用: " + id : null;
                if (!duplicate)
                {
                    property.intValue = id;
                }
            }
            if (_error != null)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }
        }
    }
}
