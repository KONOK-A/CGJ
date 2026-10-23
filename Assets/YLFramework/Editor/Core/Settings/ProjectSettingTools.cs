using UnityEditor;

namespace YLFramework.Core.Editor
{
    public class ProjectSettingTools
    {
        public static void OpenProjectSettings(string path)
        {
            SettingsService.OpenProjectSettings(path);
        }

    }
}
