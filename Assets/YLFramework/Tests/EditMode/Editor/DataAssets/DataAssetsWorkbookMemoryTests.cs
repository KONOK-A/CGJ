using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.DataAssets;
using YLFramework.Editor.DataAssets;

namespace YLFramework.Tests.Extensions.Data
{
    public class DataAssetsWorkbookMemoryTests
    {
        private ScriptableObjectConfigs _root;
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            string name = "YLWorkbookMemory-" + Guid.NewGuid().ToString("N");
            _folder = "Assets/" + name;
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(_folder);
            AssetDatabase.CreateFolder("Assets", name);
            _root = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            AssetDatabase.CreateAsset(_root, _folder + "/Root.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root)
            {
                DataAssetsWorkbookMemory.Forget(_root);
            }

            if (!string.IsNullOrEmpty(_folder))
            {
                AssetDatabase.DeleteAsset(_folder);
            }
        }

        [Test]
        public void RemembersExistingWorkbookAndStopsWhenFileDisappears()
        {
            string path = Path.Combine(Path.GetTempPath(), "YL-workbook-" + Guid.NewGuid().ToString("N") + ".xlsx");
            File.WriteAllBytes(path, new byte[] { 1 });
            try
            {
                Assert.That(DataAssetsWorkbookMemory.CanReveal(_root), Is.False);
                Assert.That(DataAssetsWorkbookMemory.Remember(_root, path), Is.True);
                Assert.That(DataAssetsWorkbookMemory.TryGetPath(_root, out var stored), Is.True);
                Assert.That(stored, Is.EqualTo(Path.GetFullPath(path)));
                Assert.That(DataAssetsWorkbookMemory.CanReveal(_root), Is.True);
                File.Delete(path);
                Assert.That(DataAssetsWorkbookMemory.CanReveal(_root), Is.False);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Test]
        public void RejectsNonWorkbookAndUnsavedAsset()
        {
            Assert.That(DataAssetsWorkbookMemory.Remember(_root, "not-a-workbook.txt"), Is.False);
            Assert.That(DataAssetsWorkbookMemory.TryGetPath(_root, out _), Is.False);
            var unsaved = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            try
            {
                Assert.That(DataAssetsWorkbookMemory.Remember(unsaved, "somewhere.xlsx"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(unsaved);
            }
        }
    }
}
