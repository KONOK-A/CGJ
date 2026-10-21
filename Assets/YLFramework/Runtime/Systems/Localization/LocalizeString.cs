using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Localization
{
    // TMP可用Other的公开string属性绑定，不让主体程序集依赖可选TMP包。
    public enum ComponentType { Text, TextMesh, Other }
    [AddComponentMenu("YLFramework/Localization/Localize String")]
    public class LocalizeString : LocalizeBase
    {
        [SerializeField] private ComponentType componentType;
        [SerializeField] private string componentFullName;
        [SerializeField] private string fieldName = "text";
        [SerializeField] private List<AppendData> args = new();
        [SerializeField] private List<AppendData> appends = new();
        public List<AppendData> Appends => appends;

        private void Reset() => componentType = GetComponent<TextMesh>() ? ComponentType.TextMesh : ComponentType.Text;

        protected override void OnLanguageValueChange()
        {
            // 目标可能在组件Enable之后才绑定；不缓存一个永久missing的结果。
            var value = GetString();
            if (componentType == ComponentType.Text)
            {
                var target = GetComponent<Text>();
                if (target) target.text = value;
                return;
            }
            if (componentType == ComponentType.TextMesh)
            {
                var target = GetComponent<TextMesh>();
                if (target) target.text = value;
                return;
            }
            var other = string.IsNullOrEmpty(componentFullName) ? null : GetComponent(componentFullName);
            if (!other) throw new InvalidOperationException($"Localization ID={ID}: component '{componentFullName}' is missing.");
            var property = other.GetType().GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(string) && property.CanWrite && property.GetIndexParameters().Length == 0)
            { property.SetValue(other, value); return; }
            var field = other.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(string) && !field.IsInitOnly)
            { field.SetValue(other, value); return; }
            throw new InvalidOperationException($"Localization ID={ID}: '{componentFullName}.{fieldName}' is not a writable public string member. Preserve reflected members in IL2CPP.");
        }

        public string GetString()
        {
            try { return LocalizationText.Compose(ID, id => LocalizationManager.GetString(id), args, appends); }
            catch (FormatException error)
            {
                throw new FormatException($"Localization language={LocalizationManager.RequestedLanguage}, ID={ID}, configs={LocalizationManager.ConfigNames()}: invalid argument/append format.", error);
            }
        }
        public string[] GetArgs() => LocalizationText.GetArguments(id => LocalizationManager.GetString(id), args);

        public void SetArgs(IList<AppendData> data)
        {
            args = data == null ? new List<AppendData>() : new List<AppendData>(data);
            Refresh();
        }

        public void Append(int id, string richText = null)
        {
            appends ??= new List<AppendData>();
            appends.Add(new AppendData { type = AppendType.ID, id = id, richText = richText });
            Refresh();
        }

        public void Append(string str, string richText = null)
        {
            appends ??= new List<AppendData>();
            appends.Add(new AppendData { type = AppendType.String, str = str, richText = richText });
            Refresh();
        }

        public void ClearAppend() { appends?.Clear(); Refresh(); }
    }
}
