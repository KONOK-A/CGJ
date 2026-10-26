#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using XFGameFramework;

public class GameViewLoadingTests
{
    [UnitySetUp]
    public IEnumerator ReadyResources() => GameTestResources.Ready();

    private MainModule module;
    private readonly List<GameObject> instances = new List<GameObject>();

    [SetUp]
    public void SetUp()
    {
        module = new MainModule();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var instance in instances)
            if (instance) Object.Destroy(instance);
        instances.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator SnakeInitializesEventsAndCanBeDisabledAndReused()
    {
        var snake = module.LoadView<SnakeHead>("Snake");
        Assert.NotNull(snake);
        instances.Add(snake.gameObject);
        var events = typeof(View).GetField("_events", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(events.GetValue(snake), "Unity must run the component's base field initializers.");
        Assert.IsFalse(snake.InputDisable);
        snake.InputDisable = true;
        Assert.IsTrue(snake.InputDisable);
        snake.InputDisable = false;
        Assert.IsFalse(snake.InputDisable);
        snake.Close();
        Assert.IsFalse(snake.gameObject.activeSelf);
        var reused = module.LoadView<SnakeHead>("Snake");
        Assert.AreSame(snake, reused);
        Assert.AreEqual(1, reused.Segments.Count);
        Assert.AreSame(module, reused.Module);
        yield return null;
        reused.Close();
        LogAssert.NoUnexpectedReceived();
    }

    [TestCase("HeadGun")]
    [TestCase("MachineGun")]
    [TestCase("ShotGun")]
    [TestCase("RocketGun")]
    public void WeaponsLoadByNameThroughEquipBase(string assetName)
    {
        var equip = module.LoadView<EquipBase>(assetName);
        Assert.NotNull(equip, "FunctionalSegment loads its equipment through EquipBase.");
        instances.Add(equip.gameObject);
        Assert.IsInstanceOf<WeaponBase>(equip);
        Assert.AreEqual(assetName, equip.GetType().Name);
        Assert.AreSame(module, equip.Module);
        equip.Close();
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator GrowingFunctionalSegmentsLoadsAllThreePickupWeapons()
    {
        var snake = module.LoadView<SnakeHead>("Snake");
        Assert.NotNull(snake);
        instances.Add(snake.gameObject);
        snake.SetDirection(Vector2.up);
        foreach (var assetName in new[] { "ShotGun", "MachineGun", "RocketGun" })
        {
            var before = snake.Segments.Count;
            snake.Grow(SegmentType.Functional, assetName);
            var segment = snake.Segments[1];
            instances.Add(segment.gameObject);
            Assert.AreEqual(before + 1, snake.Segments.Count);
            var equip = segment.GetComponentInChildren<EquipBase>();
            Assert.NotNull(equip);
            Assert.AreEqual(assetName, equip.GetType().Name);
            Assert.AreSame(snake, segment.HeadSegment);
            Assert.AreSame(module, equip.Module);
        }
        yield return null;
        LogAssert.NoUnexpectedReceived();
    }

}
#endif
