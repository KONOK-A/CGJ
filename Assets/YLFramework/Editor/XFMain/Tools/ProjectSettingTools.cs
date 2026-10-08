// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/Tools/ProjectSettingTools.cs
using System; 
using System.Reflection;
using UnityEditor; 

namespace YLFramework.Core.Editor
{
    public class ProjectSettingTools 
    { 
        public static void OpenProjectSettings(string path)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var item in assemblies)
            {
                Type t = item.GetType("UnityEditor.SettingsWindow");
                if (t == null) continue;
                MethodInfo ShowProjectSettingsMethod = t.GetMethod("Show", BindingFlags.Static | BindingFlags.NonPublic);
                ShowProjectSettingsMethod.Invoke(null, new object[] { SettingsScope.Project, path });
            }
        }

    }
}
