namespace YLFramework.Services.UI
{
    public static class UIWindowExtension
    {

        public static T OpenAsUI<T>(this T self, int layerNum = -1, params object[] args) where T : UIWindowBase
        {
            return !self ? null : UIManager.Instance.Open<T>(self.MyWindowData.WindowKey, layerNum, args);
        }

        public static T CloseAsUI<T>(this T self) where T : UIWindowBase
        {
            if (!self)
            {
                return self;
            }
            
            UIManager.Instance.Close(self.MyWindowData.WindowKey);
            return self;
        }

        public static bool TryCloseAsUI<T>(this T self) where T : UIWindowBase
        {
            return self && UIManager.Instance.TryClose(self.MyWindowData.WindowKey);
        }
    }
}