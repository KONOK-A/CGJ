using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceBuildIntegrationTests
    {
        [Test]
        public void IsolatedDefaultAndSubprojectBuildReadFailureAndCleanupRoundTrip()
        {
            Assert.IsEmpty(AssetDatabase.GetAllAssetBundleNames(), "Integration probe cannot run alongside existing Bundle marks.");
            Assert.IsEmpty(AssetDatabase.FindAssets("t:SubProject"), "Integration probe cannot take over existing owners.");
            var token = Guid.NewGuid().ToString("N");
            var folder = "Assets/YLFramework/Tests/EditMode/Build62-" + token;
            var name = "Build62-" + token;
            var platform = AssetBundleHelper.GetPlatformName();
            Assert.AreEqual("Windows", platform, "This controlled integration test requires the existing Windows target.");
            var defaults = Path.GetFullPath("AssetBundles/Windows");
            var streamDefault = Path.GetFullPath(Application.streamingAssetsPath + "/AssetBundles/Windows");
            var subproject = Path.GetFullPath("AssetBundles/" + name + "/Windows");
            var streamProject = Path.GetFullPath(Application.streamingAssetsPath + "/" + name + "/AssetBundles/Windows");
            // 不接管预先存在的默认产物，即使看起来归属正确也不作为测试清理对象。
            foreach (var path in new[] { defaults, streamDefault, Path.GetDirectoryName(subproject), Path.GetDirectoryName(Path.GetDirectoryName(streamProject)) })
                Assert.IsFalse(Directory.Exists(path) || File.Exists(path), "Test output already exists: " + path);
            var hadStreamingContainer = Directory.Exists(Path.GetDirectoryName(streamDefault));
            var paths = new[] { defaults, streamDefault, subproject, streamProject };
            var assets = new[] { folder + "/First.bytes", folder + "/Second.bytes", folder + "/Project/Project.bytes" };
            var operation = typeof(BuildScript).Assembly.GetType("YLFramework.Editor.Resource.ResourceBuildOperation");
            var manifests = new List<AssetBundleManifest>();
            var replayManifests = new List<AssetBundleManifest>();
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/EditMode", Path.GetFileName(folder));
            try
            {
                AssetDatabase.CreateFolder(folder, "Project");
                var owner = ScriptableObject.CreateInstance<SubProject>();
                AssetDatabase.CreateAsset(owner, folder + "/Project/" + name + ".asset");
                var firstName = "resource62-" + token + "/first";
                var secondName = "resource62-" + token + "/second";
                var projectName = "project62-" + token;
                CreateText(folder + "/First.bytes", firstName, "first payload");
                CreateText(folder + "/Second.bytes", secondName, "second payload");
                CreateText(folder + "/Project/Project.bytes", projectName, "project payload");
                Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> build = (path, builds, target) =>
                {
                    Assert.AreEqual(EditorUserBuildSettings.activeBuildTarget, target);
                    Assert.AreEqual("Windows", Path.GetFileName(path));
                    Assert.IsFalse(paths.Any(output => output == Path.GetFullPath(path)), "Native build must run in staging, not live output.");
                    var manifest = BuildPipeline.BuildAssetBundles(path, builds,
                        BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
                    manifests.Add(manifest);
                    return manifest;
                };
                Assert.IsTrue((bool)ResourceEditorTests.Invoke(typeof(BuildScript), "RunBuild", EditorUserBuildSettings.activeBuildTarget,
                    new Func<string, bool>(_ => { CheckedPlan(true); return true; }), build));
                Assert.AreEqual(2, manifests.Count, "One native map for defaults, one for the isolated subproject.");
                foreach (var path in paths) Assert.IsTrue(File.Exists(Path.Combine(path, ResData.FileName)));
                var table = new ResData();
                table.LoadFromFile(Path.Combine(streamDefault, ResData.FileName));
                AssertRecord(table, "first", firstName, 0);
                AssertRecord(table, "second", secondName, 1);
                var projectTable = new ResData();
                projectTable.LoadFromFile(Path.Combine(streamProject, ResData.FileName));
                AssertRecord(projectTable, "project", projectName, 0);
                var bundle = AssetBundle.LoadFromFile(Path.Combine(streamDefault, firstName));
                Assert.IsNotNull(bundle);
                try { Assert.AreEqual("first payload", bundle.LoadAsset<TextAsset>(folder + "/First.bytes").text); }
                finally { bundle.Unload(true); }

                var tableMeta = Path.Combine(streamDefault, ResData.FileName + ".meta");
                Assert.IsTrue(File.Exists(tableMeta));
                var meta = File.ReadAllBytes(tableMeta);
                var existing = paths.SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    .ToDictionary(path => path, File.ReadAllBytes);
                var calls = 0;
                Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> laterFailure = (stage, _, _) =>
                {
                    if (calls++ > 0) return null;
                    CopyNativeOutput(defaults, stage);
                    return ReadManifest(defaults);
                };
                Assert.Throws<InvalidOperationException>(() => ResourceEditorTests.Invoke(typeof(BuildScript), "RunBuild",
                    EditorUserBuildSettings.activeBuildTarget, new Func<string, bool>(_ => { CheckedPlan(true); return true; }), laterFailure));
                Assert.AreEqual(2, calls, "The failure must occur after staging the first owner, not on a stale native manifest.");
                foreach (var file in existing) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), "Failure changed " + file.Key);
                foreach (var replay in replayManifests) UnityEngine.Object.DestroyImmediate(replay);
                replayManifests.Clear();

                // 重用本次实际构建的 manifest/文件，只验证事务发布与 meta 保留，不再次调用原生构建。
                calls = 0;
                Func<string, AssetBundleBuild[], BuildTarget, AssetBundleManifest> publish = (stage, _, _) =>
                {
                    var index = calls++;
                    CopyNativeOutput(index == 0 ? defaults : subproject, stage);
                    return ReadManifest(index == 0 ? defaults : subproject);
                };
                Assert.IsTrue((bool)ResourceEditorTests.Invoke(typeof(BuildScript), "RunBuild", EditorUserBuildSettings.activeBuildTarget,
                    new Func<string, bool>(_ => { CheckedPlan(true); return true; }), publish));
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(tableMeta), "Publishing must preserve existing config asset identity.");

                existing = paths.SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    .ToDictionary(path => path, File.ReadAllBytes);
                calls = 0;
                using (var locked = new FileStream(Path.Combine(streamDefault, ResData.FileName), FileMode.Open, FileAccess.Read, FileShare.None))
                    Assert.Throws<IOException>(() => ResourceEditorTests.Invoke(typeof(BuildScript), "RunBuild", EditorUserBuildSettings.activeBuildTarget,
                        new Func<string, bool>(_ => { CheckedPlan(true); return true; }), publish));
                foreach (var file in existing) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key), "Publication rollback changed " + file.Key);
                AssetBundleExporter.BuildDataTable(new[] { firstName, secondName }, streamDefault);
                CollectionAssert.AreEqual(meta, File.ReadAllBytes(tableMeta));
                var refreshed = new ResData();
                refreshed.LoadFromFile(Path.Combine(streamDefault, ResData.FileName));
                AssertRecord(refreshed, "second", secondName, 1);

                var oldTable = File.ReadAllBytes(Path.Combine(streamDefault, ResData.FileName));
                var renamed = folder + "/Renamed.bytes";
                Assert.IsEmpty(AssetDatabase.MoveAsset(assets[0], renamed));
                try
                {
                    Assert.Throws<InvalidOperationException>(() => AssetBundleExporter.BuildDataTable(null, streamDefault));
                    CollectionAssert.AreEqual(oldTable, File.ReadAllBytes(Path.Combine(streamDefault, ResData.FileName)),
                        "Exporting changed source names must not replace the table for old Bundles.");
                }
                finally { Assert.IsEmpty(AssetDatabase.MoveAsset(renamed, assets[0])); }

                var clearPlan = CheckedPlan(false);
                Assert.IsTrue((bool)ResourceEditorTests.Invoke(operation, "Execute", clearPlan, false, new Func<string, bool>(_ => true), null));
                foreach (var path in paths) Assert.IsFalse(Directory.Exists(path));
                Assert.IsTrue(Directory.Exists("AssetBundles/MainModule"), "Only read presence; never operate on MainModule.");
            }

            finally
            {
                // 以下位置全部在运行前确认不存在，仅清理由本用例创建的资产和目录。
                foreach (var path in paths)
                {
                    if (path.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        var assetPath = "Assets/" + path.Substring(Path.GetFullPath(Application.dataPath).Length + 1).Replace('\\', '/');
                        if (Directory.Exists(path)) AssetDatabase.DeleteAsset(assetPath);
                    }
                    else if (Directory.Exists(path)) Directory.Delete(path, true);
                }
                if (Directory.Exists("AssetBundles/" + name)) Directory.Delete("AssetBundles/" + name, true);
                if (AssetDatabase.IsValidFolder("Assets/StreamingAssets/" + name)) AssetDatabase.DeleteAsset("Assets/StreamingAssets/" + name);
                if (!hadStreamingContainer && AssetDatabase.IsValidFolder("Assets/StreamingAssets/AssetBundles") &&
                    !Directory.EnumerateFileSystemEntries("Assets/StreamingAssets/AssetBundles").Any())
                    AssetDatabase.DeleteAsset("Assets/StreamingAssets/AssetBundles");
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.RemoveUnusedAssetBundleNames();
                foreach (var replay in replayManifests) if (replay) UnityEngine.Object.DestroyImmediate(replay);
            }

            object CheckedPlan(bool build)
            {
                var plan = ResourceEditorTests.Invoke(operation, "CreatePlan", EditorUserBuildSettings.activeBuildTarget, build);
                AssertIsolatedPlan(plan, paths, assets);
                return plan;
            }

            AssetBundleManifest ReadManifest(string source)
            {
                var manifestBundle = AssetBundle.LoadFromFile(Path.Combine(source, "Windows"));
                Assert.IsNotNull(manifestBundle);
                try
                {
                    var copy = UnityEngine.Object.Instantiate(manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest"));
                    copy.hideFlags = HideFlags.HideAndDontSave;
                    replayManifests.Add(copy);
                    return copy;
                }
                finally { manifestBundle.Unload(true); }
            }
        }

        private static void AssertIsolatedPlan(object plan, string[] expectedPaths, string[] expectedAssets)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var outputs = ((System.Collections.IEnumerable)plan.GetType().GetField("Outputs", flags).GetValue(plan)).Cast<object>().ToArray();
            CollectionAssert.AreEquivalent(expectedPaths, outputs.Select(output =>
                Path.GetFullPath((string)output.GetType().GetField("Path", flags).GetValue(output))).ToArray(),
                "Integration plan contains non-owned outputs or owners; refuse before build/clear.");
            var assets = outputs.SelectMany(output => (AssetBundleBuild[])output.GetType().GetField("Builds", flags).GetValue(output))
                .SelectMany(build => build.assetNames).Distinct(StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEquivalent(expectedAssets, assets, "Integration plan includes non-owned sources; refuse before native build.");
        }

        private static void CreateText(string path, string bundle, string text)
        {
            File.WriteAllText(path, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            importer.assetBundleName = bundle;
            importer.SaveAndReimport();
        }

        private static void AssertRecord(ResData table, string asset, string bundle, int index)
        {
            var key = ResKey.Allocate(asset, bundle, typeof(TextAsset));
            try
            {
                var record = table.GetAssetData(key);
                Assert.IsNotNull(record);
                Assert.AreEqual(bundle, record.OwnerBundleName);
                Assert.AreEqual(index, record.AbIndex);
            }
            finally { key.RecycleToCache(); }
        }

        private static void CopyNativeOutput(string source, string stage)
        {
            foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var relative = path.Substring(source.Length + 1);
                if (relative.EndsWith(".meta", StringComparison.Ordinal) || relative == ".yl-resource-output.json") continue;
                var destination = Path.Combine(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(path, destination);
            }
        }
    }
}
