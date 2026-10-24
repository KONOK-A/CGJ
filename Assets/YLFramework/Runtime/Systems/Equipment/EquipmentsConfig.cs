using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YLFramework.Equipment
{
    [CreateAssetMenu(menuName = "YLFramework/EquipmentSystem(装备配置)")]
    public class EquipmentsConfig : ScriptableObject
    {
        public string displayName;
        [HideInInspector, FormerlySerializedAs("start_id")] public int startId;
        [SerializeField] private List<EquipmentConfigData> equipments = new();
        [HideInInspector, FormerlySerializedAs("type_script_guid")] public string typeScriptGuid;
        private Dictionary<int, EquipmentConfigData> _index;
        private EquipmentConfigData[] _snapshot;
        private int[] _ids;

        /// <summary>读取不扫描或保存资产；编辑列表后查询会核对引用与 ID 快照。</summary>
        public List<EquipmentConfigData> Equipments => equipments;

        public EquipmentConfigData GetEquipmentConfigData(int id)
        {
            if (!CacheMatches())
            {
                RebuildCache();
            }

            return _index.TryGetValue(id, out var value) && value ? value : null;
        }

        public void ClearCache()
        {
            _index = null;
            _snapshot = null;
            _ids = null;
        }

        private bool CacheMatches()
        {
            if (_index == null || _snapshot.Length != equipments.Count)
            {
                return false;
            }

            for (var i = 0; i < _snapshot.Length; i++)
            {
                if (!ReferenceEquals(_snapshot[i], equipments[i]) || _ids[i] != (equipments[i] ? equipments[i].id : 0))
                {
                    return false;
                }
            }

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
                if (!data)
                {
                    continue;
                }

                keys[i] = data.id;
                if (next.ContainsKey(data.id))
                {
                    throw new InvalidOperationException($"Equipment '{name}': duplicate ID {data.id}, row {i}.");
                }

                next.Add(data.id, data);
            }
            _index = next;
            _snapshot = entries;
            _ids = keys;
        }
    }
}
