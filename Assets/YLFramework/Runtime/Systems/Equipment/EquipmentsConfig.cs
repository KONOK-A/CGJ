// Adapted from XF EquipmentSystem 1.1.9 (MIT). See LICENSE.txt.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.EquipmentSystem
{
    [CreateAssetMenu(menuName = "YLFramework/EquipmentSystem(装备配置)")]
    public class EquipmentsConfig : ScriptableObject
    {
        public string displayName;
        [HideInInspector] public int start_id;
        [SerializeField] private List<EquipmentConfigData> equipments = new();
        [HideInInspector] public string type_script_guid;
        private Dictionary<int, EquipmentConfigData> index;
        private EquipmentConfigData[] snapshot;
        private int[] ids;

        /// <summary>读取不扫描或保存资产；编辑列表后查询会核对引用与 ID 快照。</summary>
        public List<EquipmentConfigData> Equipments => equipments;

        public EquipmentConfigData GetEquipmentConfigData(int id)
        {
            if (!CacheMatches()) RebuildCache();
            return index.TryGetValue(id, out var value) && value ? value : null;
        }

        public void ClearCache() { index = null; snapshot = null; ids = null; }

        private bool CacheMatches()
        {
            if (index == null || snapshot.Length != equipments.Count) return false;
            for (var i = 0; i < snapshot.Length; i++)
                if (!ReferenceEquals(snapshot[i], equipments[i]) || ids[i] != (equipments[i] ? equipments[i].id : 0)) return false;
            return true;
        }

        private void RebuildCache()
        {
            var next = new Dictionary<int, EquipmentConfigData>();
            var entries = equipments.ToArray();
            var keys = new int[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var data = entries[i];
                if (!data) continue;
                keys[i] = data.id;
                if (next.ContainsKey(data.id)) throw new InvalidOperationException($"Equipment '{name}': duplicate ID {data.id}, row {i}.");
                next.Add(data.id, data);
            }
            index = next;
            snapshot = entries;
            ids = keys;
        }
    }
}
