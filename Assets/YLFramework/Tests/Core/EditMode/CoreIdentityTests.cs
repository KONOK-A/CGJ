using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using YLFramework.Core.Pool;
using YLFramework.Foundation;

namespace YLFramework.Core.Tests
{
    public class CoreIdentityTests
    {
        [Test]
        public void SerializedDictionaryHasOneFoundationOwnerAndLegacyTypesExit()
        {
            var foundation = typeof(CustomAsyncOperation<>).Assembly;
            var core = typeof(Module).Assembly;
            var runtime = typeof(YLFramework.Services.Save.SaveManager).Assembly;
            var dictionary = foundation.GetType("YLFramework.Foundation.SerializedDictionary`2");
            Assert.That(dictionary, Is.Not.Null);
            var products = new[] { foundation, core, runtime };
            foreach (var assembly in products)
            {
                Assert.That(assembly.GetType("YLFramework.Core.SerializedDictionary`2"), Is.Null);
                Assert.That(assembly.GetType("YLFramework.Core.DictionaryItem`2"), Is.Null);
                Assert.That(assembly.GetType("YLFramework.Services.Save.SerializedDictionary`2"), Is.Null);
            }
            var owners = AppDomain.CurrentDomain.GetAssemblies().Where(a =>
                a.GetType("YLFramework.Foundation.SerializedDictionary`2") != null).ToArray();
            Assert.That(owners, Is.EqualTo(new[] { foundation }));
            foreach (var consumer in new[] { core, runtime })
                Assert.That(consumer.GetReferencedAssemblies().Select(a => a.Name),
                    Does.Contain("YLFramework.Foundation.Runtime"));
            var original = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "XFGameFramework");
            Assert.That(original.GetType("XFGameFramework.SerializedDictionary`2"), Is.Not.Null);
            Assert.That(original.GetType("XFGameFramework.DictionaryItem`2").IsPublic, Is.True);
        }

        [Test]
        public void CoreLegacyPoolExitsWhileFoundationSupportsExplicitDynamicCreationAndCallbacks()
        {
            var core = typeof(Module).Assembly;
            Assert.That(core.GetType("YLFramework.Core.ClassObjectPoolManager"), Is.Null);
            Assert.That(core.GetType("YLFramework.Core.IReuseable"), Is.Null);
            Assert.That(typeof(ObjectPool<>).Assembly.GetName().Name, Is.EqualTo("YLFramework.Foundation.Runtime"));
            var type = typeof(System.Text.StringBuilder);
            var loads = 0;
            var returns = 0;
            using var pool = new ObjectPool<object>(() => Activator.CreateInstance(type),
                onAllocate: _ => loads++, reset: value => { ((System.Text.StringBuilder)value).Clear(); returns++; });
            var first = pool.Allocate();
            ((System.Text.StringBuilder)first).Append("old lease");
            Assert.That(pool.Recycle(first), Is.True);
            var second = pool.Allocate();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.ToString(), Is.Empty);
            Assert.That(loads, Is.EqualTo(2), "Both cold and cached allocations must run the optional callback.");
            Assert.That(pool.Recycle(second), Is.True);
            Assert.That(returns, Is.EqualTo(2));
            pool.Clear();
            Assert.That(pool.CurStoreCount, Is.Zero);
        }

        [Test]
        public void ModuleHasOneCloseHookAndExplicitShutdownWithoutLegacyExitEntry()
        {
            Assert.That(typeof(Module).GetMethod("OnExit"), Is.Null);
            var hook = typeof(Module).GetMethod("OnClose", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(hook.IsFamily && hook.IsVirtual, Is.True);
            var shutdown = typeof(ModuleManager).GetMethod("Shutdown");
            Assert.That(shutdown.IsStatic, Is.True);
            Assert.That(shutdown.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(shutdown.GetParameters(), Is.Empty);
        }

        [TestCase(typeof(StartUpModuleRequest))]
        [TestCase(typeof(SendWebRequest))]
        [TestCase(typeof(LoadViewRequest<View>))]
        [TestCase(typeof(LoadPanelRequest<Panel>))]
        public void RequestsBelongToCoreAndInheritYLFoundation(Type requestType)
        {
            Assert.That(requestType.Assembly.GetName().Name, Is.EqualTo("YLFramework.Core.Runtime"));
            Assert.That(requestType.BaseType.GetGenericTypeDefinition(), Is.EqualTo(typeof(CustomAsyncOperation<>)));
            Assert.That(requestType.BaseType.GenericTypeArguments, Is.EqualTo(new[] { requestType }));
            Assert.That(requestType.BaseType.Assembly.GetName().Name, Is.EqualTo("YLFramework.Foundation.Runtime"));
        }

        [Test]
        public void CoreDependsOnFoundationAndTemporaryBackendNotOriginalXFOrExistingYL()
        {
            var references = typeof(Module).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references, Does.Contain("YLFramework.Foundation.Runtime"));
            Assert.That(references, Does.Contain("XFABManager"));
            Assert.That(references, Does.Contain("UnityEngine.UI"));
            Assert.That(references, Does.Not.Contain("XFGameFramework"));
            Assert.That(references, Does.Not.Contain("YLFramework.Runtime"));
            Assert.That(references.Any(n => n.EndsWith(".Editor") || n == "XFGameFrameworkEditor"), Is.False);
        }

        [Test]
        public void CoreTypesHaveUniqueNamesAcrossCoexistingFrameworkAssemblies()
        {
            var core = typeof(Module).Assembly;
            // Unity emits global MonoScript metadata types, including nested structs.
            var types = core.GetTypes().Where(t =>
            {
                var declaringType = t;
                while (declaringType.DeclaringType != null)
                    declaringType = declaringType.DeclaringType;
                return !declaringType.IsDefined(typeof(CompilerGeneratedAttribute), false);
            }).ToArray();
            Assert.That(types.All(t => t.Namespace != null &&
                (t.Namespace == "YLFramework.Core" || t.Namespace.StartsWith("YLFramework.Core."))), Is.True);
            Assert.That(typeof(YLFramework.Foundation.Attributes.CustomClassNameGUI).Assembly,
                Is.SameAs(typeof(CustomAsyncOperation<>).Assembly));
            Assert.That(typeof(Extensions.GameObjectExtensions).Namespace, Is.EqualTo("YLFramework.Core.Extensions"));

            var others = AppDomain.CurrentDomain.GetAssemblies().Where(a => a != core &&
                (a.GetName().Name.StartsWith("YLFramework") || a.GetName().Name.StartsWith("XFGameFramework") ||
                 a.GetName().Name == "XFABManager")).ToArray();
            foreach (var type in types)
            {
                foreach (var assembly in others)
                    Assert.That(assembly.GetType(type.FullName), Is.Null, type.FullName + " also exists in " + assembly.GetName().Name);
            }
        }
    }
}
