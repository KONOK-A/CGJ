using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Tests.ResourceEditor
{
    public sealed class ResourceWorkbenchUX5Tests
    {
        private static readonly BindingFlags Internal = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void InactiveOwnedRuntimeEvidenceDoesNotLeaveAnEditorUpdateSubscription()
        {
            var type = typeof(YLFramework.Tests.ResourceBuild.ResourceWorkbenchUX5RuntimeEvidence);
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            Assert.That((UnityEditor.EditorApplication.update?.GetInvocationList() ?? Array.Empty<Delegate>()).Any(x => x.Method.DeclaringType == type), Is.False);
            Assert.That(ResourceSystem.Current, Is.Null);
        }

        [Test]
        public void GuideReferencesUseTheActualFormalSampleAndDoNotInventACiProduct()
        {
            var field = typeof(ResourceGuideView).GetField("References", Internal);
            Assert.That(field, Is.Not.Null);
            var links = (ValueTuple<string, string>[])field.GetValue(null);
            var sample = links.Single(x => x.Item2.EndsWith("ResourceBuildRecipeExample.cs", StringComparison.Ordinal));
            Assert.That(sample.Item1, Does.Contain("无窗口构建样例").And.Contain("不执行"));
            Assert.That(ResourceGuideView.BatchExampleHelp, Does.Contain("不是通用CI工具"));
            Assert.That(File.Exists(sample.Item2), Is.True);
            foreach (var link in links.Where(x => !x.Item2.Contains("Tests/Fixtures"))) Assert.That(File.Exists(link.Item2), Is.True, link.Item2);
        }

        [UnityTest]
        public IEnumerator MinimumRuntimeWithAReportAndExpandedOutputKeepsTheOutputReachable()
        {
            var window = ScriptableObject.CreateInstance<ResourceUX2GuiWindow>(); var focus = EditorWindow.focusedWindow;
            try
            {
                window.ShowUtility(); window.position = new Rect(100, 80, 620, 440);
                var view = typeof(ResServiceEditorWindow).GetField("_resServiceView", Internal).GetValue(window);
                view.GetType().GetField("tab", Internal).SetValue(view, 4);
                var layout = typeof(ResServiceEditorWindow).GetField("layout", Internal).GetValue(window);
                layout.GetType().GetField("Task").SetValue(layout, 4); layout.GetType().GetField("Output").SetValue(layout, true);
                var diag = view.GetType().GetField("diagnostics", Internal).GetValue(view);
                var report = (ResourceDiagnosticsReport)Activator.CreateInstance(typeof(ResourceDiagnosticsReport), Internal, null,
                    new object[] { ResourceSystemState.Ready, default(ResourceDiagnosticsSnapshot), null, null, null, null, null, null, null, null, 0, default(ResourceContentDiagnosticsSnapshot) }, null);
                diag.GetType().GetField("report", Internal).SetValue(diag, report); diag.GetType().GetField("section", Internal).SetValue(diag, 7);
                for (var i = 0; i < 8; i++) { window.Repaint(); yield return null; }
                Assert.That(window.RootContentHeight, Is.LessThanOrEqualTo(window.position.height), "Long runtime headers must scroll inside their allocated page, not push expanded task output outside the 620x440 window.\n" + window.LayoutTrace);
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); if (focus) focus.Focus(); }
        }

        [UnityTest]
        public IEnumerator MinimumStoreReportWithExpandedOutputAlsoKeepsRootHeightBounded()
        {
            var window = ScriptableObject.CreateInstance<ResourceUX2GuiWindow>(); var focus = EditorWindow.focusedWindow;
            try
            {
                window.ShowUtility(); window.position = new Rect(100, 80, 620, 440);
                var view = typeof(ResServiceEditorWindow).GetField("_resServiceView", Internal).GetValue(window);
                view.GetType().GetField("tab", Internal).SetValue(view, 4);
                var layout = typeof(ResServiceEditorWindow).GetField("layout", Internal).GetValue(window);
                layout.GetType().GetField("Task").SetValue(layout, 4); layout.GetType().GetField("Output").SetValue(layout, true);
                var diag = view.GetType().GetField("diagnostics", Internal).GetValue(view); diag.GetType().GetField("panel", Internal).SetValue(diag, 1);
                var query = (ResourceStoreQueryView)diag.GetType().GetField("store", Internal).GetValue(diag);
                query.Begin(Path.Combine(Path.GetTempPath(), "kilo", "ux5-viewport-" + Guid.NewGuid().ToString("N")), new[] { new ResourceProjectMount("viewport", "StandaloneWindows64", "operator-app") }, 10000);
                while (query.IsQuerying) query.Tick();
                for (var i = 0; i < 8; i++) { window.Repaint(); yield return null; }
                Assert.That(window.RootContentHeight, Is.LessThanOrEqualTo(window.position.height), "Store summary/notes must not push expanded output below the minimum window.");
                LogAssert.NoUnexpectedReceived();
            }
            finally { window.Close(); if (focus) focus.Focus(); }
        }

        [Test]
        public void AHiddenDockWindowPausesAndDisposesWithoutUsingLostFocusAsVisibility()
        {
            var hidden = typeof(ResServiceEditorWindow).GetMethod("OnBecameInvisible", Internal);
            Assert.That(hidden, Is.Not.Null, "A dock tab can be invisible without OnDisable or another OnGUI; hidden work must stop.");
            var window = ScriptableObject.CreateInstance<ResServiceEditorWindow>();
            try
            {
                var view = typeof(ResServiceEditorWindow).GetField("_resServiceView", Internal).GetValue(window);
                var pending = new UnityEditor.EditorApplication.CallbackFunction(() => { });
                view.GetType().GetField("pending", Internal).SetValue(view, pending);
                var diag = (ResourceDiagnosticsView)view.GetType().GetField("diagnostics", Internal).GetValue(view);
                diag.SetVisible(true); typeof(ResourceDiagnosticsView).GetField("automatic", Internal).SetValue(diag, true);
                var store = typeof(ResourceDiagnosticsView).GetField("store", Internal).GetValue(diag);
                var root = Path.Combine(Path.GetTempPath(), "kilo", "ux5-hidden-" + Guid.NewGuid().ToString("N"));
                store.GetType().GetMethod("Begin", Internal).Invoke(store, new object[] { root, new[] { new ResourceProjectMount("hidden", "StandaloneWindows64", "explicit-app") }, 10000 });
                hidden.Invoke(window, null);
                Assert.That(typeof(ResourceDiagnosticsView).GetField("automatic", Internal).GetValue(diag), Is.False);
                Assert.That(store.GetType().GetProperty("IsQuerying", Internal).GetValue(store), Is.False);
                Assert.That(store.GetType().GetField("report", Internal).GetValue(store), Is.Null);
                Assert.That(view.GetType().GetField("pending", Internal).GetValue(view), Is.SameAs(pending), "Only diagnostic visibility changes; legitimate queued writes are not a focus/visibility cancellation policy.");
                Assert.That(Directory.Exists(root), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(window); }
        }

        [Test]
        public void ContentRowsUseTheSameReportAndKeepBudgetNativeAndProcessCngCostsSeparate()
        {
            var content = (ResourceContentDiagnosticsSnapshot)Activator.CreateInstance(typeof(ResourceContentDiagnosticsSnapshot), Internal, null,
                new object[] { 1024L, 128L, 64L, 256L, 2, 1, 4096L, 0 }, null);
            var report = (ResourceDiagnosticsReport)Activator.CreateInstance(typeof(ResourceDiagnosticsReport), Internal, null,
                new object[] { ResourceSystemState.Ready, default(ResourceDiagnosticsSnapshot), null, null, null, null, null, null, null, null, 0, content }, null);
            var view = new ResourceDiagnosticsView();
            typeof(ResourceDiagnosticsView).GetField("report", Internal).SetValue(view, report);
            typeof(ResourceDiagnosticsView).GetField("section", Internal).SetValue(view, 7);
            typeof(ResourceDiagnosticsView).GetMethod("BuildRows", Internal).Invoke(view, null);
            var rows = (ValueTuple<string, string>[])typeof(ResourceDiagnosticsView).GetField("rows", Internal).GetValue(view);
            Assert.That(rows, Is.Not.Empty, "The original Content snapshot must be exposed, not dropped behind asset statistics.");
            var text = string.Join("\n", rows.Select(x => x.Item2));
            foreach (var token in new[] { "Limit=1024", "Allocated=128", "Reserved=64", "Peak=256", "NativeReadBuffer=4096", "CNG", "不是 Profiler 分配计数或进程总内存" })
                Assert.That(text, Does.Contain(token));
            view.Dispose(); Assert.That(ResourceSystem.Current, Is.Null);
        }

        [Test]
        public void StoreQueriesAreExplicitReadOnlyAndStoppedWithoutInventingACanceledReport()
        {
            var type = typeof(ResourceEditorOperations).Assembly.GetType("YLFramework.Editor.Resource.ResourceStoreQueryView");
            Assert.That(type, Is.Not.Null);
            var view = Activator.CreateInstance(type, true);
            var root = Path.Combine(Path.GetTempPath(), "kilo", "ux5-missing-" + Guid.NewGuid().ToString("N"));
            var begin = type.GetMethod("Begin", Internal); var tick = type.GetMethod("Tick", Internal); var stop = type.GetMethod("Stop", Internal);
            var mounts = new[] { new ResourceProjectMount("ux5-query", "StandaloneWindows64", "operator-app") };
            try
            {
                begin.Invoke(view, new object[] { root, mounts, 10000 });
                for (var i = 0; i < 100 && (bool)type.GetProperty("IsQuerying", Internal).GetValue(view); i++) tick.Invoke(view, null);
                var first = (ArtifactStoreReport)type.GetField("report", Internal).GetValue(view);
                Assert.That(first.Status, Is.EqualTo(ArtifactQueryStatus.Missing));
                begin.Invoke(view, new object[] { root, mounts, 10000 });
                stop.Invoke(view, new object[] { "left page" });
                Assert.That(type.GetField("report", Internal).GetValue(view), Is.SameAs(first));
                Assert.That(first.Status, Is.Not.EqualTo(ArtifactQueryStatus.Canceled), "Dispose before callback is not a new Canceled backend report.");
                Assert.That((bool)type.GetProperty("IsQuerying", Internal).GetValue(view), Is.False);
                Assert.That(Directory.Exists(root), Is.False); Assert.That(ResourceSystem.Current, Is.Null);
            }
            finally { ((IDisposable)view).Dispose(); }
        }

        [Test]
        public void AtMostOneQueryCanRunAndDisposingBeforeFirstMoveCannotPublishLateResults()
        {
            var type = typeof(ResourceEditorOperations).Assembly.GetType("YLFramework.Editor.Resource.ResourceStoreQueryView");
            Assert.That(type, Is.Not.Null);
            var view = Activator.CreateInstance(type, true);
            var root = Path.Combine(Path.GetTempPath(), "kilo", "ux5-query-" + Guid.NewGuid().ToString("N"));
            var mounts = new[] { new ResourceProjectMount("ux5-query", "StandaloneWindows64", "provided-app") };
            try
            {
                type.GetMethod("Begin", Internal).Invoke(view, new object[] { root, mounts, 1 });
                var failure = Assert.Throws<TargetInvocationException>(() => type.GetMethod("Begin", Internal).Invoke(view, new object[] { root, mounts, 1 }));
                Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
                ((IDisposable)view).Dispose();
                type.GetMethod("Tick", Internal).Invoke(view, null);
                Assert.That(type.GetField("report", Internal).GetValue(view), Is.Null);
                Assert.That(Directory.Exists(root), Is.False);
            }
            finally { ((IDisposable)view).Dispose(); }
        }

        [Test]
        public void LeavingDiagnosticsPausesAutomaticSamplingWithoutChangingApplicationHistory()
        {
            var view = new ResourceDiagnosticsView();
            var active = typeof(ResourceDiagnosticsView).GetMethod("SetVisible", Internal);
            Assert.That(active, Is.Not.Null);
            active.Invoke(view, new object[] { true });
            typeof(ResourceDiagnosticsView).GetField("automatic", Internal).SetValue(view, true);
            active.Invoke(view, new object[] { false });
            Assert.That(typeof(ResourceDiagnosticsView).GetField("automatic", Internal).GetValue(view), Is.False);
            active.Invoke(view, new object[] { true });
            Assert.That(typeof(ResourceDiagnosticsView).GetField("automatic", Internal).GetValue(view), Is.False, "Returning must not silently re-enable another business session's sampling.");
            Assert.That(ResourceSystem.Current, Is.Null); view.Dispose();
        }
    }
}
