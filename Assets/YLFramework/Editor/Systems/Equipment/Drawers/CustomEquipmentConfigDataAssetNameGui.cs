using UnityEditor;
using UnityEngine;
using YLFramework.Attributes;
using YLFramework.Equipment;
namespace YLFramework.Editor.Equipment
{
    internal sealed class CustomEquipmentConfigDataAssetNameGui : CustomPropertyGui
    {
        public override void OnPropertyGui(SerializedProperty property)
        {
            if (property.serializedObject.FindProperty("equipmentType").intValue != (int)EquipmentType.Asset)
            {
                return;
            }

            EquipmentAuthoring.DrawAddress(property.serializedObject.FindProperty("projectName"), property, typeof(GameObject));
            EditorGUILayout.HelpBox("Prefab 根节点必须有且仅有一个 IEquipment；运行前初始化 ResourceSystem。", MessageType.Info);
        }
    }
}
