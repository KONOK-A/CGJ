using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using YLFramework.Actions;
using YLFramework.Binding;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioMixerTests
    {
        private const string FixturePath = "Assets/YLFramework/Tests/Fixtures/Audio/AudioMixerFixture.mixer";
        private const string ForeignPath = "Assets/YLFramework/Tests/Fixtures/Audio/ForeignAudioMixerFixture.mixer";
        private AudioTestSettings mSettings;
        private AudioMixer mMixer, mForeignMixer;
        private AudioMixerGroup mMusic, mVoice, mSound, mUI;
        private AudioMixerConfig mConfig;
        private AudioClip mClip;
        private GameObject mTarget;

        [SetUp]
        public void SetUp()
        {
            mSettings = new AudioTestSettings();
            mMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(FixturePath);
            mForeignMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(ForeignPath);
            Assert.IsNotNull(mMixer, "Import the independent test mixer before running these tests.");
            Assert.IsNotNull(mForeignMixer);
            mMusic = Group("Music"); mVoice = Group("Voice"); mSound = Group("Sound"); mUI = Group("UI");
            mConfig = ScriptableObject.CreateInstance<AudioMixerConfig>();
            Configure(mMixer, mMusic, mVoice, mSound);
            mClip = AudioClip.Create("mixer-test", 44100, 1, 44100, false);
            mTarget = new GameObject("MixerTest.Target");
        }

        [TearDown]
        public void TearDown()
        {
            mSettings?.Dispose();
            Object.DestroyImmediate(mConfig);
            Object.DestroyImmediate(mClip);
            Object.DestroyImmediate(mTarget);
        }

        private AudioMixerGroup Group(string name)
        {
            var matches = mMixer.FindMatchingGroups(name).Where(group => group.name == name).ToArray();
            Assert.That(matches, Has.Length.EqualTo(1), name);
            return matches[0];
        }

        private void Configure(AudioMixer mixer, AudioMixerGroup music, AudioMixerGroup voice, AudioMixerGroup sound)
        {
            var serialized = new SerializedObject(mConfig);
            serialized.FindProperty("mixer").objectReferenceValue = mixer;
            serialized.FindProperty("musicGroup").objectReferenceValue = music;
            serialized.FindProperty("voiceGroup").objectReferenceValue = voice;
            serialized.FindProperty("soundGroup").objectReferenceValue = sound;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private AudioHandle Play(int entry, AudioOutputRoute route)
        {
            var options = AudioSoundOptions.Defaults;
            options.OutputRoute = route;
            options.Loop = true;
            switch (entry)
            {
                case 0: return AudioKit.PlayMusic(mClip, outputRoute: route);
                case 1: return AudioKit.PlayVoice(mClip, true, outputRoute: route);
                case 2: return AudioKit.PlayVoiceAt(mClip, Vector3.one, options);
                case 3: return AudioKit.PlayVoiceFollowing(mClip, mTarget.transform, options);
                case 4: return AudioKit.PlaySound(mClip, options);
                case 5: return AudioKit.PlaySoundAt(mClip, Vector3.one, options);
                case 6: return AudioKit.PlaySoundFollowing(mClip, mTarget.transform, options);
                case 7: return AudioKit.PlayRandomSound(new[] { null, mClip }, options);
                default: throw new ArgumentOutOfRangeException(nameof(entry));
            }
        }

        [Test]
        public void FixtureHasRealGroupsSnapshotsAndExposedParameters()
        {
            CollectionAssert.AreEquivalent(new[] { "Master", "Music", "Voice", "Sound", "UI" },
                mMixer.FindMatchingGroups(string.Empty).Select(group => group.name));
            foreach (var group in new[] { mMusic, mVoice, mSound, mUI }) Assert.AreSame(mMixer, group.audioMixer);
            var baseline = mMixer.FindSnapshot("Default");
            var quiet = mMixer.FindSnapshot("Quiet");
            Assert.IsNotNull(baseline); Assert.IsNotNull(quiet);
            Assert.AreNotSame(baseline, quiet);
            Assert.AreSame(mMixer, baseline.audioMixer);
            Assert.AreSame(mMixer, quiet.audioMixer);
            Assert.IsTrue(mMixer.GetFloat("MusicVolume", out _), "An exposed name must map to an actual group parameter.");
            Assert.IsTrue(mMixer.GetFloat("SnapshotVolume", out _));
        }

        [Test]
        public void RouteFactoriesDefaultAndOptionsHaveExactThreeStateContract()
        {
            Assert.IsTrue(typeof(AudioOutputRoute).IsSerializable);
            Assert.AreEqual(0, (int)AudioOutputRoute.RouteMode.Inherit);
            Assert.AreEqual(1, (int)AudioOutputRoute.RouteMode.MixerGroup);
            Assert.AreEqual(2, (int)AudioOutputRoute.RouteMode.Direct);
            var inherited = default(AudioOutputRoute);
            var direct = AudioOutputRoute.Direct;
            var explicitRoute = AudioOutputRoute.ToMixer(mUI);
            Assert.AreEqual(AudioOutputRoute.RouteMode.Inherit, inherited.Mode);
            Assert.IsNull(inherited.Group);
            Assert.AreEqual(inherited, AudioOutputRoute.Inherit);
            Assert.AreEqual(inherited, AudioSoundOptions.Defaults.OutputRoute);
            Assert.AreEqual(AudioOutputRoute.RouteMode.Direct, direct.Mode);
            Assert.IsNull(direct.Group);
            Assert.AreEqual(AudioOutputRoute.RouteMode.MixerGroup, explicitRoute.Mode);
            Assert.AreSame(mUI, explicitRoute.Group);
            Assert.DoesNotThrow(() => inherited.Validate());
            Assert.DoesNotThrow(() => direct.Validate());
            Assert.DoesNotThrow(() => explicitRoute.Validate());
            Assert.That(() => AudioOutputRoute.ToMixer(null), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void SerializedIllegalRouteCombinationsAreRejected()
        {
            var invalid = new[]
            {
                new AudioOutputRoute { Mode = (AudioOutputRoute.RouteMode)99 },
                new AudioOutputRoute { Mode = AudioOutputRoute.RouteMode.MixerGroup },
                new AudioOutputRoute { Mode = AudioOutputRoute.RouteMode.Inherit, Group = mUI },
                new AudioOutputRoute { Mode = AudioOutputRoute.RouteMode.Direct, Group = mUI }
            };
            foreach (var route in invalid)
            {
                Assert.That(() => route.Validate(), Throws.InstanceOf<ArgumentException>());
                var options = AudioSoundOptions.Defaults;
                options.OutputRoute = route;
                Assert.That(() => options.Validate(false), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => options.Validate(true), Throws.InstanceOf<ArgumentException>());
            }
        }

        [TestCase(AudioOutputRoute.RouteMode.Inherit)] [TestCase(AudioOutputRoute.RouteMode.Direct)]
        public void InspectorEmptyGroupIsValidForInheritedAndDirectRoutes(AudioOutputRoute.RouteMode mode)
        {
            var emitter = mTarget.AddComponent<AudioEmitter>();
            var serialized = new SerializedObject(emitter);
            var property = serialized.FindProperty("outputRoute");
            property.FindPropertyRelative("Mode").intValue = (int)mode;
            property.FindPropertyRelative("Group").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var route = (AudioOutputRoute)typeof(AudioEmitter).GetField("outputRoute",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(emitter);
            Assert.IsTrue(route.Group == null, "An empty Inspector reference is Unity-null.");
            Assert.DoesNotThrow(() => route.Validate());
            var options = AudioSoundOptions.Defaults;
            options.OutputRoute = route;
            AudioKit.Settings.SoundOutputGroup.Value = mSound;
            var handle = AudioKit.PlaySound(mClip, options);
            handle.Pause();
            Assert.AreSame(mode == AudioOutputRoute.RouteMode.Inherit ? mSound : null, handle.Voice.Source.outputAudioMixerGroup);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        public void EveryEntryValidatesRoutingBeforeHostAllocationOrSlotReplacement(int entry)
        {
            var invalid = new AudioOutputRoute { Mode = AudioOutputRoute.RouteMode.MixerGroup };
            Assert.That(() => Play(entry, invalid), Throws.InstanceOf<ArgumentException>());
            Assert.IsFalse(AudioManager.HasInstance);
            var old = Play(entry, AudioOutputRoute.ToMixer(mUI));
            old.Pause();
            var generation = old.Generation;
            var source = old.Voice.Source;
            var count = AudioKit.SoundCount;
            Assert.That(() => Play(entry, invalid), Throws.InstanceOf<ArgumentException>());
            Assert.IsTrue(old.IsValid, "Invalid replacement must not stop the existing request.");
            Assert.IsTrue(old.IsPaused);
            Assert.AreEqual(generation, old.Generation);
            Assert.AreSame(source, old.Voice.Source);
            Assert.AreSame(mUI, source.outputAudioMixerGroup);
            Assert.AreEqual(count, AudioKit.SoundCount);
        }

        [Test]
        public void InvalidRouteDoesNotConsumeDeduplicationOrBypassValidationOnRejectedSound()
        {
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames;
            var options = AudioSoundOptions.Defaults;
            options.OutputRoute = new AudioOutputRoute { Mode = (AudioOutputRoute.RouteMode)9 };
            AudioKit.Settings.IsSoundOn.Value = false;
            Assert.That(() => AudioKit.PlaySound(mClip, options), Throws.InstanceOf<ArgumentException>());
            Assert.IsFalse(AudioManager.HasInstance);
            AudioKit.Settings.IsSoundOn.Value = true;
            Assert.That(() => AudioKit.PlaySound(mClip, options), Throws.InstanceOf<ArgumentException>());
            var accepted = AudioKit.PlaySound(mClip);
            accepted.Pause();
            Assert.IsTrue(accepted.IsValid);
            Assert.That(() => AudioKit.PlaySound(mClip, options), Throws.InstanceOf<ArgumentException>(),
                "Deduplication rejection must not hide a malformed route.");
            Assert.IsFalse(AudioKit.PlaySound(mClip).IsValid);
            Assert.AreEqual(1, AudioKit.SoundCount);
        }

        [Test]
        public void ActionRejectsInvalidRouteBeforeAllocationOrPlayback()
        {
            var initialized = YLFramework.Actions.ActionKit.Initialized;
            var invalid = new AudioOutputRoute { Mode = AudioOutputRoute.RouteMode.Direct, Group = mUI };
            Assert.That(() => PlaySoundAction.Allocate(mClip, outputRoute: invalid), Throws.InstanceOf<ArgumentException>());
            Assert.AreEqual(initialized, YLFramework.Actions.ActionKit.Initialized);
            Assert.IsFalse(AudioManager.HasInstance);
            Assert.AreEqual(0, AudioKit.SoundCount);
        }

        [Test]
        public void CaptureCopiesRouteValueAndBorrowsTheExistingMixerGroup()
        {
            var options = new AudioSoundOptions { OutputRoute = AudioOutputRoute.ToMixer(mUI) };
            var captured = AudioPlaybackOptions.Capture(options);
            var copy = captured;
            Assert.AreSame(mUI, captured.OutputRoute.Group, "Capture borrows the asset instead of cloning it.");
            options.OutputRoute.Group = mSound;
            copy.OutputRoute.Group = mVoice;
            Assert.AreSame(mUI, captured.OutputRoute.Group);
            Assert.AreSame(mSound, options.OutputRoute.Group);
            Assert.AreSame(mVoice, copy.OutputRoute.Group);
            var handle = AudioKit.PlaySound(mClip, options);
            handle.Pause();
            Assert.AreSame(mSound, handle.Voice.Options.OutputRoute.Group);
            handle.Stop();
            Assert.AreEqual(default(AudioPlaybackOptions), handle.Voice.Options);
            Assert.IsTrue(ReferenceEquals(null, handle.Voice.Options.OutputRoute.Group));
            Assert.AreSame(mSound, options.OutputRoute.Group);
            Assert.IsTrue(mUI); Assert.IsTrue(mSound); Assert.IsTrue(mVoice);
        }

        [Test]
        public void ActionResetRetainsBorrowedTemplateAndDeinitClearsTemplateAndRequestReferences()
        {
            var initialized = YLFramework.Actions.ActionKit.Initialized;
            var field = typeof(PlaySoundAction).GetField("mOptions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.AreEqual(typeof(AudioPlaybackOptions), field.FieldType);
            var options = new AudioSoundOptions { Loop = true, OutputRoute = AudioOutputRoute.ToMixer(mUI) };
            var action = PlaySoundAction.AllocateWithOptions(mClip, options);
            try
            {
                var template = (AudioPlaybackOptions)field.GetValue(action);
                Assert.AreSame(mUI, template.OutputRoute.Group);
                options.OutputRoute = AudioOutputRoute.Direct;
                Assert.IsFalse(action.Execute(0f));
                var old = AudioKit.GetPlayingSounds()[0];
                old.Pause();
                Assert.AreSame(mUI, old.Voice.Options.OutputRoute.Group);
                Assert.AreSame(mUI, old.Voice.Source.outputAudioMixerGroup);
                old.SetVolumeScale(.2f);
                action.Reset();
                Assert.IsFalse(old.IsValid);
                Assert.AreEqual(default(AudioPlaybackOptions), old.Voice.Options);
                Assert.IsTrue(ReferenceEquals(null, old.Voice.Options.OutputRoute.Group));
                Assert.AreEqual(template, (AudioPlaybackOptions)field.GetValue(action));
                Assert.IsFalse(action.Execute(0f));
                var current = AudioKit.GetPlayingSounds()[0];
                current.Pause();
                Assert.AreEqual(template, current.Voice.Options);
                Assert.AreSame(mUI, current.Voice.Source.outputAudioMixerGroup);
                action.Deinit();
                var cleared = (AudioPlaybackOptions)field.GetValue(action);
                Assert.AreEqual(default(AudioPlaybackOptions), cleared);
                Assert.IsTrue(ReferenceEquals(null, cleared.OutputRoute.Group));
                Assert.AreEqual(default(AudioPlaybackOptions), current.Voice.Options);
                Assert.IsTrue(ReferenceEquals(null, current.Voice.Options.OutputRoute.Group));
                Assert.IsNull(current.Voice.Source.outputAudioMixerGroup);
                Assert.IsTrue(mUI, "Request cleanup must not destroy a borrowed mixer group.");
                Assert.IsTrue(mMixer); Assert.IsTrue(mClip);
            }
            finally
            {
                if (!action.Deinited) action.Deinit();
                if (!initialized) YLFramework.Actions.ActionKit.Shutdown();
            }
        }

        [Test]
        public void ConfigurationValidatesAllCategoriesBeforeApplyingAnything()
        {
            Assert.AreSame(mMixer, mConfig.Mixer);
            Assert.AreSame(mMusic, mConfig.MusicGroup);
            Assert.AreSame(mVoice, mConfig.VoiceGroup);
            Assert.AreSame(mSound, mConfig.SoundGroup);
            Assert.DoesNotThrow(() => mConfig.Validate());
            AudioKit.ApplyMixerConfig(mConfig);
            var foreign = mForeignMixer.FindMatchingGroups("Master").Single();
            for (var category = 0; category < 3; category++)
            {
                Configure(mMixer, category == 0 ? foreign : mUI, category == 1 ? foreign : mUI, category == 2 ? foreign : mUI);
                Assert.That(() => mConfig.Validate(), Throws.InstanceOf<ArgumentException>());
                Assert.That(() => AudioKit.ApplyMixerConfig(mConfig), Throws.InstanceOf<ArgumentException>());
                Assert.AreSame(mMusic, AudioKit.Settings.MusicOutputGroup.Value);
                Assert.AreSame(mVoice, AudioKit.Settings.VoiceOutputGroup.Value);
                Assert.AreSame(mSound, AudioKit.Settings.SoundOutputGroup.Value);
            }
            Configure(null, mMusic, null, null);
            Assert.That(() => AudioKit.ApplyMixerConfig(mConfig), Throws.InstanceOf<ArgumentException>());
            Configure(null, null, null, null);
            Assert.DoesNotThrow(() => mConfig.Validate());
            AudioKit.ApplyMixerConfig(mConfig);
            Assert.IsNull(AudioKit.Settings.MusicOutputGroup.Value);
            Assert.IsNull(AudioKit.Settings.VoiceOutputGroup.Value);
            Assert.IsNull(AudioKit.Settings.SoundOutputGroup.Value);
            Assert.That(() => AudioKit.ApplyMixerConfig(null), Throws.InstanceOf<ArgumentException>());
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void ApplyCopiesConfigurationAndClearIsNonCreatingEvenBeforeInitialization()
        {
            AudioKit.Shutdown();
            Assert.IsNull(AudioKit.CurrentSettings);
            AudioKit.ClearMixerRouting();
            Assert.IsNull(AudioKit.CurrentSettings);
            var sourceCount = Object.FindObjectsOfType<AudioSource>(true).Length;
            var listenerCount = Object.FindObjectsOfType<AudioListener>(true).Length;
            AudioKit.ApplyMixerConfig(mConfig);
            Assert.AreSame(mMusic, AudioKit.Settings.MusicOutputGroup.Value);
            Configure(mMixer, mUI, null, null);
            Assert.AreSame(mMusic, AudioKit.Settings.MusicOutputGroup.Value, "Asset edits do not automatically reapply routing.");
            AudioKit.ApplyMixerConfig(mConfig);
            Assert.AreSame(mUI, AudioKit.Settings.MusicOutputGroup.Value);
            Assert.IsNull(AudioKit.Settings.VoiceOutputGroup.Value);
            Assert.IsNull(AudioKit.Settings.SoundOutputGroup.Value);
            AudioKit.ClearMixerRouting();
            Assert.IsNull(AudioKit.Settings.MusicOutputGroup.Value);
            Assert.IsFalse(AudioManager.HasInstance);
            Assert.AreEqual(sourceCount, Object.FindObjectsOfType<AudioSource>(true).Length);
            Assert.AreEqual(listenerCount, Object.FindObjectsOfType<AudioListener>(true).Length);
            Assert.AreSame(mMixer, mConfig.Mixer, "Clear must not modify the borrowed configuration.");
            Assert.AreSame(mUI, mConfig.MusicGroup);
        }

        [Test]
        public void RoutingIsTransientPreservesSixSaveKeysAndDoesNotChangeGlobalComparer()
        {
            var comparer = BindableProperty<AudioMixerGroup>.Comparer;
            AudioKit.Shutdown();
            foreach (var key in AudioTestSettings.Keys) PlayerPrefs.DeleteKey(key);
            var settings = AudioKit.Settings;
            var groups = new[] { settings.MusicOutputGroup, settings.VoiceOutputGroup, settings.SoundOutputGroup };
            foreach (var property in groups)
            {
                Assert.AreEqual(typeof(BindableProperty<AudioMixerGroup>), property.GetType());
                Assert.IsNull(property.Value);
            }
            AudioKit.ApplyMixerConfig(mConfig);
            foreach (var key in AudioTestSettings.Keys) Assert.IsFalse(PlayerPrefs.HasKey(key), key);
            AudioKit.Shutdown();
            foreach (var property in groups) Assert.IsNull(property.Value, "Shutdown releases borrowed group references.");
            var recreated = AudioKit.Settings;
            Assert.IsNull(recreated.MusicOutputGroup.Value);
            Assert.IsNull(recreated.VoiceOutputGroup.Value);
            Assert.IsNull(recreated.SoundOutputGroup.Value);
            Assert.AreSame(comparer, BindableProperty<AudioMixerGroup>.Comparer);
            foreach (var key in AudioTestSettings.Keys) Assert.IsFalse(PlayerPrefs.HasKey(key), key);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [Test]
        public void ShutdownDisconnectsOldRoutingSubscriptionsAndOldHandles()
        {
            AudioKit.ApplyMixerConfig(mConfig);
            var settings = AudioKit.Settings;
            var calls = 0;
            settings.MusicOutputGroup.Register(_ => calls++);
            settings.VoiceOutputGroup.Register(_ => calls++);
            settings.SoundOutputGroup.Register(_ => calls++);
            var old = AudioKit.PlaySound(mClip);
            old.Pause();
            AudioKit.Shutdown();
            var callsAfterShutdown = calls;
            var fresh = AudioKit.Settings;
            fresh.IsSoundOn.SetValueWithoutCallback(true);
            fresh.SoundOutputGroup.Value = mSound;
            var current = AudioKit.PlaySound(mClip);
            current.Pause();
            settings.MusicOutputGroup.Value = mUI;
            settings.VoiceOutputGroup.Value = mUI;
            settings.SoundOutputGroup.Value = mUI;
            Assert.AreEqual(callsAfterShutdown, calls, "Old subscriptions must be detached.");
            Assert.AreSame(mSound, current.Voice.Source.outputAudioMixerGroup);
            Assert.IsNull(fresh.MusicOutputGroup.Value);
            Assert.IsNull(fresh.VoiceOutputGroup.Value);
            old.Stop(); old.Resume();
            Assert.IsFalse(old.IsValid);
            Assert.IsTrue(current.IsValid);
            Assert.IsTrue(current.IsPaused);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ReentrantDefaultChangesUseLatestSettingsRatherThanStaleNotificationPayload(int category)
        {
            var settings = AudioKit.Settings;
            var property = category == 0 ? settings.MusicOutputGroup : category == 1 ? settings.VoiceOutputGroup : settings.SoundOutputGroup;
            var foreign = mForeignMixer.FindMatchingGroups("Master").Single();
            var calls = 0;
            // Subscribe before host creation so the outer manager notification runs after our nested change.
            property.Register(group =>
            {
                calls++;
                if (group == mUI) property.Value = foreign;
            });
            var handle = Play(category == 0 ? 0 : category == 1 ? 1 : 4, AudioOutputRoute.Inherit);
            handle.Pause();
            var generation = handle.Generation;
            property.Value = mUI;
            Assert.AreEqual(2, calls, "Exercise both the outer and nested notifications.");
            Assert.AreSame(foreign, property.Value);
            Assert.AreSame(property.Value, handle.Voice.Source.outputAudioMixerGroup,
                "The stale outer payload must not overwrite the newer default.");
            Assert.AreEqual(generation, handle.Generation);
            Assert.IsTrue(handle.IsPaused);
        }

        [TestCase(false)] [TestCase(true)]
        public void ShutdownDuringApplyDoesNotRestoreReferencesInDisposedSettingsOrContinueIntoNewSession(bool recreate)
        {
            var oldSettings = AudioKit.Settings;
            var called = false;
            oldSettings.MusicOutputGroup.Register(_ =>
            {
                Assert.IsFalse(called);
                called = true;
                AudioKit.Shutdown();
                if (recreate) AudioKit.Initialize();
            });
            AudioKit.ApplyMixerConfig(mConfig);
            Assert.IsTrue(called);
            Assert.IsNull(oldSettings.MusicOutputGroup.Value);
            Assert.IsNull(oldSettings.VoiceOutputGroup.Value);
            Assert.IsNull(oldSettings.SoundOutputGroup.Value, "An interrupted Apply must not retain assets in a disposed session.");
            if (recreate)
            {
                var current = AudioKit.CurrentSettings;
                Assert.IsNotNull(current);
                Assert.AreNotSame(oldSettings, current);
                Assert.IsNull(current.MusicOutputGroup.Value);
                Assert.IsNull(current.VoiceOutputGroup.Value);
                Assert.IsNull(current.SoundOutputGroup.Value);
            }
            else Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
            Assert.AreSame(mMusic, mConfig.MusicGroup);
            Assert.AreSame(mVoice, mConfig.VoiceGroup);
            Assert.AreSame(mSound, mConfig.SoundGroup);
        }

        [Test]
        public void DestroyedConfigurationIsRejectedWithoutChangingDefaults()
        {
            AudioKit.ApplyMixerConfig(mConfig);
            Object.DestroyImmediate(mConfig);
            Assert.That(() => AudioKit.ApplyMixerConfig(mConfig), Throws.InstanceOf<ArgumentException>());
            Assert.AreSame(mMusic, AudioKit.Settings.MusicOutputGroup.Value);
            Assert.AreSame(mVoice, AudioKit.Settings.VoiceOutputGroup.Value);
            Assert.AreSame(mSound, AudioKit.Settings.SoundOutputGroup.Value);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [TestCase(0f, -80f)] [TestCase(.000001f, -80f)] [TestCase(.5f, -6.0206f)] [TestCase(1f, 0f)]
        public void LinearVolumeConversionHasFloorAndCorrectLogarithmicMapping(float volume, float expected)
        {
            Assert.AreEqual(expected, AudioMixerControl.LinearVolumeToDecibels(volume), .0001f);
        }

        [TestCase(-100f, 0f)] [TestCase(-80f, 0f)] [TestCase(-6.0206f, .5f)] [TestCase(0f, 1f)] [TestCase(20f, 1f)]
        public void DecibelConversionClampsAndTreatsFloorAsZero(float decibels, float expected)
        {
            Assert.AreEqual(expected, AudioMixerControl.DecibelsToLinearVolume(decibels), .0001f);
        }

        [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void LinearVolumeRejectsInvalidInputs(float value)
        {
            Assert.That(() => AudioMixerControl.LinearVolumeToDecibels(value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TrySetVolume(mMixer, "MusicVolume", value), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void RawFloatAndDecibelConversionRejectNonFiniteInputs(float value)
        {
            Assert.That(() => AudioMixerControl.TrySetFloat(mMixer, "MusicVolume", value), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.DecibelsToLinearVolume(value), Throws.InstanceOf<ArgumentException>());
        }

        [TestCase(null)] [TestCase("")] [TestCase(" \t")]
        public void ParameterMethodsRejectMissingNames(string name)
        {
            Assert.That(() => AudioMixerControl.TrySetFloat(mMixer, name, 0f), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryGetFloat(mMixer, name, out _), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryClearFloat(mMixer, name), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TrySetVolume(mMixer, name, .5f), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryGetVolume(mMixer, name, out _), Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ControlMethodsRejectNullMixerWithoutInitializingAudioKit()
        {
            AudioKit.Shutdown();
            Assert.That(() => AudioMixerControl.TrySetFloat(null, "MusicVolume", 0), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryGetFloat(null, "MusicVolume", out _), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryClearFloat(null, "MusicVolume"), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TrySetVolume(null, "MusicVolume", .5f), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TryGetVolume(null, "MusicVolume", out _), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshot(null, 0), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(null, new[] { mMixer.FindSnapshot("Default") }, new[] { 1f }, 0),
                Throws.InstanceOf<ArgumentException>());
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void SnapshotTransitionsRejectInvalidDuration(float duration)
        {
            var snapshot = mMixer.FindSnapshot("Default");
            Assert.That(() => AudioMixerControl.TransitionToSnapshot(snapshot, duration), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, new[] { snapshot }, new[] { 1f }, duration),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void SnapshotMixRejectsMalformedArraysForeignSnapshotsAndInvalidWeightsWithoutMutation()
        {
            var baseline = mMixer.FindSnapshot("Default");
            var quiet = mMixer.FindSnapshot("Quiet");
            var foreign = mForeignMixer.FindSnapshot("Default");
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, null, new[] { 1f }, 0), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, new[] { baseline }, null, 0), Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, Array.Empty<AudioMixerSnapshot>(), Array.Empty<float>(), 0),
                Throws.InstanceOf<ArgumentException>());
            Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, new[] { baseline, quiet }, new[] { 1f }, 0),
                Throws.InstanceOf<ArgumentException>());
            foreach (var snapshots in new[] { new[] { baseline, (AudioMixerSnapshot)null }, new[] { baseline, foreign } })
            {
                var copy = (AudioMixerSnapshot[])snapshots.Clone();
                Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, snapshots, new[] { 1f, 1f }, 0),
                    Throws.InstanceOf<ArgumentException>());
                CollectionAssert.AreEqual(copy, snapshots);
            }
            foreach (var weights in new[]
            {
                new[] { 0f, 0f }, new[] { 1f, -1f }, new[] { float.NaN, 1f },
                new[] { 1f, float.PositiveInfinity }, new[] { float.NegativeInfinity, 1f }
            })
            {
                var copy = (float[])weights.Clone();
                var snapshots = new[] { baseline, quiet };
                Assert.That(() => AudioMixerControl.TransitionToSnapshots(mMixer, snapshots, weights, 0), Throws.InstanceOf<ArgumentException>());
                CollectionAssert.AreEqual(copy, weights);
                CollectionAssert.AreEqual(new[] { baseline, quiet }, snapshots);
            }
        }
    }
}
