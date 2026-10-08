using System;

namespace YLFramework.Services.UI
{
    [AttributeUsage(AttributeTargets.Class,AllowMultiple = true)]
    public class UIWindowDataAttribute : Attribute
    {
        public readonly string WindowKey;
        public readonly bool IsCache;
        public readonly string AssetName = null;
        public readonly string BundleName = null;
        public readonly int LayerNum;

        public UIWindowDataAttribute(string windowKey, bool isCache, string assetName, int layerNum)
        {
            WindowKey = windowKey;
            IsCache = isCache;
            AssetName = assetName;
            BundleName = null;
            LayerNum = layerNum;
        }

        public UIWindowDataAttribute(string windowKey, bool isCache, string bundleName, string assetName, int layerNum)
        {
            WindowKey = windowKey;
            IsCache = isCache;
            AssetName = assetName;
            BundleName = bundleName;
            LayerNum = layerNum;
        }
    }
}