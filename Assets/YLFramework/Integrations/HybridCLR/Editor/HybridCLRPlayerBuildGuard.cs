using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    /// <summary>只校验本管线的显式上下文；无 profile 的普通构建不安装、不生成、不改设置。</summary>
    public sealed class HybridCLRPlayerBuildGuard : IPreprocessBuildWithReport
    {
        private static HybridCLRBuildProfile profile;
        private static bool scriptsOnly;
        private static string fingerprint;
        public int callbackOrder => -1000;

        internal static IDisposable Enter(HybridCLRBuildProfile selected, bool strip)
        {
            if (profile) throw new InvalidOperationException("Nested HybridCLR Player builds are not supported.");
            fingerprint = HybridCLRBuildPipeline.SettingsFingerprint(selected); profile = selected; scriptsOnly = strip;
            return new Scope();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!profile) return;
            HybridCLRBuildPipeline.ValidateEnvironment(profile);
#if UNITY_EDITOR_WIN
            if (!scriptsOnly) HybridCLRBuildPipeline.ValidateExecutableBuildMode(UnityEditor.EditorUserBuildSettings.buildScriptsOnly,
                UnityEditor.WindowsStandalone.UserBuildSettings.createSolution);
#endif
            if (report.summary.platform != profile.Recipe.Target ||
                ((report.summary.options & UnityEditor.BuildOptions.Development) != 0) != profile.Development ||
                UnityEditor.EditorUserBuildSettings.buildScriptsOnly != scriptsOnly ||
                fingerprint != HybridCLRBuildPipeline.SettingsFingerprint(profile))
                throw new BuildFailedException("The explicit HybridCLR Player build context or native settings changed.");
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose() { profile = null; fingerprint = null; scriptsOnly = false; }
        }
    }
}
