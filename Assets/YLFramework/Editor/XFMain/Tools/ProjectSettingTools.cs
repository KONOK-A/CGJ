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
