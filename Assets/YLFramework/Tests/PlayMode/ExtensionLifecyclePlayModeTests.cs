using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Foundation;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Tests
{
    public class ExtensionLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator DeferredDestroyKeepsManagedReferenceButMaskAndGracefulHelpersRespectFakeNull()
        {
            var go = new GameObject("YL.Tests.ExtensionLifetime.53");
            Component component = go.transform;
            try
            {
                go.layer = 31;
                Assert.That(go.IsInLayerMask(int.MinValue), Is.True);
                Assert.That(component.IsInLayerMask(int.MinValue), Is.True);
                Assert.That(go.IsDestroy() || component.IsDestroy(), Is.False);
                component.DestroyGameObjGracefully();
                Assert.That(go != null, Is.True, "Destroy 在帧末生效");
                Assert.That(go.IsDestroy(), Is.False);
                yield return null;
                Assert.That(ReferenceEquals(go, null), Is.False);
                Assert.That(ReferenceEquals(component, null), Is.False);
                Assert.That(go == null && component == null, Is.True);
                Assert.That(go.IsDestroy() && component.IsDestroy(), Is.True);
                Assert.That(((Object)null).IsDestroy(), Is.True);
                Assert.That(go.IsInLayerMask(-1), Is.False);
                Assert.That(component.IsInLayerMask(-1), Is.False);
                Assert.That(LayerMaskUtility.IsInLayerMask(go, -1), Is.False);
                Assert.DoesNotThrow(() => go.DestroySelfGracefully());
                Assert.DoesNotThrow(() => component.DestroyGameObjGracefully());
                Assert.DoesNotThrow(() => ((Component)null).DestroyGameObjGracefully());
            }
            finally { if (go) Object.Destroy(go); }
        }
    }
}
