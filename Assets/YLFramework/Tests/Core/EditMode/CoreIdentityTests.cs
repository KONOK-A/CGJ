using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using YLFramework.Foundation;

namespace YLFramework.Core.Tests
{
    public class CoreIdentityTests
    {
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
            Assert.That(typeof(CustomClassNameGUI).FullName, Is.EqualTo("YLFramework.Core.CustomClassNameGUI"));
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
