using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Utilities.Timing;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class TimingContractTests
    {
        private sealed class HeapElement : IBinaryHeapElement
        {
            public float SortScore { get; set; }
            public int HeapIndex { get; set; }
            public void RebuildHeap<T>(BinaryHeap<T> heap) where T : IBinaryHeapElement => heap.RebuildAtIndex(HeapIndex);
        }

        private GameObject mRoot;

        [SetUp]
        public void SetUp()
        {
            mRoot = null;
        }

        [TearDown]
        public void TearDown()
        {
            Timer.Shutdown();
            if (mRoot) Object.DestroyImmediate(mRoot);
        }

        [TestCase(BinaryHeapSortMode.kMin, BinaryHeapBuildMode.kN)]
        [TestCase(BinaryHeapSortMode.kMin, BinaryHeapBuildMode.kNLog)]
        [TestCase(BinaryHeapSortMode.kMax, BinaryHeapBuildMode.kN)]
        [TestCase(BinaryHeapSortMode.kMax, BinaryHeapBuildMode.kNLog)]
        public void BulkHeapBuildTracksEveryIndexAndPopsInOrder(BinaryHeapSortMode sortMode, BinaryHeapBuildMode buildMode)
        {
            var data = new[] { new HeapElement { SortScore = 3 }, new HeapElement { SortScore = 1 },
                new HeapElement { SortScore = 4 }, new HeapElement { SortScore = 2 }, new HeapElement { SortScore = 5 } };
            var heap = new BinaryHeap<HeapElement>(0, sortMode);
            heap.Insert(data, buildMode);
            foreach (var element in data) Assert.That(heap.GetElement(element.HeapIndex), Is.SameAs(element));
            var values = new List<float>();
            while (heap.HasValue())
            {
                var element = heap.Pop();
                Assert.That(element.HeapIndex, Is.Zero);
                values.Add(element.SortScore);
            }
            CollectionAssert.AreEqual(sortMode == BinaryHeapSortMode.kMin ? new float[] { 1, 2, 3, 4, 5 }
                : new float[] { 5, 4, 3, 2, 1 }, values);
            Assert.That(heap.Count, Is.Zero);
            Assert.That(heap.GetElement(-1), Is.Null);
            Assert.DoesNotThrow(() => heap.RebuildAtIndex(0));
        }

        [Test]
        public void HeapRepairsScoreChangesRemovalsAndSortDirection()
        {
            var first = new HeapElement { SortScore = 1 };
            var second = new HeapElement { SortScore = 2 };
            var leaf = new HeapElement { SortScore = 3 };
            var heap = new BinaryHeap<HeapElement>(new[] { first, second, leaf }, BinaryHeapSortMode.kMin);
            leaf.SortScore = 0;
            leaf.RebuildHeap(heap);
            Assert.That(heap.Top(), Is.SameAs(leaf));
            heap.RemoveAt(leaf.HeapIndex);
            Assert.That(leaf.HeapIndex, Is.Zero);
            heap.Sort(BinaryHeapSortMode.kMax);
            Assert.That(heap.Top(), Is.SameAs(second));
            heap.Clear();
            Assert.That(first.HeapIndex, Is.Zero);
            Assert.That(second.HeapIndex, Is.Zero);
        }

        [Test]
        public void TimerResetFromCallbackReleasesPendingAndCurrentRepeatedTasks()
        {
            mRoot = new GameObject("AudioKit.UtilityTests.Timer");
            var timer = mRoot.AddComponent<Timer>();
            timer.OnSingletonInit();
            var calls = 0;
            var repeated = timer.Post2Really(_ => { calls++; timer.ResetMgr(); }, 0, -1);
            var pending = timer.Post2Scale(_ => calls++, 100);
            repeated.SortScore = -100;
            timer.UpdateMgr();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(repeated.IsRecycled, Is.True);
            Assert.That(pending.IsRecycled, Is.True);
            Assert.That(repeated.callback, Is.Null);
            Assert.That(pending.callback, Is.Null);
        }

        [Test]
        public void TimeItemCountsTicksRepeatsAndStopsAfterCancellation()
        {
            var ticks = new List<int>();
            var item = TimeItem.Allocate(ticks.Add, 0, 2);
            item.OnTimeTick();
            Assert.That(item.NeedRepeat(), Is.True);
            item.OnTimeTick();
            Assert.That(item.NeedRepeat(), Is.False);
            CollectionAssert.AreEqual(new[] { 1, 2 }, ticks);
            item.Set(ticks.Add, 0, -1);
            item.OnTimeTick();
            Assert.That(item.NeedRepeat(), Is.True);
            item.Cancel();
            item.OnTimeTick();
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, ticks);
            item.Recycle2Cache();
            Assert.That(item.callback, Is.Null);
            Assert.That(item.HeapIndex, Is.Zero);
            Assert.That(item.IsRecycled, Is.True);
        }

        [Test]
        public void TimerHelperCancelAndRecycleNeverReuseExposedHandles()
        {
            var helper = new TimerHelper();
            var previous = TimeItem.Allocate(_ => { }, 0);
            helper.Add(previous);
            helper.Clear();
            Assert.That(previous.IsEnabled, Is.False);
            previous.Recycle2Cache();
            var current = TimeItem.Allocate(_ => { }, 0);
            Assert.That(current, Is.Not.SameAs(previous));
            previous.Cancel();
            Assert.That(current.IsEnabled, Is.True);
            current.Cancel();
            current.Set(_ => { }, 1, 1);
            Assert.That(current.IsEnabled, Is.True);
        }

    }
}
