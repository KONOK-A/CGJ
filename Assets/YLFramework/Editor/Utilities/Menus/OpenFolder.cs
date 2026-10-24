#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Utilities
{
    public static class OpenFolder
    {
        [MenuItem("YLFramework/Toolkit/打开目录/Unity Package", priority = 1)]
        public static void OpenPackageFolder()
        {
            //打开一个 本地/网页 路径
            Application.OpenURL("file:///" + Path.Combine(Application.dataPath, "../"));
        }
        [MenuItem("YLFramework/Toolkit/打开目录/PersistentDataPath", priority = 2)]
        public static void OpenPersistentFolder()
        {
            Application.OpenURL("file:///" + Application.persistentDataPath);
        }

    }
}

#endif
