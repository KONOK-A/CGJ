using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Localization
{
    public class LanguageConfig : ScriptableObject
    {
        public SystemLanguage language;
        public string displayName;
        public List<LanguageInfo> languagesInfo = new();
        private readonly LocalizationEntryIndex _index = new();

        public LanguageInfo GetLanguageInfo(int id) => _index.Find(languagesInfo, id, name + "/" + language);
        public void ClearCache() => _index.Clear();
        private void OnValidate() => ClearCache();
        private void OnDisable() => ClearCache();
    }
}
