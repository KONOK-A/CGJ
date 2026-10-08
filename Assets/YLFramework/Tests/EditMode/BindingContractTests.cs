using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Binding;

namespace YLFramework.Tests
{
    public class BindingContractTests
    {
        private sealed class FirstSetProperty : BindableProperty<string>
        {
            private bool _set;
            protected override bool IsSameValue(string value) => _set && base.IsSameValue(value);
            protected override void SetValue(string value) { base.SetValue(value); _set = true; }
        }

        [Test]
        public void InitialCallbackPrecedesSubscriptionAndSameValuesDoNotNotify()
        {
            var property = new BindableProperty<string>("first");
            var observed = new List<string>();
            var token = property.RegisterWithInitValue(value =>
            {
                observed.Add(value);
                if (value == "first") property.Value = "second";
            });
            property.Value = "second";
            property.Value = "third";
            property.Value = null;
            property.Value = null;
            CollectionAssert.AreEqual(new[] { "first", "third", null }, observed);
            token.UnRegister();
            token.UnRegister();
            property.Value = "fourth";
            Assert.That(observed.Count, Is.EqualTo(3));
        }

        [Test]
        public void InstanceComparerDoesNotChangeOtherPropertiesOrStaticComparer()
        {
            var previous = BindableProperty<string>.Comparer;
            var custom = new BindableProperty<string>("audio").WithComparer(
                (a, b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase));
            var ordinary = new BindableProperty<string>("audio");
            Assert.That(custom.TrySetValue("AUDIO"), Is.False);
            Assert.That(ordinary.TrySetValue("AUDIO"), Is.True);
            Assert.That(BindableProperty<string>.Comparer, Is.SameAs(previous));
        }

        [Test]
        public void SilentSetForceNotifyAndDuplicateUnregisterKeepTheirContracts()
        {
            var property = new BindableProperty<int>(1);
            var observed = new List<int>();
            Action<int> listener = observed.Add;
            var first = property.Register(listener);
            var second = property.Register(listener);
            property.SetValueWithoutCallback(2);
            Assert.That(property.Value, Is.EqualTo(2));
            Assert.That(observed, Is.Empty);
            Assert.That(property.TrySetValue(2), Is.False);
            property.ForceNotify();
            first.UnRegister();
            property.Value = 3;
            property.UnRegisterAll();
            second.UnRegister();
            property.Value = 4;
            CollectionAssert.AreEqual(new[] { 2, 2, 3 }, observed);
        }

        [Test]
        public void ComparisonHookCanNotifyFirstEqualValueWithoutChangingDefaultPolicy()
        {
            var property = new FirstSetProperty();
            var calls = 0;
            property.Register(_ => calls++);
            Assert.That(property.TrySetValue(null), Is.True);
            Assert.That(property.TrySetValue(null), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(new BindableProperty<string>().TrySetValue(null), Is.False);
        }

        [TestCase(-1, false)]
        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        public void PlayerPrefsBoolRetainsZeroOneEncoding(int stored, bool expected)
        {
            var key = "YL.Binding.Tests." + Guid.NewGuid().ToString("N");
            try
            {
                var defaults = new PlayerPrefsBoolProperty(key, true);
                Assert.That(defaults.Value, Is.True);
                Assert.That(defaults.HasKey, Is.False);
                PlayerPrefs.SetInt(key, stored);
                var property = new PlayerPrefsBoolProperty(key, true);
                Assert.That(property.Value, Is.EqualTo(expected));
                property.Value = !expected;
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(expected ? 0 : 1));
                Assert.That(new PlayerPrefsBoolProperty(key).Value, Is.EqualTo(!expected));
            }
            finally { PlayerPrefs.DeleteKey(key); }
        }

        [Test]
        public void PlayerPrefsFloatWritesBeforeNotificationAndSilentSetDoesNotPersist()
        {
            var key = "YL.Binding.Tests." + Guid.NewGuid().ToString("N");
            try
            {
                var property = new PlayerPrefsFloatProperty(key, .75f);
                property.WithComparer((a, b) => a.Equals(b));
                Assert.That(property.Value, Is.EqualTo(.75f));
                Assert.That(property.HasKey, Is.False);
                var calls = 0;
                property.Register(value =>
                {
                    calls++;
                    Assert.That(property.Value, Is.EqualTo(value));
                    Assert.That(PlayerPrefs.GetFloat(key), Is.EqualTo(value));
                });
                var changed = .75f + .0000001f;
                property.Value = changed;
                property.Value = changed;
                property.SetValueWithoutCallback(.5f);
                Assert.That(property.Value, Is.EqualTo(.5f));
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(new PlayerPrefsFloatProperty(key).Value, Is.EqualTo(changed));
            }
            finally { PlayerPrefs.DeleteKey(key); }
        }

        [Test]
        public void PlayerPrefsReloadAndDeleteDoNotWriteBackButReentrantChangesDo()
        {
            var key = "YL.Binding.Tests." + Guid.NewGuid().ToString("N");
            try
            {
                var property = new PlayerPrefsIntProperty(key, 3);
                property.Value = 4;
                PlayerPrefs.SetInt(key, 5);
                property.Reload();
                Assert.That(property.Value, Is.EqualTo(5));
                property.Delete();
                Assert.That(property.Value, Is.EqualTo(3));
                Assert.That(property.HasKey, Is.False);
                property.Register(value => { if (value == 6) property.Value = 7; });
                PlayerPrefs.SetInt(key, 6);
                property.Reload();
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(7));
            }
            finally { PlayerPrefs.DeleteKey(key); }
        }
    }
}
