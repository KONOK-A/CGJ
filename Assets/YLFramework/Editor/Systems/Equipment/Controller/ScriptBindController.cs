// Adapted from XF EquipmentSystem 1.1.9 (MIT). See Runtime/Systems/Equipment/LICENSE.txt.
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    public static class ScriptBindController
    {
        [InitializeOnLoadMethod]
        private static void Initialize() => EditorApplication.delayCall += RefreshBindings;

        public static bool IsEquipmentType(Type type) => type != null && type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters &&
            typeof(IEquipment).IsAssignableFrom(type) && !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null;

        public static string GetScriptGuid(Type type) => AssetDatabase.FindAssets(type.Name + " t:MonoScript")
            .FirstOrDefault(g => { var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)); return script && script.GetClass() == type; }) ?? "";

        public static void RefreshBindings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var root in EquipmentsConfigController.GetAllEquipmentsConfig())
                foreach (var data in root.Equipments)
                {
                    if (!data || data.equipmentType != EquipmentType.Script || string.IsNullOrEmpty(data.equipment_class_guid)) continue;
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(data.equipment_class_guid));
                    var type = script ? script.GetClass() : null;
                    if (!IsEquipmentType(type) || data.equipment_class_full_name == type.FullName) continue;
                    Undo.RecordObject(data, "Refresh equipment binding"); data.equipment_class_full_name = type.FullName;
                    EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data);
                }
        }
    }
}
