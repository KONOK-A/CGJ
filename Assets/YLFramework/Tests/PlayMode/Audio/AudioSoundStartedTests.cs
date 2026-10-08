using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Services.Audio;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Audio;
#endif
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace YLFramework.Tests.Audio
{
    public class AudioSoundStartedTests
    {
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };
        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private readonly List<GameObject> mObjects = new List<GameObject>();
        private AudioClip mClip, mOther, mShort;
        private Transform mTarget;
        private Random.State mRandomState;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            mRandomState = Random.state;
            AudioKit.Shutdown();
            for (var i = 0; i < Keys.Length; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++)
            {
                mSwitches[i] = PlayerPrefs.GetInt(Keys[i]);
                mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]);
            }
            ConfigureSettings();
            mClip = AudioClip.Create("started-first", 220500, 1, 44100, false);
            mOther = AudioClip.Create("started-other", 220500, 1, 44100, false);
            mShort = AudioClip.Create("started-natural", 4410, 1, 44100, false);
            mTarget = OwnObject("Started.Target").transform;
            mTarget.position = new Vector3(3f, 4f, 5f);
            var hasListener = false;
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (!hasListener) OwnObject("Started.Listener").AddComponent<AudioListener>();
            yield return null;
        }

        private static void ConfigureSettings()
        {
            var settings = AudioKit.Settings;
            settings.IsSoundOn.SetValueWithoutCallback(true);
            settings.IsMusicOn.SetValueWithoutCallback(true);
            settings.IsVoiceOn.SetValueWithoutCallback(true);
            settings.SoundVolume.SetValueWithoutCallback(1f);
            settings.MusicVolume.SetValueWithoutCallback(1f);
            settings.VoiceVolume.SetValueWithoutCallback(1f);
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            AudioKit.GlobalFrameCountForIgnoreSameSound = 10000;
            AudioKit.SoundFrameCountForIgnoreSameSound = 10000;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown();
            foreach (var root in mObjects) if (root) Object.Destroy(root);
            mObjects.Clear();
            if (mClip) Object.Destroy(mClip);
            if (mOther) Object.Destroy(mOther);
            if (mShort) Object.Destroy(mShort);
            for (var i = 0; i < 3; i++)
            {
                if (mHadKeys[i]) PlayerPrefs.SetInt(Keys[i], mSwitches[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
                if (mHadKeys[i + 3]) PlayerPrefs.SetFloat(Keys[i + 3], mVolumes[i]);
                else PlayerPrefs.DeleteKey(Keys[i + 3]);
            }
            yield return null;
            Random.state = mRandomState;
        }

        private GameObject OwnObject(string name)
        {
            var root = new GameObject(name);
            mObjects.Add(root);
            return root;
        }

        private static AudioSoundConcurrency Concurrency(int limit, SoundOverflowPolicy policy = SoundOverflowPolicy.RejectNew) =>
            new AudioSoundConcurrency { Limit = limit, OverflowPolicy = policy };

        private static AudioSoundOptions Loop()
        {
            var options = AudioSoundOptions.Defaults;
            options.Loop = true;
            return options;
        }

        private AudioHandle PlayEntry(int entry, AudioSoundOptions options, Action<AudioClip, AudioHandle> started,
            Action<AudioClip, AudioHandle> finished = null)
        {
            switch (entry)
            {
                case 0: return AudioKit.PlaySound(mClip, options, finished, started);
                case 1: return AudioKit.PlaySoundAt(mClip, Vector3.one, options, finished, started);
                case 2: return AudioKit.PlaySoundFollowing(mClip, mTarget, options, finished, started);
                case 3: return AudioKit.PlayRandomSound(new[] { mClip, null, mOther }, options, finished, started);
                default: return AudioKit.PlayWeightedRandomSound(new[] { new AudioWeightedClip(mClip), new AudioWeightedClip(mOther, 3f) }, options, finished, started);
            }
        }

        private static IEnumerator WaitForEnd(AudioHandle handle)
        {
            var deadline = Time.realtimeSinceStartup + 3f;
            while (handle.IsValid && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(handle.IsValid, "The real source must finish and release its sound capacity within the deadline.");
        }

        [UnityTest]
        public IEnumerator AllFiveEntrypointsCommitOptionsLeaseCountAndDeduplicationBeforeSynchronousStarted()
        {
            AudioKit.SoundConcurrency = Concurrency(2);
            AudioKit.GlobalFrameCountForIgnoreSameSound = 0;
            AudioKit.SoundFrameCountForIgnoreSameSound = 0;
            AudioKit.Settings.SoundVolume.Value = .6f;
            AudioKit.Settings.IsSoundMuted.Value = true;
            var options = AudioSoundOptions.Defaults;
            options.Volume = .4f;
            options.Pitch = 1.25f;
            options.Priority = 23;
            options.MinDistance = 2f;
            options.MaxDistance = 30f;
            options.RolloffMode = AudioRolloffMode.Logarithmic;
            options.OutputRoute = AudioOutputRoute.Direct;
            options.AutoFade = new AudioAutoFade(1f, 1f);
            var starts = 0;
            var finishes = 0;
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                AudioKit.PlaySoundMode = mode;
                for (var entry = 0; entry < 5; entry++)
                {
                    var expected = mClip;
                    var before = Random.state;
                    if (entry == 3) Assert.IsTrue(AudioKit.TrySelectRandomClip(new[] { mClip, null, mOther }, out expected));
                    if (entry == 4) Assert.IsTrue(AudioKit.TrySelectWeightedRandomClip(new[] { new AudioWeightedClip(mClip), new AudioWeightedClip(mOther, 3f) }, out expected));
                    var afterSelection = Random.state;
                    Random.state = before;
                    var returned = false;
                    var observed = default(AudioHandle);
                    var handle = PlayEntry(entry, options, (actual, callbackHandle) =>
                    {
                        starts++;
                        observed = callbackHandle;
                        Assert.IsFalse(returned, "Sound onStarted must run before the playback method returns.");
                        Assert.AreSame(expected, actual);
                        Assert.IsTrue(callbackHandle.IsValid);
                        Assert.IsTrue(callbackHandle.IsPlaying);
                        Assert.AreEqual(1, AudioKit.SoundCount);
                        Assert.AreEqual(1, AudioKit.GetSoundCount(actual));
                        Assert.AreEqual(23, callbackHandle.Voice.Priority);
                        Assert.AreEqual(actual.GetInstanceID(), callbackHandle.Voice.ClipId);
                        Assert.AreEqual(1, AudioKit.SoundStats.ActiveCount);
                        Assert.AreEqual(1, AudioKit.SoundStats.PeakActiveCount);
                        Assert.AreEqual(starts, AudioKit.SoundStats.AdmittedCount);
                        CollectionAssert.Contains(AudioKit.GetPlayingSounds(), callbackHandle);
                        var snapshot = callbackHandle.Voice.Options;
                        Assert.AreEqual(options.Loop, snapshot.Loop);
                        Assert.AreEqual(options.Volume, snapshot.Volume);
                        Assert.AreEqual(options.Pitch, snapshot.Pitch);
                        Assert.AreEqual(options.PlaySoundMode, snapshot.PlaySoundMode);
                        Assert.AreEqual(options.MinDistance, snapshot.MinDistance);
                        Assert.AreEqual(options.MaxDistance, snapshot.MaxDistance);
                        Assert.AreEqual(options.RolloffMode, snapshot.RolloffMode);
                        Assert.AreEqual(options.OutputRoute, snapshot.OutputRoute);
                        Assert.AreEqual(options.AutoFade, snapshot.AutoFade);
                        Assert.AreEqual(options.Priority, snapshot.Priority);
                        snapshot.Volume = .2f;
                        snapshot.Pitch = 2f;
                        snapshot.AutoFade.FadeInSeconds = .5f;
                        snapshot.OutputRoute = AudioOutputRoute.Inherit;
                        Assert.AreEqual(.4f, callbackHandle.Voice.Options.Volume);
                        Assert.AreEqual(1.25f, callbackHandle.Voice.Options.Pitch);
                        Assert.AreEqual(new AudioAutoFade(1f, 1f), callbackHandle.Voice.Options.AutoFade);
                        Assert.AreEqual(AudioOutputRoute.Direct, callbackHandle.Voice.Options.OutputRoute);
                        var source = callbackHandle.Voice.Source;
                        Assert.IsTrue(source.isPlaying);
                        Assert.AreSame(actual, source.clip);
                        Assert.IsFalse(source.loop);
                        Assert.IsTrue(source.mute);
                        Assert.AreEqual(1.25f, source.pitch);
                        Assert.AreEqual(0f, source.volume, .0001f, "The automatic head envelope is configured before Play and notification.");
                        Assert.IsNull(source.outputAudioMixerGroup);
                        Assert.AreEqual(entry == 1 || entry == 2 ? 1f : 0f, source.spatialBlend);
                        Assert.AreEqual(0f, source.dopplerLevel);
                        if (entry == 1 || entry == 2)
                        {
                            Assert.AreEqual(entry == 1 ? Vector3.one : mTarget.position, source.transform.position);
                            Assert.AreEqual(2f, source.minDistance);
                            Assert.AreEqual(30f, source.maxDistance);
                            Assert.AreEqual(AudioRolloffMode.Logarithmic, source.rolloffMode);
                        }
                        Assert.IsFalse(AudioKit.Channels.CanPlay(actual.GetInstanceID(), mode, Time.frameCount));
                        var nestedStarts = 0;
                        Assert.IsFalse(AudioKit.PlaySound(actual, options, onStarted: (_, __) => nestedStarts++).IsValid);
                        Assert.AreEqual(0, nestedStarts, "Same-clip reentry must be rejected by the committed record even with a free capacity slot.");
                        Assert.AreEqual(1, AudioKit.SoundCount);
                    }, (_, __) => finishes++);
                    returned = true;
                    Assert.AreEqual(observed, handle);
                    Assert.AreEqual(afterSelection, Random.state);
                    handle.Stop();
                    yield return null;
                }
            }
            Assert.AreEqual(10, starts);
            Assert.AreEqual(0, finishes);
        }

        [UnityTest]
        public IEnumerator InvalidDisabledDeduplicatedAndCapacityRejectedRequestsNeverNotify()
        {
            var starts = 0;
            var finishes = 0;
            Action<AudioClip, AudioHandle> started = (_, __) => starts++;
            Action<AudioClip, AudioHandle> finished = (_, __) => finishes++;
            Assert.IsFalse(AudioKit.PlaySound(null, onStarted: started, onFinished: finished).IsValid);
            Assert.IsFalse(AudioKit.PlaySoundAt(null, Vector3.zero, onStarted: started, onFinished: finished).IsValid);
            Assert.IsFalse(AudioKit.PlaySoundFollowing(mClip, null, onStarted: started, onFinished: finished).IsValid);
            Assert.IsFalse(AudioKit.PlayRandomSound(new AudioClip[] { null }, onStarted: started, onFinished: finished).IsValid);
            Assert.IsFalse(AudioKit.PlayWeightedRandomSound(new[] { new AudioWeightedClip(null) }, onStarted: started, onFinished: finished).IsValid);
            AudioKit.Settings.IsSoundOn.Value = false;
            for (var entry = 0; entry < 5; entry++) Assert.IsFalse(PlayEntry(entry, Loop(), started, finished).IsValid);
            Assert.IsFalse(AudioManager.HasInstance);
            AudioKit.Settings.IsSoundOn.Value = true;
            AudioKit.SoundConcurrency = Concurrency(1);
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames;
            var admitted = AudioKit.PlaySound(mClip, Loop());
            admitted.Pause();
            Assert.IsFalse(AudioKit.PlaySound(mClip, Loop(), finished, started).IsValid);
            Assert.IsFalse(AudioKit.PlaySound(mOther, Loop(), finished, started).IsValid);
            Assert.IsTrue(admitted.IsPaused);
            Assert.AreEqual(1, AudioKit.SoundCount);
            Assert.AreEqual(0, starts);
            Assert.AreEqual(0, finishes);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseResumeStartsOnlyOnceAndNaturalCompletionReleasesCapacityWithOriginalDedupSemantics()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames })
            {
                AudioKit.Shutdown();
                ConfigureSettings();
                AudioKit.PlaySoundMode = mode;
                AudioKit.SoundConcurrency = Concurrency(1);
                var starts = 0;
                var finishes = 0;
                var observed = default(AudioHandle);
                var handle = AudioKit.PlaySound(mShort, onStarted: (clip, callbackHandle) =>
                {
                    starts++;
                    observed = callbackHandle;
                    Assert.AreSame(mShort, clip);
                    Assert.IsTrue(callbackHandle.Voice.Source.isPlaying);
                    callbackHandle.Pause();
                }, onFinished: (clip, callbackHandle) =>
                {
                    finishes++;
                    Assert.AreSame(mShort, clip);
                    Assert.AreEqual(observed, callbackHandle);
                    Assert.IsTrue(callbackHandle.IsValid);
                });
                Assert.AreEqual(handle, observed);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.IsTrue(handle.IsPaused);
                Assert.AreEqual(1, AudioKit.SoundCount);
                Assert.AreEqual(0, finishes);
                Assert.AreEqual(1, AudioKit.GetSoundCount(mShort));
                handle.Resume();
                var host = AudioManager.Instance;
                host.enabled = false;
                try
                {
                    var deadline = Time.realtimeSinceStartup + 3f;
                    while (handle.Voice.Source.isPlaying && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsFalse(handle.Voice.Source.isPlaying, "Wait for real native completion without running host Update.");
                    Assert.IsTrue(handle.IsValid);
                    Assert.AreEqual(1, AudioKit.SoundCount);
                    Assert.IsFalse(AudioKit.PlaySound(mOther, Loop(), (_, __) => finishes++, (_, __) => starts++).IsValid);
                    Assert.AreEqual(1, starts);
                    Assert.AreEqual(0, finishes, "Admission must not proactively CheckFinished to reclaim capacity and run user code.");
                }
                finally { if (host) host.enabled = true; }
                yield return WaitForEnd(handle);
                Assert.AreEqual(1, starts);
                Assert.AreEqual(1, finishes);
                Assert.AreEqual(0, AudioKit.SoundCount);
                Assert.AreEqual(0, AudioKit.GetSoundCount(mShort));
                var replay = AudioKit.PlaySound(mShort, Loop());
                Assert.AreEqual(mode == AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames, replay.IsValid);
                replay.Stop();
                Assert.IsTrue(AudioKit.PlaySound(mOther, Loop()).IsValid, "Natural completion releases capacity even when the global clip record remains.");
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulStartedStopStopAllAndSoundOffKeepTheirCommittedRecord()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                for (var stopMode = 0; stopMode < 3; stopMode++)
                {
                    AudioKit.Shutdown();
                    ConfigureSettings();
                    AudioKit.PlaySoundMode = mode;
                    AudioKit.SoundConcurrency = Concurrency(2);
                    var options = Loop();
                    options.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
                    var other = AudioKit.PlaySound(mOther, options);
                    var finished = 0;
                    var observed = default(AudioHandle);
                    var handle = AudioKit.PlaySound(mClip, Loop(), (_, __) => finished++, (clip, callbackHandle) =>
                    {
                        observed = callbackHandle;
                        Assert.AreSame(mClip, clip);
                        Assert.AreEqual(2, AudioKit.SoundCount);
                        if (stopMode == 0) callbackHandle.Stop();
                        else if (stopMode == 1) AudioKit.StopAllSound();
                        else AudioKit.Settings.IsSoundOn.Value = false;
                        Assert.IsFalse(callbackHandle.IsValid);
                    });
                    Assert.AreEqual(observed, handle, "Return the original lease, not any later pool occupant.");
                    Assert.IsFalse(handle.IsValid);
                    Assert.AreEqual(stopMode == 0, other.IsValid);
                    Assert.AreEqual(stopMode == 0 ? 1 : 0, AudioKit.SoundCount);
                    Assert.IsFalse(AudioKit.Channels.CanPlay(mClip.GetInstanceID(), mode, Time.frameCount));
                    AudioKit.Settings.IsSoundOn.Value = true;
                    Assert.IsFalse(AudioKit.PlaySound(mClip, Loop()).IsValid);
                    Assert.AreEqual(0, finished);
                    other.Stop();
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartedCanChangeCapacityAndTrimItselfOrOlderRequestsWithoutNaturalCompletion()
        {
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
            var unrecorded = Loop();
            unrecorded.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            var first = AudioKit.PlaySound(mClip, unrecorded);
            first.Pause();
            var finished = 0;
            var observed = default(AudioHandle);
            var trimmed = AudioKit.PlaySound(mOther, Loop(), (_, __) => finished++, (_, callbackHandle) =>
            {
                observed = callbackHandle;
                Assert.AreEqual(2, AudioKit.SoundCount);
                AudioKit.SoundConcurrency = Concurrency(1);
                Assert.IsFalse(callbackHandle.IsValid);
                Assert.IsTrue(first.IsValid);
            });
            Assert.AreEqual(observed, trimmed);
            Assert.IsFalse(trimmed.IsValid);
            Assert.IsFalse(AudioKit.Channels.CanPlay(mOther.GetInstanceID(), AudioKit.PlaySoundMode, Time.frameCount));
            AudioKit.SoundConcurrency = Concurrency(0);
            var current = AudioKit.PlaySound(mClip, Loop(), (_, __) => finished++, (_, callbackHandle) =>
            {
                AudioKit.SoundConcurrency = Concurrency(1, SoundOverflowPolicy.StopOldest);
                Assert.IsFalse(first.IsValid);
                Assert.IsTrue(callbackHandle.IsPlaying);
                Assert.AreEqual(1, AudioKit.SoundCount);
            });
            Assert.IsTrue(current.IsValid);
            Assert.AreEqual(0, finished);
            yield return null;
            Assert.IsTrue(current.IsPlaying);
        }

        [UnityTest]
        public IEnumerator SuccessfulStartedShutdownDoesNotInitializeAnUnrequestedReplacementSession()
        {
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
            AudioKit.SoundConcurrency = Concurrency(1);
            var channels = AudioKit.Channels;
            var starts = 0;
            var finishes = 0;
            var observed = default(AudioHandle);
            var handle = AudioKit.PlaySound(mClip, Loop(), (_, __) => finishes++, (_, callbackHandle) =>
            {
                starts++;
                observed = callbackHandle;
                AudioKit.Shutdown();
                Assert.IsFalse(callbackHandle.IsValid);
                Assert.IsNull(AudioKit.CurrentSettings);
            });
            Assert.AreEqual(observed, handle);
            Assert.IsFalse(handle.IsValid);
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
            Assert.AreEqual(0, AudioKit.SoundCount);
            Assert.IsFalse(channels.CanPlay(mClip.GetInstanceID(), AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, Time.frameCount));
            yield return null;
            Assert.IsNull(AudioKit.CurrentSettings);
            Assert.IsFalse(AudioManager.HasInstance);
            Assert.AreEqual(1, starts);
            Assert.AreEqual(0, finishes);
            Assert.AreEqual(default(AudioSoundConcurrency), AudioKit.SoundConcurrency);
            Assert.IsFalse(AudioManager.HasInstance);
        }

        [UnityTest]
        public IEnumerator ThrowingStartedReleasesOnlyTheFailedRequestAndRollsBackItsRecord()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                AudioKit.Shutdown();
                ConfigureSettings();
                AudioKit.PlaySoundMode = mode;
                AudioKit.SoundConcurrency = Concurrency(2);
                var other = AudioKit.PlaySound(mOther, Loop());
                var failed = default(AudioHandle);
                var failure = new InvalidOperationException("expected sound start failure");
                var finished = 0;
                var actual = Assert.Throws<InvalidOperationException>(() => AudioKit.PlaySound(mClip, Loop(), (_, __) => finished++, (_, callbackHandle) =>
                {
                    failed = callbackHandle;
                    Assert.IsTrue(callbackHandle.Voice.Source.isPlaying);
                    Assert.AreEqual(2, AudioKit.SoundCount);
                    throw failure;
                }));
                Assert.AreSame(failure, actual);
                Assert.IsFalse(failed.IsValid);
                Assert.IsTrue(other.IsValid);
                Assert.AreEqual(1, AudioKit.SoundCount);
                Assert.AreEqual(0, AudioKit.GetSoundCount(mClip));
                Assert.AreEqual(1, AudioKit.GetSoundCount(mOther));
                Assert.AreEqual(2L, AudioKit.SoundStats.AdmittedCount);
                Assert.AreEqual(1L, AudioKit.SoundStats.StartFailureCount);
                Assert.AreEqual(2, AudioKit.SoundStats.PeakActiveCount);
                Assert.IsTrue(AudioKit.Channels.CanPlay(mClip.GetInstanceID(), mode, Time.frameCount));
                Assert.IsFalse(AudioKit.Channels.CanPlay(mOther.GetInstanceID(), mode, Time.frameCount));
                var retry = AudioKit.PlaySound(mClip, Loop());
                Assert.IsTrue(retry.IsValid);
                Assert.AreSame(failed.Voice, retry.Voice);
                failed.Stop();
                Assert.IsTrue(retry.IsValid);
                Assert.AreEqual(0, finished);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThrowAfterStopOldestReentryCannotStopTheReusedVoiceOrEraseTheNestedRecord()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                AudioKit.Shutdown();
                ConfigureSettings();
                AudioKit.PlaySoundMode = mode;
                AudioKit.SoundConcurrency = Concurrency(1, SoundOverflowPolicy.StopOldest);
                var nested = default(AudioHandle);
                var old = default(AudioHandle);
                var failure = new InvalidOperationException("expected reused sound start failure");
                var finished = 0;
                Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => AudioKit.PlaySound(mClip, Loop(), (_, __) => finished++, (_, callbackHandle) =>
                {
                    old = callbackHandle;
                    nested = AudioKit.PlaySound(mOther, Loop(), (_, __) => finished++);
                    Assert.AreSame(callbackHandle.Voice, nested.Voice);
                    Assert.AreNotEqual(callbackHandle.Generation, nested.Generation);
                    Assert.IsFalse(callbackHandle.IsValid);
                    throw failure;
                })));
                Assert.IsFalse(old.IsValid);
                Assert.IsTrue(nested.IsPlaying);
                Assert.AreSame(mOther, nested.Voice.Source.clip);
                Assert.AreEqual(1, AudioKit.SoundCount);
                Assert.AreEqual(0, AudioKit.GetSoundCount(mClip));
                Assert.AreEqual(1, AudioKit.GetSoundCount(mOther));
                Assert.AreEqual(mOther.GetInstanceID(), nested.Voice.ClipId);
                Assert.AreEqual(2L, AudioKit.SoundStats.AdmittedCount);
                Assert.AreEqual(1L, AudioKit.SoundStats.StartFailureCount);
                Assert.AreEqual(1L, AudioKit.SoundStats.StolenCount);
                Assert.IsTrue(AudioKit.Channels.CanPlay(mClip.GetInstanceID(), mode, Time.frameCount));
                Assert.IsFalse(AudioKit.Channels.CanPlay(mOther.GetInstanceID(), mode, Time.frameCount));
                old.Stop();
                yield return null;
                Assert.IsTrue(nested.IsPlaying);
                Assert.AreEqual(0, finished);
            }
        }

        [UnityTest]
        public IEnumerator ShutdownReentryThenThrowRollsBackTheOldDeduplicatorNotTheNewSameClipSession()
        {
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
            AudioKit.SoundConcurrency = Concurrency(1, SoundOverflowPolicy.StopOldest);
            var oldChannels = AudioKit.Channels;
            var nested = default(AudioHandle);
            var old = default(AudioHandle);
            var failure = new InvalidOperationException("expected replaced session start failure");
            Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() => AudioKit.PlaySound(mClip, Loop(), onStarted: (_, callbackHandle) =>
            {
                old = callbackHandle;
                AudioKit.Shutdown();
                ConfigureSettings();
                AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames;
                AudioKit.SoundConcurrency = Concurrency(2);
                nested = AudioKit.PlaySound(mClip, Loop());
                throw failure;
            })));
            Assert.IsFalse(old.IsValid);
            Assert.IsTrue(nested.IsValid);
            Assert.AreNotSame(oldChannels, AudioKit.Channels);
            Assert.IsTrue(oldChannels.CanPlay(mClip.GetInstanceID(), AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, Time.frameCount));
            Assert.IsFalse(AudioKit.Channels.CanPlay(mClip.GetInstanceID(), AudioKit.PlaySoundMode, Time.frameCount));
            Assert.AreEqual(1, AudioKit.SoundCount);
            Assert.AreEqual(2, AudioKit.SoundConcurrency.Limit);
            Assert.AreEqual(1, AudioKit.GetSoundCount(mClip));
            Assert.AreEqual(1L, AudioKit.SoundStats.AdmittedCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.StartFailureCount);
            Assert.AreEqual(1, AudioKit.SoundStats.OwnedSourceCount);
            old.Stop();
            yield return null;
            Assert.IsTrue(nested.IsPlaying);
            Assert.AreEqual(1L, AudioKit.SoundStats.AdmittedCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.StartFailureCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.SourceReleasedCount, "Deferred destruction of the old host cannot mutate the new session.");
        }

        [UnityTest]
        public IEnumerator FollowTargetLossAndSoundOffReleasePausedMutedLoopingCapacityWithoutFinishing()
        {
            AudioKit.SoundConcurrency = Concurrency(1);
            AudioKit.Settings.IsSoundMuted.Value = true;
            var starts = 0;
            var finishes = 0;
            var first = AudioKit.PlaySoundFollowing(mClip, mTarget, Loop(), (_, __) => finishes++, (_, __) => starts++);
            first.Pause();
            Assert.IsTrue(first.Voice.Source.mute);
            Assert.IsTrue(first.Voice.Source.loop);
            Assert.IsFalse(AudioKit.PlaySound(mOther, Loop()).IsValid);
            mTarget.gameObject.SetActive(false);
            yield return null;
            yield return null;
            Assert.IsFalse(first.IsValid);
            Assert.AreEqual(0, AudioKit.SoundCount);
            mTarget.gameObject.SetActive(true);
            var following = AudioKit.PlaySoundFollowing(mOther, mTarget, Loop(), (_, __) => finishes++, (_, __) => starts++);
            Assert.IsTrue(following.IsValid);
            Object.Destroy(mTarget.gameObject);
            yield return null;
            yield return null;
            Assert.IsFalse(following.IsValid);
            Assert.AreEqual(0, AudioKit.SoundCount);
            var fixedSound = AudioKit.PlaySoundAt(mClip, Vector3.one, Loop(), (_, __) => finishes++, (_, __) => starts++);
            AudioKit.Settings.IsSoundOn.Value = false;
            Assert.IsFalse(fixedSound.IsValid);
            Assert.AreEqual(0, AudioKit.SoundCount);
            AudioKit.Settings.IsSoundOn.Value = true;
            var current = AudioKit.PlaySoundAt(mOther, Vector3.one, onFinished: (_, __) => finishes++, onStarted: (_, __) => starts++);
            yield return null;
            Assert.IsTrue(current.IsPlaying);
            Assert.AreEqual(1, AudioKit.SoundCount);
            Assert.AreEqual(4, starts);
            Assert.AreEqual(0, finishes);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator InheritedExplicitAndDirectMixerRoutingAreResolvedBeforeEverySoundStartedEntrypoint()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/YLFramework/Tests/Fixtures/Audio/AudioMixerFixture.mixer");
            Assert.IsNotNull(mixer, "Import the existing independent mixer fixture before running this test.");
            var soundGroups = mixer.FindMatchingGroups("Sound");
            var uiGroups = mixer.FindMatchingGroups("UI");
            Assert.AreEqual(1, soundGroups.Length);
            Assert.AreEqual(1, uiGroups.Length);
            AudioKit.Settings.SoundOutputGroup.Value = soundGroups[0];
            AudioKit.SoundConcurrency = Concurrency(1);
            var routes = new[] { AudioOutputRoute.Inherit, AudioOutputRoute.ToMixer(uiGroups[0]), AudioOutputRoute.Direct };
            var expected = new[] { soundGroups[0], uiGroups[0], null };
            var starts = 0;
            for (var route = 0; route < routes.Length; route++)
            {
                var options = Loop();
                options.OutputRoute = routes[route];
                for (var entry = 0; entry < 5; entry++)
                {
                    var handle = PlayEntry(entry, options, (_, callbackHandle) =>
                    {
                        starts++;
                        Assert.IsTrue(callbackHandle.Voice.Source.isPlaying);
                        Assert.AreSame(expected[route], callbackHandle.Voice.Source.outputAudioMixerGroup);
                        Assert.AreEqual(routes[route], callbackHandle.Voice.Options.OutputRoute);
                        Assert.AreEqual(1, AudioKit.SoundCount);
                    });
                    Assert.IsTrue(handle.IsValid);
                    handle.Stop();
                }
            }
            Assert.AreEqual(15, starts);
            yield return null;
        }
#endif
    }
}
