using System;
using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Excel;
using YLFramework.Resource;

namespace YLFramework.DataAssets
{
    [Serializable]
    public class IconInfo
    {
        [Tooltip("YL Resource ProjectId")]
        public string projectName;
        [Tooltip("YL Resource address")]
        public string iconName;

        public bool TryGetAddress(out ResourceAddress address)
        {
            address = default;
            if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(iconName))
            {
                return false;
            }

            address = new ResourceAddress(projectName, iconName);
            return true;
        }
    }

    /// <summary>配置定义；查询返回借读引用，不在运行时修改共享资产。</summary>
    public class ScriptableObjectData : ScriptableObject
    {
        [HideInInspector, ExcelIgnore]
        public ScriptableObjectConfigs parent;
        [Tooltip("Unique ID within the root")]
        public int id;
        [FormerlySerializedAs("config_name")] public string configName;
        public IconInfo icon = new IconInfo();
        [TextArea(3, 10)]
        [FormerlySerializedAs("des")] public string description;

        protected virtual void OnValidate()
        {
            if (parent)
            {
                parent.ClearCache();
            }
        }
    }
}
