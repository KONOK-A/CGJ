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
        private readonly Dictionary<string, CustomPropertyGui> _drawers = new()
        {
            ["parent"] = new CustomEquipmentConfigDataParentGui(),
            ["id"] = new CustomEquipmentConfigDataIdGui(),
            ["icon"] = new CustomEquipmentConfigDataIconGui(),
            ["equipmentClassFullName"] = new CustomEquipmentConfigDataClassGui(),
            ["projectName"] = new CustomEquipmentConfigDataProjectNameGui(),
            ["assetName"] = new CustomEquipmentConfigDataAssetNameGui()
        };
        public override void OnInspectorGUI()
        {
            if (!target)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                serializedObject.Update();
                var iterator = serializedObject.GetIterator();
                var enter = true;
                while (iterator.NextVisible(enter))
                {
                    enter = false;
                    var property = iterator.Copy();
                    if (_drawers.TryGetValue(property.name, out var drawer))
                    {
                        drawer.OnPropertyGui(property);
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                        {
                            EditorGUILayout.PropertyField(property, true);
                        }
                    }
                }
                if (serializedObject.ApplyModifiedProperties())
                {
                    var data = (EquipmentConfigData)target;
                    if (data.parent)
                    {
                        data.parent.ClearCache();
                    }

                    data.Rename();
                    EditorUtility.SetDirty(data);
                    AssetDatabase.SaveAssetIfDirty(data);
                }
            }
        }
    }
}
