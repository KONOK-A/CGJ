using System;
using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>显式窗口定义。目录注册时取得快照，不保存活跃实例或资源 Loader。</summary>
    public sealed class WindowDefinition
    {
        public string Key { get; }
        public GameObject Prefab { get; }
        public string AssetName { get; }
        public bool Unique { get; set; } = true;
        public bool Reusable { get; set; } = true;
        public int Layer { get; set; }
        public bool Modal { get; set; }
        public bool Stretch { get; set; }
        public PanelType PanelType { get; set; } = PanelType.Window;
        public UIType UIType { get; set; } = UIType.UI;

        public WindowDefinition(string key, GameObject prefab) { Key = key; Prefab = prefab; }
        /// <summary>资源名沿 Module 的 XFAB project fallback 解析，不将 BundleName 当 ProjectName。</summary>
        public WindowDefinition(string key, string assetName) { Key = key; AssetName = assetName; }

        internal WindowDefinition Snapshot()
        {
            if (string.IsNullOrWhiteSpace(Key)) throw new ArgumentException("Window key is required.");
            if (!Prefab && string.IsNullOrWhiteSpace(AssetName)) throw new ArgumentException("Window prefab or asset name is required.");
            if (Layer < 0) throw new ArgumentOutOfRangeException(nameof(Layer));
            if (!Enum.IsDefined(typeof(PanelType), PanelType) || !Enum.IsDefined(typeof(UIType), UIType))
                throw new ArgumentException("Invalid window type.");
            return (WindowDefinition)MemberwiseClone();
        }
    }
}
