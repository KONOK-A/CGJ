namespace YLFramework.Services.UI
{
    
    public class UIWindowData
    {
        public readonly bool IsCache;
        public readonly string AssetName;
        public readonly string BundleName;
        public int LayerNum;
        public readonly string WindowKey;
        
        public UIWindowBase Instance;

        public UIWindowData(string windowKey, bool isCache, string assetName, int layerNum)
        {
            WindowKey = windowKey;
            IsCache = isCache;
            AssetName = assetName;
            BundleName = null;
            LayerNum = layerNum;
            Instance = null;
        }

        public UIWindowData(string windowKey, bool isCache, string bundleName, string assetName, int layerNum)
        {
            WindowKey = windowKey;
            IsCache = isCache;
            AssetName = assetName;
            BundleName = bundleName;
            LayerNum = layerNum;
            Instance = null;
        }
    }
}