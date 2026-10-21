using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.DataAssets
{
    [CreateAssetMenu(menuName = "YLFramework/Data Assets/Configuration Root", fileName = "DataConfigs")]
    public class ScriptableObjectConfigs : ScriptableObject
    {
        public string displayName;
        [HideInInspector]
        public List<ScriptableObjectData> configs = new List<ScriptableObjectData>();
        [HideInInspector]
        public string type_script_guid;

        private DataAssetIdIndex index;
        private ScriptableObjectData[] indexedItems;
        private int?[] indexedIds;

        /// <summary>空目录返回0；需要区分ID 0时使用TryGetIDRange。</summary>
        public int MinID { get { TryGetIDRange(out var min, out _); return min; } }
        public int MaxID { get { TryGetIDRange(out _, out var max); return max; } }

        public bool TryGetIDRange(out int min, out int max) => GetIndex().TryGetRange(out min, out max);

        public ScriptableObjectData GetScriptableObjectData(int id)
            => GetIndex().TryGetRow(id, out var row) ? configs[row] : null;

        public T GetScriptableObjectData<T>(int id) where T : ScriptableObjectData
            => GetScriptableObjectData(id) as T;

        /// <summary>名称精确匹配；歧义名称明确失败，不按列表顺序静默选取。</summary>
        public ScriptableObjectData GetScriptableObjectData(string configName)
        {
            if (configName == null) throw new ArgumentNullException(nameof(configName));
            GetIndex();
            ScriptableObjectData result = null;
            if (configs == null) return null;
            foreach (var item in configs)
            {
                if (!item || !StringComparer.Ordinal.Equals(item.config_name, configName)) continue;
                if (result) throw new InvalidOperationException($"DataAssets '{name}': ambiguous name '{configName}' (IDs {result.id}, {item.id}).");
                result = item;
            }
            return result;
        }

        public string GetName(int id)
        {
            var item = GetScriptableObjectData(id);
            return item ? item.config_name : null;
        }

        public IconInfo GetIcon(int id)
        {
            var item = GetScriptableObjectData(id);
            return item ? item.icon : null;
        }
        public int GenerateID() => GetIndex().NextId();

        private DataAssetIdIndex GetIndex()
        {
            var count = configs?.Count ?? 0;
            var changed = index == null || indexedItems == null || indexedItems.Length != count;
            for (var i = 0; !changed && i < count; i++)
            {
                var item = configs[i];
                int? id = item ? item.id : (int?)null;
                changed = !ReferenceEquals(indexedItems[i], item) || indexedIds[i] != id;
            }
            if (!changed) return index;
            ClearCache();
            var items = new ScriptableObjectData[count];
            var ids = new int?[count];
            for (var i = 0; i < count; i++)
            {
                items[i] = configs[i];
                ids[i] = items[i] ? items[i].id : (int?)null;
            }
            var rebuilt = new DataAssetIdIndex(ids, name);
            indexedItems = items;
            indexedIds = ids;
            index = rebuilt;
            return index;
        }

        public void ClearCache()
        {
            index = null;
            indexedItems = null;
            indexedIds = null;
        }

        protected virtual void OnEnable() => ClearCache();
        protected virtual void OnDisable() => ClearCache();
        protected virtual void OnValidate() => ClearCache();
    }
}
