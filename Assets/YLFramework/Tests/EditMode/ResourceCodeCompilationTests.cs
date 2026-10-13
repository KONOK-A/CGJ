using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public class ResourceCodeCompilationTests
    {
        private const string Key = "YL.Resource63.CompilationProbe";
        private const string Output = "Assets/YLFrameworkData";
        private const string GeneratedAssembly = "YLFramework.Generated.ResourceNames";
        private static string ConsumerAssembly => "YLFramework.Tests.ResourceNamesConsumer63." +
            Path.GetFileName(SessionState.GetString(Key, "")).Replace("NamesCompile63-", "");

        [Test]
        public void InterruptedCleanupRefusesRecreatedOrModifiedOutput()
        {
            Assert.IsEmpty(SessionState.GetString(Key, ""));
            Assert.IsFalse(Directory.Exists(Output) || File.Exists(Output) || File.Exists(Output + ".meta"));
            var root = "Assets/NamesCompile63-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Output);
            try
            {
                SessionState.SetString(Key, root);
                File.WriteAllText(Output + "/User.cs", "// independently recreated user source");
                Assert.Throws<AssertionException>(() => Cleanup().MoveNext());
                Assert.AreEqual("// independently recreated user source", File.ReadAllText(Output + "/User.cs"));

                Directory.CreateDirectory(root);
                File.WriteAllText(root + ".meta", "probe identity");
                File.WriteAllText(Output + ".meta", "output identity");
                RecordOwned();
                File.AppendAllText(Output + "/User.cs", " changed after interruption");
                Assert.Throws<AssertionException>(() => Cleanup().MoveNext());
                Assert.IsTrue(Directory.Exists(root));
                StringAssert.EndsWith(" changed after interruption", File.ReadAllText(Output + "/User.cs"));
            }
            finally
            {
                ClearState();
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (File.Exists(root + ".meta")) File.Delete(root + ".meta");
                Directory.Delete(Output, true);
                if (File.Exists(Output + ".meta")) File.Delete(Output + ".meta");
            }
        }

        [UnityTest]
        public IEnumerator RealGeneratedSourceCompilesForAsmdefAndPredefinedConsumers()
        {
            var previous = SessionState.GetString(Key, "");
            // 清理后等待编译时若被中断，旧 Key 不能授权清理后来出现的用户目录。
            if (!string.IsNullOrEmpty(previous) && !Directory.Exists(previous)) ClearState();
            if (string.IsNullOrEmpty(SessionState.GetString(Key, ""))) Prepare();
            RequireOwned();
            yield return new RecompileScripts();
            var root = SessionState.GetString(Key, "");
            Assert.IsFalse(EditorUtility.scriptCompilationFailed);
            var generated = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == GeneratedAssembly);
            var types = generated.GetTypes().Where(x => x.Namespace == "YLFramework.Services.Resource").ToArray();
            Assert.AreEqual(3, types.Length);
            AssertConstant(generated, root, "Resource63_default", "First_Icon", "First.Icon");
            AssertConstant(generated, root, "Resource63_icons", "Class", "class");
            AssertConstant(generated, root, "_9_resource63", "_9_item", "9-item");
            AssertConstant(generated, root, "_9_resource63", "BundleName", "9-resource63");
            Assert.IsNull(generated.GetType("YLFramework.Services.Resource.Resource63_default", true).GetField("Extra"),
                "First compilation must load this run's initial source, not an earlier updated assembly.");
            foreach (var assemblyName in new[] { ConsumerAssembly, "Assembly-CSharp" })
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == assemblyName);
                var consumer = assembly.GetType("YLFramework.Tests.GeneratedConsumer63." +
                    (assemblyName == ConsumerAssembly ? "Explicit" : "Predefined"), true);
                Assert.AreEqual("First.Icon|class|9-item|9-resource63", consumer.GetMethod("Read").Invoke(null, null));
            }
            var graph = CompilationPipeline.GetAssemblies().Single(x => x.name == GeneratedAssembly);
            CollectionAssert.AreEqual(new[] { Output + "/YLResData.cs" }, graph.sourceFiles.Select(x => x.Replace('\\', '/')).ToArray());
            Assert.IsEmpty(graph.assemblyReferences, "Generated constants must not depend on Framework/Editor/consumers.");
            Assert.IsFalse(graph.compiledAssemblyReferences.Any(x => Path.GetFileName(x).StartsWith("UnityEngine") ||
                                                                   Path.GetFileName(x).StartsWith("UnityEditor")));
            Assert.AreEqual(ConsumerAssembly, Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(root + "/Consumer/Explicit.cs")));
            Assert.AreEqual("Assembly-CSharp", Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(root + "/Predefined.cs")));
            var consumerGraph = CompilationPipeline.GetAssemblies().Single(x => x.name == ConsumerAssembly);
            CollectionAssert.AreEqual(new[] { root + "/Consumer/Explicit.cs" }, consumerGraph.sourceFiles.Select(x => x.Replace('\\', '/')).ToArray());
            CollectionAssert.Contains(CompilationPipeline.GetAssemblies().Single(x => x.name == "Assembly-CSharp").sourceFiles
                .Select(x => x.Replace('\\', '/')).ToArray(), root + "/Predefined.cs");

            var path = Output + "/YLResData.cs";
            var asmdef = Output + "/" + GeneratedAssembly + ".asmdef";
            var before = File.ReadAllBytes(path);
            var meta = File.ReadAllBytes(path + ".meta");
            var asmMeta = File.ReadAllBytes(asmdef + ".meta");
            var folderMeta = File.ReadAllBytes(Output + ".meta");
            var time = File.GetLastWriteTimeUtc(path);
            RequireOwned();
            BuildScript.WriteResReferenceClass();
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            Assert.AreEqual(time, File.GetLastWriteTimeUtc(path));

            // 加入真实的规范化碰撞，公共入口必须在写入之前拒绝，且旧有效产物仍可用。
            Text(root + "/First_Icon.bytes", "resource63_default");
            RequireOwned();
            Assert.Throws<InvalidOperationException>(() => BuildScript.WriteResReferenceClass());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            AssetDatabase.DeleteAsset(root + "/First_Icon.bytes");
            RecordOwned();

            Text(root + "/Extra.bytes", "resource63_default");
            RequireOwned();
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => BuildScript.WriteResReferenceClass());
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
            BuildScript.WriteResReferenceClass();
            RecordOwned();
            CollectionAssert.AreEqual(meta, File.ReadAllBytes(path + ".meta"));
            CollectionAssert.AreEqual(asmMeta, File.ReadAllBytes(asmdef + ".meta"));
            CollectionAssert.AreEqual(folderMeta, File.ReadAllBytes(Output + ".meta"));
            Assert.IsFalse(Directory.GetFiles(Output).Any(x => Path.GetFileName(x).StartsWith(".yl-names-")));
            yield return new RecompileScripts();
            Assert.IsFalse(EditorUtility.scriptCompilationFailed);
            generated = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == GeneratedAssembly);
            AssertConstant(generated, root, "Resource63_default", "Extra", "Extra");
        }

        private static void AssertConstant(System.Reflection.Assembly generated, string root, string type, string member, string expected)
        {
            var value = (string)generated.GetType("YLFramework.Services.Resource." + type, true).GetField(member).GetRawConstantValue();
            Assert.AreEqual(expected, value);
            if (member != "BundleName") Assert.AreEqual(ConfigFileUtility.AssetPathToName(root + "/" + expected + ".bytes"), value.ToLowerInvariant());
        }

        private static void Prepare()
        {
            Assert.IsEmpty(AssetDatabase.GetAllAssetBundleNames(), "Cannot probe alongside external Bundle marks.");
            Assert.IsEmpty(AssetDatabase.FindAssets("t:SubProject"), "Cannot probe alongside external owners.");
            Assert.IsFalse(Directory.Exists(Output) || File.Exists(Output) || File.Exists(Output + ".meta"), "Do not take over existing generated/user assets.");
            var root = "Assets/NamesCompile63-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            SessionState.SetString(Key, root);
            AssetDatabase.CreateFolder(root, "Project");
            Text(root + "/First.Icon.bytes", "resource63_default");
            Text(root + "/class.bytes", "resource63-icons");
            Text(root + "/Project/9-item.bytes", "9-resource63");
            var owner = ScriptableObject.CreateInstance<SubProject>();
            AssetDatabase.CreateAsset(owner, root + "/Project/Names63Project.asset");
            AssetDatabase.CreateFolder(root, "Consumer");
            var body = "public static string Read() => YLFramework.Services.Resource.Resource63_default.First_Icon + \"|\" + " +
                       "YLFramework.Services.Resource.Resource63_icons.Class + \"|\" + " +
                       "YLFramework.Services.Resource._9_resource63._9_item + \"|\" + YLFramework.Services.Resource._9_resource63.BundleName;";
            File.WriteAllText(root + "/Consumer/Explicit.cs", "namespace YLFramework.Tests.GeneratedConsumer63 { public static class Explicit { " + body + " } }");
            File.WriteAllText(root + "/Predefined.cs", "namespace YLFramework.Tests.GeneratedConsumer63 { public static class Predefined { " + body + " } }");
            File.WriteAllText(root + "/Consumer/Consumer.asmdef", "{\"name\":\"" + ConsumerAssembly + "\",\"references\":[\"" +
                GeneratedAssembly + "\",\"YLFramework.Runtime\"],\"autoReferenced\":false,\"overrideReferences\":true,\"precompiledReferences\":[],\"noEngineReferences\":true}");
            BuildScript.WriteResReferenceClass();
            RecordOwned();
        }

        private static void Text(string path, string bundle)
        {
            File.WriteAllText(path, "resource63 isolated compilation text");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path);
            importer.assetBundleName = bundle;
            importer.SaveAndReimport();
            if (!string.IsNullOrEmpty(SessionState.GetString(Key + ".Fingerprint", ""))) RecordOwned();
        }

        private static string Fingerprint()
        {
            var root = SessionState.GetString(Key, "");
            var files = new[] { root, Output }.SelectMany(directory => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
                .Concat(new[] { directory + ".meta" })).OrderBy(path => path, StringComparer.Ordinal);
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", files.Select(path =>
                path + ":" + Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))))))));
        }

        private static void RecordOwned() => SessionState.SetString(Key + ".Fingerprint", Fingerprint());

        private static void RequireOwned()
        {
            var root = SessionState.GetString(Key, "");
            Assert.IsTrue(root.StartsWith("Assets/NamesCompile63-", StringComparison.Ordinal) && Directory.Exists(root));
            Assert.IsTrue(Directory.Exists(Output), "Generated output disappeared; refuse to take over replacement assets.");
            Assert.IsTrue(AssetDatabase.GetAllAssetBundleNames().SelectMany(AssetDatabase.GetAssetPathsFromAssetBundle)
                .All(path => path.StartsWith(root + "/", StringComparison.Ordinal)), "Probe cannot include external marked sources.");
            Assert.IsTrue(AssetDatabase.FindAssets("t:SubProject").Select(AssetDatabase.GUIDToAssetPath)
                .All(path => path.StartsWith(root + "/", StringComparison.Ordinal)), "Probe cannot include external owners.");
            Assert.AreEqual(SessionState.GetString(Key + ".Fingerprint", ""), Fingerprint(),
                "Probe assets or generated identities changed outside this run; refuse cleanup.");
        }

        private static void ClearState()
        {
            SessionState.EraseString(Key);
            SessionState.EraseString(Key + ".Fingerprint");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var root = SessionState.GetString(Key, "");
            if (string.IsNullOrEmpty(root)) yield break;
            // 仅清理本探针在开始时确认不存在的位置，不触及 Bundle 输出、游戏或用户源码。
            if (Directory.Exists(root))
            {
                RequireOwned();
                Assert.IsTrue(AssetDatabase.DeleteAsset(root));
                Assert.IsTrue(AssetDatabase.DeleteAsset(Output));
            }
            else Assert.IsFalse(Directory.Exists(Output), "Never delete a recreated output after interrupted cleanup.");
            AssetDatabase.RemoveUnusedAssetBundleNames();
            yield return new RecompileScripts();
            ClearState();
            Assert.IsFalse(Directory.Exists(root) || Directory.Exists(Output));
            Assert.IsFalse(EditorUtility.scriptCompilationFailed);
            Assert.IsEmpty(AssetDatabase.GetAllAssetBundleNames());
        }
    }
}
