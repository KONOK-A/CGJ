#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Audio
{
    public class AudioNamedResourcePlaybackTests
    {
        private string mFolder;
        private GameObject mListener;
        private float mTimeScale;
        private readonly bool[] mHadKeys = new bool[6];
        private readonly int[] mSwitches = new int[3];
        private readonly float[] mVolumes = new float[3];
        private static readonly string[] Keys =
        {
            "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON",
            "KEY_AUDIO_MANAGER_SOUND_VOLUME", "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME"
        };
        [SetUp]
        public void SetUp()
        {
            AudioKit.Shutdown(); mTimeScale = Time.timeScale;
            for (var i = 0; i < 6; i++) mHadKeys[i] = PlayerPrefs.HasKey(Keys[i]);
            for (var i = 0; i < 3; i++) { mSwitches[i] = PlayerPrefs.GetInt(Keys[i]); mVolumes[i] = PlayerPrefs.GetFloat(Keys[i + 3]); }
            AudioKit.Settings.IsSoundOn.SetValueWithoutCallback(true);
            AudioKit.Settings.IsMusicOn.SetValueWithoutCallback(true);
            AudioKit.Settings.IsVoiceOn.SetValueWithoutCallback(true);
            AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            mFolder = "Assets/YLFramework/Tests/Fixtures/Audio/Named-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/YLFramework/Tests/Fixtures/Audio", Path.GetFileName(mFolder));
            AssetDatabase.CreateFolder(mFolder, "Resources");
            if (Object.FindObjectsOfType<AudioListener>().Length == 0) { mListener = new GameObject("Named.Listener"); mListener.AddComponent<AudioListener>(); }
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioKit.Shutdown(); Time.timeScale = mTimeScale;
            if (mListener) Object.Destroy(mListener);
            AssetDatabase.DeleteAsset(mFolder);
            for (var i = 0; i < 3; i++)
            {
                if (mHadKeys[i]) PlayerPrefs.SetInt(Keys[i], mSwitches[i]); else PlayerPrefs.DeleteKey(Keys[i]);
                if (mHadKeys[i + 3]) PlayerPrefs.SetFloat(Keys[i + 3], mVolumes[i]); else PlayerPrefs.DeleteKey(Keys[i + 3]);
            }
            yield return null;
        }
        private AudioClipAddress CreateClip(AudioClipLoadType loadType = AudioClipLoadType.DecompressOnLoad,
            AudioCompressionFormat compression = AudioCompressionFormat.PCM, bool background = false, int seconds = 2)
        {
            var name = "audio-" + Guid.NewGuid().ToString("N");
            var path = mFolder + "/Resources/" + name + ".wav";
            WriteWave(path, seconds);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.loadInBackground = background;
            var settings = importer.defaultSampleSettings;
            settings.preloadAudioData = false; settings.loadType = loadType; settings.compressionFormat = compression;
            importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            return new AudioClipAddress(ResLoaderAudioClipProvider.Resources, "resources://" + name);
        }
        internal static void WriteWave(string path, int seconds)
        {
            Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(path)), "Only write under an existing owned fixture directory.");
            var samples = 44100 * seconds;
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (var i = 0; i < samples; i++) writer.Write((short)(Math.Sin(i * 2 * Math.PI * 440 / 44100) * 4000));
            }
        }
        private static IEnumerator WaitAccepted(AudioPlayRequest request)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (!request.Handle.IsValid && !request.IsDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(request.Handle.IsValid, request.Error?.ToString() ?? request.EndReason.ToString());
            Assert.IsTrue(request.AssetReady); Assert.IsTrue(request.DataReady);
        }
        private static IRes FindRes(AudioClipAddress address)
        {
            var key = ResKey.Allocate(address.Key, (address.Provider as ResLoaderAudioClipProvider)?.OwnerBundleName, typeof(AudioClip));
            try { return ServiceManager.Instance.GetRes(key, false); }
            finally { key.RecycleToCache(); }
        }
        private static void AssertResourcesReturned(AudioClipAddress address, AudioClip clip)
        {
            var res = FindRes(address);
            if (res != null) { Assert.AreEqual(0, res.RefCount); Assert.IsNull(res.Asset); Assert.AreEqual(ResState.Waiting, res.State); }
            // Editor 的导入对象可仍存活；已实测资源归还后音频数据卸载，不能以 bool 冒认对象销毁。
            Assert.AreEqual(AudioDataLoadState.Unloaded, clip.loadState, "The real Resources backend must release loaded audio data.");
        }

        [UnityTest]
        public IEnumerator RealResourcesSyncDisabledReplayPauseAndIdleRelease()
        {
            var address = CreateClip();
            AudioKit.ClipCacheCapacity = 1; AudioKit.ClipCacheIdleSeconds = .1;
            var handle = AudioKit.PlayMusicNamed(address);
            var clip = handle.Voice.Clip; var res = FindRes(address);
            Assert.IsTrue(handle.IsValid); Assert.Greater(res.RefCount, 0);
            AudioKit.Settings.IsMusicOn.Value = false; Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.IsTrue(clip); Assert.IsTrue(handle.IsValid); Assert.Greater(res.RefCount, 0);
            AudioKit.Settings.IsMusicOn.Value = true;
            Assert.IsTrue(handle.IsPlaying);
            handle.Pause(); yield return null;
            Assert.IsTrue(clip); Assert.IsTrue(handle.IsPaused);
            handle.Stop(); Assert.IsTrue(clip, "Idle cache owns the backend lease.");
            var reused = AudioKit.PlayMusicNamed(address);
            Assert.AreSame(clip, reused.Voice.Clip);
            reused.Stop();
            yield return new WaitForSecondsRealtime(.2f);
            AssertResourcesReturned(address, clip);
        }

        [UnityTest]
        public IEnumerator RealResourcesSharedCancelLateShutdownAndRetry()
        {
            var address = CreateClip();
            var canceled = AudioKit.PlaySoundAsync(address, AudioSoundOptions.MusicDefaults);
            var live = AudioKit.PlaySoundAsync(address, AudioSoundOptions.MusicDefaults);
            canceled.Cancel();
            yield return WaitAccepted(live);
            Assert.AreEqual(AudioPlayEnd.Canceled, canceled.EndReason);
            var clip = live.Handle.Voice.Clip;
            Assert.IsTrue(clip); live.Cancel();
            AssertResourcesReturned(address, clip);
            var late = AudioKit.PlayMusicAsync(address);
            AudioKit.Shutdown();
            Assert.AreEqual(AudioPlayEnd.Shutdown, late.EndReason);
            yield return null; yield return null; yield return null;
            Assert.IsFalse(late.Handle.IsValid);
            var retry = AudioKit.PlayMusicAsync(address);
            yield return WaitAccepted(retry);
            Assert.IsTrue(retry.Handle.IsPlaying);
            retry.Cancel();
            yield return null;
            Assert.IsNull(FindRes(address));
        }

        [UnityTest]
        public IEnumerator RealResourcesMissingAssetFailsAndCanRetryWithoutStoppingExistingMusic()
        {
            var valid = CreateClip(); var playing = AudioKit.PlayMusicNamed(valid);
            var missing = "audio-missing-" + Guid.NewGuid().ToString("N");
            var invalid = new AudioClipAddress(ResLoaderAudioClipProvider.Resources, "resources://" + missing);
            LogAssert.Expect(LogType.Error, "#YLFramework: Fail To Load Res From Resources: " + missing);
            var request = AudioKit.PlayMusicAsync(invalid);
            var deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!request.IsDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(AudioPlayEnd.Failed, request.EndReason); Assert.NotNull(request.Error); Assert.IsTrue(playing.IsValid);
            LogAssert.Expect(LogType.Error, "#YLFramework: Fail To Load Res From Resources: " + missing);
            var retry = AudioKit.PlayVoiceAsync(invalid);
            deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!retry.IsDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(AudioPlayEnd.Failed, retry.EndReason); Assert.IsTrue(playing.IsValid);
        }

        [UnityTest]
        public IEnumerator NaturalFinishIsTerminalBeforeCallbackButResourceRemainsHeldUntilReturn()
        {
            var address = CreateClip(seconds: 1);
            AudioPlayRequest request = null;
            AudioClip finishedClip = null;
            var callbacks = 0;
            request = AudioKit.PlaySoundAsync(address, onFinished: (clip, handle) =>
            {
                Assert.IsTrue(request.IsDone); Assert.AreEqual(AudioPlayEnd.Completed, request.EndReason);
                Assert.IsTrue(handle.IsValid, "Preserve the existing natural callback window; the original Voice is finalized after return."); Assert.AreEqual(request.Handle, handle);
                Assert.Greater(FindRes(address).RefCount, 0); Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
                request.Cancel();
                Assert.AreEqual(AudioPlayEnd.Completed, request.EndReason);
                finishedClip = clip; callbacks++;
            });
            yield return WaitAccepted(request);
            var deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!request.IsDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(1, callbacks); Assert.AreEqual(AudioPlayEnd.Completed, request.EndReason);
            AssertResourcesReturned(address, finishedClip);
        }

        [UnityTest]
        public IEnumerator NamedBackgroundCompressedAndStreamingWaitForDataWithoutChangingGameImporter()
        {
            foreach (var type in new[] { AudioClipLoadType.DecompressOnLoad, AudioClipLoadType.CompressedInMemory, AudioClipLoadType.Streaming })
            {
                var address = CreateClip(type, type == AudioClipLoadType.DecompressOnLoad ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis, true, type == AudioClipLoadType.DecompressOnLoad ? 120 : 3);
                var starts = 0;
                var request = AudioKit.PlayMusicAsync(address, onStarted: (clip, original) =>
                {
                    Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
                    Assert.IsTrue(original.IsValid); starts++;
                });
                yield return WaitAccepted(request);
                var loaded = request.Handle.Voice.Clip;
                Assert.AreEqual(type, loaded.loadType); Assert.IsFalse(loaded.preloadAudioData); Assert.IsTrue(loaded.loadInBackground);
                var source = request.Handle.Voice.Source;
                yield return new WaitForSecondsRealtime(.1f);
                Assert.IsTrue(source.isPlaying); Assert.Greater(source.timeSamples, 0); Assert.AreEqual(1, starts);
                TestContext.WriteLine("Named ResLoader {0}: Loaded before Started, timeSamples={1}", type, source.timeSamples);
                request.Cancel(); AssertResourcesReturned(address, loaded);
            }
        }

        [UnityTest]
        public IEnumerator CrossfadeTailAndCallerInactiveOneShotKeepActualResourcesAlive()
        {
            var first = CreateClip(); var second = CreateClip();
            var old = AudioKit.PlayMusicNamed(first); var clip = old.Voice.Clip;
            var next = AudioKit.CrossFadeMusicAsync(second, .2f);
            yield return WaitAccepted(next);
            Assert.IsTrue(old.IsValid); Assert.IsTrue(clip);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsFalse(old.IsValid); AssertResourcesReturned(first, clip); Assert.IsTrue(next.Handle.IsValid);
            next.Cancel();
            var caller = new GameObject("named-one-shot-owner");
            try
            {
                var shot = AudioKit.PlaySoundAsync(first);
                caller.SetActive(false);
                yield return WaitAccepted(shot);
                var shotClip = shot.Handle.Voice.Clip;
                Assert.IsTrue(shotClip); Assert.IsTrue(shot.Handle.IsPlaying);
                var deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (!shot.IsDone && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(AudioPlayEnd.Completed, shot.EndReason); AssertResourcesReturned(first, shotClip);
            }
            finally { Object.Destroy(caller); }
        }

        [UnityTest]
        public IEnumerator RealAssetBundleSyncAsyncDependenciesAndFinalPhysicalUnload()
        {
            var root = SessionState.GetString("YL.Audio43.BundleRoot", "");
            var assets = SessionState.GetString("YL.Audio43.BundleAssets", "");
            var bundleName = SessionState.GetString("YL.Audio43.BundleName", "");
            var dependencyName = SessionState.GetString("YL.Audio43.BundleDependency", "");
            Assert.IsTrue(root.StartsWith(Path.Combine(Path.GetTempPath(), "kilo", "Audio43-"), StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(assets.StartsWith("Assets/YLFramework/Tests/Fixtures/Audio/Bundle-", StringComparison.Ordinal));
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var dataField = typeof(AssetBundleSetting).GetField("_assetBundleDataFile", flags);
            var persistentField = typeof(AssetBundleHelper).GetField("_persistentDataPath", flags);
            var streamingField = typeof(AssetBundleHelper).GetField("_streamingAssetsPath", flags);
            var previousData = dataField.GetValue(null);
            var previousPersistent = persistentField.GetValue(null); var previousStreaming = streamingField.GetValue(null);
            var hadSimulation = EditorPrefs.HasKey("SimulateAssetBundles"); var previousSimulation = AssetBundleHelper.SimulationMode;
            try
            {
                Assert.IsTrue(File.Exists(Path.Combine(root, "AssetBundles", "Windows", bundleName)), "Run the EditMode preparation before this test.");
                var data = new ResData();
                data.AddAssetBundleRecord(dependencyName, Array.Empty<string>(), out _);
                var index = data.AddAssetBundleRecord(bundleName, new[] { dependencyName }, out var group);
                group.AddAssetData(new AssetData(assets + "/first.wav", ResLoadType.ABAsset, index, bundleName, typeof(AudioClip).ToCode()));
                group.AddAssetData(new AssetData(assets + "/second.wav", ResLoadType.ABAsset, index, bundleName, typeof(AudioClip).ToCode()));
                AssetBundleSetting.AssetBundleDataFile = data;
                persistentField.SetValue(null, root.Replace('\\', '/') + "/"); streamingField.SetValue(null, root.Replace('\\', '/') + "/");
                AssetBundleHelper.SimulationMode = false;
                var provider = new ResLoaderAudioClipProvider(bundleName);
                var first = new AudioClipAddress(provider, assets + "/first.wav"); var second = new AudioClipAddress(provider, assets + "/second.wav");
                var handle = AudioKit.PlayMusicNamed(first);
                var firstClip = handle.Voice.Clip;
                Assert.IsTrue(handle.IsPlaying);
                var mainKey = ResKey.Allocate(bundleName, null, typeof(AssetBundle));
                var dependencyKey = ResKey.Allocate(dependencyName, null, typeof(AssetBundle));
                IRes mainRes, dependencyRes;
                try { mainRes = ServiceManager.Instance.GetRes(mainKey, false); dependencyRes = ServiceManager.Instance.GetRes(dependencyKey, false); }
                finally { mainKey.RecycleToCache(); dependencyKey.RecycleToCache(); }
                var mainBundle = mainRes.Asset as AssetBundle; var dependencyBundle = dependencyRes.Asset as AssetBundle;
                Assert.IsTrue(mainBundle); Assert.IsTrue(dependencyBundle);
                var dependencyAsset = dependencyBundle.LoadAsset<TextAsset>(assets + "/dependency.txt");
                Assert.IsTrue(dependencyAsset);
                handle.Pause(); AudioKit.Settings.IsMusicOn.Value = false;
                yield return null;
                Assert.IsTrue(firstClip); Assert.IsTrue(mainBundle); Assert.IsTrue(dependencyBundle);
                AudioKit.Settings.IsMusicOn.Value = true; handle.Resume();
                var canceled = AudioKit.PlaySoundAsync(second, AudioSoundOptions.MusicDefaults);
                var next = AudioKit.CrossFadeMusicAsync(second, .2f);
                canceled.Cancel();
                yield return WaitAccepted(next);
                Assert.AreEqual(AudioPlayEnd.Canceled, canceled.EndReason);
                Assert.IsTrue(firstClip); Assert.IsTrue(handle.IsValid);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsFalse(handle.IsValid); Assert.IsTrue(mainBundle); Assert.IsTrue(dependencyBundle);
                var secondClip = next.Handle.Voice.Clip;
                next.Cancel();
                Assert.IsFalse(mainBundle, "Final Loader return must physically Unload(true) the owned Bundle.");
                Assert.IsFalse(dependencyBundle); Assert.IsFalse(dependencyAsset); Assert.IsFalse(secondClip);
                var retry = AudioKit.PlayMusicAsync(first);
                yield return WaitAccepted(retry);
                Assert.IsTrue(retry.Handle.IsPlaying);
                retry.Cancel();
            }
            finally
            {
                AudioKit.Shutdown();
                dataField.SetValue(null, previousData); persistentField.SetValue(null, previousPersistent); streamingField.SetValue(null, previousStreaming);
                if (hadSimulation) AssetBundleHelper.SimulationMode = previousSimulation; else EditorPrefs.DeleteKey("SimulateAssetBundles");
                AssetDatabase.DeleteAsset(assets);
                if (Directory.Exists(root)) Directory.Delete(root, true);
                SessionState.EraseString("YL.Audio43.BundleRoot"); SessionState.EraseString("YL.Audio43.BundleAssets");
                SessionState.EraseString("YL.Audio43.BundleName"); SessionState.EraseString("YL.Audio43.BundleDependency");
            }
        }
    }
}
#endif
