using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Foundation;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Core.Tests
{
    public class NeutralLayerMaskConsumerTests
    {
        [Test]
        public void RuntimeWrappersAndFoundationKeepValidatedMembershipAndUnityNullSemantics()
        {
            var target = new GameObject("LayerMask consumer");
            try
            {
                foreach (int layer in new[] { 0, 31 })
                {
                    target.layer = layer;
                    LayerMask included = 1 << layer;
                    LayerMask excluded = ~(1 << layer);
                    Assert.That(included.Contains(layer), Is.True);
                    Assert.That(excluded.Contains(layer), Is.False);
                    Assert.That(target.IsInLayerMask(included), Is.True);
                    Assert.That(target.transform.IsInLayerMask(included), Is.True);
                    Assert.That(target.IsInLayerMask(excluded), Is.False);
                    Assert.That(target.transform.IsInLayerMask(excluded), Is.False);
                }
                Assert.That(LayerMaskUtility.IsInLayerMask(-1, -1), Is.False);
                Assert.That(LayerMaskUtility.IsInLayerMask(32, -1), Is.False);
                Assert.That(LayerMaskUtility.IsInLayerMask((GameObject)null, -1), Is.False);
                UnityEngine.Object.DestroyImmediate(target);
                Assert.That(LayerMaskUtility.IsInLayerMask(target, -1), Is.False);
            }
            finally { if (target) UnityEngine.Object.DestroyImmediate(target); }
        }

        [Test]
        public void CoreTriggerCallbacksFilterRealThreeAndTwoDimensionalColliderLayers()
        {
            var host = new GameObject("LayerMask listener");
            var other = new GameObject("LayerMask collider");
            var other2D = new GameObject("LayerMask collider 2D");
            try
            {
                var listener = host.AddComponent<TriggerEventListener>();
                var collider = other.AddComponent<BoxCollider>();
                var collider2D = other2D.AddComponent<BoxCollider2D>();
                Assert.That(collider && collider2D, Is.True);
                int calls = 0;
#pragma warning disable CS0618
                listener.onTriggerEnter = new TriggerEvent();
                listener.onTriggerStay = new TriggerEvent();
                listener.onTriggerExit = new TriggerEvent();
                listener.onTriggerEnter2D = new TriggerEvent2D();
                listener.onTriggerStay2D = new TriggerEvent2D();
                listener.onTriggerExit2D = new TriggerEvent2D();
                listener.onTriggerEnter.AddListener(_ => calls++);
                listener.onTriggerStay.AddListener(_ => calls++);
                listener.onTriggerExit.AddListener(_ => calls++);
                listener.onTriggerEnter2D.AddListener(_ => calls++);
                listener.onTriggerStay2D.AddListener(_ => calls++);
                listener.onTriggerExit2D.AddListener(_ => calls++);
#pragma warning restore CS0618
                var mask = typeof(TriggerEventListener).GetField("layerMask", BindingFlags.Instance | BindingFlags.NonPublic);
                mask.SetValue(listener, (LayerMask)(1 << 31));
                foreach (int layer in new[] { 0, 31 })
                {
                    other.layer = layer;
                    other2D.layer = layer;
                    foreach (string name in new[] { "OnTriggerEnter", "OnTriggerStay", "OnTriggerExit" })
                    {
                        // EditMode 下不走 Unity 的 Play 行为分发；直接验证真实消费者对 Collider 的过滤。
                        typeof(TriggerEventListener).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(listener, new object[] { collider });
                        typeof(TriggerEventListener).GetMethod(name + "2D", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(listener, new object[] { collider2D });
                    }
                    Assert.That(calls, Is.EqualTo(layer == 0 ? 0 : 6));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(other2D);
            }
        }
    }
}
