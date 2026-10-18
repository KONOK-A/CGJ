using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Tests.ResourceBuild;

namespace YLFramework.Tests.ResourceEditor
{
    public sealed class ResourceWorkbenchUX2Tests
    {
        [Test]
        public void CopiedAtlasConfigurationCannotSaveAnotherRegisteredAsset()
        {
            using var fixture = new ResourceReleaseFixture();
            var original = ResourceAtlasGenerator.CreateConfiguration(fixture.Project, fixture.Folder + "/Content", "GeneratedAtlases");
            var copyPath = fixture.Folder + "/CopiedAtlasRules.asset";
            Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), copyPath), Is.True);
            var copy = AssetDatabase.LoadAssetAtPath<ResourceAtlasConfiguration>(copyPath);
            EditorUtility.SetDirty(original); EditorUtility.SetDirty(copy);
            var save = typeof(ResourceEditorOperations).Assembly.GetType("YLFramework.Editor.Resource.ResourceAtlasFields")
                .GetMethod("Save", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(ResourceProject), typeof(ResourceAtlasConfiguration) }, null);
            Assert.That(save, Is.Not.Null);
            Assert.That(Assert.Throws<TargetInvocationException>(() => save.Invoke(null, new object[] { fixture.Project, copy })).InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(EditorUtility.IsDirty(original), Is.True); Assert.That(EditorUtility.IsDirty(copy), Is.True);
        }

        [Test]
        public void InvalidatingAnalysisRetainsItsOriginalMapAsAnExplicitlyStaleSnapshot()
        {
            using var fixture = new ResourceReleaseFixture();
            var map = ResourceBuildMap.Create(fixture.Project);
            var view = new ResourceBuildView();
            view.SetPreview(map, fixture.Project);
            view.InvalidatePreview();
            var preview = typeof(ResourceBuildView).GetField("preview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            Assert.That(preview, Is.SameAs(map), "Invalidation must mark stale, not erase the provenance and time of the explicitly computed map.");
            Assert.That(view.DefinitionChanged, Is.True);
        }

        [UnityTest]
        public IEnumerator AtlasPageSavesOnlyRulesAndActuallyGeneratesThroughItsLiveControls()
        {
            using var fixture = new ResourceReleaseFixture();
            var configuration = ResourceAtlasGenerator.CreateConfiguration(fixture.Project, fixture.Folder + "/Content", "GeneratedAtlases");
            configuration.Rules.Add(new ResourceAtlasRule { Id = "ux2", GroupId = fixture.Project.Groups[0].Id,
                Address = "atlases/ux2", Sources = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(fixture.Folder + "/Content/Sprites.png") } });
            EditorUtility.SetDirty(configuration); AssetDatabase.SaveAssetIfDirty(configuration);
            var project = fixture.Project;
            project.DisplayName = "An unrelated unsaved Project edit"; EditorUtility.SetDirty(project);
            var focus = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<ResourceUX2GuiWindow>();
            try
            {
                window.ShowUtility(); window.position = new Rect(100, 80, 1080, 720); window.SetProject(project);
                var view = typeof(ResServiceEditorWindow).GetField("_resServiceView", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                var definition = view.GetType().GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                definition.GetType().GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(definition, 2);
                configuration.Rules[0].Padding = 8; EditorUtility.SetDirty(configuration);
                for (var i = 0; i < 8; i++) { window.Repaint(); yield return null; }
                Assert.That(window.SaveRules.width, Is.GreaterThan(0), "The atlas task must expose an explicit save for the same real configuration.");
                Assert.That(window.AddRule.xMin, Is.GreaterThan(window.position.width / 2), "Wide layout must keep rule editing in a visible right pane.\n" + window.LayoutTrace);
                Assert.That(window.AddRule.xMax, Is.LessThanOrEqualTo(window.position.width), "The rule action must fit inside the window.");
                Assert.That(window.AddRule.yMin, Is.InRange(100, 650), "Rule controls must not be clipped by an empty GUILayout area.");
                window.Focus(); Click(window, window.SaveRules.center);
                yield return null;
                Assert.That(EditorUtility.IsDirty(configuration), Is.False);
                Assert.That(EditorUtility.IsDirty(project), Is.True, "Saving rules must not save another dirty Project or Scene.");
                configuration.Rules[0].Padding = 4; EditorUtility.SetDirty(configuration);
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.S, modifiers = EventModifiers.Control });
                Assert.That(EditorUtility.IsDirty(configuration), Is.False, "Atlas Ctrl+S has the same explicit save target as the button.");
                Assert.That(EditorUtility.IsDirty(project), Is.True);
                ResourceEditorOperations.SaveProject(project);
                for (var i = 0; i < 4; i++) { window.Repaint(); yield return null; }
                Assert.That(window.Generate.width, Is.GreaterThan(0));
                Click(window, window.Generate.center);
                for (var i = 0; i < 8; i++) { window.Repaint(); yield return null; }
                var plan = ResourceAtlasGenerator.Preview(fixture.Project);
                Assert.That(plan.NeedsGeneration, Is.False);
                Assert.That(plan.Changes.Single().Kind, Is.EqualTo(ResourceAtlasChangeKind.Current));
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(plan.Changes.Single().Path);
                Assert.That(atlas.spriteCount, Is.EqualTo(2), "The actual B06 prepare must run, not merely populate a UI plan.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); if (focus) focus.Focus(); }
        }

        internal static void Click(EditorWindow window, Vector2 point)
        {
            window.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = point });
            window.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = point });
        }
        [Test]
        public void MapSourceIsAnImmutableDefinitionSnapshotAndRejectsChangedSources()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            var original = project.Groups[0];
            original.Selections[0].Address = "original/address";
            var map = ResourceBuildMap.Create(project);
            var asset = map.Assets.Single(x => x.SourcePath == fixture.TextPath);
            var property = typeof(ResourceBuildAsset).GetProperty("Source");
            Assert.That(property, Is.Not.Null, "BuildMap must retain the source at parse time, not infer an old report from the live Project.");
            var source = property.GetValue(asset);
            Assert.That(Value(source, "GroupId"), Is.EqualTo(original.Id));
            Assert.That(Value(source, "SelectionGuid"), Is.EqualTo(original.Selections[0].Guid));
            Assert.That(Value(source, "SelectionAddress"), Is.EqualTo("original/address"));
            project.Groups.Reverse();
            var locator = typeof(ResServiceView).Assembly.GetType("YLFramework.Editor.Resource.ResourceBuildSourceLocator");
            Assert.That(locator, Is.Not.Null);
            var locate = locator.GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic);
            var location = locate.Invoke(null, new[] { project, source });
            Assert.That(((ValueTuple<int, int, int>)location).Item1, Is.EqualTo(project.Groups.IndexOf(original)), "Display order is not the identity.");
            original.Selections[0].Address = "changed/address";
            Assert.That(Value(source, "SelectionAddress"), Is.EqualTo("original/address"));
            var failure = Assert.Throws<TargetInvocationException>(() => locate.Invoke(null, new[] { project, source }));
            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>(), "A changed old source must not edit the entry now occupying its index.");
            project.Groups.Remove(original);
            Assert.Throws<TargetInvocationException>(() => locate.Invoke(null, new[] { project, source }));
        }

        [Test]
        public void GeneratedAtlasKeepsItsRuleSourceAndRefusesToLocateChangedOrObsoleteRules()
        {
            using var fixture = new ResourceReleaseFixture();
            var project = fixture.Project;
            project.Groups.Clear(); project.Groups.Add(new ResourceGroup { Id = "atlas" });
            EditorUtility.SetDirty(project); ResourceEditorOperations.SaveProject(project);
            var config = ResourceAtlasGenerator.CreateConfiguration(project, fixture.Folder + "/Content", "GeneratedAtlases");
            config.Rules.Add(new ResourceAtlasRule { Id = "menu", GroupId = "atlas", Address = "atlases/menu",
                Sources = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(fixture.Folder + "/Content/Sprites.png") } });
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            ResourceAtlasGenerator.Generate(project);
            var map = ResourceBuildMap.Create(project);
            var source = map.Assets.Single().Source;
            Assert.That(source.AtlasRuleId, Is.EqualTo("menu"));
            Assert.That(source.AtlasConfigurationGuid, Is.EqualTo(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(config))));
            Assert.That(ResourceBuildSourceLocator.Resolve(project, source).Rule, Is.EqualTo(0));
            config.Rules[0].Padding = 8; EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Assert.That(ResourceAtlasGenerator.Preview(project).Changes.Single().Kind, Is.EqualTo(ResourceAtlasChangeKind.Update));
            Assert.Throws<InvalidOperationException>(() => ResourceBuildSourceLocator.Resolve(project, source));
            config.Rules.Clear(); EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
            Assert.That(ResourceAtlasGenerator.Preview(project).Changes.Single().Kind, Is.EqualTo(ResourceAtlasChangeKind.Obsolete));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(map.Assets.Single().SourcePath), Is.Not.Null, "Obsolete is retained, not automatically deleted.");
            Assert.That(source.AtlasRuleId, Is.EqualTo("menu"), "An old map keeps the original rule identity.");
            Assert.Throws<InvalidOperationException>(() => ResourceBuildSourceLocator.Resolve(project, source));
        }

        [Test]
        public void BundleTableUsesPhysicalRowsAndKeepsIdentityAcrossSortAndFilter()
        {
            using var fixture = new ResourceReleaseFixture();
            var map = ResourceBuildMap.Create(fixture.Project);
            Assert.That(map.Bundles.Any(x => x.IsShared), Is.True);
            Assert.That(map.Bundles.Any(x => x.IsScene), Is.True);
            var type = typeof(ResServiceView).Assembly.GetType("YLFramework.Editor.Resource.ResourceBundleTable");
            Assert.That(type, Is.Not.Null, "The analysis must use physical multi-column Bundle rows.");
            var table = (TreeView)Activator.CreateInstance(type, true);
            type.GetMethod("SetData", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(table, new object[] { map, null });
            Assert.That(table.GetRows().Count, Is.EqualTo(map.Bundles.Count));
            var id = table.GetRows()[0].id;
            table.SetSelection(new[] { id });
            var selected = type.GetProperty("Selected", BindingFlags.Instance | BindingFlags.NonPublic);
            var before = selected.GetValue(table);
            table.multiColumnHeader.SetSorting(2, false);
            Assert.That(selected.GetValue(table), Is.SameAs(before));
            var name = ((ResourceBuildBundle)Value(before, "Bundle")).Name;
            table.searchString = name;
            Assert.That(table.GetRows().Count, Is.EqualTo(1));
            Assert.That(table.GetSelection(), Is.EquivalentTo(new[] { id }));
            Assert.That(((ResourceBuildBundle)Value(selected.GetValue(table), "Bundle")).Name, Is.EqualTo(name));
            table.searchString = "no-such-bundle";
            Assert.That(table.GetRows(), Is.Empty);
            Assert.That(table.GetSelection(), Is.EquivalentTo(new[] { id }));
        }

        internal static object Value(object instance, string name) => instance.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance) ??
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
    }

    internal sealed class ResourceUX2GuiWindow : ResServiceEditorWindow
    {
        internal Rect SaveRules;
        internal Rect Generate;
        internal Rect AddRule;
        internal Rect SaveRecipe;
        internal Rect OutputText;
        internal float RootContentHeight;
        internal readonly System.Collections.Generic.List<Rect> HistoryButtons = new();
        internal string LayoutTrace;
        private void OnGUI()
        {
            var type = Event.current.type;
            typeof(ResServiceEditorWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(this, null);
            if (type != EventType.Repaint) return;
            SaveRules = default; Generate = default; AddRule = default; SaveRecipe = default;
            OutputText = default;
            HistoryButtons.Clear();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var cache = typeof(GUILayoutUtility).GetProperty("current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) ??
                typeof(GUILayoutUtility).GetField("current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null);
            var trace = new System.Collections.Generic.List<string>();
            var top = cache.GetType().GetField("topLevel", flags).GetValue(cache);
            RootContentHeight = ((Rect)top.GetType().GetField("rect", flags).GetValue(top)).height;
            Read(cache.GetType().GetField("topLevel", flags).GetValue(cache), Vector2.zero);
            Read(cache.GetType().GetField("windows", flags).GetValue(cache), Vector2.zero);
            LayoutTrace = string.Join("\n", trace);
            void Read(object entry, Vector2 offset)
            {
                var t = entry.GetType();
                var rect = (Rect)t.GetField("rect", flags).GetValue(entry);
                if ((bool?)t.GetField("resetCoords", flags)?.GetValue(entry) == true) offset += rect.position;
                var absolute = new Rect(rect.position + offset, rect.size);
                var style = (GUIStyle)t.GetProperty("style", flags)?.GetValue(entry);
                if (string.Equals(style?.name, "textArea", StringComparison.OrdinalIgnoreCase)) OutputText = absolute;
                trace.Add(t.Name + " " + absolute + " " + style?.name);
                if (string.Equals(style?.name, "miniButton", StringComparison.OrdinalIgnoreCase) && rect.width > 200 && rect.height < 25) HistoryButtons.Add(absolute);
                if (absolute.xMin >= 0 && absolute.xMax <= position.width + 2 && style?.name == "button")
                {
                    if (Mathf.Abs(rect.width - 88) < .5f) SaveRules = absolute;
                    if (Mathf.Abs(rect.width - 154) < .5f) Generate = absolute;
                    if (Mathf.Abs(rect.width - 76) < .5f) AddRule = absolute;
                    if (Mathf.Abs(rect.width - 96) < .5f) SaveRecipe = absolute;
                }
                if (t.GetField("entries", flags)?.GetValue(entry) is IEnumerable children) foreach (var child in children) Read(child, offset);
            }
        }
    }
}
