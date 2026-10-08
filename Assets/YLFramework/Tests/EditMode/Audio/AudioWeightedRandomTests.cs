using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace YLFramework.Tests.Audio
{
    public class AudioWeightedRandomTests
    {
        private AudioTestSettings mSettings;
        private AudioClip mFirst, mSecond, mDestroyed;
        private Random.State mRandomState;

        [SetUp]
        public void SetUp()
        {
            mRandomState = Random.state;
            mSettings = new AudioTestSettings();
            AudioKit.Shutdown();
            mFirst = AudioClip.Create("weighted-first", 44100, 1, 44100, false);
            mSecond = AudioClip.Create("weighted-second", 44100, 1, 44100, false);
            mDestroyed = AudioClip.Create("weighted-destroyed", 44100, 1, 44100, false);
            Object.DestroyImmediate(mDestroyed);
        }

        [TearDown]
        public void TearDown()
        {
            mSettings.Dispose();
            Object.DestroyImmediate(mFirst);
            Object.DestroyImmediate(mSecond);
            Random.state = mRandomState;
        }

        [Test]
        public void WeightedClipIsSerializableWithTwoFieldsAndAnOptionalAssignmentOnlyConstructor()
        {
            Assert.IsTrue(typeof(AudioWeightedClip).IsValueType);
            Assert.IsTrue(typeof(AudioWeightedClip).IsSerializable);
            var fields = typeof(AudioWeightedClip).GetFields(BindingFlags.Public | BindingFlags.Instance);
            CollectionAssert.AreEquivalent(new[] { "Clip", "Weight" }, fields.Select(field => field.Name));
            Assert.AreEqual(typeof(AudioClip), typeof(AudioWeightedClip).GetField("Clip").FieldType);
            Assert.AreEqual(typeof(float), typeof(AudioWeightedClip).GetField("Weight").FieldType);
            var ctor = typeof(AudioWeightedClip).GetConstructor(new[] { typeof(AudioClip), typeof(float) });
            Assert.IsNotNull(ctor);
            Assert.IsTrue(ctor.GetParameters()[1].IsOptional);
            Assert.AreEqual(1f, ctor.GetParameters()[1].DefaultValue);
            var entry = new AudioWeightedClip(mFirst);
            Assert.AreSame(mFirst, entry.Clip);
            Assert.AreEqual(1f, entry.Weight);
            entry = new AudioWeightedClip(null, float.NaN);
            Assert.IsNull(entry.Clip);
            Assert.IsTrue(float.IsNaN(entry.Weight), "Construction stores values; selection owns validation.");
            entry = new AudioWeightedClip(mDestroyed, -1f);
            Assert.AreSame(mDestroyed, entry.Clip);
            Assert.AreEqual(-1f, entry.Weight);
            Assert.AreEqual(0f, default(AudioWeightedClip).Weight);
        }

        [Test]
        public void NewEntriesAreIndependentAndExistingRandomSignatureHasNoOverloads()
        {
            foreach (var name in new[] { "PlayRandomSound", "PlayWeightedRandomSound", "TrySelectWeightedRandomClip" })
                Assert.AreEqual(1, typeof(AudioKit).GetMethods().Count(method => method.Name == name), name);
            var existing = typeof(AudioKit).GetMethod("PlayRandomSound").GetParameters();
            Assert.AreEqual(typeof(IReadOnlyList<AudioClip>), existing[0].ParameterType);
            var weighted = typeof(AudioKit).GetMethod("PlayWeightedRandomSound").GetParameters();
            Assert.AreEqual(4, weighted.Length);
            Assert.AreEqual(typeof(IReadOnlyList<AudioWeightedClip>), weighted[0].ParameterType);
            Assert.AreEqual(typeof(AudioSoundOptions), weighted[1].ParameterType);
            Assert.IsTrue(weighted[1].IsOptional);
            Assert.IsNull(weighted[1].DefaultValue);
            Assert.AreEqual(typeof(Action<AudioClip, AudioHandle>), weighted[2].ParameterType);
            Assert.IsTrue(weighted[2].IsOptional);
            Assert.IsNull(weighted[2].DefaultValue);
            Assert.AreEqual("onFinished", weighted[2].Name);
            Assert.AreEqual(typeof(Action<AudioClip, AudioHandle>), weighted[3].ParameterType);
            Assert.AreEqual("onStarted", weighted[3].Name);
            Assert.IsTrue(weighted[3].IsOptional);
            Assert.IsNull(weighted[3].DefaultValue);
            var selector = typeof(AudioKit).GetMethod("TrySelectWeightedRandomClip");
            Assert.AreEqual(typeof(bool), selector.ReturnType);
            Assert.AreEqual(typeof(AudioClip).MakeByRefType(), selector.GetParameters()[1].ParameterType);
            Assert.IsTrue(selector.GetParameters()[1].IsOut);
        }

        [Test]
        public void EmptyAndIneligibleEntriesDoNotSampleInitializeOrCreateAHost()
        {
            var candidates = new IReadOnlyList<AudioWeightedClip>[]
            {
                null, Array.Empty<AudioWeightedClip>(),
                new[] { new AudioWeightedClip(mFirst, 0f), new AudioWeightedClip(mSecond, -0f) },
                new[] { new AudioWeightedClip(null, 5f), new AudioWeightedClip(mDestroyed, 9f) }
            };
            var state = Random.state;
            var callbacks = 0;
            foreach (var clips in candidates)
            {
                Assert.IsFalse(AudioKit.TrySelectWeightedRandomClip(clips, out var clip));
                Assert.IsNull(clip);
                Assert.IsFalse(AudioKit.PlayWeightedRandomSound(clips, onFinished: (_, __) => callbacks++).IsValid);
                Assert.AreEqual(state, Random.state);
            }
            Assert.AreEqual(0, callbacks);
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void EveryWeightIncludingNullAndDestroyedEntriesIsValidatedBeforeSampling(float weight)
        {
            var state = Random.state;
            foreach (var invalidClip in new[] { mFirst, null, mDestroyed })
            {
                foreach (var invalidFirst in new[] { true, false })
                {
                    var valid = new AudioWeightedClip(mSecond, 1f);
                    var invalid = new AudioWeightedClip(invalidClip, weight);
                    var clips = invalidFirst ? new[] { invalid, valid } : new[] { valid, invalid };
                    var selected = mFirst;
                    Assert.Throws<ArgumentOutOfRangeException>(() => AudioKit.TrySelectWeightedRandomClip(clips, out selected));
                    Assert.IsNull(selected);
                    var options = AudioSoundOptions.Defaults;
                    options.Pitch = float.NaN;
                    var error = Assert.Throws<ArgumentOutOfRangeException>(() => AudioKit.PlayWeightedRandomSound(clips, options));
                    Assert.AreEqual("clips", error.ParamName, "Validate every weight before entering the playback chain.");
                    Assert.AreEqual(state, Random.state);
                }
                Assert.Throws<ArgumentOutOfRangeException>(() => AudioKit.TrySelectWeightedRandomClip(
                    new[] { new AudioWeightedClip(invalidClip, weight) }, out _));
            }
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void SinglePositiveLiveEntryIgnoresZeroNullAndDestroyedClipsAndSamplesOnce()
        {
            var clips = new[]
            {
                new AudioWeightedClip(mSecond, 0f), new AudioWeightedClip(null, float.MaxValue),
                new AudioWeightedClip(mDestroyed, float.MaxValue), new AudioWeightedClip(mFirst, float.Epsilon)
            };
            Random.InitState(739);
            for (var i = 0; i < 64; i++)
            {
                var before = Random.state;
                _ = Random.value;
                var afterOne = Random.state;
                Random.state = before;
                Assert.IsTrue(AudioKit.TrySelectWeightedRandomClip(clips, out var clip));
                Assert.AreSame(mFirst, clip);
                Assert.AreEqual(afterOne, Random.state);
            }
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void DuplicateEntriesKeepTheirOrderedIntervalsAndDoNotModifyCallerArray()
        {
            var clips = new[]
            {
                new AudioWeightedClip(mFirst, 1f), new AudioWeightedClip(mSecond, 6f),
                new AudioWeightedClip(mFirst, 3f), new AudioWeightedClip(null, 20f)
            };
            var original = (AudioWeightedClip[])clips.Clone();
            Random.InitState(3217);
            for (var i = 0; i < 256; i++)
            {
                var before = Random.state;
                var sample = Random.value * 10d;
                var expected = sample < 1d || sample >= 7d ? mFirst : mSecond;
                var afterOne = Random.state;
                Random.state = before;
                Assert.IsTrue(AudioKit.TrySelectWeightedRandomClip(clips, out var clip));
                Assert.AreSame(expected, clip);
                Assert.AreEqual(afterOne, Random.state);
            }
            CollectionAssert.AreEqual(original, clips);
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void MultipleMaximumFloatWeightsUseDoubleAccumulation()
        {
            var clips = new[] { new AudioWeightedClip(mFirst, float.MaxValue), new AudioWeightedClip(mSecond, float.MaxValue) };
            Random.InitState(8013);
            var firstCount = 0;
            for (var i = 0; i < 512; i++)
            {
                var before = Random.state;
                var expected = Random.value < .5f ? mFirst : mSecond;
                Random.state = before;
                Assert.IsTrue(AudioKit.TrySelectWeightedRandomClip(clips, out var clip));
                Assert.AreSame(expected, clip);
                if (clip == mFirst) firstCount++;
            }
            Assert.That(firstCount, Is.InRange(170, 340));
        }

        [TestCase(false, .25f)]
        [TestCase(true, .4f)]
        public void FixedSeedDistributionHasGenerousToleranceAndDuplicateClipProbabilityAddsNaturally(bool duplicates, float expected)
        {
            var clips = duplicates
                ? new[] { new AudioWeightedClip(mFirst, 1f), new AudioWeightedClip(mSecond, 6f), new AudioWeightedClip(mFirst, 3f) }
                : new[] { new AudioWeightedClip(mFirst, 1f), new AudioWeightedClip(mSecond, 3f) };
            Random.InitState(4919);
            var count = 0;
            const int samples = 8000;
            for (var i = 0; i < samples; i++)
            {
                Assert.IsTrue(AudioKit.TrySelectWeightedRandomClip(clips, out var clip));
                if (clip == mFirst) count++;
            }
            Assert.That(count / (float)samples, Is.InRange(expected - .06f, expected + .06f));
        }

        [Test]
        public void LegacyEqualRandomStillUsesOneIntegerRangeAndCountsDuplicateEntries()
        {
            var clips = new[] { null, mFirst, mSecond, mDestroyed, mFirst };
            Random.InitState(619);
            for (var i = 0; i < 128; i++)
            {
                var before = Random.state;
                var index = Random.Range(0, 3);
                var expected = index == 1 ? mSecond : mFirst;
                var afterOne = Random.state;
                Random.state = before;
                Assert.IsTrue(AudioKit.TrySelectRandomClip(clips, out var selected));
                Assert.AreSame(expected, selected);
                Assert.AreEqual(afterOne, Random.state);
            }
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void SelectedClipStillUsesPlaybackOptionValidationBeforeHostAllocation()
        {
            var options = AudioSoundOptions.Defaults;
            options.Pitch = 0f;
            var before = Random.state;
            _ = Random.value;
            var afterOne = Random.state;
            Random.state = before;
            Assert.Throws<ArgumentOutOfRangeException>(() => AudioKit.PlayWeightedRandomSound(
                new[] { new AudioWeightedClip(mFirst) }, options));
            Assert.AreEqual(afterOne, Random.state);
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void EmitterDefaultsKeepOldSerializedClipsAndSeparateWeightedList()
        {
            var root = new GameObject("WeightedEmitter.Defaults");
            try
            {
                var emitter = root.AddComponent<AudioEmitter>();
                var type = typeof(AudioEmitter);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (var name in new[] { "clips", "useWeightedRandom", "weightedClips" })
                    Assert.IsTrue(type.GetField(name, flags).IsDefined(typeof(SerializeField), false), name);
                Assert.AreEqual(false, type.GetField("useWeightedRandom", flags).GetValue(emitter));
                var clips = (List<AudioClip>)type.GetField("clips", flags).GetValue(emitter);
                var weighted = (List<AudioWeightedClip>)type.GetField("weightedClips", flags).GetValue(emitter);
                Assert.IsNotNull(clips);
                Assert.IsNotNull(weighted);
                Assert.IsEmpty(clips);
                Assert.IsEmpty(weighted);
                clips.Add(mFirst);
                Assert.IsEmpty(weighted);
                weighted.Add(new AudioWeightedClip(mSecond));
                CollectionAssert.AreEqual(new[] { mFirst }, clips);
                JsonUtility.FromJsonOverwrite("{\"volume\":0.6,\"loop\":true}", emitter);
                Assert.AreEqual(false, type.GetField("useWeightedRandom", flags).GetValue(emitter));
                Assert.AreSame(mFirst, clips[0]);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
