using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YLFramework.DataAssets
{
    [CreateAssetMenu(menuName = "YLFramework/Data Assets/Configuration Root", fileName = "DataConfigs")]
    public class ScriptableObjectConfigs : ScriptableObject
    {
        public string displayName;
        [HideInInspector]
        public List<ScriptableObjectData> configs = new List<ScriptableObjectData>();
        [HideInInspector]
        [FormerlySerializedAs("type_script_guid")] public string typeScriptGuid;

        private DataAssetIdIndex _index;
        private ScriptableObjectData[] _indexedItems;
        private int?[] _indexedIds;

        /// <summary>空目录返回0；需要区分ID 0时使用TryGetIDRange。</summary>
        public int MinId
        {
            get
            {
                TryGetIdRange(out var min, out _);
                return min;
            }
        }
        public int MaxId
        {
            get
            {
                TryGetIdRange(out _, out var max);
                return max;
            }
        }

        public bool TryGetIdRange(out int min, out int max) => GetIndex().TryGetRange(out min, out max);

        public ScriptableObjectData GetScriptableObjectData(int id)
            => GetIndex().TryGetRow(id, out var row) ? configs[row] : null;

        public T GetScriptableObjectData<T>(int id) where T : ScriptableObjectData
            => GetScriptableObjectData(id) as T;

        /// <summary>名称精确匹配；歧义名称明确失败，不按列表顺序静默选取。</summary>
        public ScriptableObjectData GetScriptableObjectData(string configName)
        {
            if (configName == null)
            {
                throw new ArgumentNullException(nameof(configName));
            }

            GetIndex();
            ScriptableObjectData result = null;
            if (configs == null)
            {
                return null;
            }

            foreach (var item in configs)
            {
                if (!item || !StringComparer.Ordinal.Equals(item.configName, configName))
                {
                    continue;
                }

                if (result)
                {
                    throw new InvalidOperationException($"DataAssets '{name}': ambiguous name '{configName}' (IDs {result.id}, {item.id}).");
                }

                result = item;
            }
            return result;
        }

        public string GetName(int id)
        {
            var item = GetScriptableObjectData(id);
            return item ? item.configName : null;
        }

        public IconInfo GetIcon(int id)
        {
            var item = GetScriptableObjectData(id);
            return item ? item.icon : null;
        }
        public int GenerateId() => GetIndex().NextId();

        private DataAssetIdIndex GetIndex()
        {
            var count = configs?.Count ?? 0;
            var changed = _index == null || _indexedItems == null || _indexedItems.Length != count;
            for (var i = 0; !changed && i < count; i++)
            {
                var item = configs[i];
                int? id = item ? item.id : (int?)null;
                changed = !ReferenceEquals(_indexedItems[i], item) || _indexedIds[i] != id;
            }
            if (!changed)
            {
                return _index;
            }

            ClearCache();
            var items = new ScriptableObjectData[count];
            var ids = new int?[count];
            for (var i = 0; i < count; i++)
            {
                items[i] = configs[i];
                ids[i] = items[i] ? items[i].id : (int?)null;
            }
            var rebuilt = new DataAssetIdIndex(ids, name);
            _indexedItems = items;
            _indexedIds = ids;
            _index = rebuilt;
            return _index;
        }

        public void ClearCache()
        {
            _index = null;
            _indexedItems = null;
            _indexedIds = null;
        }

        protected virtual void OnEnable() => ClearCache();
        protected virtual void OnDisable() => ClearCache();
        protected virtual void OnValidate() => ClearCache();
    }
}
