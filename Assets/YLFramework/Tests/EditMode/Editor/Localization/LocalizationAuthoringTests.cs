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
        private string _path;
        private string _languagesPath;
        private MultiLanguageConfigs _root;
        private LanguageConfig _english;
        private LanguageConfig _chinese;

        [SetUp]
        public void SetUp()
        {
            const string Parent = "Assets/YLFramework/Tests/Fixtures/Extensions/Data/Localization";
            EnsureFolder(Parent);
            _path = Parent + "/Run-" + Guid.NewGuid().ToString("N") + ".asset";
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(_path);
            _root = ScriptableObject.CreateInstance<MultiLanguageConfigs>();
            AssetDatabase.CreateAsset(_root, _path);
            _languagesPath = LocalizationAuthoring.GetLanguageFolder(_root);
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(_languagesPath);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root)
            {
                Undo.ClearUndo(_root);
            }

            if (_english)
            {
                Undo.ClearUndo(_english);
            }

            if (_chinese)
            {
                Undo.ClearUndo(_chinese);
            }

            if (!string.IsNullOrEmpty(_languagesPath))
            {
                AssetDatabase.DeleteAsset(_languagesPath);
            }

            if (!string.IsNullOrEmpty(_path))
            {
                AssetDatabase.DeleteAsset(_path);
            }
        }

        [Test]
        public void AuthoringCreateDuplicateUndoSaveReopenAndRemoveReferenceKeepsAsset()
        {
            _english = LocalizationAuthoring.CreateLanguage(_root, SystemLanguage.English);
            LocalizationAuthoring.AddEntry(_root, 1, MultiLanguageType.String);
            _english.languagesInfo[0].str = "source";
            _chinese = LocalizationAuthoring.CreateLanguage(_root, SystemLanguage.ChineseSimplified);
            Assert.That(_chinese.languagesInfo[0].id, Is.EqualTo(1));
            Assert.That(_chinese.languagesInfo[0].str, Is.Null, "A new language must not impersonate the source translation.");
            LocalizationAuthoring.DuplicateEntry(_root, 1, 2);
            Assert.That(_english.GetLanguageInfo(2).str, Is.EqualTo("source"));
            var languagePath = AssetDatabase.GetAssetPath(_chinese);
            Undo.IncrementCurrentGroup();
            LocalizationAuthoring.RemoveLanguageReference(_root, _chinese);
            Undo.FlushUndoRecordObjects();
            Assert.That(AssetDatabase.LoadAssetAtPath<LanguageConfig>(languagePath), Is.SameAs(_chinese));
            Undo.PerformUndo();
            Assert.That(_root.Languages.Contains(_chinese), Is.True);
            EditorUtility.SetDirty(_english);
            AssetDatabase.SaveAssetIfDirty(_english);
            AssetDatabase.SaveAssetIfDirty(_root);
            AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
            var reopened = AssetDatabase.LoadAssetAtPath<MultiLanguageConfigs>(_path);
            Assert.That(reopened.Languages.Count, Is.EqualTo(2));
            Assert.That(reopened.GetLanguageConfigData(SystemLanguage.English).GetLanguageInfo(2).str, Is.EqualTo("source"));
            Assert.Throws<InvalidOperationException>(() => LocalizationAuthoring.AddEntry(reopened, 2, MultiLanguageType.String));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
