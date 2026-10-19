using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Character.Controller2D;

namespace YLFramework.Tests.Extensions.Behavior
{
    public class CharacterMotor2DTests
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private GameObject NewObject(string name, Vector3 position)
        {
            var obj = new GameObject("YL.Behavior." + name);
            obj.transform.position = position;
            owned.Add(obj);
            return obj;
        }

        private CharacterController2D Motor(Vector3 center)
        {
            var obj = NewObject("Motor", center);
            obj.SetActive(false);
            obj.AddComponent<Rigidbody2D>().gravityScale = 2;
            obj.AddComponent<BoxCollider2D>().size = new Vector2(1, 2);
            var motor = obj.AddComponent<CharacterController2D>();
            motor.MaxJumpCount = 2;
            motor.onJumpCountValueChange = new IntUnityEvent();
            obj.SetActive(true);
            return motor;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var obj in owned) if (obj) Object.DestroyImmediate(obj);
            owned.Clear();
        }

        [Test]
        public void JumpCountIsCommittedBeforeCallbackAndLimitIsPreserved()
        {
            var motor = Motor(new Vector3(14000, 14000));
            int observed = 0;
            motor.onJumpCountValueChange.AddListener(value => { Assert.That(motor.CurrentJumpCount, Is.EqualTo(value)); observed++; });
            motor.Jump(5);
            motor.Jump(5);
            motor.Jump(5);
            Assert.That(motor.CurrentJumpCount, Is.EqualTo(2));
            Assert.That(observed, Is.EqualTo(2));
        }

        [Test]
        public void DisableRestoresGravityAndDiscardsQueuedMovementAndJump()
        {
            var motor = Motor(new Vector3(14000, 14100));
            motor.Move(new Vector2(2, 0));
            motor.JumpStart(5, 10);
            motor.enabled = false;
            Assert.That(motor.Rigidbody.gravityScale, Is.EqualTo(2));
            Assert.That(motor.CurrentJumpCount, Is.Zero);
            Assert.That(motor.Velocity, Is.EqualTo(Vector2.zero));
            motor.enabled = true;
            Assert.That(motor.IsGrounded || motor.IsTouchWall || motor.IsTop, Is.False);
            Assert.That(typeof(CharacterController2D).GetField("moveDelta", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motor), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TwoMotorsHaveDistinctRaycastAndContactBuffers()
        {
            var a = Motor(new Vector3(14000, 14200));
            var b = Motor(new Vector3(14010, 14200));
            foreach (var name in new[] { "hits", "contacts" })
            {
                var field = typeof(CharacterController2D).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                Assert.That(field.GetValue(a), Is.Not.SameAs(field.GetValue(b)));
            }
        }

        [Test]
        public void MissingColliderIsDiagnosedAndExplicitRefreshRecovers()
        {
            var obj = NewObject("InvalidMotor", new Vector3(14000, 14300));
            obj.SetActive(false);
            var motor = obj.AddComponent<CharacterController2D>();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Rigidbody2D requires an attached"));
            obj.SetActive(true);
            obj.AddComponent<CircleCollider2D>();
            Assert.That(motor.RefreshColliders(), Is.True);
        }

        [UnityTest]
        public IEnumerator GroundAndMultipleAttachedCollidersSurviveDisableEnable()
        {
            var center = new Vector3(14000, 14400);
            var floor = NewObject("Floor", center + Vector3.down * 1.5f).AddComponent<BoxCollider2D>();
            floor.size = new Vector2(10, 1);
            var motor = Motor(center);
            var child = NewObject("SecondCollider", center + Vector3.up * 0.5f);
            child.transform.SetParent(motor.transform, true);
            child.AddComponent<CircleCollider2D>().radius = 0.2f;
            Assert.That(motor.RefreshColliders(), Is.True);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 20; i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(motor.GroundInfo.collider, Is.SameAs(floor));
            motor.enabled = false;
            motor.enabled = true;
            for (int i = 0; i < 10; i++) { yield return new WaitForFixedUpdate(); yield return null; }
            Assert.That(motor.IsGrounded, Is.True);
        }
    }
}
