using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YLFramework.Localization
{
    public enum AppendType { ID, String }
    [Serializable]
    public struct AppendData
    {
        public AppendType type;
        public int id;
        public string str;
        public string richText;
    }

    /// <summary>原参数、追加文本和richText工作流的纯逻辑部分；每个包装只格式化一次。</summary>
    public static class LocalizationText
    {
        public static string Compose(int id, Func<int, string> lookup, IList<AppendData> args, IList<AppendData> appends)
        {
            if (lookup == null) throw new ArgumentNullException(nameof(lookup));
            try
            {
                var template = lookup(id) ?? string.Empty;
                var values = GetArguments(lookup, args);
                var builder = new StringBuilder(values.Length == 0 ? template : string.Format(CultureInfo.CurrentCulture, template, values));
                if (appends != null) foreach (var item in appends) builder.Append(GetValue(lookup, item));
                return builder.ToString();
            }
            catch (FormatException error) { throw new FormatException($"Localization ID={id}: invalid argument/append format.", error); }
        }

        public static string[] GetArguments(Func<int, string> lookup, IList<AppendData> args)
        {
            if (args == null || args.Count == 0) return Array.Empty<string>();
            var result = new string[args.Count];
            for (var i = 0; i < result.Length; i++) result[i] = GetValue(lookup, args[i]);
            return result;
        }

        private static string GetValue(Func<int, string> lookup, AppendData item)
        {
            var value = item.type == AppendType.ID ? lookup(item.id) ?? string.Empty : item.str ?? string.Empty;
            return string.IsNullOrEmpty(item.richText) ? value : string.Format(CultureInfo.CurrentCulture, item.richText, value);
        }
    }
}
