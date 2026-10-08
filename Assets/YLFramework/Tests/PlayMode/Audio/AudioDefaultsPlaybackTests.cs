using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Actions;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioDefaultsPlaybackTests
    {
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };
        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private AudioClip mClip;
        private GameObject mListener;
        private bool mOwnActionSession;

        [SetUp]
        public void SetUp()
        {
            AudioKit.Shutdown();
            mOwnActionSession = false;
            for (var i = 0; i < Keys.Length; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++)
            {
                mSwitches[i] = PlayerPrefs.GetInt(Keys[i]);
                mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]);
            }
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            mClip = AudioClip.Create("defaults-playback", 441000, 1, 44100, false);
            var hasListener = false;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                hasListener |= listener.isActiveAndEnabled;
            if (!hasListener)
            {
                mListener = new GameObject("AudioDefaults.Listener");
                mListener.AddComponent<AudioListener>();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown();
            if (mOwnActionSession) ActionKit.Shutdown();
            for (var i = 0; i < 3; i++)
            {
                if (mHadKeys[i]) PlayerPrefs.SetInt(Keys[i], mSwitches[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
                if (mHadKeys[i + 3]) PlayerPrefs.SetFloat(Keys[i + 3], mVolumes[i]);
                else PlayerPrefs.DeleteKey(Keys[i + 3]);
            }
            Object.Destroy(mClip);
            if (mListener) Object.Destroy(mListener);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MinimalPlaybackAndPartialConfigurationWorkWithoutMixerOrInitialization()
        {
            Assert.IsFalse(AudioManager.HasInstance);
            var music = AudioKit.PlayMusic(mClip);
            var voice = AudioKit.PlayVoice(mClip);
            var sound = AudioKit.PlaySound(mClip);
            yield return null;
            foreach (var handle in new[] { music, voice, sound })
            {
                Assert.IsTrue(handle.IsValid);
                Assert.IsTrue(handle.IsPlaying);
                Assert.AreEqual(1f, handle.Voice.Source.volume);
                Assert.AreEqual(1f, handle.Voice.Source.pitch);
                Assert.AreEqual(0f, handle.Voice.Source.spatialBlend);
                Assert.IsNull(handle.Voice.Source.outputAudioMixerGroup);
            }
            Assert.IsTrue(music.Voice.Source.loop);
            Assert.IsFalse(voice.Voice.Source.loop);
            Assert.IsFalse(sound.Voice.Source.loop);
            var options = new AudioSoundOptions { Volume = .5f };
            var first = AudioKit.PlaySound(mClip, options);
            var second = AudioKit.PlaySound(mClip, options);
            options.Volume = 0f;
            options.Pitch = 0f;
            first.SetVolumeScale(.25f);
            first.SetPitch(2f);
            yield return null;
            Assert.AreEqual(.25f, first.Voice.Source.volume);
            Assert.AreEqual(2f, first.Voice.Source.pitch);
            Assert.AreEqual(.5f, second.Voice.Source.volume);
            Assert.AreEqual(1f, second.Voice.Source.pitch);
            Assert.AreEqual(0f, options.Volume);
            Assert.AreEqual(0f, options.Pitch);
            foreach (var key in Keys) Assert.IsFalse(PlayerPrefs.HasKey(key), "Playback must not create preferences.");
        }

        [UnityTest]
        public IEnumerator ActionKeepsAllocationSnapshotAcrossFramesAndReset()
        {
            Assert.IsFalse(ActionKit.Initialized, "Do not release another owner's Action session.");
            Assert.IsEmpty(Object.FindObjectsByType<ActionRecycleDriver>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<MonoUpdateActionExecutor>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            mOwnActionSession = true;
            var options = new AudioSoundOptions { Volume = .4f };
            var action = PlaySoundAction.AllocateWithOptions(mClip, options);
            try
            {
                options.Pitch = 0f;
                options.Volume = 0f;
                Assert.IsFalse(action.Execute(0f));
                var first = AudioKit.GetPlayingSounds()[0];
                first.SetVolumeScale(.2f);
                yield return null;
                action.Reset();
                Assert.IsFalse(first.IsValid);
                Assert.IsFalse(action.Execute(0f));
                var second = AudioKit.GetPlayingSounds()[0];
                yield return null;
                Assert.IsTrue(second.IsPlaying);
                Assert.AreEqual(.4f, second.Voice.Source.volume);
                Assert.AreEqual(1f, second.Voice.Source.pitch);
                Assert.AreEqual(0f, options.Volume);
            }
            finally { action.Deinit(); }
        }
    }
}
