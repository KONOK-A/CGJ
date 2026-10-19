// Derived from XF Localization 1.0.9. Copyright (c) 2024 弦风课堂. MIT; see LICENSE.txt in this directory.
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Localization
{
    public class LanguageConfig : ScriptableObject
    {
        public SystemLanguage language;
        public string displayName;
        public List<LanguageInfo> languagesInfo = new();
        private readonly LocalizationEntryIndex index = new();

        public LanguageInfo GetLanguageInfo(int id) => index.Find(languagesInfo, id, name + "/" + language);
        public void ClearCache() => index.Clear();
        private void OnValidate() => ClearCache();
        private void OnDisable() => ClearCache();
    }
}
