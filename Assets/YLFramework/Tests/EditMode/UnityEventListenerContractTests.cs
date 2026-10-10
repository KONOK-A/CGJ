using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using YLFramework.Core.Pool;
using YLFramework.Events;
using EventType = YLFramework.Events.EventType;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class UnityEventListenerContractTests
    {
        [Test]
        public void NativePayloadSchemaMatchesAllTwentyUnityMessages()
        {
            var root = new GameObject("Events.Listener.SchemaTest");
            try
            {
                var listener = root.AddComponent<EventListener>();
                CheckPayload<PointerEventData>(listener, EventType.OnMouseEnter, EventType.OnMouseExit,
                    EventType.OnClick, EventType.OnClickDown, EventType.OnClickUp,
                    EventType.OnBeginDrag, EventType.OnDrag, EventType.OnEndDrag);
                CheckPayload<Collision>(listener, EventType.OnCollisionEnter, EventType.OnCollisionStay, EventType.OnCollisionExit);
                CheckPayload<Collision2D>(listener, EventType.OnCollisionEnter2D, EventType.OnCollisionStay2D, EventType.OnCollisionExit2D);
                CheckPayload<Collider>(listener, EventType.OnTriggerEnter, EventType.OnTriggerStay, EventType.OnTriggerExit);
                CheckPayload<Collider2D>(listener, EventType.OnTriggerEnter2D, EventType.OnTriggerStay2D, EventType.OnTriggerExit2D);
                Assert.Throws<ArgumentOutOfRangeException>(() => listener.Register<int>((EventType)999, _ => { }));
                Assert.Throws<ArgumentException>(() => listener.Register<BaseEventData>(EventType.OnClick, null));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void CheckPayload<T>(EventListener listener, params EventType[] eventTypes)
        {
            foreach (var eventType in eventTypes)
            {
                Assert.Throws<ArgumentException>(() => listener.Register<int>(eventType, _ => { }), eventType.ToString());
                var calls = 0;
                var token = listener.Register<T>(eventType, value =>
                {
                    Assert.That(value, Is.Null);
                    calls++;
                });
                var methodName = eventType switch
                {
                    EventType.OnMouseEnter => "OnPointerEnter",
                    EventType.OnMouseExit => "OnPointerExit",
                    EventType.OnClick => "OnPointerClick",
                    EventType.OnClickDown => "OnPointerDown",
                    EventType.OnClickUp => "OnPointerUp",
                    _ => eventType.ToString()
                };
                var method = typeof(EventListener).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method.Invoke(listener, new object[] { null });
                Assert.That(calls, Is.EqualTo(1), eventType.ToString());
                token.UnRegister();
            }
        }

        [Test]
        public void PersistentUnityEventSurvivesDefaultCleanupAndExplicitTrueOnlyDisconnectsTheField()
        {
            var root = new GameObject("Events.Listener.PersistentTest");
            var receiverRoot = new GameObject("Events.Listener.PersistentReceiver");
            try
            {
                var listener = root.AddComponent<EventListener>();
                var receiver = receiverRoot.AddComponent<EventListener>();
                var order = new List<string>();
                receiver.Register<PointerEventData>(EventType.OnClick, _ => order.Add("persistent"));
                var unityEvent = new UnityEvent<PointerEventData>();
                UnityEventTools.AddPersistentListener(unityEvent, receiver.OnPointerClick);
                unityEvent.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
                var field = typeof(EventListener).GetField("pointerClick", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(listener, unityEvent);
                listener.Register<PointerEventData>(EventType.OnClick, _ => order.Add("code"));
                listener.OnPointerClick(null);
                Assert.That(order, Is.EqualTo(new[] { "code", "persistent" }));
                listener.UnRegisterAll();
                order.Clear();
                listener.OnPointerClick(null);
                Assert.That(order, Is.EqualTo(new[] { "persistent" }));
                Assert.That(unityEvent.GetPersistentEventCount(), Is.EqualTo(1));
                listener.UnRegisterAll(true);
                order.Clear();
                listener.OnPointerClick(null);
                Assert.That(order, Is.Empty);
                unityEvent.Invoke(null);
                Assert.That(order, Is.EqualTo(new[] { "persistent" }));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(receiverRoot);
            }
        }

        [Test]
        public void ListenerExtensionsDoNotCreateOnRemovalAndResetStopsOldCodeSnapshot()
        {
            var root = new GameObject("Events.Listener.ExtensionTest");
            try
            {
                root.transform.RemoveAllEvent();
                root.transform.UnRegisterEvent(EventType.OnClick);
                Assert.That(root.GetComponent<EventListener>(), Is.Null);
                var calls = new List<string>();
                root.transform.OnClick(_ =>
                {
                    calls.Add("first");
                    root.transform.UnRegisterEvent(EventType.OnClick);
                    root.transform.OnClick(__ => calls.Add("new"));
                });
                var old = root.transform.OnClick(_ => calls.Add("old"));
                var listener = root.GetComponent<EventListener>();
                listener.OnPointerClick(null);
                Assert.That(calls, Is.EqualTo(new[] { "first" }));
                old.UnRegister();
                listener.OnPointerClick(null);
                Assert.That(calls, Is.EqualTo(new[] { "first", "new" }));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ListenerCleanupDoesNotDependOnTheSharedEasyEventPoolLifetime()
        {
            var root = new GameObject("Events.Listener.PoolLifetimeTest");
            var pool = SafeObjectPool<EasyEvent<PointerEventData>>.Instance;
            Exception cleanupError = null;
            try
            {
                var listener = root.AddComponent<EventListener>();
                var oldCalls = 0;
                listener.Register<PointerEventData>(EventType.OnClick, _ =>
                {
                    pool.Dispose();
                    try { listener.UnRegisterAll(); }
                    catch (Exception error) { cleanupError = error; }
                });
                listener.Register<PointerEventData>(EventType.OnClick, _ => oldCalls++);
                listener.OnPointerClick(null);
                Assert.That(cleanupError, Is.Null, "组件自有代码监听的结束不应依赖全局池仍存活。");
                Assert.That(oldCalls, Is.Zero, "清理必须终结旧 owner 的在途快照。");
                listener.OnPointerClick(null);
                Assert.That(oldCalls, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
                pool.Dispose();
            }
        }
    }
}
