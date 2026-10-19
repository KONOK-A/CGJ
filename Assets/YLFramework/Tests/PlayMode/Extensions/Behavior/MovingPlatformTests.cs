using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Platform2D;

namespace YLFramework.Tests.Extensions.Behavior
{
    public class MovingPlatformTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private GameObject NewObject(string name, Vector3 position)
        {
            var obj = new GameObject("YL.Behavior." + name);
            obj.transform.position = position;
            owned.Add(obj);
            return obj;
        }

        private Moveable NewMoveable(Vector3 origin, params Vector3[] points)
        {
            var obj = NewObject("Moveable", origin);
            obj.SetActive(false);
            var moveable = obj.AddComponent<Moveable>();
            moveable.Positions.AddRange(points);
            obj.SetActive(true);
            return moveable;
        }

        private static void Tick(Moveable moveable) => typeof(Moveable).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(moveable, null);

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in owned) if (obj) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        [Test]
        public void SingleNodeCompletesSafelyAndPauseStopDoNotRestartStoppedPath()
        {
            var point = new Vector3(16000, 16000);
            var moveable = NewMoveable(point + Vector3.right, point);
            Tick(moveable);
            Assert.That(moveable.transform.position, Is.EqualTo(point));
            Assert.That(moveable.IsRunning, Is.False);
            moveable.Pause(false);
            Assert.That(moveable.IsRunning, Is.False);
        }

        [Test]
        public void StopFromMoveCallbackPreventsPositionCommit()
        {
            var point = new Vector3(16000, 16100);
            var moveable = NewMoveable(point, point, point + Vector3.right * 4);
            moveable.onMove = new Vector3UnityEvent();
            moveable.onMove.AddListener(_ => moveable.Stop());
            Tick(moveable);
            Assert.That(moveable.transform.position, Is.EqualTo(point));
            Assert.That(moveable.IsRunning, Is.False);
        }

        [Test]
        public void DeletedTransformGivesIndexedDiagnosticWithoutBreakingAnotherPath()
        {
            var point = new Vector3(16000, 16200);
            var moveable = NewMoveable(point, point, point + Vector3.right);
            moveable.Stop();
            moveable.PositionType = PositionType.Transforms;
            var node = NewObject("Node", point);
            moveable.Transforms.Add(node.transform);
            moveable.Transforms.Add(null);
            Assert.That(moveable.ValidatePath(out string error), Is.False);
            Assert.That(error, Does.Contain("1"));
            var other = NewMoveable(point + Vector3.up * 10, point, point + Vector3.right);
            Assert.That(other.ValidatePath(out _), Is.True);
        }

        [Test]
        public void CarrierMovesEachRigidbodyOnlyOnceAcrossAreasAndMultipleColliders()
        {
            var point = new Vector3(16000, 16300);
            var platformObject = NewObject("Carrier", point);
            platformObject.SetActive(false);
            var catcher = platformObject.AddComponent<PlatformCatcher2D>();
            var platform = platformObject.AddComponent<MoveablePlatform2D>();
            platform.Positions.Add(point);
            platform.Positions.Add(point + Vector3.right * 4);
            catcher.areas = new[] { new Area { from = new Vector3(-2, 1), to = new Vector3(2, 1) }, new Area { from = new Vector3(-2, 1), to = new Vector3(2, 1) } };
            var passenger = NewObject("Passenger", point + Vector3.up);
            var body = passenger.AddComponent<Rigidbody2D>();
            body.gravityScale = 0;
            passenger.AddComponent<BoxCollider2D>();
            passenger.AddComponent<CircleCollider2D>();
            platformObject.SetActive(true);
            Physics2D.SyncTransforms();
            catcher.RefreshContacts();
            Assert.That(catcher.Contacts.Count, Is.GreaterThanOrEqualTo(2));
            var before = body.position;
            typeof(MoveablePlatform2D).GetMethod("Move", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Vector3) }, null).Invoke(platform, new object[] { point + Vector3.right });
            Assert.That(body.position, Is.EqualTo(before + Vector2.right));
            catcher.enabled = false;
            Assert.That(catcher.Contacts, Is.Empty);
        }
    }
}
