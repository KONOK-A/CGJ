using System;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Localization;

namespace YLFramework.Tests.Extensions.Data
{
    public class LocalizationUnityTests
    {
        private MultiLanguageConfigs root;
        private LanguageConfig english;
        private LanguageConfig chinese;

        [SetUp]
        public void SetUp()
        {
            Assert.That(LocalizationManager.Configs, Is.Empty, "Do not close another localization owner.");
            LocalizationManager.Shutdown();
            root = ScriptableObject.CreateInstance<MultiLanguageConfigs>();
            root.name = "Localization-test-root";
            english = ScriptableObject.CreateInstance<LanguageConfig>();
            english.language = SystemLanguage.English;
            english.languagesInfo.Add(new LanguageInfo { id = 1, str = "English {0}" });
            chinese = ScriptableObject.CreateInstance<LanguageConfig>();
            chinese.language = SystemLanguage.Chinese;
            chinese.languagesInfo.Add(new LanguageInfo { id = 1, str = "Chinese" });
            root.Languages.Add(chinese);
            root.Languages.Add(english);
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationManager.Shutdown();
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(english);
            UnityEngine.Object.DestroyImmediate(chinese);
        }

        [Test]
        public void RootRegistration_RefreshesAndTwoOwnersReleaseIndependently()
        {
            var refreshes = 0;
            var token = LocalizationManager.Changed.Register(() => refreshes++);
            using var first = LocalizationManager.AddConfig(root);
            using var second = LocalizationManager.AddConfig(root);
            Assert.That(refreshes, Is.EqualTo(2));
            Assert.That(LocalizationManager.Configs.Count, Is.EqualTo(1));
            first.Dispose();
            Assert.That(LocalizationManager.GetLanguageInfo(1, SystemLanguage.Japanese).str, Is.EqualTo("English {0}"));
            second.Dispose();
            Assert.That(LocalizationManager.GetLanguageInfo(1, SystemLanguage.Japanese), Is.Null);
            token.UnRegister();
        }

        [Test]
        public void DuplicateValidation_ChecksEveryLanguageAndRollsBackFailedRegistration()
        {
            var other = ScriptableObject.CreateInstance<MultiLanguageConfigs>();
            var duplicate = ScriptableObject.CreateInstance<LanguageConfig>();
            duplicate.language = SystemLanguage.English;
            duplicate.languagesInfo.Add(new LanguageInfo { id = 1, str = "duplicate" });
            other.Languages.Add(duplicate);
            try
            {
                using var handle = LocalizationManager.AddConfig(root);
                Assert.Throws<InvalidOperationException>(() => LocalizationManager.AddConfig(other));
                Assert.That(LocalizationManager.Configs.Count, Is.EqualTo(1));
                Assert.That(LocalizationManager.GetLanguageInfo(1, SystemLanguage.English).str, Is.EqualTo("English {0}"));
            }
            finally { UnityEngine.Object.DestroyImmediate(other); UnityEngine.Object.DestroyImmediate(duplicate); }
        }

        [Test]
        public void EmptyImageEntry_FallsBackRatherThanDereferencingNull()
        {
            chinese.languagesInfo[0].type = MultiLanguageType.Image;
            chinese.languagesInfo[0].image = null;
            using var handle = LocalizationManager.AddConfig(root);
            Assert.That(LocalizationManager.GetLanguageInfo(1, SystemLanguage.Chinese).str, Is.EqualTo("English {0}"));
        }
    }
}
