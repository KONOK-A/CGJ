using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Localization
{
    /// <summary>语言选择与条目查询共享的有限回退顺序，不依赖Dictionary的枚举顺序。</summary>
    public static class LocalizationFallback
    {
        public static T Find<T>(SystemLanguage requested, IEnumerable<SystemLanguage> registered, Func<SystemLanguage, T> lookup) where T : class
        {
            if (lookup == null) throw new ArgumentNullException(nameof(lookup));
            foreach (var candidate in GetCandidates(requested, registered))
            {
                var value = lookup(candidate);
                if (value != null) return value;
            }
            return null;
        }

        public static IReadOnlyList<SystemLanguage> GetCandidates(SystemLanguage requested, IEnumerable<SystemLanguage> registered)
        {
            var result = new List<SystemLanguage>();
            Add(requested);
            if (requested == SystemLanguage.ChineseSimplified) { Add(SystemLanguage.Chinese); Add(SystemLanguage.ChineseTraditional); }
            else if (requested == SystemLanguage.ChineseTraditional) { Add(SystemLanguage.Chinese); Add(SystemLanguage.ChineseSimplified); }
            else if (requested == SystemLanguage.Chinese) { Add(SystemLanguage.ChineseSimplified); Add(SystemLanguage.ChineseTraditional); }
            Add(SystemLanguage.English);
            if (registered != null) foreach (var language in registered) Add(language);
            return result;

            void Add(SystemLanguage language)
            {
                if (language != SystemLanguage.Unknown && !result.Contains(language)) result.Add(language);
            }
        }
    }
}
