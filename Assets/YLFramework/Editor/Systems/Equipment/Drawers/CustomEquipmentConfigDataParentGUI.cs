using UnityEditor;
using YLFramework.Foundation.Attributes;
namespace YLFramework.EquipmentSystem
{
    internal sealed class CustomEquipmentConfigDataParentGUI : CustomPropertyGUI
    {
        public override void OnPropertyGUI(SerializedProperty property)
        { using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property); }
    }
}
