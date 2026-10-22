using System;
using System.IO;
using System.Linq;
using System.Threading;
using dnlib.DotNet;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Integrations.HybridCLR;
using YLFramework.Integrations.HybridCLR.Editor;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public class HybridCLRBuildPipelineTests
    {
        private string root;
        private string temporary;
        private HybridCLRBuildProfile profile;
        private string workspaceRoot;
        private string releaseRoot;

        [SetUp]
        public void SetUp()
        {
            var id = Guid.NewGuid().ToString("N");
            root = "Assets/YLFramework/Tests/Fixtures/HybridCLRBuild-" + id;
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures", "HybridCLRBuild-" + id);
            temporary = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "kilo", "YLHybridCLRBuild-" + id);
            Directory.CreateDirectory(temporary);
            var project = ScriptableObject.CreateInstance<ResourceProject>(); project.ProjectId = "hot-" + id;
            AssetDatabase.CreateAsset(project, root + "/Project.asset");
            var recipe = ScriptableObject.CreateInstance<ResourceBuildRecipe>(); recipe.Project = project;
            recipe.Action = ResourceBuildAction.BuildAndPublish; recipe.AppContentCompatibilityId = "base-v1";
            AssetDatabase.CreateAsset(recipe, root + "/Recipe.asset");
            profile = ScriptableObject.CreateInstance<HybridCLRBuildProfile>(); profile.Recipe = recipe;
            profile.GeneratedAssetDirectory = root + "/Generated";
            profile.BaselineDirectory = System.IO.Path.GetFullPath("HybridCLRBaselines/" + id);
            AssetDatabase.CreateAsset(profile, root + "/Profile.asset");
            AssetDatabase.SaveAssetIfDirty(project); AssetDatabase.SaveAssetIfDirty(recipe); AssetDatabase.SaveAssetIfDirty(profile);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(root);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            if (workspaceRoot != null && Directory.Exists(workspaceRoot)) Directory.Delete(workspaceRoot, true);
            if (releaseRoot != null && Directory.Exists(releaseRoot)) Directory.Delete(releaseRoot, true);
        }

        [Test]
        public void InvalidProfileReportsThePreflightErrorWithoutChangingPlayerSettings()
        {
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            var compatibility = PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone);
            var report = HybridCLRBuildPipeline.Validate(null);
            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Stages.Single().Name, Is.EqualTo("Preflight"));
            Assert.That(report.Stages.Single().Error, Is.TypeOf<ArgumentNullException>());
            Assert.That(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), Is.EqualTo(backend));
            Assert.That(PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone), Is.EqualTo(compatibility));
        }

        [Test]
        public void AnInvalidBuildStopsBeforeCompilePayloadPublicationAndBaseline()
        {
            var report = HybridCLRBuildPipeline.BuildBasePlayer(null, new BuildPlayerOptions());
            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Stages.Select(s => s.Name), Is.EqualTo(new[] { "Preflight" }));
            Assert.That(report.Release, Is.Null); Assert.That(report.Manifest, Is.Null); Assert.That(report.PlayerBuild, Is.Null);
            Assert.That(Directory.Exists(profile.GeneratedAssetDirectory), Is.False);
            Assert.That(Directory.Exists(profile.BaselineDirectory), Is.False);
        }

        [TestCase("library")]
        [TestCase("temporary")]
        [TestCase("assets")]
        [TestCase("overlap")]
        public void BaselinesMustBePersistentAndSeparateFromGeneratedInputs(string invalid)
        {
            switch (invalid)
            {
                case "library": profile.BaselineDirectory = "Library/HybridCLR/OldAot"; break;
                case "temporary": profile.BaselineDirectory = "Temp/OldAot"; break;
                case "assets": profile.BaselineDirectory = root + "/NativeBaseline"; break;
                default: profile.BaselineDirectory = profile.GeneratedAssetDirectory; break;
            }
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidateInputPaths(profile));
        }

        [Test]
        public void UnownedFilesAreRejectedWithoutOverwritingThem()
        {
            Directory.CreateDirectory(profile.GeneratedAssetDirectory);
            var file = profile.GeneratedAssetDirectory + "/user-file.bytes"; File.WriteAllText(file, "user-content");
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidateInputPaths(profile));
            Assert.That(File.ReadAllText(file), Is.EqualTo("user-content"));
        }

        [Test]
        public void EmptyPredeclaredPayloadFoldersRetainTheirMetaAndCanBeInitialized()
        {
            AssetDatabase.CreateFolder(root, "Generated");
            AssetDatabase.CreateFolder(profile.GeneratedAssetDirectory, "hybridclr");
            AssetDatabase.CreateFolder(profile.GeneratedAssetDirectory + "/hybridclr", "assemblies");
            var before = AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory + "/hybridclr/assemblies");
            var baseline = CreateFormatFixture();
            var inputs = FixtureInputs();
            Assert.DoesNotThrow(() => HybridCLRBuildPipeline.CommitPayloads(profile, baseline.InitialManifest, inputs));
            Assert.That(AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory + "/hybridclr/assemblies"), Is.EqualTo(before));
        }

        [Test]
        public void UnchangedOwnedBytesDoNotAttemptAnotherAtomicReplacement()
        {
            var bytes = new byte[] { 1, 2, 3 }; var path = temporary + "/owned.bytes"; File.WriteAllBytes(path, bytes);
            Assert.DoesNotThrow(() => HybridCLRBuildPipeline.WriteOwned(temporary, "owned.bytes", bytes,
                (_, _) => throw new InvalidOperationException("An unchanged payload should not be replaced."), _ => { }));
            CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        }

        [Test]
        public void AtomicReplaceRetriesOnlyTheKnownNonDestructiveWindowsError()
        {
            var original = new byte[] { 1, 2, 3 }; var replacement = new byte[] { 4, 5, 6 };
            File.WriteAllBytes(temporary + "/owned.bytes", original); var calls = 0;
            Assert.DoesNotThrow(() => HybridCLRBuildPipeline.WriteOwned(temporary, "owned.bytes", replacement, (from, to) =>
            {
                calls++;
                if (calls <= 2) throw new IOException("Intentional Win32 1175 atomic replacement failure.", unchecked((int)0x80070497));
                File.Replace(from, to, null);
            }, _ => { }));
            Assert.That(calls, Is.EqualTo(3)); CollectionAssert.AreEqual(replacement, File.ReadAllBytes(temporary + "/owned.bytes"));
        }

        [Test]
        public void AnEmptyStartupPackageInfoGitFieldIsBackedByExactManifestAndLockEvidence()
        {
            const string url = "https://gitee.com/focus-creative-games/hybridclr_unity.git#v9.0.3";
            const string hash = "6cadd24d101f4cbd00cd80190106f6621132d24d";
            Assert.DoesNotThrow(() => HybridCLRBuildPipeline.ValidateGitProvenance("9.0.3", "Git", url, "git", url, hash, "", ""));
            Assert.Throws<InvalidOperationException>(() => HybridCLRBuildPipeline.ValidateGitProvenance("9.0.3", "Git", url, "git", url, new string('a', 40), "", ""));
            Assert.Throws<InvalidOperationException>(() => HybridCLRBuildPipeline.ValidateGitProvenance("9.0.3", "Git", url, "git", url, hash, "v7.3.0", hash));
            Assert.Throws<InvalidOperationException>(() => HybridCLRBuildPipeline.ValidateGitProvenance("9.0.3", "Embedded", url, "git", url, hash, "", ""));
        }

        [Test]
        public void SuccessfulBuildReportWithoutAnExecutableAndNativeAssemblyIsNotPlayerSuccess()
        {
            var exe = temporary + "/NotARealPlayer.exe";
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePlayerOutputs(exe, exe));
            File.WriteAllBytes(exe, new byte[] { 1, 2, 3 });
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePlayerOutputs(exe, exe));
            File.WriteAllBytes(temporary + "/GameAssembly.dll", new byte[] { 4, 5, 6 });
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePlayerOutputs(exe, temporary + "/Other.exe"));
        }

        [Test]
        public void NativeTypeRequirementsPreserveTheAssemblyOfNestedSameNamedValueTypeArguments()
        {
            using var root = new ModuleDefUser("GenericRoot.dll") { Kind = ModuleKind.Dll };
            using var first = new ModuleDefUser("PayloadA.dll") { Kind = ModuleKind.Dll };
            using var second = new ModuleDefUser("PayloadB.dll") { Kind = ModuleKind.Dll };
            new AssemblyDefUser("GenericRoot", new Version(1, 0, 0, 0)).Modules.Add(root);
            new AssemblyDefUser("PayloadA", new Version(1, 0, 0, 0)).Modules.Add(first);
            new AssemblyDefUser("PayloadB", new Version(1, 0, 0, 0)).Modules.Add(second);
            var box = new TypeDefUser("Fixture", "Box`1", root.CorLibTypes.Object.TypeDefOrRef); root.Types.Add(box);
            var nested = new TypeDefUser("Fixture", "Nested`1", root.CorLibTypes.Object.TypeDefOrRef); root.Types.Add(nested);
            var a = new TypeDefUser("Shared", "Payload", first.CorLibTypes.GetTypeRef("System", "ValueType")); first.Types.Add(a);
            var b = new TypeDefUser("Shared", "Payload", second.CorLibTypes.GetTypeRef("System", "ValueType")); second.Types.Add(b);
            var left = new global::HybridCLR.Meta.GenericClass(box, new System.Collections.Generic.List<TypeSig>
                { new GenericInstSig(new ClassSig(nested), new ValueTypeSig(a)) });
            var right = new global::HybridCLR.Meta.GenericClass(box, new System.Collections.Generic.List<TypeSig>
                { new GenericInstSig(new ClassSig(nested), new ValueTypeSig(b)) });
            Assert.That(left.ToTypeSig().FullName, Is.EqualTo(right.ToTypeSig().FullName), "This must cover a real scope collision, not different type names.");
            Assert.That(HybridCLRBuildPipeline.NativeTypeRequirement(left), Is.Not.EqualTo(HybridCLRBuildPipeline.NativeTypeRequirement(right)));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void ScriptOnlyOrVisualStudioExportIsNotACompletedNativeExecutableBuild(bool scripts, bool solution) =>
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidateExecutableBuildMode(scripts, solution));

        [Test]
        public void NativeGenericTypeRequirementsDistinguishDifferentValueTypeInstantiations()
        {
            using var module = new ModuleDefUser("Fixture.dll") { Kind = ModuleKind.Dll };
            new AssemblyDefUser("Fixture", new Version(1, 0, 0, 0)).Modules.Add(module);
            var type = new TypeDefUser("Fixture", "Box`1", module.CorLibTypes.Object.TypeDefOrRef); module.Types.Add(type);
            var first = new global::HybridCLR.Meta.GenericClass(type, new System.Collections.Generic.List<TypeSig> { module.CorLibTypes.Int32 });
            var second = new global::HybridCLR.Meta.GenericClass(type, new System.Collections.Generic.List<TypeSig> { module.CorLibTypes.Int64 });
            Assert.That(HybridCLRBuildPipeline.NativeTypeRequirement(first), Is.Not.EqualTo(HybridCLRBuildPipeline.NativeTypeRequirement(second)));
        }

        [Test]
        public void MissingNativeToolsFailBeforeAnInstallerOrPlayerBuildIsStarted()
        {
            Assert.Throws<InvalidOperationException>(() => HybridCLRBuildPipeline.ValidateNativeToolchain(temporary + "/missing.exe", temporary));
        }

        [Test]
        public void PlayerOptionsCannotBuildAnotherTargetOrOverwriteAnotherPlayer()
        {
            var options = new BuildPlayerOptions { target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android };
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePlayerOptions(profile, options));
            Assert.That(Directory.Exists(profile.BaselineDirectory), Is.False);
        }

        [Test]
        public void NoProfilePlayerGuardDoesNotReadOrChangeGlobalSettings()
        {
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone);
            Assert.DoesNotThrow(() => new HybridCLRPlayerBuildGuard().OnPreprocessBuild(null));
            Assert.That(PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone), Is.EqualTo(backend));
        }

        [Test]
        public void UnrelatedEmptyGeneratedDirectoryIsNotClaimed()
        {
            Directory.CreateDirectory(profile.GeneratedAssetDirectory + "/UserData");
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidateInputPaths(profile));
            Assert.That(Directory.Exists(profile.GeneratedAssetDirectory + "/UserData"), Is.True);
        }

        [TestCase("7.3.0", "9.0.3", "9.0.3")]
        [TestCase("9.0.3", "9.0.4", "9.0.3")]
        [TestCase("9.0.3", "9.0.3", null)]
        [TestCase("9.0.3", "9.0.3", "9.0.3\n")]
        public void PackageAndInstalledVersionsMustMatchExactly(string resolved, string declared, string installed)
        {
            Assert.Throws<InvalidOperationException>(() => HybridCLRBuildPipeline.ValidateSupportedVersions(resolved, declared, installed));
        }

        [Test]
        public void RepeatedPayloadCommitPreservesGuidAndBadInputCannotReplaceTheManifest()
        {
            var baseline = CreateFormatFixture(); var inputs = FixtureInputs();
            HybridCLRBuildPipeline.CommitPayloads(profile, baseline.InitialManifest, inputs);
            var address = baseline.InitialManifest.HotUpdateAssemblies[0].Address;
            var path = profile.GeneratedAssetDirectory + "/" + address;
            var guid = AssetDatabase.AssetPathToGUID(path);
            var manifestPath = profile.GeneratedAssetDirectory + "/" + HybridCLRManifest.ManifestPath;
            var original = File.ReadAllBytes(manifestPath);
            HybridCLRBuildPipeline.CommitPayloads(profile, baseline.InitialManifest, inputs);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            File.WriteAllBytes(inputs.HotUpdate.Single().Path, new byte[] { 1, 2, 3 });
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.CommitPayloads(profile, baseline.InitialManifest, inputs));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(manifestPath));
            Assert.That(AssetImporter.GetAtPath(path).assetBundleName, Is.Empty);
        }

        [Test]
        public void RealDllBytesPublishThroughResourceAndCancellationPreservesTheOldRelease()
        {
            var baseline = CreateFormatFixture(); var inputs = FixtureInputs();
            HybridCLRBuildPipeline.CommitPayloads(profile, baseline.InitialManifest, inputs);
            ConfigureResourceSelection();
            var report = new HybridCLRBuildReport { Manifest = baseline.InitialManifest };
            HybridCLRBuildPipeline.PublishResources(report, profile, default, null);
            Assert.That(report.Release, Is.Not.Null, report.ResourceBuild?.Summary);
            var digest = report.Release.Catalog.CatalogDigest;
            var oldCatalog = File.ReadAllBytes(report.Release.DirectoryPath + "/resource-catalog.json");
            var canceled = new HybridCLRBuildReport { Manifest = baseline.InitialManifest };
            Assert.Throws<OperationCanceledException>(() => HybridCLRBuildPipeline.PublishResources(canceled, profile, new CancellationToken(true), null));
            Assert.That(canceled.Release, Is.Null);
            Assert.That(ResourcePublisher.ReadCurrent(profile.Recipe.Project, profile.Recipe.Target, "base-v1").Catalog.CatalogDigest, Is.EqualTo(digest));
            CollectionAssert.AreEqual(oldCatalog, File.ReadAllBytes(report.Release.DirectoryPath + "/resource-catalog.json"));
        }

        [Test]
        public void PatchCannotAddAnAotDependencyOrChangeTheRegisteredAssemblySet()
        {
            var baseline = CreateFormatFixture();
            var changed = WriteAssembly("Hot.Entry", "Base");
            var inputs = HybridCLRAssemblyAnalysis.Analyze(new[] { changed }, new[] { temporary + "/Aot/Base.dll" });
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePatchSet(baseline, inputs));
            var renamed = HybridCLRAssemblyAnalysis.Analyze(new[] { WriteAssembly("Hot.Other") }, new[] { temporary + "/Aot/Base.dll" });
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.ValidatePatchSet(baseline, renamed));
        }

        private HybridCLRAssemblySet FixtureInputs() => HybridCLRAssemblyAnalysis.Analyze(new[] { temporary + "/HotUpdate/Hot.Entry.dll" }, new[] { temporary + "/Aot/Base.dll" });

        private void ConfigureResourceSelection()
        {
            var project = profile.Recipe.Project;
            project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory) });
            project.Groups.Add(new ResourceGroup { Id = "hotupdate", Selections = new System.Collections.Generic.List<ResourceSelection>
                { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(profile.GeneratedAssetDirectory + "/hybridclr"), Extensions = new[] { ".bytes", ".json" } } } });
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            workspaceRoot = System.IO.Path.GetDirectoryName(ResourceBundleBuilder.GetWorkspacePath(project, profile.Recipe.Target));
            releaseRoot = System.IO.Path.GetFullPath("AssetBundles/YLFramework/" + project.ProjectId + "/" + profile.Recipe.Target);
            Assert.That(Directory.Exists(workspaceRoot), Is.False); Assert.That(Directory.Exists(releaseRoot), Is.False);
        }

        [Test]
        public void AStaleDllNotReportedByTheCompilerCannotBecomeSuccessfulOutput()
        {
            var stale = WriteAssembly("Hot.Entry");
            Assert.That(File.Exists(stale), Is.True);
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.VerifyCompiledOutputs(Array.Empty<string>(), new[] { "Hot.Entry" }));
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildPipeline.VerifyCompiledOutputs(new[] { stale }, new[] { "Another" }));
            Assert.That(HybridCLRBuildPipeline.VerifyCompiledOutputs(new[] { stale }, new[] { "Hot.Entry" }).Single().Name, Is.EqualTo("Hot.Entry"));
        }

        [TestCase("missing-marker")]
        [TestCase("changed-dll")]
        [TestCase("changed-native-file")]
        [TestCase("unknown-file")]
        [TestCase("changed-receipt")]
        public void ACorruptOrIncompleteFrozenBaselineIsNotPatchInput(string invalid)
        {
            var baseline = CreateFormatFixture();
            switch (invalid)
            {
                case "missing-marker": File.Delete(temporary + "/complete.json"); break;
                case "changed-dll": File.AppendAllText(temporary + "/Aot/Base.dll", "corrupted"); break;
                case "changed-native-file": File.AppendAllText(temporary + "/Generated/MethodBridge.inc", "changed"); break;
                case "unknown-file": File.WriteAllText(temporary + "/unknown.dll", "unknown"); break;
                default: File.AppendAllText(temporary + "/baseline.json", " "); break;
            }
            Assert.Throws<InvalidDataException>(() => HybridCLRBuildBaseline.Read(temporary));
            Assert.That(File.ReadAllText(temporary + "/Generated/MethodBridge.inc"), Does.StartWith("format-test-only"));
        }

        [Test]
        public void CompleteFormatFixtureRetainsTheExactDllIdentityAndNativeFileHash()
        {
            var baseline = CreateFormatFixture();
            var loaded = HybridCLRBuildBaseline.Read(temporary);
            Assert.That(loaded.PlayerBuildHash, Is.EqualTo(baseline.PlayerBuildHash));
            Assert.That(loaded.Files.Single(f => f.Path == "Aot/Base.dll").Identity, Is.EqualTo("Base, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null"));
            Assert.That(loaded.Root, Is.EqualTo(System.IO.Path.GetFullPath(temporary)));
        }

        private string WriteAssembly(string name, params string[] dependencies)
        {
            var path = temporary + "/" + name + ".dll";
            using var module = new ModuleDefUser(name + ".dll") { Kind = ModuleKind.Dll };
            new AssemblyDefUser(name, new Version(1, 0, 0, 0)).Modules.Add(module);
            foreach (var dependency in dependencies)
                module.Types.Add(new TypeDefUser("Fixture", "Uses" + module.Types.Count,
                    new TypeRefUser(module, "Fixture", "Dependency", new AssemblyRefUser(dependency, new Version(1, 0, 0, 0)))));
            module.Write(path); return path;
        }

        // 仅测试冻结格式完整性，绝不把此 fixture 作为真实 Player/IL2CPP 成功证据。
        private HybridCLRBuildBaseline CreateFormatFixture()
        {
            Directory.CreateDirectory(temporary + "/Aot"); Directory.CreateDirectory(temporary + "/Generated");
            var aot = HybridCLRAssemblyAnalysis.Read(WriteAssembly("Base")); File.Move(aot.Path, temporary + "/Aot/Base.dll");
            var hot = HybridCLRAssemblyAnalysis.Read(WriteAssembly("Hot.Entry"));
            var manifest = new HybridCLRManifest
            {
                ProjectId = profile.Recipe.Project.ProjectId, AppContentCompatibilityId = "base-v1", UnityVersion = Application.unityVersion,
                HotUpdateAssemblies = new[] { new HybridCLRPayload { Name = hot.Name, Identity = hot.Identity, Mvid = hot.Mvid,
                    Address = HybridCLRManifest.PayloadAddress(hot.Name, false), Length = hot.Length, Sha256 = hot.Sha256 } }
            };
            manifest.Seal();
            Directory.CreateDirectory(temporary + "/HotUpdate");
            File.Move(hot.Path, temporary + "/HotUpdate/" + hot.Name + ".dll");
            var bridge = System.Text.Encoding.UTF8.GetBytes("format-test-only: not a native Player receipt");
            foreach (var name in HybridCLRBuildBaseline.GeneratedNames) File.WriteAllBytes(temporary + "/Generated/" + name, bridge);
            var baseline = new HybridCLRBuildBaseline
            {
                ProfileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile)),
                SettingsFingerprint = new string('a', 64), PlayerBuildHash = new string('b', 64), NativeRequirements = Array.Empty<string>(), InitialManifest = manifest,
                Files = new[] { new HybridCLRBaselineFile { Path = "Aot/Base.dll", Length = aot.Length, Sha256 = aot.Sha256, Identity = aot.Identity, Mvid = aot.Mvid },
                    new HybridCLRBaselineFile { Path = "HotUpdate/" + hot.Name + ".dll", Length = hot.Length, Sha256 = hot.Sha256, Identity = hot.Identity, Mvid = hot.Mvid } }
                    .Concat(HybridCLRBuildBaseline.GeneratedNames.Select(n => new HybridCLRBaselineFile { Path = "Generated/" + n, Length = bridge.Length, Sha256 = HybridCLRManifest.Hash(bridge) })).ToArray()
            };
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(baseline));
            File.WriteAllBytes(temporary + "/baseline.json", bytes);
            File.WriteAllText(temporary + "/complete.json", JsonConvert.SerializeObject(new { schemaVersion = 1, baselineSha256 = HybridCLRManifest.Hash(bytes) }));
            return baseline;
        }
    }
}
