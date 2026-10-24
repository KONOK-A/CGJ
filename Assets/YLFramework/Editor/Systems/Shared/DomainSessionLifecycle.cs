using UnityEditor;
using YLFramework.Buff;
using YLFramework.Logging;
using YLFramework.Mission;
using YLFramework.Skill;

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
