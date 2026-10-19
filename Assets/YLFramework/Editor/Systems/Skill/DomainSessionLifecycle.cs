using UnityEditor;
using YLFramework.SkillSystem;
using YLFramework.Logging;
using YLFramework.BuffSystem;

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
            try { try { SkillManager.Shutdown(); } finally { BuffManager.Shutdown(); } }
            finally { LoggerManager.Disable(); }
        }
    }
}
