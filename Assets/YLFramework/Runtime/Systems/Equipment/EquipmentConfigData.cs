using System;
using UnityEngine;
using YLFramework.Excel;

namespace YLFramework.EquipmentSystem
{
    [Serializable]
    public class IconInfo
    {
        [Tooltip("YL Resource ProjectId")] public string projectName;
        [Tooltip("YL Resource Path")] public string iconName;
    }

    public enum EquipmentType { Asset = 0, Script = 1 }

    /// <summary>共享装备定义；已登记期间不可修改，运行态数据属于装备实例。</summary>
    public class EquipmentConfigData : ScriptableObject
    {
        [ExcelIgnore] public EquipmentsConfig parent;
        [ExcelScriptableObjectID] public int id;
        public string equipment_name;
        public IconInfo icon = new();
        [TextArea(3, 8)] public string description;
        public EquipmentType equipmentType;
        public string equipment_class_full_name;
        [HideInInspector, ExcelIgnore] public string equipment_class_guid;
        [Tooltip("YL Resource ProjectId")] public string projectName;
        [Tooltip("YL Resource Path")] public string assetName;
    }
}
