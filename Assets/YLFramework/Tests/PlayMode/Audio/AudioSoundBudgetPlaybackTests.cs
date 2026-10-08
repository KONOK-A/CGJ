using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Profiling;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace YLFramework.Tests.Audio
{
    public class AudioSoundBudgetPlaybackTests
    {
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };
        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private readonly List<AudioClip> mBurstClips = new List<AudioClip>();
        private AudioClip mClip;
        private GameObject mListener;
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
            mClip = AudioClip.Create("budget-playback", 220500, 1, 44100, false);
            var hasListener = false;
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (!hasListener)
            {
                mListener = new GameObject("BudgetPlayback.Listener");
                mListener.AddComponent<AudioListener>();
            }
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
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown();
            if (mClip) Object.Destroy(mClip);
            foreach (var clip in mBurstClips) if (clip) Object.Destroy(clip);
            mBurstClips.Clear();
            if (mListener) Object.Destroy(mListener);
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

        private static AudioSoundOptions Loop(int priority = 0)
        {
            var options = AudioSoundOptions.Defaults;
            options.Loop = true;
            options.Priority = priority;
            return options;
        }

        [UnityTest]
        public IEnumerator BothDedupModesKeepZeroAndTenFrameBoundariesAcrossGlobalClipAndJointBudgets()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                foreach (var frames in new[] { 0, 10 })
                {
                    for (var scope = 0; scope < 3; scope++)
                    {
                        AudioKit.Shutdown();
                        ConfigureSettings();
                        AudioKit.GlobalFrameCountForIgnoreSameSound = frames;
                        AudioKit.SoundFrameCountForIgnoreSameSound = frames;
                        AudioKit.PlaySoundMode = mode;
                        AudioKit.SoundConcurrency = new AudioSoundConcurrency
                        {
                            Limit = scope == 1 ? 0 : 1,
                            DefaultClipLimit = scope == 0 ? 0 : 1,
                            OverflowPolicy = SoundOverflowPolicy.RejectNew
                        };
                        var first = AudioKit.PlaySound(mClip, Loop(10));
                        first.Pause();
                        AudioKit.Settings.IsSoundMuted.Value = true;
                        var startFrame = Time.frameCount;
                        var checks = 0;
                        while (Time.frameCount - startFrame <= frames)
                        {
                            Assert.IsFalse(AudioKit.PlaySound(mClip, Loop(int.MaxValue)).IsValid);
                            checks++;
                            Assert.IsTrue(first.IsPaused);
                            Assert.AreEqual(1, AudioKit.GetSoundCount(mClip));
                            Assert.AreEqual(0L, AudioKit.SoundStats.GlobalRejectedCount);
                            Assert.AreEqual(0L, AudioKit.SoundStats.ClipRejectedCount);
                            yield return null;
                        }
                        Assert.AreEqual(checks, AudioKit.SoundStats.DedupRejectedCount);
                        Assert.IsFalse(AudioKit.PlaySound(mClip, Loop(int.MaxValue)).IsValid, "Passing dedup does not bypass active capacity.");
                        Assert.AreEqual(scope == 0 ? 1L : 0L, AudioKit.SoundStats.GlobalRejectedCount);
                        Assert.AreEqual(scope == 0 ? 0L : 1L, AudioKit.SoundStats.ClipRejectedCount);
                        Assert.IsTrue(AudioKit.Channels.CanPlay(mClip.GetInstanceID(), mode, Time.frameCount));
                        first.Stop();
                        var retry = AudioKit.PlaySound(mClip, Loop(10));
                        Assert.IsTrue(retry.IsValid, "Capacity rejection cannot commit or extend a dedup window.");
                        AudioKit.SoundConcurrency = new AudioSoundConcurrency
                        {
                            Limit = scope == 1 ? 0 : 1,
                            DefaultClipLimit = scope == 0 ? 0 : 1,
                            OverflowPolicy = SoundOverflowPolicy.StopLowestPriority
                        };
                        Assert.IsFalse(AudioKit.PlaySound(mClip, Loop(int.MaxValue)).IsValid);
                        Assert.IsTrue(retry.IsValid, "Higher priority still cannot steal through same-frame dedup.");
                        Assert.AreEqual(0L, AudioKit.SoundStats.StolenCount);
                        Assert.AreEqual(0L, AudioKit.SoundStats.PriorityRejectedCount);
                        retry.Stop();
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryOneOverridesOnlyDedupAndNeverEitherCapacityBudget()
        {
            foreach (var mode in new[] { AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames, AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames })
            {
                AudioKit.Shutdown();
                ConfigureSettings();
                AudioKit.PlaySoundMode = mode;
                AudioKit.SoundConcurrency = new AudioSoundConcurrency { Limit = 2, DefaultClipLimit = 2 };
                var first = AudioKit.PlaySound(mClip, Loop());
                var options = Loop(int.MaxValue);
                options.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
                var second = AudioKit.PlaySound(mClip, options);
                Assert.IsTrue(first.IsValid);
                Assert.IsTrue(second.IsValid);
                Assert.IsFalse(AudioKit.PlaySound(mClip, options).IsValid);
                Assert.AreEqual(2, AudioKit.GetSoundCount(mClip));
                Assert.AreEqual(1L, AudioKit.SoundStats.ClipRejectedCount);
                Assert.AreEqual(0L, AudioKit.SoundStats.GlobalRejectedCount, "Joint rejection has exactly one clip-first reason.");
                first.Stop();
                Assert.IsTrue(AudioKit.PlaySound(mClip, options).IsValid);
                Assert.IsFalse(AudioKit.PlaySound(mClip, Loop(int.MaxValue)).IsValid, "EveryOne does not clear an earlier committed record.");
                Assert.AreEqual(1L, AudioKit.SoundStats.DedupRejectedCount);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator StealingRefreshesClipCountAfterSourceDisableStopsAnotherRequest()
        {
            foreach (var clipLimit in new[] { 0, 2 })
            {
                AudioKit.SoundConcurrency = new AudioSoundConcurrency
                {
                    Limit = 2, DefaultClipLimit = clipLimit, OverflowPolicy = SoundOverflowPolicy.StopOldest
                };
                var first = AudioKit.PlaySound(mClip, Loop());
                var second = AudioKit.PlaySound(mClip, Loop());
                var relay = first.Voice.Source.gameObject.AddComponent<AudioBudgetStopOnDisable>();
                relay.Other = second;
                var replacement = AudioKit.PlaySound(mClip, Loop());
                Assert.AreEqual(1, relay.Calls);
                Assert.IsFalse(first.IsValid);
                Assert.IsFalse(second.IsValid);
                Assert.IsTrue(replacement.IsPlaying);
                Assert.AreEqual(1, AudioKit.SoundCount);
                Assert.AreEqual(1, AudioKit.GetSoundCount(mClip));
                replacement.Stop();
                Assert.AreEqual(0, AudioKit.SoundCount);
                Assert.AreEqual(0, AudioKit.GetSoundCount(mClip));
                Object.Destroy(relay);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PrewarmedTwentyFourSourceBurstStopsAndReusesAllVoicesAndSourcesInTheNextBurst()
        {
            AudioKit.SoundCacheCapacity = 24;
            AudioKit.SoundConcurrency = new AudioSoundConcurrency { Limit = 24 };
            var clips = new AudioClip[24];
            clips[0] = mClip;
            for (var i = 1; i < clips.Length; i++)
            {
                clips[i] = AudioClip.Create("budget-burst-" + i, 44100, 1, 44100, false);
                mBurstClips.Add(clips[i]);
            }
            var random = Random.state;
            const ProfilerRecorderOptions recording = ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
            long calibrationAllocations, prewarmAllocations, firstBurstAllocations, steadyBurstAllocations;
            bool validRecorder;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, recording))
            {
                validRecorder = recorder.Valid;
                var probe = new byte[4096];
                GC.KeepAlive(probe);
                recorder.Stop();
                calibrationAllocations = recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
            }
            Assert.IsTrue(validRecorder);
            Assert.Greater(calibrationAllocations, 0, "Verify the allocation recorder with a known allocation before accepting zero samples.");
            int added;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, recording))
            {
                added = AudioKit.PrewarmSounds(24);
                recorder.Stop();
                prewarmAllocations = recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
            }
            Assert.AreEqual(24, added);
            Assert.AreEqual(random, Random.state);
            var handles = new AudioHandle[24];
            var voices = new HashSet<AudioVoice>();
            var sourceIds = new HashSet<int>();
            foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>(true))
            {
                Assert.IsFalse(source.gameObject.activeSelf);
                Assert.IsNull(source.clip);
                Assert.IsFalse(source.isPlaying);
                Assert.IsFalse(source.playOnAwake);
                sourceIds.Add(source.GetInstanceID());
            }
            Assert.AreEqual(24, sourceIds.Count);
            var options = Loop();
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, recording))
            {
                for (var i = 0; i < handles.Length; i++) handles[i] = AudioKit.PlaySound(clips[i], options);
                recorder.Stop();
                firstBurstAllocations = recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
            }
            foreach (var handle in handles)
            {
                Assert.IsTrue(handle.IsPlaying);
                Assert.IsTrue(voices.Add(handle.Voice));
                Assert.IsTrue(sourceIds.Contains(handle.Voice.Source.GetInstanceID()));
            }
            Assert.AreEqual(24, AudioKit.SoundStats.ActiveCount);
            foreach (var clip in clips) Assert.AreEqual(1, AudioKit.GetSoundCount(clip));
            Assert.AreEqual(24, AudioKit.SoundStats.PeakActiveCount);
            Assert.AreEqual(0, AudioKit.SoundStats.CachedCount);
            Assert.AreEqual(24L, AudioKit.SoundStats.CacheHitCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.CacheMissCount);
            Assert.AreEqual(24L, AudioKit.SoundStats.SourceCreatedCount);
            yield return null;
            AudioKit.StopAllSound();
            foreach (var handle in handles) Assert.IsFalse(handle.IsValid);
            Assert.AreEqual(24, AudioKit.SoundStats.CachedCount);
            Assert.AreEqual(24, AudioKit.SoundStats.OwnedSourceCount);
            foreach (var clip in clips) Assert.AreEqual(0, AudioKit.GetSoundCount(clip));
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, recording))
            {
                for (var i = 0; i < handles.Length; i++) handles[i] = AudioKit.PlaySound(clips[i], options);
                recorder.Stop();
                steadyBurstAllocations = recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;
            }
            var reusedVoices = new HashSet<AudioVoice>();
            var reusedSources = new HashSet<int>();
            foreach (var handle in handles)
            {
                Assert.IsTrue(handle.IsPlaying);
                reusedVoices.Add(handle.Voice);
                reusedSources.Add(handle.Voice.Source.GetInstanceID());
            }
            CollectionAssert.AreEquivalent(voices, reusedVoices);
            CollectionAssert.AreEquivalent(sourceIds, reusedSources);
            Assert.AreEqual(48L, AudioKit.SoundStats.AdmittedCount);
            Assert.AreEqual(48L, AudioKit.SoundStats.CacheHitCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.CacheMissCount);
            Assert.AreEqual(24L, AudioKit.SoundStats.SourceCreatedCount);
            Assert.AreEqual(0L, AudioKit.SoundStats.SourceReleasedCount);
            TestContext.WriteLine("Current-thread GC.Alloc samples: calibration={0}, prewarm={1}, first 24-request burst={2}, steady 24-request burst={3}.",
                calibrationAllocations, prewarmAllocations, firstBurstAllocations, steadyBurstAllocations);
            Debug.Log("Audio budget benchmark (current-thread GC.Alloc samples): calibration=" + calibrationAllocations +
                "; prewarm (sources + containers)=" + prewarmAllocations + "; first 24-request burst=" + firstBurstAllocations +
                "; steady 24-request burst=" + steadyBurstAllocations + ".");
            yield return null;
            foreach (var handle in handles) Assert.IsTrue(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator CacheConfigurationDoesNotInitializeAnAdoptedSceneHostBeforeExplicitPrewarm()
        {
            var root = new GameObject("BudgetPlayback.AdoptedHost");
            try
            {
                var host = root.AddComponent<AudioManager>();
                AudioKit.SoundCacheCapacity = 0;
                AudioKit.SoundCacheCapacity = 2;
                Assert.AreSame(host, AudioManager.TryGetInstance(out var cached) ? cached : null);
                Assert.IsNull(host.MusicPlayback, "Cache configuration must not initialize a lazily adopted scene host.");
                Assert.AreEqual(default(AudioSoundStats), AudioKit.SoundStats);
                Assert.AreEqual(2, AudioKit.PrewarmSounds(2));
                Assert.AreSame(host, AudioManager.Instance);
                Assert.AreEqual(2, AudioKit.SoundStats.OwnedSourceCount);
                Assert.AreEqual(0, AudioKit.SoundCount);
                AudioKit.Shutdown();
                yield return null;
                Assert.IsTrue(root, "The borrowed host GameObject is not owned by AudioKit.");
                Assert.AreEqual(0, root.transform.childCount);
            }
            finally { if (root) Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator LiveCacheShrinkReleasesOnlyIdleOwnershipThenDestroysSourcesAndTheirGameObjects()
        {
            AudioKit.PrewarmSounds(10);
            var roots = new List<GameObject>();
            foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>(true)) roots.Add(source.gameObject);
            var playing = AudioKit.PlaySound(mClip, Loop());
            AudioKit.SoundCacheCapacity = 2;
            Assert.IsTrue(playing.IsValid);
            Assert.AreEqual(1, AudioKit.SoundCount);
            Assert.AreEqual(2, AudioKit.SoundStats.CachedCount);
            Assert.AreEqual(3, AudioKit.SoundStats.OwnedSourceCount);
            Assert.AreEqual(7L, AudioKit.SoundStats.SourceReleasedCount, "Release counts requests; native destruction may be deferred.");
            yield return null;
            var alive = 0;
            foreach (var root in roots) if (root) alive++;
            Assert.AreEqual(3, alive);
            AudioKit.SoundCacheCapacity = 0;
            Assert.IsTrue(playing.IsValid);
            Assert.AreEqual(1, AudioKit.SoundStats.OwnedSourceCount);
            playing.Stop();
            Assert.AreEqual(0, AudioKit.SoundStats.OwnedSourceCount);
            Assert.AreEqual(0, AudioKit.SoundStats.CachedCount);
            Assert.AreEqual(10L, AudioKit.SoundStats.SourceReleasedCount);
            yield return null;
            foreach (var root in roots) Assert.IsFalse(root, "Released Sound sources must not leave orphan roots.");
            Assert.AreEqual(0, AudioManager.Instance.GetComponentsInChildren<AudioSource>(true).Length);
        }
    }
}
