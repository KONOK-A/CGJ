using UnityEditor;
using YLFramework.SkillSystem;
using YLFramework.Logging;

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
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            try { SkillManager.Shutdown(); }
            finally { LoggerManager.Disable(); }
        }
    }
}
