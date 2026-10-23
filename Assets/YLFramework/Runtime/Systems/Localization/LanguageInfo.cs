using System;

namespace YLFramework.Localization
{
    [Serializable]
    public class ImageInfo
    {
        public string projectName;
        public string imageName;
        public string subAsset;
    }

    public enum MultiLanguageType
    {
        String,
        Image,
        NetworkImage
    }

    /// <summary>保留原多语言条目字段布局；图片项目名在YL中表示显式Resource ProjectId。</summary>
    [Serializable]
    public class LanguageInfo
    {
        public int id;
        public MultiLanguageType type;
        public string str;
        public ImageInfo image;
        public string url;
        public string notes;
    }
}
