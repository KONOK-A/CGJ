using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Newtonsoft.Json;
using YLFramework.Excel;

namespace YLFramework.Buff
{
    /// <summary>沿用效果ID数组的作者格式；仅Editor解析真实资产，不序列化Unity对象JSON。</summary>
    public sealed class BuffEffectExcel : CustomExcelFieldImportAndExport
    {
        public override object Export(object target, object value, ExcelFieldsType type)
        {
            var ids = new List<int>();
            foreach (var effect in (List<EffectData>)value)
            {
                if (!effect)
                {
                    throw new InvalidOperationException("Buff效果引用为空，不能导出。");
                }

                ids.Add(effect.id);
            }
            return JsonConvert.SerializeObject(ids);
        }
        public override object Import(object target, object value, ExcelFieldsType type)
        {
#if UNITY_EDITOR
            var ids = JsonConvert.DeserializeObject<List<int>>(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "[]") ?? new List<int>();
            var assets = new Dictionary<int, EffectData>();
            var needed = new HashSet<int>(ids);
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:EffectData"))
            {
                var effect = UnityEditor.AssetDatabase.LoadAssetAtPath<EffectData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (!effect || !needed.Contains(effect.id))
                {
                    continue;
                }

                if (assets.ContainsKey(effect.id))
                {
                    throw new InvalidOperationException($"效果ID歧义: {effect.id}");
                }

                assets.Add(effect.id, effect);
            }
            var result = new List<EffectData>();
            foreach (var id in ids)
            {
                if (!assets.TryGetValue(id, out var effect))
                {
                    throw new InvalidOperationException($"效果ID未找到: {id}");
                }

                result.Add(effect);
            }
            return result;
#else
            throw new InvalidOperationException("Buff效果Excel导入仅在Editor作者工具执行。");
#endif
        }
        public override void SetValue(object target, FieldInfo field, object value) => field.SetValue(target, value);
    }
}
