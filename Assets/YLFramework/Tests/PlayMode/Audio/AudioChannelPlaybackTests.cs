using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioChannelPlaybackTests
    {
        private AudioClip mClip, mShortClip;
        private GameObject mExternal, mListener;
        private float mTimeScale;
        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };
        private sealed class Resource : IDisposable
        {
            internal int Releases;
            public void Dispose() => Releases++;
        }
        [SetUp]
        public void SetUp()
        {
            AudioKit.Shutdown();
            mTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            for (var i = 0; i < 6; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++)
            {
                mSwitches[i] = PlayerPrefs.GetInt(Keys[i]);
                mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]);
            }
            var settings = AudioKit.Settings;
            settings.IsSoundOn.SetValueWithoutCallback(true); settings.IsMusicOn.SetValueWithoutCallback(true); settings.IsVoiceOn.SetValueWithoutCallback(true);
            settings.SoundVolume.SetValueWithoutCallback(1f); settings.MusicVolume.SetValueWithoutCallback(1f); settings.VoiceVolume.SetValueWithoutCallback(1f);
            mClip = AudioClip.Create("channel-loop", 44100 * 4, 1, 44100, false);
            mShortClip = AudioClip.Create("channel-short", 2205, 1, 44100, false);
            mExternal = new GameObject("caller-source");
            if (Object.FindObjectsOfType<AudioListener>().Length == 0)
            {
                mListener = new GameObject("Channel.Listener"); mListener.AddComponent<AudioListener>();
            }
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown(); Time.timeScale = mTimeScale;
            Object.Destroy(mExternal); Object.Destroy(mClip); Object.Destroy(mShortClip);
            if (mListener) Object.Destroy(mListener);
            for (var i = 0; i < 3; i++)
            {
                if (mHadKeys[i]) PlayerPrefs.SetInt(Keys[i], mSwitches[i]); else PlayerPrefs.DeleteKey(Keys[i]);
                if (mHadKeys[i + 3]) PlayerPrefs.SetFloat(Keys[i + 3], mVolumes[i]); else PlayerPrefs.DeleteKey(Keys[i + 3]);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator IndependentLoopsAdvanceAlongsideBgmAndStopLocally()
        {
            var windChannel = AudioKit.RegisterChannel("wind");
            var rainChannel = AudioKit.RegisterChannel("rain");
            var bgm = AudioKit.PlayMusic(mClip);
            var wind = windChannel.Play(mClip, AudioSoundOptions.MusicDefaults);
            var rain = rainChannel.Play(mClip, AudioSoundOptions.MusicDefaults);
            yield return new WaitForSecondsRealtime(.12f);
            Assert.IsTrue(bgm.IsPlaying); Assert.IsTrue(wind.IsPlaying); Assert.IsTrue(rain.IsPlaying);
            Assert.Greater(bgm.Time, 0f); Assert.Greater(wind.Time, 0f); Assert.Greater(rain.Time, 0f);
            windChannel.Volume = .3f; windChannel.Muted = true;
            Assert.AreEqual(.3f, wind.Voice.Source.volume, .001f); Assert.IsTrue(wind.IsPlaying);
            Assert.AreEqual(1f, rain.Voice.Source.volume); Assert.AreEqual(1f, bgm.Voice.Source.volume);
            windChannel.Stop();
            yield return null;
            Assert.IsFalse(wind.IsValid); Assert.IsTrue(bgm.IsPlaying); Assert.IsTrue(rain.IsPlaying);
            rainChannel.Pause(); var time = rain.Time;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(time, rain.Time, .01f); Assert.IsTrue(bgm.IsPlaying);
        }

        [UnityTest]
        public IEnumerator BorrowedSourcePreservesHierarchySpatialCurvesAndCallerOverrides()
        {
            var source = mExternal.AddComponent<AudioSource>();
            var parent = new GameObject("caller-parent");
            try
            {
                mExternal.transform.SetParent(parent.transform, false); mExternal.transform.localPosition = new Vector3(2f, 3f, 4f);
                source.volume = .42f; source.pitch = .8f; source.playOnAwake = true;
                source.spatialBlend = .7f; source.dopplerLevel = 1.5f;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0f, 1f, 1f, .1f));
                var curve = source.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                var channel = AudioKit.RegisterChannel("borrowed");
                var handle = channel.PlayBorrowed(source, mClip, AudioSoundOptions.MusicDefaults);
                yield return new WaitForSecondsRealtime(.1f);
                Assert.IsTrue(source.isPlaying); Assert.Greater(handle.Time, 0f);
                Assert.Throws<InvalidOperationException>(() => channel.PlayBorrowed(source, mClip));
                source.pitch = 1.3f;
                handle.Stop();
                Assert.AreSame(parent.transform, mExternal.transform.parent);
                Assert.AreEqual(new Vector3(2f, 3f, 4f), mExternal.transform.localPosition);
                Assert.AreEqual(.42f, source.volume); Assert.AreEqual(1.3f, source.pitch);
                Assert.IsTrue(source.playOnAwake); Assert.IsTrue(source.enabled); Assert.IsTrue(mExternal.activeSelf);
                Assert.AreEqual(.7f, source.spatialBlend); Assert.AreEqual(1.5f, source.dopplerLevel);
                Assert.AreEqual(curve.keys, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).keys);
                Assert.IsNull(source.clip); Assert.IsFalse(source.isPlaying);
                AudioKit.Shutdown(); yield return null;
                Assert.IsTrue(source); Assert.IsTrue(mExternal.activeSelf);
            }
            finally { mExternal.transform.SetParent(null); Object.Destroy(parent); }
        }

        [UnityTest]
        public IEnumerator BorrowedNaturalCompletionReentryAndSourceDestructionReleaseExactlyOnce()
        {
            var source = mExternal.AddComponent<AudioSource>(); source.volume = .31f;
            var channel = AudioKit.RegisterChannel("short", AudioCategory.Sound);
            var resource = new Resource(); var finishes = 0; AudioHandle next = default;
            var old = channel.PlayWithSnapshot(mShortClip, AudioPlaybackOptions.Defaults, finished: (_, original) =>
            {
                finishes++; Assert.IsTrue(original.IsValid); Assert.AreEqual(0, resource.Releases);
                original.Stop();
                next = channel.PlayBorrowed(source, mClip, AudioSoundOptions.MusicDefaults);
            }, resource: new AudioResourceLease(resource), source: source, borrowed: true);
            var deadline = Time.realtimeSinceStartup + 2f;
            while (old.IsValid && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, finishes); Assert.AreEqual(1, resource.Releases);
            Assert.IsTrue(next.IsPlaying); old.Stop(); Assert.IsTrue(next.IsPlaying);
            Object.Destroy(source); yield return null; yield return null;
            Assert.IsFalse(next.IsValid); Assert.AreEqual(0, channel.RequestCount); Assert.AreEqual(0, AudioKit.SoundCount);
            Assert.IsTrue(mExternal.activeSelf); AudioKit.Shutdown(); Assert.AreEqual(1, resource.Releases);
        }

        [UnityTest]
        public IEnumerator OptInAutomaticPausePreservesManualPauseAndDefaultBgmAtZeroTimeScale()
        {
            var listenerPause = AudioListener.pause;
            var bgm = AudioKit.PlayMusic(mClip);
            var channel = AudioKit.RegisterChannel("auto");
            var options = AudioSoundOptions.MusicDefaults; options.AutoPauseWhenTimeScaleZero = true;
            var automatic = channel.Play(mClip, options);
            yield return new WaitForSecondsRealtime(.08f);
            Time.timeScale = 0f; yield return null; yield return null;
            Assert.IsTrue(automatic.IsPaused); Assert.IsFalse(automatic.IsPlaying); Assert.IsTrue(bgm.IsPlaying);
            var frozen = automatic.Time; var bgmTime = bgm.Time;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.AreEqual(frozen, automatic.Time, .01f); Assert.Greater(bgm.Time, bgmTime);
            automatic.Pause(); Time.timeScale = 1f; yield return null; yield return null;
            Assert.IsTrue(automatic.IsPaused); Assert.IsFalse(automatic.IsPlaying);
            automatic.Resume(); yield return new WaitForSecondsRealtime(.08f);
            Assert.IsTrue(automatic.IsPlaying); Assert.Greater(automatic.Time, frozen);
            Time.timeScale = 0f;
            var starts = 0;
            var waiting = AudioKit.PlaySound(mClip, options, onStarted: (_, __) => starts++);
            Assert.IsTrue(waiting.IsPaused); Assert.AreEqual(0, starts);
            Time.timeScale = 1f; yield return null; yield return null;
            Assert.IsTrue(waiting.IsPlaying); Assert.AreEqual(1, starts);
            Assert.AreEqual(listenerPause, AudioListener.pause);
        }

        [UnityTest]
        public IEnumerator BorrowedStartedStopReplacementAndShutdownCannotRestoreOverNewLease()
        {
            var source = mExternal.AddComponent<AudioSource>(); source.volume = .35f;
            var channel = AudioKit.RegisterChannel("callback"); AudioHandle next = default;
            var old = channel.PlayBorrowed(source, mClip, onStarted: (_, handle) =>
            {
                handle.Stop(); next = channel.PlayBorrowed(source, mClip, new AudioSoundOptions { Loop = true, Volume = .6f });
            });
            Assert.IsFalse(old.IsValid); Assert.IsTrue(next.IsPlaying); Assert.AreEqual(.6f, source.volume, .001f);
            next.Stop(); Assert.AreEqual(.35f, source.volume);
            channel.PlayBorrowed(source, mClip, onStarted: (_, __) => { AudioKit.Shutdown(); source.volume = .22f; });
            yield return null;
            Assert.AreEqual(.22f, source.volume); Assert.IsTrue(mExternal.activeSelf); Assert.IsTrue(source);
        }
    }
}
