using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Actions;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace YLFramework.Tests.Audio
{
    public class AudioOptionsAllocationPlaybackTests
    {
        private const int Requests = 24;
        private const int Rounds = 4;
        private const ProfilerRecorderOptions Recording = ProfilerRecorderOptions.SumAllSamplesInFrame |
                                                          ProfilerRecorderOptions.CollectOnlyOnCurrentThread;
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };

        private enum Path
        {
            SoundDefault, SoundReused,
            MusicSimple, MusicNull, MusicReused,
            VoiceSimple, VoiceNull, VoiceReused,
            CrossfadeDefault, CrossfadeReused,
            Emitter, MusicPlayer, MusicPlayerCrossfade,
            ActionAllocateDefault, ActionAllocateReused, ActionStartDefault, ActionStartReused,
            ChannelClosedDefault, ChannelClosedReused,
            DedupDefault, DedupReused,
            GlobalBudgetDefault, GlobalBudgetReused, ClipBudgetDefault, ClipBudgetReused
        }

        private struct Measurement
        {
            public bool Valid;
            public long Events;
            public long Ticks;
        }

        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private readonly AudioHandle[] mHandles = new AudioHandle[Requests];
        private readonly PlaySoundAction[] mActions = new PlaySoundAction[Requests];
        private readonly HashSet<AudioVoice> mSoundVoices = new HashSet<AudioVoice>();
        private readonly HashSet<int> mSoundSources = new HashSet<int>();
        private readonly HashSet<PlaySoundAction> mPooledActions = new HashSet<PlaySoundAction>();
        private readonly HashSet<int> mSlotSources = new HashSet<int>();
        private AudioClip mClip;
        private GameObject mListener, mEmitterRoot, mMusicRoot;
        private AudioEmitter mEmitter;
        private AudioMusicPlayer mMusicPlayer;
        private AudioSoundOptions mOptions;
        private Action<AudioClip, AudioHandle> mStarted;
        private ActionRecycleDriver mPreviousDriver;
        private AudioHandle mBlocker;
        private Random.State mRandomState;
        private int mStarts, mEmitterStarts;
        private bool mInvalidCallback;
        private byte[] mCalibrationProbe;
        private AudioPlaybackOptions mLastSnapshot;
        private double mSnapshotChecksum;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            mRandomState = Random.state;
            mPreviousDriver = Object.FindObjectOfType<ActionRecycleDriver>(true);
            ActionQueue.Drain();
            AudioKit.Shutdown();
            for (var i = 0; i < Keys.Length; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++)
            {
                mSwitches[i] = PlayerPrefs.GetInt(Keys[i]);
                mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]);
            }
            var settings = AudioKit.Settings;
            settings.IsSoundOn.SetValueWithoutCallback(true);
            settings.IsMusicOn.SetValueWithoutCallback(true);
            settings.IsVoiceOn.SetValueWithoutCallback(true);
            settings.SoundVolume.SetValueWithoutCallback(1f);
            settings.MusicVolume.SetValueWithoutCallback(1f);
            settings.VoiceVolume.SetValueWithoutCallback(1f);
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            AudioKit.SoundCacheCapacity = Requests;
            AudioKit.SoundConcurrency = new AudioSoundConcurrency { Limit = Requests };
            mClip = AudioClip.Create("options-allocation-playback", 220500, 1, 44100, false);
            mOptions = AudioSoundOptions.Defaults;
            mStarted = OnStarted;
            var hasListener = false;
            foreach (var listener in Object.FindObjectsOfType<AudioListener>())
                if (listener.isActiveAndEnabled) { hasListener = true; break; }
            if (!hasListener)
            {
                mListener = new GameObject("OptionsAllocation.Listener");
                mListener.AddComponent<AudioListener>();
            }

            mEmitterRoot = new GameObject("OptionsAllocation.Emitter");
            mEmitterRoot.SetActive(false);
            mEmitter = mEmitterRoot.AddComponent<AudioEmitter>();
            SetField(mEmitter, "clips", new List<AudioClip> { mClip });
            SetField(mEmitter, "playOnAwake", false);
            mEmitter.Started.AddListener(OnEmitterStarted);
            mEmitterRoot.SetActive(true);
            mMusicRoot = new GameObject("OptionsAllocation.MusicPlayer");
            mMusicRoot.SetActive(false);
            mMusicPlayer = mMusicRoot.AddComponent<AudioMusicPlayer>();
            SetField(mMusicPlayer, "clip", mClip);
            SetField(mMusicPlayer, "playOnAwake", false);
            SetField(mMusicPlayer, "loop", false);
            mMusicRoot.SetActive(true);
            mSoundVoices.Clear();
            mSoundSources.Clear();
            mPooledActions.Clear();
            mSlotSources.Clear();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CleanupBatch();
            AudioKit.Shutdown();
            if (mEmitterRoot) Object.Destroy(mEmitterRoot);
            if (mMusicRoot) Object.Destroy(mMusicRoot);
            if (mListener) Object.Destroy(mListener);
            if (mClip) Object.Destroy(mClip);
            var driver = Object.FindObjectOfType<ActionRecycleDriver>(true);
            if (driver && driver != mPreviousDriver) Object.Destroy(driver.gameObject);
            mCalibrationProbe = null;
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

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private void OnStarted(AudioClip clip, AudioHandle handle)
        {
            mHandles[mStarts++] = handle;
            mInvalidCallback |= clip != mClip || !handle.IsPlaying || handle.Voice.Source.clip != mClip;
        }

        private void OnEmitterStarted() => mEmitterStarts++;

        [UnityTest]
        public IEnumerator GlobalModeValidationDoesNotBoxEnumsAfterInitialization()
        {
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            long calibrationEvents, emptyEvents, modeEvents;
            var valid = true;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                recorder.Stop();
                valid &= recorder.Valid;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                mCalibrationProbe = new byte[4096];
                recorder.Stop();
                calibrationEvents = AllocationEvents(recorder);
                valid &= recorder.Valid;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                recorder.Stop();
                emptyEvents = AllocationEvents(recorder);
                valid &= recorder.Valid;
            }
            var checksum = 0;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                for (var i = 0; i < 4096; i++)
                {
                    AudioKit.PlaySoundMode = (AudioKit.PlaySoundModes)(i % 3);
                    checksum += (int)AudioKit.PlaySoundMode;
                }
                recorder.Stop();
                modeEvents = AllocationEvents(recorder);
                valid &= recorder.Valid;
            }
            Assert.IsTrue(valid);
            Assert.Greater(calibrationEvents, 0);
            Assert.AreEqual(0, emptyEvents);
            Assert.AreEqual(4095, checksum);
            Assert.AreEqual(0, modeEvents, "Valid global mode changes must not allocate enum boxes.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotDefaultsCaptureValidationAndVoiceValueStorageAllocateNoManagedEvents()
        {
            mOptions.Volume = .5f;
            mOptions.Pitch = 1.5f;
            mOptions.AutoFade = new AudioAutoFade(.25f, .5f);
            var handle = AudioKit.PlayVoiceWithOptions(mClip, mOptions);
            handle.Pause();
            var voice = handle.Voice;
            mOptions.Priority = -7;
            mOptions.PlaySoundMode = (AudioKit.PlaySoundModes)999;
            Enum.IsDefined(typeof(AudioCrossfadeCurve), AudioCrossfadeCurve.EqualPower);
            RunSnapshotStages(voice, 1);
            long calibrationEvents, emptyEvents, snapshotEvents, enumEvents;
            var validRecorders = true;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                recorder.Stop();
                validRecorders &= recorder.Valid;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                mCalibrationProbe = new byte[4096];
                recorder.Stop();
                calibrationEvents = AllocationEvents(recorder);
                validRecorders &= recorder.Valid;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                recorder.Stop();
                emptyEvents = AllocationEvents(recorder);
                validRecorders &= recorder.Valid;
            }
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                RunSnapshotStages(voice, 4096);
                recorder.Stop();
                snapshotEvents = AllocationEvents(recorder);
                validRecorders &= recorder.Valid;
            }
            var enumMatches = 0;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                for (var i = 0; i < Requests; i++)
                    if (Enum.IsDefined(typeof(AudioCrossfadeCurve), AudioCrossfadeCurve.EqualPower)) enumMatches++;
                recorder.Stop();
                enumEvents = AllocationEvents(recorder);
                validRecorders &= recorder.Valid;
            }
            Assert.IsTrue(validRecorders);
            Assert.AreEqual(Requests, enumMatches);
            Assert.AreEqual(4096, mCalibrationProbe.Length);
            Assert.Greater(calibrationEvents, 0);
            Assert.AreEqual(0, emptyEvents);
            Assert.AreEqual(0, snapshotEvents, "Default values, Capture, normalization, validation and request value storage must not allocate.");
            Assert.Greater(mSnapshotChecksum, 0d);
            Assert.AreEqual(.5f, mLastSnapshot.Volume);
            Assert.AreEqual(1.5f, voice.Options.Pitch);
            Assert.AreEqual(.5f, voice.Options.Volume);
            Assert.AreEqual(-7, mOptions.Priority, "Normalization must not modify the public class.");
            UnityEngine.Debug.Log("Audio snapshot strict benchmark: iterations=4096; calibration events=" + calibrationEvents +
                                  "; empty events=" + emptyEvents + "; snapshot/storage events=" + snapshotEvents +
                                  "; isolated Enum.IsDefined events/24 calls=" + enumEvents + ".");
            handle.Stop();
            Assert.AreEqual(default(AudioPlaybackOptions), voice.Options);
            yield return null;
        }

        private void RunSnapshotStages(AudioVoice voice, int iterations)
        {
            double checksum = 0;
            for (var i = 0; i < iterations; i++)
            {
                var defaults = AudioPlaybackOptions.Defaults;
                var music = AudioPlaybackOptions.MusicDefaults;
                var omitted = AudioPlaybackOptions.Capture(null, true);
                defaults.Validate(true);
                music.Validate(false);
                omitted.Validate(false);
                var captured = AudioPlaybackOptions.Capture(mOptions);
                captured.Priority = 0;
                captured.PlaySoundMode = null;
                captured.Validate(true);
                captured.ValidateClip(mClip);
                captured.AutoFade.Normalize(5d, captured.Loop, out var fadeIn, out var fadeOut);
                voice.SetVolumeScale(captured.Volume);
                voice.SetPitch(captured.Pitch);
                var stored = voice.Options;
                mLastSnapshot = captured;
                checksum += stored.Volume + stored.Pitch + defaults.MaxDistance +
                            (music.Loop && omitted.Loop ? 1d : 0d) + fadeIn + fadeOut;
                stored.Pitch = 3f;
            }
            mSnapshotChecksum = checksum;
        }

        [UnityTest]
        public IEnumerator WarmedPlaybackEntryPointsReportCurrentThreadAllocationEvents()
        {
            // Warm instrumentation itself before testing its empty interval.
            Stopwatch.GetTimestamp();
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                recorder.Stop();
                AllocationEvents(recorder);
            }
            Measurement calibration;
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                mCalibrationProbe = new byte[4096];
                recorder.Stop();
                calibration = new Measurement { Valid = recorder.Valid, Events = AllocationEvents(recorder) };
            }
            Assert.IsTrue(calibration.Valid, "GC.Alloc recorder must be available on this PlayMode runner.");
            Assert.Greater(calibration.Events, 0, "A known byte[] allocation must be detected before accepting zero events.");
            Assert.AreEqual(4096, mCalibrationProbe.Length);
            var empty = Measure(Path.SoundDefault, true);
            Assert.IsTrue(empty.Valid);
            Assert.AreEqual(0L, empty.Events, "The empty interval must not include harness allocations.");
            UnityEngine.Debug.Log("Audio options benchmark: calibration events=" + calibration.Events +
                                  "; empty events=" + empty.Events + "; requests/batch=" + Requests + "; units=events (not bytes).");
            TestContext.WriteLine("Audio options allocation baseline: Unity={0}; requests/batch={1}; rounds={2}; " +
                                  "calibration events={3}; empty events={4}; units=GC.Alloc event Count (not bytes).",
                Application.unityVersion, Requests, Rounds, calibration.Events, empty.Events);

            Assert.AreEqual(Requests, AudioKit.PrewarmSounds(Requests));
            // A full real burst warms active-list and per-clip dictionary capacity as well as sources.
            PrepareBatch(Path.SoundReused);
            RunBatch(Path.SoundReused);
            for (var i = 0; i < Requests; i++)
            {
                Assert.IsTrue(mHandles[i].IsPlaying);
                Assert.IsTrue(mSoundVoices.Add(mHandles[i].Voice));
                Assert.IsTrue(mSoundSources.Add(mHandles[i].Voice.Source.GetInstanceID()));
            }
            VerifyBatch(Path.SoundReused, false);
            CleanupBatch();
            AudioKit.PlayMusic(mClip).Stop();
            AudioKit.PlayVoice(mClip).Stop();
            AudioKit.CrossFadeMusic(mClip, 10f);
            AudioKit.CrossFadeMusic(mClip, 10f);
            foreach (var source in AudioManager.Instance.GetComponentsInChildren<AudioSource>(true))
                if (!mSoundSources.Contains(source.GetInstanceID())) mSlotSources.Add(source.GetInstanceID());
            Assert.AreEqual(3, mSlotSources.Count, "Warm both fixed Music sources and the fixed Voice source.");
            AudioKit.StopMusic();

            foreach (Path path in Enum.GetValues(typeof(Path)))
            {
                // First-use costs (component callback/list and Action pool growth) remain separate from steady playback.
                PrepareBatch(path);
                var first = Measure(path);
                VerifyBatch(path, false);
                if (IsAllocate(path))
                    for (var i = 0; i < Requests; i++) mPooledActions.Add(mActions[i]);
                CleanupBatch();
                if (IsDedup(path)) yield return null;
                PrepareBatch(path);
                RunBatch(path);
                VerifyBatch(path, true);
                CleanupBatch();
                if (IsDedup(path)) yield return null;
                TestContext.WriteLine("{0}: preparatory batch events={1} (not steady-state).", path, first.Events);
                Assert.IsTrue(first.Valid);

                for (var round = 0; round < Rounds; round++)
                {
                    PrepareBatch(path);
                    var measurement = Measure(path);
                    // All NUnit, reflection, logging, cleanup and pool draining occur after recorder.Stop().
                    Assert.IsTrue(measurement.Valid);
                    VerifyBatch(path, true);
                    var stats = AudioKit.SoundStats;
                    UnityEngine.Debug.Log("Audio options benchmark: " + path + "; round=" + (round + 1) +
                                          "; events=" + measurement.Events + "; us/request=" +
                                          (measurement.Ticks * 1000000d / Stopwatch.Frequency / Requests).ToString("F3"));
                    TestContext.WriteLine("{0}: round={1}; requests={2}; allocation events={3}; events/request={4:F3}; " +
                                          "elapsed us/request={5:F3}; started={6}; emitter started={7}; " +
                                          "admitted={8}; dedup rejected={9}; global rejected={10}; clip rejected={11}; " +
                                          "cache hits={12}; cache misses={13}; source creations={14}.",
                        path, round + 1, Requests, measurement.Events, (double)measurement.Events / Requests,
                        measurement.Ticks * 1000000d / Stopwatch.Frequency / Requests, mStarts, mEmitterStarts,
                        stats.AdmittedCount, stats.DedupRejectedCount, stats.GlobalRejectedCount,
                        stats.ClipRejectedCount, stats.CacheHitCount, stats.CacheMissCount, stats.SourceCreatedCount);
                    if (IsCrossfade(path))
                        Assert.AreEqual(0, measurement.Events, "Prewarmed Crossfade must not allocate enum boxes in the API or component.");
                    CleanupBatch();
                    if (IsDedup(path)) yield return null;
                }
            }
            TestContext.WriteLine("Action Start includes the production completion/Started closures. " +
                                  "Only the calibrated prewarmed Crossfade paths assert zero events; other paths have no blanket zero-GC or timing threshold assertion.");
            yield return null;
        }

        private static long AllocationEvents(ProfilerRecorder recorder) =>
            recorder.Count == 0 ? 0 : recorder.GetSample(0).Count;

        private Measurement Measure(Path path, bool empty = false)
        {
            using (var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GC.Alloc", 1, Recording))
            {
                var start = Stopwatch.GetTimestamp();
                if (!empty) RunBatch(path);
                var elapsed = Stopwatch.GetTimestamp() - start;
                recorder.Stop();
                return new Measurement { Valid = recorder.Valid, Events = AllocationEvents(recorder), Ticks = elapsed };
            }
        }

        private static bool IsAllocate(Path path) => path == Path.ActionAllocateDefault || path == Path.ActionAllocateReused;
        private static bool IsStart(Path path) => path == Path.ActionStartDefault || path == Path.ActionStartReused;
        private static bool IsRejected(Path path) => path >= Path.ChannelClosedDefault;
        private static bool IsDedup(Path path) => path == Path.DedupDefault || path == Path.DedupReused;
        private static bool IsSound(Path path) => path == Path.SoundDefault || path == Path.SoundReused ||
                                                path == Path.Emitter || IsStart(path);
        private static bool IsCrossfade(Path path) => path == Path.CrossfadeDefault || path == Path.CrossfadeReused ||
                                                    path == Path.MusicPlayerCrossfade;

        private void PrepareBatch(Path path)
        {
            AudioKit.Settings.IsSoundOn.Value = true;
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            AudioKit.SoundConcurrency = new AudioSoundConcurrency { Limit = Requests };
            SetField(mMusicPlayer, "crossFadeOnPlay", path == Path.MusicPlayerCrossfade);
            Array.Clear(mHandles, 0, mHandles.Length);
            if (path == Path.ChannelClosedDefault || path == Path.ChannelClosedReused)
                AudioKit.Settings.IsSoundOn.Value = false;
            else if (IsDedup(path))
            {
                AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames;
                // Stop retains the dedup record; advance a frame outside recording between same-frame batches.
                AudioKit.SoundFrameCountForIgnoreSameSound = 0;
                mBlocker = AudioKit.PlaySound(mClip, mOptions);
                Assert.IsTrue(mBlocker.IsPlaying);
            }
            else if (path == Path.GlobalBudgetDefault || path == Path.GlobalBudgetReused ||
                     path == Path.ClipBudgetDefault || path == Path.ClipBudgetReused)
            {
                var clipBudget = path == Path.ClipBudgetDefault || path == Path.ClipBudgetReused;
                AudioKit.SoundConcurrency = new AudioSoundConcurrency
                {
                    Limit = clipBudget ? Requests : 1,
                    DefaultClipLimit = clipBudget ? 1 : 0,
                    OverflowPolicy = SoundOverflowPolicy.RejectNew
                };
                mBlocker = AudioKit.PlaySound(mClip, mOptions);
                Assert.IsTrue(mBlocker.IsPlaying);
            }
            if (IsStart(path))
                for (var i = 0; i < Requests; i++)
                    mActions[i] = path == Path.ActionStartDefault
                        ? PlaySoundAction.Allocate(mClip, onStarted: mStarted)
                        : PlaySoundAction.AllocateWithOptions(mClip, mOptions, onStarted: mStarted);
            mStarts = mEmitterStarts = 0;
            mInvalidCallback = false;
            AudioKit.ResetSoundStats();
        }

        private void RunBatch(Path path)
        {
            // No harness-created delegates, containers, assertions, reflection, logs, timers or yields here.
            for (var i = 0; i < Requests; i++)
            {
                switch (path)
                {
                    case Path.SoundDefault:
                    case Path.ChannelClosedDefault:
                    case Path.DedupDefault:
                    case Path.GlobalBudgetDefault:
                    case Path.ClipBudgetDefault:
                        mHandles[i] = AudioKit.PlaySound(mClip, onStarted: mStarted);
                        break;
                    case Path.SoundReused:
                    case Path.ChannelClosedReused:
                    case Path.DedupReused:
                    case Path.GlobalBudgetReused:
                    case Path.ClipBudgetReused:
                        mHandles[i] = AudioKit.PlaySound(mClip, mOptions, onStarted: mStarted);
                        break;
                    case Path.MusicSimple:
                        mHandles[i] = AudioKit.PlayMusic(mClip, loop: false, onStarted: mStarted);
                        break;
                    case Path.MusicNull:
                        mHandles[i] = AudioKit.PlayMusicWithOptions(mClip, null, onStarted: mStarted);
                        break;
                    case Path.MusicReused:
                        mHandles[i] = AudioKit.PlayMusicWithOptions(mClip, mOptions, onStarted: mStarted);
                        break;
                    case Path.VoiceSimple:
                        mHandles[i] = AudioKit.PlayVoice(mClip, onStarted: mStarted);
                        break;
                    case Path.VoiceNull:
                        mHandles[i] = AudioKit.PlayVoiceWithOptions(mClip, null, onStarted: mStarted);
                        break;
                    case Path.VoiceReused:
                        mHandles[i] = AudioKit.PlayVoiceWithOptions(mClip, mOptions, onStarted: mStarted);
                        break;
                    case Path.CrossfadeDefault:
                        mHandles[i] = AudioKit.CrossFadeMusic(mClip, 10f, onStarted: mStarted);
                        break;
                    case Path.CrossfadeReused:
                        mHandles[i] = AudioKit.CrossFadeMusic(mClip, 10f, mOptions, onStarted: mStarted);
                        break;
                    case Path.Emitter:
                        mEmitter.Play();
                        break;
                    case Path.MusicPlayer:
                    case Path.MusicPlayerCrossfade:
                        mMusicPlayer.Play();
                        mHandles[i] = AudioKit.Music;
                        break;
                    case Path.ActionAllocateDefault:
                        mActions[i] = PlaySoundAction.Allocate(mClip, onStarted: mStarted);
                        break;
                    case Path.ActionAllocateReused:
                        mActions[i] = PlaySoundAction.AllocateWithOptions(mClip, mOptions, onStarted: mStarted);
                        break;
                    case Path.ActionStartDefault:
                    case Path.ActionStartReused:
                        mActions[i].Execute(0f);
                        break;
                }
            }
        }

        private void VerifyBatch(Path path, bool checkActionReuse)
        {
            Assert.IsFalse(mInvalidCallback, "Started must see the accepted, playing clip.");
            var stats = AudioKit.SoundStats;
            Assert.AreEqual(0L, stats.SourceCreatedCount, "Only warmed sources may be used.");
            Assert.AreEqual(0L, stats.SourceReleasedCount);
            Assert.AreEqual(0L, stats.CacheMissCount);
            Assert.AreEqual(0L, stats.StartFailureCount);
            Assert.AreEqual(0L, stats.StolenCount);
            Assert.AreEqual(Requests, stats.OwnedSourceCount);
            if (IsAllocate(path))
            {
                Assert.AreEqual(0, mStarts);
                Assert.AreEqual(0, AudioKit.SoundCount);
                var unique = new HashSet<PlaySoundAction>();
                for (var i = 0; i < Requests; i++)
                {
                    Assert.IsNotNull(mActions[i]);
                    Assert.IsFalse(mActions[i].Deinited);
                    Assert.AreEqual(ActionStatus.NotStart, mActions[i].Status);
                    Assert.IsTrue(unique.Add(mActions[i]));
                    if (checkActionReuse) Assert.IsTrue(mPooledActions.Contains(mActions[i]), "Allocate must reuse the warmed Action pool.");
                }
                return;
            }
            if (IsRejected(path))
            {
                Assert.AreEqual(0, mStarts);
                for (var i = 0; i < Requests; i++) Assert.IsFalse(mHandles[i].IsValid);
                var closed = path == Path.ChannelClosedDefault || path == Path.ChannelClosedReused;
                Assert.AreEqual(closed ? 0 : 1, AudioKit.SoundCount);
                Assert.AreEqual(0L, stats.AdmittedCount);
                Assert.AreEqual(0L, stats.CacheHitCount);
                Assert.AreEqual(path == Path.DedupDefault || path == Path.DedupReused ? Requests : 0L, stats.DedupRejectedCount);
                Assert.AreEqual(path == Path.GlobalBudgetDefault || path == Path.GlobalBudgetReused ? Requests : 0L, stats.GlobalRejectedCount);
                Assert.AreEqual(path == Path.ClipBudgetDefault || path == Path.ClipBudgetReused ? Requests : 0L, stats.ClipRejectedCount);
                if (!closed) Assert.IsTrue(mBlocker.IsPlaying);
                return;
            }
            if (path == Path.Emitter)
            {
                Assert.AreEqual(Requests, mEmitterStarts);
                var sounds = AudioKit.GetPlayingSounds();
                Assert.AreEqual(Requests, sounds.Length);
                Array.Copy(sounds, mHandles, Requests);
            }
            else if (path != Path.MusicPlayer && path != Path.MusicPlayerCrossfade)
                Assert.AreEqual(Requests, mStarts);
            if (IsSound(path))
            {
                Assert.AreEqual(Requests, stats.AdmittedCount);
                Assert.AreEqual(Requests, stats.CacheHitCount);
                Assert.AreEqual(Requests, AudioKit.GetSoundCount(mClip));
                var unique = new HashSet<AudioVoice>();
                for (var i = 0; i < Requests; i++)
                {
                    Assert.IsTrue(mHandles[i].IsPlaying);
                    Assert.AreSame(mClip, mHandles[i].Voice.Source.clip);
                    Assert.IsTrue(unique.Add(mHandles[i].Voice));
                    Assert.IsTrue(mSoundVoices.Contains(mHandles[i].Voice));
                    Assert.IsTrue(mSoundSources.Contains(mHandles[i].Voice.Source.GetInstanceID()));
                    if (IsStart(path)) Assert.AreEqual(ActionStatus.Started, mActions[i].Status);
                }
            }
            else
            {
                Assert.AreEqual(0, AudioKit.SoundCount);
                Assert.AreEqual(0L, stats.AdmittedCount);
                for (var i = 0; i < Requests; i++)
                    Assert.IsTrue(mSlotSources.Contains(mHandles[i].Voice.Source.GetInstanceID()));
                Assert.IsTrue(mHandles[Requests - 1].IsPlaying);
                Assert.AreSame(mClip, mHandles[Requests - 1].Voice.Source.clip);
                for (var i = 0; i < Requests - (IsCrossfade(path) ? 2 : 1); i++)
                    Assert.IsFalse(mHandles[i].IsValid, "Replaced slot leases must be invalid.");
                if (IsCrossfade(path))
                {
                    Assert.IsTrue(mHandles[Requests - 2].IsPlaying, "Crossfade must retain a real outgoing participant.");
                    Assert.AreNotSame(mHandles[Requests - 2].Voice.Source, mHandles[Requests - 1].Voice.Source);
                    Assert.AreEqual(0f, mHandles[Requests - 1].Voice.Source.volume, .001f);
                }
            }
        }

        private void CleanupBatch()
        {
            for (var i = 0; i < Requests; i++)
            {
                if (mActions[i] != null && !mActions[i].Deinited) mActions[i].Deinit();
                mActions[i] = null;
            }
            ActionQueue.Drain();
            if (mEmitter) mEmitter.Stop();
            if (mMusicPlayer) mMusicPlayer.Stop();
            AudioKit.StopAllSound();
            AudioKit.StopMusic();
            AudioKit.StopVoice();
            mBlocker = default;
            AudioKit.Settings.IsSoundOn.Value = true;
        }
    }
}
