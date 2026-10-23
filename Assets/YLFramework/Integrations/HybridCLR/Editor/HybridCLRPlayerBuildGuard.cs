using System;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    /// <summary>只校验本管线的显式上下文；无 profile 的普通构建不安装、不生成、不改设置。</summary>
    public sealed class HybridCLRPlayerBuildGuard : IPreprocessBuildWithReport
    {
        private static HybridCLRBuildProfile _profile;
        private static bool _scriptsOnly;
        private static string _fingerprint;
        public int callbackOrder => -1000;

        internal static IDisposable Enter(HybridCLRBuildProfile selected, bool strip)
        {
            if (_profile)
            {
                throw new InvalidOperationException("Nested HybridCLR Player builds are not supported.");
            }

            _fingerprint = HybridCLRBuildPipeline.SettingsFingerprint(selected);
            _profile = selected;
            _scriptsOnly = strip;
            return new Scope();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!_profile)
            {
                return;
            }

            HybridCLRBuildPipeline.ValidateEnvironment(_profile);
#if UNITY_EDITOR_WIN
            if (!_scriptsOnly)
            {
                HybridCLRBuildPipeline.ValidateExecutableBuildMode(UnityEditor.EditorUserBuildSettings.buildScriptsOnly,
                UnityEditor.WindowsStandalone.UserBuildSettings.createSolution);
            }
#endif
            if (report.summary.platform != _profile.recipe.target ||
                ((report.summary.options & UnityEditor.BuildOptions.Development) != 0) != _profile.development ||
                UnityEditor.EditorUserBuildSettings.buildScriptsOnly != _scriptsOnly ||
                _fingerprint != HybridCLRBuildPipeline.SettingsFingerprint(_profile))
            {
                throw new BuildFailedException("The explicit HybridCLR Player build context or native settings changed.");
            }
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
                _profile = null;
                _fingerprint = null;
                _scriptsOnly = false;
            }
        }
    }
}
