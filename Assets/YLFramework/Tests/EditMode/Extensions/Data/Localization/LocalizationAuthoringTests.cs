using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Localization;
using YLFramework.Localization;

namespace YLFramework.Tests.Extensions.Data
{
    public class LocalizationAuthoringTests
    {
        private string path;
        private string languagesPath;
        private MultiLanguageConfigs root;
        private LanguageConfig english;
        private LanguageConfig chinese;

        [SetUp]
        public void SetUp()
        {
            const string parent = "Assets/YLFramework/Tests/Fixtures/Extensions/Data/Localization";
            EnsureFolder(parent);
            path = parent + "/Run-" + Guid.NewGuid().ToString("N") + ".asset";
            root = ScriptableObject.CreateInstance<MultiLanguageConfigs>();
            AssetDatabase.CreateAsset(root, path);
            languagesPath = LocalizationAuthoring.GetLanguageFolder(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Undo.ClearUndo(root);
            if (english) Undo.ClearUndo(english);
            if (chinese) Undo.ClearUndo(chinese);
            if (!string.IsNullOrEmpty(languagesPath)) AssetDatabase.DeleteAsset(languagesPath);
            if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void Authoring_CreateDuplicateUndoSaveReopenAndRemoveReferenceKeepsAsset()
        {
            english = LocalizationAuthoring.CreateLanguage(root, SystemLanguage.English);
            LocalizationAuthoring.AddEntry(root, 1, MultiLanguageType.String);
            english.languagesInfo[0].str = "source";
            chinese = LocalizationAuthoring.CreateLanguage(root, SystemLanguage.ChineseSimplified);
            Assert.That(chinese.languagesInfo[0].id, Is.EqualTo(1));
            Assert.That(chinese.languagesInfo[0].str, Is.Null, "A new language must not impersonate the source translation.");
            LocalizationAuthoring.DuplicateEntry(root, 1, 2);
            Assert.That(english.GetLanguageInfo(2).str, Is.EqualTo("source"));
            var languagePath = AssetDatabase.GetAssetPath(chinese);
            Undo.IncrementCurrentGroup();
            LocalizationAuthoring.RemoveLanguageReference(root, chinese);
            Undo.FlushUndoRecordObjects();
            Assert.That(AssetDatabase.LoadAssetAtPath<LanguageConfig>(languagePath), Is.SameAs(chinese));
            Undo.PerformUndo();
            Assert.That(root.Languages.Contains(chinese), Is.True);
            EditorUtility.SetDirty(english);
            AssetDatabase.SaveAssetIfDirty(english);
            AssetDatabase.SaveAssetIfDirty(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath<MultiLanguageConfigs>(path);
            Assert.That(reopened.Languages.Count, Is.EqualTo(2));
            Assert.That(reopened.GetLanguageConfigData(SystemLanguage.English).GetLanguageInfo(2).str, Is.EqualTo("source"));
            Assert.Throws<InvalidOperationException>(() => LocalizationAuthoring.AddEntry(reopened, 2, MultiLanguageType.String));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
