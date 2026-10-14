using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceEditorTests
    {
        private string _folder;
        private Object[] _selection;

        [Test]
        public void ResourceEditorPathsFollowBuildServiceCodeGenerationAndDataResponsibilities()
        {
            const string root = "Assets/YLFramework/Editor/Systems/Resource/";
            foreach (var file in new[] { "Build/AssetBundleExporter.cs", "Build/BuildScript.cs", "Build/ResourceBuildOperation.cs",
                "Service/ResServiceAssetsMenu.cs", "Service/ResServiceEditorAPI.cs", "Service/ResServiceEditorWindow.cs",
                "CodeGeneration/ResDataCodeGenerator.cs", "CodeGeneration/ResReferenceOutput.cs" })
            {
                Assert.That(File.Exists(root + file), Is.True, file);
                Assert.That(File.Exists(root + file + ".meta"), Is.True, file);
            }
            Assert.That(Directory.GetFiles(root, "*.cs"), Is.Empty);
            Assert.That(Directory.GetFiles(root + "Data", "*.cs"), Is.Not.Empty);
        }

        [SetUp]
        public void SetUp()
        {
            _selection = Selection.objects;
            _folder = "Assets/YLFramework/Tests/EditMode/Resource62-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode", Path.GetFileName(_folder));
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _selection;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { _folder }))
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer) Undo.ClearUndo(importer);
            }
            AssetDatabase.DeleteAsset(_folder);
            AssetDatabase.RemoveUnusedAssetBundleNames();
        }

        private string Text(string folder, string name, string bundle = null)
        {
            var path = folder + "/" + name + ".bytes";
            File.WriteAllText(path, "Resource62 isolated text");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (bundle != null)
            {
                var importer = AssetImporter.GetAtPath(path);
                importer.assetBundleName = bundle;
                importer.SaveAndReimport();
            }
            return path;
        }

        private SubProjectData Owner(string folder, string name)
        {
            var asset = ScriptableObject.CreateInstance<SubProject>();
            AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset");
            return SubProjectData.SearchAllInProject().Single(x => x.Path == folder + "/" + name + ".asset");
        }

        [Test]
        public void DirectoryBoundaryDoesNotClaimSiblingWithSamePrefix()
        {
            AssetDatabase.CreateFolder(_folder, "Owner");
            AssetDatabase.CreateFolder(_folder, "OwnerExtra");
            var owner = Owner(_folder + "/Owner", "Project");
            var bundle = "resource62-" + Guid.NewGuid().ToString("N");
            Text(_folder + "/OwnerExtra", "Text", bundle);
            var defaults = new SubProjectData();
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, new List<SubProjectData> { owner });
            Assert.IsEmpty(owner.Builds);
            Assert.IsTrue(defaults.Builds.Any(x => x.assetBundleName == bundle));
        }

        [Test]
        public void NestedOwnersAreRejectedBeforeAnyBuildListChanges()
        {
            AssetDatabase.CreateFolder(_folder, "Child");
            var parent = Owner(_folder, "Parent");
            var child = Owner(_folder + "/Child", "Child");
            Text(_folder + "/Child", "Text", "resource62-" + Guid.NewGuid().ToString("N"));
            var defaults = new SubProjectData();
            Assert.Throws<InvalidOperationException>(() => SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(
                defaults, new List<SubProjectData> { parent, child }));
            Assert.IsEmpty(defaults.Builds);
            Assert.IsEmpty(parent.Builds);
            Assert.IsEmpty(child.Builds);
        }

        [Test]
        public void OneBundleAcrossDefaultAndSubprojectIsRejected()
        {
            AssetDatabase.CreateFolder(_folder, "Owner");
            var owner = Owner(_folder + "/Owner", "Project");
            var bundle = "resource62-" + Guid.NewGuid().ToString("N");
            Text(_folder, "Default", bundle);
            Text(_folder + "/Owner", "Owned", bundle);
            Assert.Throws<InvalidOperationException>(() => SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(
                new SubProjectData(), new List<SubProjectData> { owner }));
        }

        [Test]
        public void RepeatedPartitionDoesNotDuplicateBuilds()
        {
            var owner = Owner(_folder, "Project");
            Text(_folder, "Text", "resource62-" + Guid.NewGuid().ToString("N"));
            var defaults = new SubProjectData();
            var owners = new List<SubProjectData> { owner };
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, owners);
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, owners);
            Assert.AreEqual(1, owner.Builds.Count);
        }

        [Test]
        public void MarkAndCancelPersistAcrossImportWithInvariantCase()
        {
            var path = Text(_folder, "FILE.I");
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
                ResServiceAssetsMenu.MarkAB(path);
                Assert.IsTrue(ResServiceAssetsMenu.IsMarked(path));
                Assert.AreEqual("file_i_bytes", AssetImporter.GetAtPath(path).assetBundleName);
                StringAssert.Contains("assetBundleName: file_i_bytes", File.ReadAllText(path + ".meta"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Assert.IsTrue(ResServiceAssetsMenu.IsMarked(path));
                ResServiceAssetsMenu.MarkAB(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                Assert.IsEmpty(AssetImporter.GetAtPath(path).assetBundleName);
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }

        [Test]
        public void UnknownExistingBundleMarkIsNotOverwritten()
        {
            var path = Text(_folder, "Text", "foreign-" + Guid.NewGuid().ToString("N"));
            var before = File.ReadAllBytes(path + ".meta");
            Assert.Throws<InvalidOperationException>(() => ResServiceAssetsMenu.MarkAB(path));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path + ".meta"));
        }

        [Test]
        public void BuildCancellationDoesNotCreateOutputDirectories()
        {
            var name = "Resource62-" + Guid.NewGuid().ToString("N");
            Owner(_folder, name);
            Text(_folder, "Text", name.ToLowerInvariant());
            var output = Path.GetFullPath("AssetBundles/" + name + "/Windows");
            var streaming = Path.Combine(Application.streamingAssetsPath, name, "AssetBundles", "Windows");
            Func<string, bool> cancel = summary =>
            {
                StringAssert.Contains(output, summary);
                StringAssert.Contains(Path.GetFullPath(streaming), summary);
                return false;
            };
            Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> neverBuild = (_, _, _) =>
            { Assert.Fail("Cancelled operation must not invoke BuildPipeline"); return null; };
            Assert.IsFalse((bool)Invoke(typeof(BuildScript), "RunBuild", EditorUserBuildSettings.activeBuildTarget, cancel, neverBuild));
            Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(output)));
            Assert.IsFalse(Directory.Exists(Path.Combine(Application.streamingAssetsPath, name)));
        }

        [Test]
        public void MenuValidationTracksCurrentSingleSelectionWithoutReplacingSubscribers()
        {
            var first = Text(_folder, "First");
            var second = Text(_folder, "Second");
            var notifications = 0;
            Action listener = () => notifications++;
            Selection.selectionChanged += listener;
            try
            {
                Selection.objects = new[] { AssetDatabase.LoadAssetAtPath<Object>(first) };
                Assert.IsTrue((bool)Invoke(typeof(ResServiceAssetsMenu), "ValidateMarkDirOrFile"));
                Assert.IsFalse(Menu.GetChecked("Assets/YLFramework - AssetBundle Mark"));
                ResServiceAssetsMenu.MarkDirOrFile();
                Assert.IsTrue(Menu.GetChecked("Assets/YLFramework - AssetBundle Mark"));
                Selection.objects = new[] { AssetDatabase.LoadAssetAtPath<Object>(second) };
                Invoke(typeof(ResServiceAssetsMenu), "ValidateMarkDirOrFile");
                Assert.IsFalse(Menu.GetChecked("Assets/YLFramework - AssetBundle Mark"));
                Selection.objects = new[] { AssetDatabase.LoadAssetAtPath<Object>(first), AssetDatabase.LoadAssetAtPath<Object>(second) };
                Assert.IsFalse((bool)Invoke(typeof(ResServiceAssetsMenu), "ValidateMarkDirOrFile"));
                Assert.Greater(notifications, 0);
                foreach (var path in new[] { "Assets", "Packages/com.unity.test-framework/package.json" })
                    Assert.IsFalse((bool)Invoke(typeof(ResServiceAssetsMenu), "IsValidAssetPath", path));
            }
            finally { Selection.selectionChanged -= listener; }
        }

        [Test]
        public void BundleFileAndDirectoryCollisionIsRejectedBeforeBuild()
        {
            var name = "resource62-" + Guid.NewGuid().ToString("N");
            Text(_folder, "First", name);
            Text(_folder, "Second", name + "/second");
            Assert.Throws<InvalidOperationException>(() => SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(
                new SubProjectData(), new List<SubProjectData>()));
        }

        [Test]
        public void MarkValidatorRejectsJunctionSourceWithoutImportingOrWritingIt()
        {
            var outside = Path.GetFullPath("Temp/Resource62-" + Guid.NewGuid().ToString("N"));
            var link = Path.GetFullPath(_folder + "/Linked");
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "Foreign.bytes"), "keep external source");
            try
            {
                ResourceOutputSafetyTests.CreateJunction(link, outside);
                Assert.IsFalse((bool)Invoke(typeof(ResServiceAssetsMenu), "IsValidAssetPath", _folder + "/Linked/Foreign.bytes"));
                Assert.AreEqual("keep external source", File.ReadAllText(Path.Combine(outside, "Foreign.bytes")));
            }
            finally
            {
                if (Directory.Exists(link)) Directory.Delete(link);
                Directory.Delete(outside, true);
            }
        }

        [TestCase("Assets/../Packages/manifest.json")]
        public void InvalidMarkTargetIsRejected(string path)
        {
            Assert.Throws<ArgumentException>(() => ResServiceAssetsMenu.MarkAB(path));
        }

        internal static object Invoke(Type type, string method, params object[] args)
        {
            var member = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .SingleOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            Assert.IsNotNull(member, "Missing operation: " + method);
            try { return member.Invoke(null, args); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
    }
}
