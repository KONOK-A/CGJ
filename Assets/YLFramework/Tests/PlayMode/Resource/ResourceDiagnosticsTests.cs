using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;
using ResourceLocation = YLFramework.Services.Resource.ResourceLocation;

namespace YLFramework.Tests.Resource
{
    internal static class Resource89TestApi
    {
        internal static object Call(object target, string method, params object[] arguments)
        {
            var member = target.GetType().GetMethods().SingleOrDefault(x => x.Name == method && x.GetParameters().Length == arguments.Length);
            Assert.That(member, Is.Not.Null, method + " must expose the formal 8.9 API.");
            return member.Invoke(target, arguments);
        }

        internal static T Read<T>(object target, string name)
        {
            var member = target.GetType().GetProperty(name);
            Assert.That(member, Is.Not.Null, "Missing diagnostic/result property: " + name);
            return (T)member.GetValue(target);
        }

        internal static object[] Rows(object target, string name) => Read<IEnumerable>(target, name).Cast<object>().ToArray();
        internal static void EndSession(string method) => typeof(ResourceSystem).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }

    public class ResourceDiagnosticsTests
    {
        private TextAsset asset;
        private ResourceSystemTests.ControlledSource source;
        private ResourceSystem system;
        private readonly ResourceAddress address = new("diagnostics89", "text");
        private readonly ResourceAddress alias = new("diagnostics89", "alias");

        [SetUp]
        public void SetUp()
        {
            Assert.That(ResourceSystem.Current, Is.Null, "Do not take over another resource authority.");
            asset = new TextAsset("diagnostics89-owned");
            source = new ResourceSystemTests.ControlledSource("diagnostics89") { Asset = asset };
            using var request = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[]
            {
                new ResourceCatalog("diagnostics89", new[]
                {
                    new ResourceCatalogEntry("text-id", address, new ResourceLocation(source.Id, "Exact/Locator", typeof(TextAsset)), new[] { alias })
                })
            }, new IResourceSource[] { source }));
            system = request.TakeResult();
        }

        [TearDown]
        public void TearDown()
        {
            Resource89TestApi.EndSession("EndSession");
            foreach (var operation in source.Operations) if (!operation.IsDone) operation.Finish(asset);
            Resource89TestApi.EndSession("ResetSession");
            if (asset) Object.DestroyImmediate(asset);
        }

        [Test]
        public void ReportCopiesCanonicalProducerWaitersAndLeaseOwnersWithoutKeepingObjectsAlive()
        {
            using var first = system.AcquireAsync<TextAsset>(address);
            using var second = system.AcquireAsync<TextAsset>(alias);
            var before = Resource89TestApi.Call(system, "GetDiagnosticsReport");
            var loading = Resource89TestApi.Rows(before, "Assets").Single();
            Assert.That(Resource89TestApi.Read<ResourceAddress>(loading, "Address"), Is.EqualTo(address));
            Assert.That(Resource89TestApi.Read<int>(loading, "WaiterCount"), Is.EqualTo(2));
            Assert.That(Resource89TestApi.Read<bool>(loading, "IsLoading"), Is.True);
            Assert.That(Resource89TestApi.Read<bool>(loading, "ObjectAlive"), Is.False);
            source.Operations.Single().Finish(asset);
            using var a = first.TakeResult();
            using var b = second.TakeResult();
            var label = a.GetType().GetProperty("OwnerLabel");
            Assert.That(label, Is.Not.Null, "A real lease must accept an explicit diagnostic owner label.");
            label.SetValue(a, "HUD title");
            var ready = Resource89TestApi.Call(system, "GetDiagnosticsReport");
            var row = Resource89TestApi.Rows(ready, "Assets").Single();
            Assert.That(Resource89TestApi.Read<int>(row, "LeaseCount"), Is.EqualTo(2));
            Assert.That(Resource89TestApi.Read<int>(row, "ObjectInstanceId"), Is.EqualTo(asset.GetInstanceID()));
            Assert.That(Resource89TestApi.Read<bool>(row, "ObjectAlive"), Is.True);
            var owners = Resource89TestApi.Rows(row, "Owners");
            Assert.That(owners.All(x => Resource89TestApi.Read<ResourceAddress>(x, "Address") == address), Is.True);
            Assert.That(owners.Count(x => Resource89TestApi.Read<ResourceAddress>(x, "RequestedAddress") == alias), Is.EqualTo(1));
            Assert.That(owners.Count(x => Resource89TestApi.Read<string>(x, "OwnerLabel") == "HUD title"), Is.EqualTo(1));
            Assert.That(owners.Count(x => !Resource89TestApi.Read<bool>(x, "IsLabeled")), Is.EqualTo(1), "Unlabeled responsibility must not be guessed.");
            Assert.That(Resource89TestApi.Read<IList>(ready, "Assets").IsReadOnly, Is.True);
            Assert.That(Resource89TestApi.Read<int>(loading, "WaiterCount"), Is.EqualTo(2), "Earlier snapshots cannot follow mutable live records.");
            a.Dispose();
            b.Dispose();
            Assert.That(Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "Assets"), Is.Empty);
            Assert.That(Resource89TestApi.Read<int>(row, "LeaseCount"), Is.EqualTo(2));
            Assert.That(source.Releases, Is.EqualTo(1));
        }

        [Test]
        public void ReportUsesUnityFakeNullRatherThanManagedReferenceForDestroyedAssets()
        {
            using var lease = system.Acquire<TextAsset>(address);
            Object.DestroyImmediate(asset);
            var row = Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "Assets").Single();
            Assert.That(Resource89TestApi.Read<bool>(row, "ObjectAlive"), Is.False);
            Assert.That(Resource89TestApi.Read<int>(row, "LeaseCount"), Is.EqualTo(1));
            Assert.That(source.Releases, Is.Zero, "Reading diagnostics must not collect or repair resource ownership.");
        }

        [Test]
        public void HistoryIsOptInBoundedAndReportsCancellationFailureAndLateCleanup()
        {
            using (var ignored = system.AcquireAsync<TextAsset>(address)) ignored.Cancel();
            source.Operations[0].Finish(asset);
            Assert.That(Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "History"), Is.Empty);
            Resource89TestApi.Call(system, "ConfigureDiagnosticsHistory", 3);
            using (var late = system.AcquireAsync<TextAsset>(address)) late.Cancel();
            var canceled = Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "History").Single();
            Assert.That(Resource89TestApi.Read<ResourceRequestStatus?>(canceled, "Status"), Is.EqualTo(ResourceRequestStatus.Canceled));
            Assert.That(Resource89TestApi.Read<bool>(canceled, "HasDuration"), Is.True);
            Assert.That(Resource89TestApi.Read<double>(canceled, "DurationMilliseconds"), Is.GreaterThan(0));
            source.Operations[1].Finish(asset);
            using var failed = system.AcquireAsync<TextAsset>(address);
            source.Operations[2].Finish(null, new InvalidOperationException("diagnostics89-cause"));
            var history = Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "History");
            Assert.That(history.Length, Is.EqualTo(3));
            Assert.That(history.Any(x => Resource89TestApi.Read<string>(x, "Kind") == "LateResult"), Is.True);
            Assert.That(history.Any(x => Resource89TestApi.Read<string>(x, "Cause")?.Contains("diagnostics89-cause") == true &&
                Resource89TestApi.Read<bool>(x, "HasDuration")), Is.True);
            Assert.That(history.All(x => Resource89TestApi.Read<double>(x, "DurationMilliseconds") >= 0), Is.True);
            Resource89TestApi.Call(system, "ConfigureDiagnosticsHistory", 0);
            Assert.That(Resource89TestApi.Rows(Resource89TestApi.Call(system, "GetDiagnosticsReport"), "History"), Is.Empty);
        }
    }
}
