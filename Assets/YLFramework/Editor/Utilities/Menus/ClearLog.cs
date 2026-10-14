#if UNITY_EDITOR

using System.Reflection;
using UnityEditor;

namespace YLFramework.Editor
{
    public static class ClearLog
    {
        [MenuItem("YLFramework/Toolkit/清空控制台",false,priority = 0)] // # 代表Shift
        public static void MenuClicked()
        {
            //LogEntries 为 internal 权限 所以需要利用反射来处理
            var assembly = Assembly.GetAssembly(typeof(SceneView));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            if (method != null)
            {
                method.Invoke(new object(), null);
            }
        }
    }
}

#endif