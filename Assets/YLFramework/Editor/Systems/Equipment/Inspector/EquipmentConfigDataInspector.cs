using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YLFramework.Foundation.Attributes;

namespace YLFramework.EquipmentSystem
{
    [CustomEditor(typeof(EquipmentConfigData), true)]
    public sealed class EquipmentConfigDataInspector : UnityEditor.Editor
    {
        private readonly Dictionary<string, CustomPropertyGUI> drawers = new()
        {
            ["parent"] = new CustomEquipmentConfigDataParentGUI(), ["id"] = new CustomEquipmentConfigDataIDGUI(),
            ["icon"] = new CustomEquipmentConfigDataIconGUI(), ["equipment_class_full_name"] = new CustomEquipmentConfigDataClassGUI(),
            ["projectName"] = new CustomEquipmentConfigDataProjectNameGUI(), ["assetName"] = new CustomEquipmentConfigDataAssetNameGUI()
        };
        public override void OnInspectorGUI()
        {
            if (!target) return;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                serializedObject.Update();
                var iterator = serializedObject.GetIterator();
                var enter = true;
                while (iterator.NextVisible(enter))
                {
                    enter = false;
                    var property = iterator.Copy();
                    if (drawers.TryGetValue(property.name, out var drawer)) drawer.OnPropertyGUI(property);
                    else using (new EditorGUI.DisabledScope(property.name == "m_Script")) EditorGUILayout.PropertyField(property, true);
                }
                if (serializedObject.ApplyModifiedProperties())
                {
                    var data = (EquipmentConfigData)target;
                    if (data.parent) data.parent.ClearCache();
                    data.Rename();
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
            }
        }
    }
}
