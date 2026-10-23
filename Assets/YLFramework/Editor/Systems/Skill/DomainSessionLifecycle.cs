using UnityEditor;
using YLFramework.BuffSystem;
using YLFramework.Logging;
using YLFramework.MissionSystem;
using YLFramework.SkillSystem;

namespace YLFramework.Editor.Domain
{
    internal static class DomainSessionLifecycle
    {
        [InitializeOnLoadMethod]
        private static void Install()
        {
            EditorApplication.playModeStateChanged -= OnStateChanged;
            EditorApplication.playModeStateChanged += OnStateChanged;
        }

        private static void OnStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            try
            {
                try
                {
                    SkillManager.Shutdown();
                }
                finally
                {
                    try
                    {
                        BuffManager.Shutdown();
                    }
                    finally
                    {
                        MissionManager.Shutdown();
                    }
                }
            }
            finally
            {
                LoggerManager.Disable();
            }
        }
    }
}
