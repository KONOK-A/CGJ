using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceBuild
{
    public class ResourceEditorSimulationTests
    {
        [Test]
        public void CorePreviewUsesFormalIdentityAndNeverKeepsAStaleAssetOrProjectIndex()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            project.Groups.Add(new ResourceGroup { Id = "core-preview-texture", Selections =
                { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(fixture.Folder + "/Content/Shared.asset") } } });
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            var simulation = new ResourceEditorSimulation(new[] { fixture.Project });
            var property = typeof(YLFramework.Core.Editor.EditorAssetTool).GetProperty("PreviewResolver");
            Assert.That(property, Is.Not.Null, "Core Editor must consume an installed preview delegate, not XFAB project lookup.");
            var resolver = (Func<string, string, string, UnityEngine.Object>)property.GetValue(null);
            Assert.That(resolver, Is.Not.Null, "The Resource Editor installs the formal resolver on domain load.");
            var main = AssetDatabase.LoadAssetAtPath<Texture2D>(fixture.Folder + "/Content/Shared.asset");
            Assert.That(YLFramework.Core.Editor.EditorAssetTool.GetTexture2D(project.ProjectId, "Shared.asset"), Is.SameAs(main));
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(fixture.Folder + "/Content/Sprites.png").OfType<Sprite>().First();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id);
            Assert.That(resolver(project.ProjectId, "Sprites.png", id.ToString()), Is.SameAs(sprite));
            project.Groups[0].Selections[0].Aliases = new[] { "core-preview-alias" };
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            Assert.That(resolver(project.ProjectId, "core-preview-alias", null), Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(fixture.TextPath)));
            project.Groups[0].Selections[0].Address = "moved";
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            Assert.Throws<ArgumentException>(() => resolver(project.ProjectId, "entry.txt", null));
            var duplicate = ScriptableObject.CreateInstance<ResourceProject>();
            duplicate.ProjectId = project.ProjectId;
            duplicate.AssetRootGuid = project.AssetRootGuid;
            duplicate.Groups = project.Groups;
            AssetDatabase.CreateAsset(duplicate, fixture.Folder + "/DuplicateProject.asset");
            Assert.Throws<InvalidOperationException>(() => resolver(project.ProjectId, "moved", null));
            AssetDatabase.DeleteAsset(fixture.Folder + "/DuplicateProject.asset");
            Assert.That(resolver(project.ProjectId, "moved", null), Is.Not.Null);
            try
            {
                property.SetValue(null, null);
                Assert.Throws<InvalidOperationException>(() => YLFramework.Core.Editor.EditorAssetTool.GetTexture2D(project.ProjectId, "Shared.asset"));
            }
            finally { property.SetValue(null, resolver); }
            Assert.That(main, Is.Not.Null, "Preview only borrows AssetDatabase objects.");
        }

        [Test]
        public void SimulationCanComposeTheUniqueRuntimeAuthorityWithoutOwningBorrowedAssets()
        {
            using var fixture = new ResourceReleaseFixture();
            var simulation = new ResourceEditorSimulation(new[] { fixture.Project });
            var factory = typeof(ResourceEditorSimulation).GetMethod("CreateConfiguration");
            Assert.That(factory, Is.Not.Null, "Editor simulation must compose formal Source/Lease contracts, not another resource manager.");
            var config = (ResourceConfiguration)factory.Invoke(simulation, null);
            var entry = config.Catalogs[0].Resolve(new ResourceAddress(fixture.Project.ProjectId, "entry.txt"));
            var source = config.Sources.Single(x => x.Id.Equals(ResourceSourceId.Editor));
            source.Validate(entry);
            var result = source.Load(entry, typeof(TextAsset));
            var text = (TextAsset)result.Asset;
            Assert.That(text.text, Is.EqualTo("first release content"));
            source.Release(text);
            Assert.That(text, Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(fixture.TextPath)));
            Assert.That(text == null, Is.False);
        }

        private static object Simulation(ResourceProject[] projects, ResourceCatalogDocument[] catalogs = null)
        {
            var type = typeof(ResourceProject).Assembly.GetType("YLFramework.Editor.Resource.ResourceEditorSimulation");
            Assert.That(type, Is.Not.Null, "Editor simulation must use the formal Project/Catalog, without XFAB fallback.");
            return Activator.CreateInstance(type, new object[] { projects, catalogs });
        }

        private static object Call(object simulation, string method, params object[] args)
        {
            try { return simulation.GetType().GetMethods().Single(x => x.Name == method && !x.ContainsGenericParameters && x.GetParameters().Length == args.Length).Invoke(simulation, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        internal static void Aliases(ResourceSelection selection, params string[] values)
        {
            var field = typeof(ResourceSelection).GetField("Aliases");
            Assert.That(field, Is.Not.Null, "Multiple aliases must be explicit formal definition data.");
            field.SetValue(selection, values);
        }

        [Test]
        public void DraftAliasesSubassetsGroupsAndMultipleProjectsResolveExactBorrowedObjects()
        {
            using var first = new ResourceReleaseFixture();
            using var second = new ResourceReleaseFixture();
            // 嵌套夹具各自只断言创建前快照；当前两者均不改受保护工程。
            var project = first.Project;
            Aliases(project.Groups[0].Selections[0], "text-alias", "second-alias");
            var simulation = Simulation(new[] { project, second.Project });
            var address = new ResourceAddress(project.ProjectId, "entry.txt");
            var canonical = (ResourceCatalogEntry)Call(simulation, "Resolve", address);
            Assert.That(((ResourceCatalogEntry)Call(simulation, "Resolve", new ResourceAddress(project.ProjectId, "text-alias"))).AssetId,
                Is.EqualTo(canonical.AssetId));
            var text = (TextAsset)Call(simulation, "Load", new ResourceAddress(project.ProjectId, "second-alias"), typeof(TextAsset));
            Assert.That(text.text, Is.EqualTo("first release content"));
            Assert.That(Call(simulation, "Load", address, typeof(UnityEngine.Object)), Is.SameAs(text));
            Assert.That(Call(simulation, "Load", new ResourceAddress(second.Project.ProjectId, "entry.txt"), typeof(TextAsset)), Is.Not.SameAs(text));
            var sprite = AssetDatabase.LoadAllAssetRepresentationsAtPath(first.Folder + "/Content/Sprites.png").OfType<Sprite>().First(x => x.name == "Left");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id);
            Assert.That(Call(simulation, "Load", new ResourceAddress(project.ProjectId, "Sprites.png", id.ToString()), typeof(Sprite)), Is.SameAs(sprite));
            var group = (System.Collections.Generic.IReadOnlyList<ResourceCatalogEntry>)Call(simulation, "GetGroup", project.ProjectId, "entry");
            Assert.That(group.Single().AssetId, Is.EqualTo(canonical.AssetId));
            Assert.Throws<InvalidOperationException>(() => Call(simulation, "Load", address, typeof(GameObject)));
            Assert.Throws<ArgumentException>(() => Call(simulation, "Resolve", new ResourceAddress(project.ProjectId, "ENTRY.TXT")));
            Assert.That(text, Is.Not.Null, "Simulation must not unload or destroy shared AssetDatabase objects.");
        }

        [Test]
        public void DefinitionMoveImportDeletionAndAmbiguityNeverReuseAnOldIndex()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            var simulation = Simulation(new[] { project });
            var old = new ResourceAddress(project.ProjectId, "entry.txt");
            Assert.That(Call(simulation, "Load", old, typeof(TextAsset)), Is.Not.Null);
            project.Groups[0].Selections[0].Address = "renamed-address";
            Assert.Throws<ArgumentException>(() => Call(simulation, "Resolve", old));
            var renamed = new ResourceAddress(project.ProjectId, "renamed-address");
            Assert.That(Call(simulation, "Load", renamed, typeof(TextAsset)), Is.Not.Null);
            var moved = fixture.Folder + "/Content/moved.txt";
            Assert.That(AssetDatabase.MoveAsset(fixture.TextPath, moved), Is.Empty);
            File.WriteAllText(moved, "after import");
            AssetDatabase.ImportAsset(moved, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(((TextAsset)Call(simulation, "Load", renamed, typeof(TextAsset))).text, Is.EqualTo("after import"));
            Aliases(project.Groups[1].Selections[0], "RENAMED-ADDRESS");
            Assert.Throws<InvalidOperationException>(() => Call(simulation, "Resolve", renamed));
            Aliases(project.Groups[1].Selections[0]);
            Assert.That(AssetDatabase.DeleteAsset(moved), Is.True);
            Assert.Throws<InvalidOperationException>(() => Call(simulation, "Resolve", renamed));
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public void SharedDescriptionRejectsMainSubAndAssignableTypeNativeAmbiguityBeforePreview(bool baseAndDerived)
        {
            using var fixture = new ResourceReleaseFixture();
            var path = fixture.Folder + "/Content/Shared.asset";
            if (baseAndDerived)
            {
                AssetDatabase.AddObjectToAsset(new AnimationClip { name = "Same" }, path);
                AssetDatabase.AddObjectToAsset(new UnityEditor.Animations.BlendTree { name = "Same", hideFlags = HideFlags.None }, path);
            }
            else
            {
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                AssetDatabase.AddObjectToAsset(new Texture2D(2, 2) { name = main.name }, path);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var project = fixture.Project; project.Groups.Clear();
            project.Groups.Add(new ResourceGroup { Id = "main", Selections = { new ResourceSelection { Guid = AssetDatabase.AssetPathToGUID(path) } } });
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            var simulation = new ResourceEditorSimulation(new[] { project });
            Assert.Throws<InvalidOperationException>(() => simulation.GetCatalog(project.ProjectId));
            Assert.Throws<InvalidOperationException>(() => ResourceEditorOperations.Preflight(project, "fixture-v1"));
        }

        [Test]
        public void PublishedCatalogAliasesRoundTripAndStaleDefinitionIsRejected()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            Aliases(project.Groups[0].Selections[0], "stable-alias");
            EditorUtility.SetDirty(project); AssetDatabase.SaveAssetIfDirty(project);
            var result = fixture.Build();
            var document = ResourceCatalogBuilder.Create(result, "fixture-v1");
            var bytes = ResourceCatalogCodec.Write(document);
            var read = ResourceCatalogCodec.Read(bytes, document.ProjectId, document.Platform, document.UnityCompatibility, "fixture-v1");
            var simulation = Simulation(new[] { fixture.Project }, new[] { read });
            var alias = new ResourceAddress(read.ProjectId, "stable-alias");
            Assert.That(((TextAsset)Call(simulation, "Load", alias, typeof(TextAsset))).text, Is.EqualTo("first release content"));
            fixture.Project.Groups[0].Selections[0].Address = "changed-definition";
            Assert.Throws<InvalidOperationException>(() => Call(simulation, "Resolve", alias), "Published simulation must not silently accept changed definitions.");
            fixture.AssertProtected();
        }
    }
}
