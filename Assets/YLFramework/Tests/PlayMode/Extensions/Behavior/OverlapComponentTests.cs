using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Overlap;

namespace YLFramework.Tests.Extensions.Behavior
{
    public class OverlapComponentTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private GameObject NewObject(string name, Vector3 position)
        {
            var obj = new GameObject("YL.Behavior." + name);
            obj.transform.position = position;
            owned.Add(obj);
            return obj;
        }

        private OverlapCircle2D Circle(Vector3 center)
        {
            var circle = NewObject("Query", center).AddComponent<OverlapCircle2D>();
            circle.Radius = 3;
            circle.LayerMask = 1;
            circle.Running = false;
            circle.onColliderEnter = new OverlapEvent2D();
            circle.onColliderExit = new OverlapEvent2D();
            return circle;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in owned) if (obj) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        [Test]
        public void ResultsEnterExitAndCrossInstanceCallbackQueriesRemainIsolated()
        {
            var center = new Vector3(12000, 12000);
            var first = Circle(center);
            var second = Circle(center + Vector3.right * 20);
            var firstCollider = NewObject("First", center).AddComponent<BoxCollider2D>();
            var secondCollider = NewObject("Second", second.transform.position).AddComponent<BoxCollider2D>();
            int enters = 0, exits = 0;
            first.onColliderEnter.AddListener(hit => { enters++; first.CheckOverlap(); second.CheckOverlap(); });
            first.onColliderExit.AddListener(hit => exits++);
            Physics2D.SyncTransforms();
            first.CheckOverlap();
            Assert.That(first.Results.Count, Is.EqualTo(1));
            Assert.That(first.Results[0].collider, Is.SameAs(firstCollider));
            Assert.That(second.Results[0].collider, Is.SameAs(secondCollider));
            first.CheckOverlap();
            Assert.That(enters, Is.EqualTo(1));
            firstCollider.transform.position += Vector3.up * 20;
            Physics2D.SyncTransforms();
            first.CheckOverlap();
            Assert.That(first.Results, Is.Empty);
            Assert.That(exits, Is.EqualTo(1));
        }

        [Test]
        public void DisableDuringEnterClearsSnapshotAndEnableStartsFresh()
        {
            var center = new Vector3(12000, 12100);
            var circle = Circle(center);
            NewObject("TargetA", center).AddComponent<BoxCollider2D>();
            NewObject("TargetB", center + Vector3.right).AddComponent<BoxCollider2D>();
            int enters = 0;
            circle.onColliderEnter.AddListener(hit => { enters++; circle.enabled = false; });
            Physics2D.SyncTransforms();
            circle.CheckOverlap();
            Assert.That(enters, Is.EqualTo(1));
            Assert.That(circle.Results, Is.Empty);
            circle.onColliderEnter.RemoveAllListeners();
            circle.enabled = true;
            circle.CheckOverlap();
            Assert.That(circle.Results.Count, Is.EqualTo(2));
        }

        [Test]
        public void DestroyedColliderAndEmptyAreaEmitExitWithoutStaleResults()
        {
            var center = new Vector3(12000, 12200);
            var area = NewObject("Area", center).AddComponent<OverlapArea2D>();
            area.Running = false;
            area.LayerMask = 1;
            area.Positions.Add(new Vector3(-2, -2));
            area.Positions.Add(new Vector3(2, 2));
            var collider = NewObject("Target", center).AddComponent<BoxCollider2D>();
            int exits = 0;
            area.onColliderExit = new OverlapEvent2D();
            area.onColliderExit.AddListener(hit => exits++);
            Physics2D.SyncTransforms();
            area.CheckOverlap();
            Assert.That(area.Results.Count, Is.EqualTo(1));
            Object.DestroyImmediate(collider);
            area.Positions.Clear();
            area.CheckOverlap();
            Assert.That(area.Results, Is.Empty);
            Assert.That(exits, Is.EqualTo(1));
        }
    }
}
