using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Services.Audio;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioRequestLifecyclePlaybackTests
    {
        private AudioClip mClip;
        private GameObject mListener;
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
            for (var i = 0; i < 6; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++)
            {
                mSwitches[i] = PlayerPrefs.GetInt(Keys[i]);
                mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]);
            }
            AudioKit.Settings.IsSoundOn.SetValueWithoutCallback(true);
            AudioKit.Settings.IsMusicOn.SetValueWithoutCallback(true);
            AudioKit.Settings.IsVoiceOn.SetValueWithoutCallback(true);
            mClip = AudioClip.Create("lifecycle-short", 2205, 1, 44100, false);
            if (Object.FindObjectsOfType<AudioListener>().Length == 0)
            {
                mListener = new GameObject("Lifecycle.Listener");
                mListener.AddComponent<AudioListener>();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown();
            Object.Destroy(mClip);
            if (mListener) Object.Destroy(mListener);
            for (var i = 0; i < 3; i++)
            {
                if (mHadKeys[i]) PlayerPrefs.SetInt(Keys[i], mSwitches[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
                if (mHadKeys[i + 3]) PlayerPrefs.SetFloat(Keys[i + 3], mVolumes[i]);
                else PlayerPrefs.DeleteKey(Keys[i + 3]);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealShortCompletionAndSourceLossReleaseExactlyOnce()
        {
            var resource = new Resource();
            var finishes = 0;
            var handle = AudioKit.PlaySoundWithSnapshot(mClip, AudioPlaybackOptions.Defaults, (_, original) =>
            {
                finishes++;
                Assert.IsTrue(original.IsValid);
                Assert.AreEqual(0, resource.Releases);
            }, resource: new AudioResourceLease(resource));
            var deadline = Time.realtimeSinceStartup + 2f;
            while (handle.IsValid && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsFalse(handle.IsValid);
            Assert.AreEqual(1, finishes);
            Assert.AreEqual(1, resource.Releases);
            var missing = new Resource();
            var options = AudioPlaybackOptions.Defaults;
            options.Loop = true;
            var lost = AudioKit.PlaySoundWithSnapshot(mClip, options, (_, __) => finishes++, resource: new AudioResourceLease(missing));
            Object.Destroy(lost.Voice.Source);
            yield return null;
            yield return null;
            Assert.IsFalse(lost.IsValid);
            Assert.AreEqual(1, missing.Releases);
            Assert.AreEqual(1, finishes);
            AudioKit.Shutdown();
            AudioKit.Shutdown();
            Assert.IsTrue(mClip);
            Assert.AreEqual(1, resource.Releases);
            Assert.AreEqual(1, missing.Releases);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator BackgroundDataWaitKeepsOriginalLeaseAndSynchronousStartedContract()
        {
            // 仅测试自己生成的素材，不改变原游戏 importer；结束后删除资产和 meta。
            var path = "Assets/YLFramework/Tests/Fixtures/Audio/LifecycleData-" + Guid.NewGuid().ToString("N") + ".wav";
            AudioHandle handle = default;
            try
            {
                const int samples = 44100 * 120;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                    writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                    writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                    writer.Write(new byte[samples * 2]);
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.loadInBackground = true;
                var sampleSettings = importer.defaultSampleSettings;
                sampleSettings.preloadAudioData = false;
                sampleSettings.loadType = AudioClipLoadType.DecompressOnLoad;
                sampleSettings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = sampleSettings;
                importer.SaveAndReimport();
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsTrue(clip.loadInBackground);
                Assert.IsFalse(clip.preloadAudioData);
                if (clip.loadState == AudioDataLoadState.Loaded) Assert.IsTrue(clip.UnloadAudioData());
                Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
                var resource = new Resource();
                var started = 0;
                var finishes = 0;
                AudioHandle observed = default;
                handle = AudioKit.PlaySoundWithSnapshot(clip, AudioPlaybackOptions.Defaults, (_, __) => finishes++,
                    (_, original) => { started++; observed = original; }, resource: new AudioResourceLease(resource));
                var initialState = clip.loadState;
                var initialPlaying = handle.IsPlaying;
                Assert.AreEqual(1, started, "Started remains the synchronous Source.Play submission notification.");
                Assert.AreEqual(handle, observed);
                Assert.IsTrue(handle.IsValid);
                handle.Voice.CheckFinished();
                Assert.IsTrue(handle.IsValid, "Waiting data cannot be classified as natural completion.");
                var deadline = Time.realtimeSinceStartup + 5f;
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
                yield return null;
                Assert.IsTrue(handle.IsPlaying);
                Assert.AreEqual(0, finishes);
                Assert.AreEqual(0, resource.Releases);
                Assert.IsTrue(clip.UnloadAudioData());
                Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState);
                handle.Voice.CheckFinished();
                Assert.IsFalse(handle.IsValid, "Externally unloaded data ends playback without natural notification.");
                Assert.AreEqual(0, finishes);
                Assert.AreEqual(1, resource.Releases);
                Assert.IsTrue(clip.LoadAudioData());
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline) yield return null;
                var secondResource = new Resource();
                handle = AudioKit.PlaySoundWithSnapshot(clip, AudioPlaybackOptions.Defaults, resource: new AudioResourceLease(secondResource));
                handle.Stop();
                Assert.AreEqual(1, secondResource.Releases);
                Assert.AreEqual(1, resource.Releases);
                Assert.IsTrue(clip);
                Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState, "Borrowed data is not unloaded by Audio.");
                TestContext.WriteLine("Background PCM: initial loadState={0}, isPlaying={1}; Loaded playback observed.", initialState, initialPlaying);
                Debug.Log("Audio lifecycle background PCM: initial loadState=" + initialState + ", isPlaying=" + initialPlaying + "; Loaded playback observed.");
            }
            finally
            {
                handle.Stop();
                AssetDatabase.DeleteAsset(path);
            }
        }
#endif
    }
}
