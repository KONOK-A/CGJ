using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.ResourceBuild
{
    public class ResourceCatalogBuildTests
    {
        internal static ResourceCatalogDocument Create(ResourceBuildResult result, string app = "fixture-v1")
        {
            var creator = typeof(ResourceProject).Assembly.GetType("YLFramework.Editor.Resource.ResourceCatalogBuilder");
            Assert.That(creator, Is.Not.Null, "The catalog must be generated from verified native build results.");
            return (ResourceCatalogDocument)creator.GetMethod("Create").Invoke(null, new object[] { result, app });
        }

        [Test]
        public void RealManifestAssetsSubassetsGroupsAndArtifactIdentitiesRoundTrip()
        {
            using var fixture = new ResourceReleaseFixture();
            var result = fixture.Build();
            var document = Create(result);
            var bytes = ResourceCatalogCodec.Write(document);
            var read = ResourceCatalogCodec.Read(bytes, result.Map.ProjectId, EditorUserBuildSettings.activeBuildTarget.ToString(), Application.unityVersion, "fixture-v1");
            Assert.That(read.CatalogDigest, Is.EqualTo(ResourceCatalogCodecTests.Hash(bytes)));
            Assert.That(read.Bundles.Count, Is.EqualTo(result.Map.Bundles.Count));
            foreach (var bundle in read.Bundles)
            {
                var native = result.Bundles.Single(x => x.Name == bundle.LogicalId);
                CollectionAssert.AreEqual(native.Dependencies, bundle.Dependencies);
                Assert.That(bundle.UnityHash, Is.EqualTo(native.UnityHash));
                Assert.That(bundle.Sha256, Is.EqualTo(result.Files.Single(x => x.Name == bundle.NativeName).Sha256));
                Assert.That(bundle.RelativeFile, Is.EqualTo("b_" + ResourceCatalogCodecTests.Hash(System.Text.Encoding.UTF8.GetBytes(bundle.NativeName))[..32]));
            }
            foreach (var main in result.Map.Assets)
            {
                var asset = read.Assets.Single(x => x.AssetId == main.AssetId);
                Assert.That(asset.Guid, Is.EqualTo(main.Guid));
                Assert.That(asset.LocalId, Is.EqualTo(main.LocalId));
                Assert.That(asset.Address, Is.EqualTo(main.Address.Path));
                Assert.That(asset.BundleId, Is.EqualTo(main.BundleName));
                Assert.That(asset.Kind, Is.EqualTo(main.IsScene ? "Scene" : "Asset"));
            }
            var sprites = read.Assets.Where(x => x.Kind == "SubAsset").ToArray();
            CollectionAssert.AreEquivalent(new[] { "Left", "Right" }, sprites.Select(x => x.SubAssetName));
            Assert.That(sprites.All(x => x.AssetType == typeof(Sprite).FullName && x.SubAssetKey == x.LocalId.ToString()), Is.True);
            foreach (var group in read.Groups)
                CollectionAssert.AreEqual(result.Map.Groups[group.Id], group.Members.Select(x => x.AssetId));
            var loaded = new Dictionary<string, AssetBundle>();
            try
            {
                foreach (var bundle in read.Bundles.OrderBy(x => x.Dependencies.Count))
                    loaded.Add(bundle.LogicalId, AssetBundle.LoadFromFile(Path.Combine(result.WorkspacePath, bundle.NativeName)));
                foreach (var sprite in sprites)
                {
                    var matches = loaded[sprite.BundleId].LoadAssetWithSubAssets<Sprite>(sprite.LoadName).Where(x => x.name == sprite.SubAssetName).ToArray();
                    Assert.That(matches.Length, Is.EqualTo(1));
                    Assert.That(matches[0].rect.width, Is.EqualTo(2));
                }
                var scene = read.Assets.Single(x => x.Kind == "Scene");
                CollectionAssert.Contains(loaded[scene.BundleId].GetAllScenePaths(), scene.LoadName);
            }
            finally { foreach (var bundle in loaded.Values.Reverse()) if (bundle) bundle.Unload(true); }
            fixture.AssertProtected();
        }
    }

    internal sealed class ResourceReleaseFixture : IDisposable
    {
        internal readonly string Folder;
        internal readonly string TextPath;
        internal ResourceProject Project => AssetDatabase.LoadAssetAtPath<ResourceProject>(Folder + "/Project.asset");
        private readonly HashSet<string> ownedWorkspaces = new(StringComparer.Ordinal);
        private readonly string[] protectedBefore;
        internal string ReleaseRoot;
        internal bool RetainRelease;

        internal ResourceReleaseFixture()
        {
            protectedBefore = ProtectedSnapshot();
            const string parent = "Assets/YLFramework/Tests/Fixtures";
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets/YLFramework/Tests", "Fixtures");
            Folder = parent + "/Release82b-" + Guid.NewGuid().ToString("N");
            Assert.That(AssetDatabase.CreateFolder(parent, Path.GetFileName(Folder)), Is.Not.Empty);
            AssetDatabase.CreateFolder(Folder, "Content");
            TextPath = Folder + "/Content/entry.txt";
            File.WriteAllText(TextPath, "first release content");
            AssetDatabase.ImportAsset(TextPath, ImportAssetOptions.ForceSynchronousImport);
            var shared = new Texture2D(2, 2) { name = "Shared" };
            AssetDatabase.CreateAsset(shared, Folder + "/Content/Shared.asset");
            var prefabs = new[] { Prefab("A", shared), Prefab("B", shared) };
            var png = new Texture2D(4, 2);
            png.SetPixels(Enumerable.Repeat(Color.red, 8).ToArray()); png.Apply();
            var spritePath = Folder + "/Content/Sprites.png";
            File.WriteAllBytes(spritePath, png.EncodeToPNG());
            Object.DestroyImmediate(png);
            AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritesheet = new[] { new SpriteMetaData { name = "Left", rect = new Rect(0, 0, 2, 2), pivot = Vector2.one / 2 },
                new SpriteMetaData { name = "Right", rect = new Rect(2, 0, 2, 2), pivot = Vector2.one / 2 } };
            importer.SaveAndReimport();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var scenePath = Folder + "/Content/Owned.unity";
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var material = new Material(Shader.Find("Unlit/Texture")) { mainTexture = shared };
                AssetDatabase.CreateAsset(material, Folder + "/Content/SceneMaterial.mat");
                GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshRenderer>().sharedMaterial = material;
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            var project = ScriptableObject.CreateInstance<ResourceProject>();
            project.ProjectId = "yl-release-" + Guid.NewGuid().ToString("N");
            project.AssetRootGuid = AssetDatabase.AssetPathToGUID(Folder + "/Content");
            foreach (var path in new[] { TextPath }.Concat(prefabs).Concat(new[] { spritePath, scenePath }))
                project.Groups.Add(new ResourceGroup { Id = Path.GetFileNameWithoutExtension(path),
                    Selections = new List<ResourceSelection> { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(path) } } });
            AssetDatabase.CreateAsset(project, Folder + "/Project.asset");
        }

        internal ResourceBuildResult Build()
        {
            var expected = ResourceBundleBuilder.GetWorkspacePath(Project, EditorUserBuildSettings.activeBuildTarget);
            var root = Path.GetDirectoryName(expected);
            if (ownedWorkspaces.Add(root)) Assert.That(Directory.Exists(root), Is.False, "Do not adopt any existing fixture workspace.");
            return ResourceBundleBuilder.Build(Project, EditorUserBuildSettings.activeBuildTarget);
        }
        private string Prefab(string name, Texture texture)
        {
            var material = new Material(Shader.Find("Unlit/Texture")) { name = name, mainTexture = texture };
            AssetDatabase.CreateAsset(material, Folder + "/Content/" + name + ".mat");
            var go = new GameObject(name);
            try
            {
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                var path = Folder + "/Content/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, path);
                return path;
            }
            finally { Object.DestroyImmediate(go); }
        }
        internal void AssertProtected() => CollectionAssert.AreEqual(protectedBefore, ProtectedSnapshot());
        internal static string[] Snapshot(string root) => !Directory.Exists(root) ? Array.Empty<string>() :
            Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)
                .Where(x => !Path.GetFileName(x).EndsWith(".lock", StringComparison.Ordinal))
                .Select(x => x.Substring(root.Length) + ":" + ResourceCatalogCodecTests.Hash(File.ReadAllBytes(x))).ToArray();
        private static string[] ProtectedSnapshot() => new[] { "Assets/Games", "Assets/Scenes", "Assets/StreamingAssets", "AssetBundles/MainModule", "Packages", "ProjectSettings" }
            .SelectMany(x => Snapshot(Path.GetFullPath(x)).Select(f => x + f)).ToArray();
        public void Dispose()
        {
            AssertProtected();
            foreach (var root in ownedWorkspaces) if (Directory.Exists(root)) Directory.Delete(root, true);
            if (!RetainRelease && ReleaseRoot != null && Directory.Exists(ReleaseRoot)) Directory.Delete(ReleaseRoot, true);
            AssetDatabase.DeleteAsset(Folder);
        }
    }
}
