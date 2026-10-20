// Derived from XF Localization 1.0.9. Copyright (c) 2024 弦风课堂. MIT; see LICENSE.txt in this directory.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Localization
{
    [CreateAssetMenu(menuName = "YLFramework/Localization/Multi Language Configs")]
    public class MultiLanguageConfigs : ScriptableObject
    {
        public string displayName = string.Empty;
        [SerializeField] private List<LanguageConfig> languages = new();
        public List<LanguageConfig> Languages => languages;

        // 列表仍可由作者修改；不持有按数量失效的语言字典，也不在getter扫描/保存资产。
        public LanguageConfig GetLanguageConfigData(SystemLanguage language)
        {
            if (languages == null) throw new InvalidOperationException($"Localization '{name}': language list is null.");
            LanguageConfig result = null;
            var seen = new HashSet<SystemLanguage>();
            for (var i = 0; i < languages.Count; i++)
            {
                var item = languages[i];
                if (!item) throw new InvalidOperationException($"Localization '{name}': missing language asset at index {i}.");
                if (!seen.Add(item.language)) throw new InvalidOperationException($"Localization '{name}': duplicate language {item.language} at index {i}.");
                if (item.language == language) result = item;
            }
            return result;
        }

        public void ClearCache()
        {
            if (languages == null) return;
            foreach (var item in languages) if (item) item.ClearCache();
        }
    }
}
