using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using YLFramework.Services.Audio;

namespace YLFramework.Tests.AudioSession
{
    public class AudioContinuousPlayTests
    {
        private static readonly object DomainIdentity = new object();
        private SceneSetup[] mScenes;
        private bool mConfigured, mOptionsEnabled;
        private EnterPlayModeOptions mOptions;
        private string mPath;
        private AudioClip mClip;
        private AudioHandle mHandle;
        private AudioChannel mChannel;
        private AudioPlayRequest mPending;
        private Provider mProvider;
        private AudioKitSettings mSettings;
        private Action mUnregister;
        private int mNotifications, mQuitCount;
        private bool mQuitClean, mExitRejected;
        private ILogHandler mOriginalHandler, mNativeHandler;

        private sealed class Owner : IDisposable
        {
            internal int Releases;
            internal Action OnRelease;
            public void Dispose() { Releases++; OnRelease?.Invoke(); }
        }
        private sealed class Provider : IAudioClipProvider
        {
            internal Action<AudioClipLease, Exception> Complete;
            internal readonly Owner ActiveOwner = new Owner();
            public bool CanCache => false;
            public AudioClipLease Load(string key) => new AudioClipLease(CurrentClip, ActiveOwner);
            internal AudioClip CurrentClip;
            public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
            { Complete = completed; progress(.25f); }
        }

        [SetUp]
        public void SetUp()
        {
            Assert.IsFalse(EditorApplication.isPlayingOrWillChangePlaymode);
            Assert.IsFalse(AudioManager.HasInstance, "不接管其他 Audio owner。");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                Assert.IsFalse(SceneManager.GetSceneAt(i).isDirty, "不得覆盖未保存场景。");
            mScenes = EditorSceneManager.GetSceneManagerSetup();
            mOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            mOptions = EditorSettings.enterPlayModeOptions;
            mOriginalHandler = Debug.unityLogger.logHandler;
            mNativeHandler = (ILogHandler)Activator.CreateInstance(typeof(Debug).Assembly.GetType("UnityEngine.DebugLogHandler"), true);
            mConfigured = true;
            mPath = "Temp/YLAudio.Continuous." + Guid.NewGuid().ToString("N") + ".unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, mPath));
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorApplication.playModeStateChanged += OnState;
        }

        private void OnState(PlayModeStateChange state)
        {
            // 仅本轮获授权的验收隔离：保留原生日志输出，避免原 XF Logger 在退场时重建自身。
            Debug.unityLogger.logHandler = mNativeHandler;
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            var hosts = UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length;
            var rejected = false;
            try { AudioKit.PlaySound(mClip); }
            catch (InvalidOperationException) { rejected = true; }
            mExitRejected = rejected && hosts == UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length;
        }
        private void OnQuit()
        {
            mQuitCount++;
            mQuitClean = !mHandle.IsValid && !mChannel.IsValid && mPending.IsDone &&
                mPending.EndReason == AudioPlayEnd.Shutdown && mProvider.ActiveOwner.Releases == 1 &&
                !AudioManager.HasInstance && AudioKit.CurrentSettings == null && AudioKit.SoundCount == 0;
            if (mClip) UnityEngine.Object.DestroyImmediate(mClip);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mConfigured) yield break;
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            Application.quitting -= OnQuit;
            EditorApplication.playModeStateChanged -= OnState;
            AudioKit.Shutdown();
            mUnregister?.Invoke();
            if (mClip) UnityEngine.Object.DestroyImmediate(mClip);
            EditorSettings.enterPlayModeOptions = mOptions;
            EditorSettings.enterPlayModeOptionsEnabled = mOptionsEnabled;
            EditorSceneManager.RestoreSceneManagerSetup(mScenes);
            File.Delete(mPath);
            Debug.unityLogger.logHandler = mOriginalHandler;
            // 两轮真实退出的断言已结束；仅恢复 fixture 静态环境，不作为 Play 或退出证据。
            typeof(AudioKit).GetMethod("ResetSession", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        }

        [UnityTest]
        public IEnumerator ConsecutiveRealPlayRejectsExitReentryAndIsolatesOldIdentities()
        {
            var domain = DomainIdentity;
            yield return new EnterPlayMode();
            Assert.AreSame(domain, DomainIdentity, "必须在同一脚本域内。");
            Assert.AreEqual(EnterPlayModeOptions.DisableDomainReload, EditorSettings.enterPlayModeOptions);
            new GameObject("AudioContinuous.Listener").AddComponent<AudioListener>();
            mClip = AudioClip.Create("continuous-play", 44100, 1, 44100, false);
            mSettings = AudioKit.Settings;
            mSettings.IsSoundOn.SetValueWithoutCallback(true);
            mSettings.SoundVolume.SetValueWithoutCallback(1f);
            var token = mSettings.IsSoundMuted.Register(_ => mNotifications++);
            mUnregister = token.UnRegister;
            mProvider = new Provider { CurrentClip = mClip };
            mChannel = AudioKit.RegisterChannel("continuous-play", AudioCategory.Sound, AudioChannelMode.Overlap);
            AudioKit.PrewarmSounds(3);
            mHandle = mChannel.PlayNamed(new AudioClipAddress(mProvider, "active"), AudioSoundOptions.MusicDefaults);
            mPending = mChannel.PlayAsync(new AudioClipAddress(mProvider, "pending"));
            Assert.IsTrue(mHandle.IsValid);
            Assert.IsFalse(mPending.IsDone);
            var releaseRejected = false;
            mProvider.ActiveOwner.OnRelease = () =>
            {
                try { AudioKit.PlaySound(mClip); }
                catch (InvalidOperationException) { releaseRejected = true; }
            };
            Application.quitting += OnQuit;
            yield return null;
            yield return new ExitPlayMode();
            Assert.AreEqual(1, mQuitCount);
            Assert.IsTrue(mQuitClean, "Application.quitting 返回前应同步终结请求/租约/设置/宿主。");
            Assert.IsTrue(mExitRejected);
            Assert.IsTrue(releaseRejected);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length);
            var oldHandle = mHandle;
            var oldChannel = mChannel;
            var oldPending = mPending;
            var oldSettings = mSettings;
            var oldProvider = mProvider;
            Application.quitting -= OnQuit;

            yield return new EnterPlayMode();
            Assert.AreSame(domain, DomainIdentity);
            new GameObject("AudioContinuous.Listener").AddComponent<AudioListener>();
            mClip = AudioClip.Create("continuous-play-second", 44100, 1, 44100, false);
            mSettings = AudioKit.Settings;
            Assert.AreNotSame(oldSettings, mSettings);
            mSettings.IsSoundOn.SetValueWithoutCallback(true);
            mSettings.SoundVolume.SetValueWithoutCallback(1f);
            mProvider = new Provider { CurrentClip = mClip };
            mChannel = AudioKit.RegisterChannel("continuous-play", AudioCategory.Sound, AudioChannelMode.Overlap);
            mHandle = mChannel.PlayNamed(new AudioClipAddress(mProvider, "active"), AudioSoundOptions.MusicDefaults);
            mPending = mChannel.PlayAsync(new AudioClipAddress(mProvider, "pending"));
            var source = mHandle.Voice.Source;
            Assert.IsTrue(mHandle.IsPlaying);
            Assert.IsFalse(oldHandle.IsValid);
            Assert.IsFalse(oldChannel.IsValid);
            oldHandle.Stop(); oldChannel.Stop(); oldChannel.Pause(); oldChannel.Dispose(); oldPending.Cancel();
            oldSettings.IsSoundMuted.Value = true;
            mUnregister();
            Assert.AreEqual(0, mNotifications, "旧 Settings 订阅必须退出，旧 token 不能伤及新会话。");
            Assert.IsFalse(source.mute);
            var lateOwner = new Owner();
            oldProvider.Complete(new AudioClipLease(mClip, lateOwner), null);
            yield return null;
            Assert.AreEqual(1, lateOwner.Releases, "旧 producer 迟到结果成对归还。");
            Assert.IsTrue(mHandle.IsPlaying);
            Assert.AreEqual(1, mChannel.RequestCount);
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length);
            mSettings.IsSoundMuted.Value = true;
            Assert.IsTrue(source.mute);
            mSettings.IsSoundMuted.Value = false;
            Assert.IsFalse(source.mute);
            Application.quitting += OnQuit;
            mQuitClean = false; mExitRejected = false;
            yield return new ExitPlayMode();
            Assert.AreEqual(2, mQuitCount);
            Assert.IsTrue(mQuitClean);
            Assert.IsTrue(mExitRejected);
            Assert.AreEqual(0, AudioKit.SoundStats.OwnedSourceCount);
            Assert.AreEqual(0, UnityEngine.Object.FindObjectsOfType<AudioManager>(true).Length);
        }
    }
}
