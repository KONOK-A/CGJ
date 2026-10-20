using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.DataAssets;
using YLFramework.DataAssets.Editor;

namespace YLFramework.Tests.Extensions.Data
{
    public class DataAssetsWorkbookMemoryTests
    {
        private ScriptableObjectConfigs root;
        private string folder;

        [SetUp]
        public void SetUp()
        {
            string name = "YLWorkbookMemory-" + Guid.NewGuid().ToString("N");
            folder = "Assets/" + name;
            YLFramework.Tests.DataAssets.DataAcceptanceOwnership.Own(folder);
            AssetDatabase.CreateFolder("Assets", name);
            root = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            AssetDatabase.CreateAsset(root, folder + "/Root.asset");
        }

        [TearDown]
        public void TearDown()
        {
            if (root) DataAssetsWorkbookMemory.Forget(root);
            if (!string.IsNullOrEmpty(folder)) AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void RemembersExistingWorkbookAndStopsWhenFileDisappears()
        {
            string path = Path.Combine(Path.GetTempPath(), "YL-workbook-" + Guid.NewGuid().ToString("N") + ".xlsx");
            File.WriteAllBytes(path, new byte[] { 1 });
            try
            {
                Assert.That(DataAssetsWorkbookMemory.CanReveal(root), Is.False);
                Assert.That(DataAssetsWorkbookMemory.Remember(root, path), Is.True);
                Assert.That(DataAssetsWorkbookMemory.TryGetPath(root, out var stored), Is.True);
                Assert.That(stored, Is.EqualTo(Path.GetFullPath(path)));
                Assert.That(DataAssetsWorkbookMemory.CanReveal(root), Is.True);
                File.Delete(path);
                Assert.That(DataAssetsWorkbookMemory.CanReveal(root), Is.False);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Test]
        public void RejectsNonWorkbookAndUnsavedAsset()
        {
            Assert.That(DataAssetsWorkbookMemory.Remember(root, "not-a-workbook.txt"), Is.False);
            Assert.That(DataAssetsWorkbookMemory.TryGetPath(root, out _), Is.False);
            var unsaved = ScriptableObject.CreateInstance<ScriptableObjectConfigs>();
            try { Assert.That(DataAssetsWorkbookMemory.Remember(unsaved, "somewhere.xlsx"), Is.False); }
            finally { UnityEngine.Object.DestroyImmediate(unsaved); }
        }
    }
}
