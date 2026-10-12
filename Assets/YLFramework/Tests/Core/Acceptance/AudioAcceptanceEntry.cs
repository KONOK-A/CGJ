using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using YLFramework.Services.Audio;
using YLFramework.Services.Resource;

namespace YLFramework.Core.Tests
{
    /// <summary>4.5 Editor/Mono Player 共用的窄音频验收入口，不参与框架分发。</summary>
    public sealed class AudioAcceptanceEntry : MonoBehaviour
    {
        public AudioGroupPlayer Group;
        public AudioClip Signal;
        public AudioClip SecondSignal;
        public AudioMixerConfig MixerConfig;
        public string ResourceKey;
        public bool GameMode;
        public static string Result;
        public static string Stage;
        private AudioHandle mExitHandle;
        private AudioPlayRequest mExitPending;
        private Provider mExitProvider;
        private bool mOwnAudioSession;
        private readonly float[] mSamples = new float[2048];

        private sealed class Owner : IDisposable
        {
            internal int Releases;
            public void Dispose() { Releases++; }
        }
        private sealed class Provider : IAudioClipProvider
        {
            internal AudioClip Clip;
            internal Action<AudioClipLease, Exception> Complete;
            internal readonly Owner Owner = new Owner();
            public bool CanCache => false;
            public AudioClipLease Load(string key) => new AudioClipLease(Clip, Owner);
            public void LoadAsync(string key, Action<AudioClipLease, Exception> completed, Action<float> progress)
            { Complete = completed; progress(.2f); }
        }

        private IEnumerator Start()
        {
            Result = "RUNNING";
            DontDestroyOnLoad(gameObject);
            GameMode |= Array.IndexOf(Environment.GetCommandLineArgs(), "-audio45-game") >= 0;
            if (GameMode)
            {
                GetComponent<AudioListener>().enabled = false;
                GetComponent<Camera>().enabled = false;
            }
            var stack = new Stack<IEnumerator>();
            stack.Push(GameMode ? AudioGameAcceptanceFlow.Run() : RunAudio());
            try
            {
                while (stack.Count > 0)
                {
                    object current = null;
                    bool next = false;
                    Exception failure = null;
                    try { next = stack.Peek().MoveNext(); if (next) current = stack.Peek().Current; }
                    catch (Exception error) { failure = error; }
                    if (failure != null)
                    {
                        Result = "FAILED: " + failure;
                        Debug.LogError("AUDIO45 " + Result);
                        if (!Application.isEditor) Application.Quit(1);
                        yield break;
                    }
                    if (!next) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    if (current is IEnumerator nested) stack.Push(nested);
                    else yield return current;
                }
                Result = GameMode ? "GAME_PASS" : "AUDIO_PASS";
                Debug.Log("AUDIO45 " + Result);
                if (!Application.isEditor) Application.Quit(0);
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                if (mOwnAudioSession && Result.StartsWith("FAILED", StringComparison.Ordinal))
                {
                    Application.quitting -= VerifyQuit;
                    if (Group) Group.Stop();
                    AudioKit.Shutdown();
                    Debug.Log("AUDIO45 FAILURE_CLEANUP");
                }
            }
        }

        private IEnumerator RunAudio()
        {
            var keys = new[] { "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_VOICE_ON", "KEY_AUDIO_MANAGER_SOUND_ON",
                "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_VOICE_VOLUME", "KEY_AUDIO_MANAGER_SOUND_VOLUME" };
            var existed = new bool[6]; var values = new float[6];
            for (var i = 0; i < 6; i++) { existed[i] = PlayerPrefs.HasKey(keys[i]); values[i] = i < 3 ? PlayerPrefs.GetInt(keys[i]) : PlayerPrefs.GetFloat(keys[i]); }
            var scale = Time.timeScale;
            try
            {
                Check(Signal && SecondSignal && Group && MixerConfig, "formal serialized fixture dependencies");
                Check(!AudioKit.Music.IsValid && !AudioKit.Voice.IsValid && AudioKit.SoundCount == 0 &&
                    AudioKit.SoundStats.OwnedSourceCount == 0, "fresh isolated audio fixture");
                mOwnAudioSession = true;
                AudioKit.Settings.IsOn = true;
                AudioKit.Settings.MusicVolume.Value = 1; AudioKit.Settings.VoiceVolume.Value = 1; AudioKit.Settings.SoundVolume.Value = 1;
                AudioKit.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
                AudioKit.PrewarmSounds(3);
                Group.Play();
                yield return Wait(() => Group.LastHandle.IsPlaying, "formal GroupPlayer async Resources submission");
                Check(Group.LastRequest.AssetReady && Group.LastRequest.DataReady, "named asset/data readiness");
                double audible = 0;
                yield return Measure("group-signal", value => audible = value);
                Check(audible > .001, "nonzero real listener output for recognizable PCM");
                var playing = Group.LastHandle;
                var source = FindSource();
                Check(source && source.loop, "real Source loop");
                Group.Channel.Volume = .5f;
                double half = 0;
                yield return Measure("half-volume", value => half = value);
                Check(half > audible * .35 && half < audible * .65, "channel volume changes actual output");
                Group.Channel.Volume = 1;
                Group.Channel.Muted = true;
                var time = playing.Time;
                double silence = 1;
                yield return Measure("muted", value => silence = value);
                Check(playing.Time > time && silence < .00001, "mute silences output without pausing");
                Group.Channel.Muted = false;
                playing.Pause(); time = playing.Time;
                yield return Measure("paused", value => silence = value);
                Check(playing.IsPaused && Mathf.Abs(playing.Time - time) < .02f && silence < .00001, "pause freezes time and output");
                playing.Resume();
                yield return Measure("resumed", value => Check(value > .001, "resume restores output"));
                playing.FadeOut(.25f, false);
                yield return new WaitForSecondsRealtime(.35f);
                yield return Measure("faded-out", value => Check(value < .00001 && playing.IsValid, "fade silence keeps original lease"));
                playing.FadeIn(.25f);
                yield return new WaitForSecondsRealtime(.35f);
                yield return Measure("faded-in", value => Check(value > .001, "fade-in restores output"));
                Group.Stop();
                Check(!playing.IsValid, "component Stop terminates original handle");
                yield return Measure("stopped", value => Check(value < .00001, "Stop silences output"));

                var address = new AudioClipAddress(ResLoaderAudioClipProvider.Resources, ResourceKey);
                var loop = AudioSoundOptions.MusicDefaults;
                var first = AudioKit.PlayMusicNamed(address, loop);
                var namedClip = FindSource().clip;
                var resource = ObserveResource();
                Check(first.IsPlaying && resource.RefCount > 0, "sync Resources holds real Loader");
                AudioKit.ApplyMixerConfig(MixerConfig);
                Check(FindSource().outputAudioMixerGroup == MixerConfig.MusicGroup, "formal Mixer inherited route");
                double mixerFull = 0, mixerHalf = 0;
                yield return Measure("mixer-full", value => mixerFull = value);
                Check(AudioMixerControl.TrySetVolume(MixerConfig.Mixer, "MusicVolume", .5f), "native exposed Mixer write");
                yield return Measure("mixer-half", value => mixerHalf = value);
                Check(mixerHalf > mixerFull * .35 && mixerHalf < mixerFull * .65, "native Mixer changes actual output");
                AudioMixerControl.TryClearFloat(MixerConfig.Mixer, "MusicVolume");
                var second = AudioKit.CrossFadeMusic(SecondSignal, .4f);
                yield return new WaitForSecondsRealtime(.12f);
                Check(first.IsValid && second.IsPlaying && resource.RefCount > 0, "two real crossfade voices retain tail resource");
                yield return Measure("crossfade", value => Check(value > .001, "crossfade has real output"), .1f);
                yield return new WaitForSecondsRealtime(.45f);
                Check(!first.IsValid && second.IsPlaying && resource.RefCount == 0, "tail completion returns real Loader");
                Check(resource.Asset == null && resource.State == ResState.Waiting &&
                    (!namedClip || namedClip.loadState == AudioDataLoadState.Unloaded), "tail release unloads actual named audio data");
                second.Stop(); AudioKit.ClearMixerRouting();

                var spatial = AudioKit.PlaySoundAt(Signal, new Vector3(0, 0, 1), loop);
                source = FindSource(Signal);
                Check(source && source.spatialBlend == 1f, "real spatial Source");
                double near = 0, far = 0;
                yield return Measure("spatial-near", value => near = value);
                // 固定点请求每帧保持其提交位置；移动本 fixture 的 Listener，而非改写框架自有 Source。
                transform.position = new Vector3(0, 0, 1000);
                yield return Measure("spatial-far", value => far = value);
                Check(near > .001 && far < near * .1, "distance affects actual output");
                transform.position = Vector3.zero;
                spatial.Stop();
                var canceled = AudioKit.PlaySoundAsync(address, loop); canceled.Cancel();
                yield return null; yield return null;
                Check(canceled.IsDone && !canceled.Handle.IsValid && canceled.EndReason == AudioPlayEnd.Canceled, "named async logical cancel");
                var async = AudioKit.PlaySoundAsync(address, loop);
                yield return Wait(() => async.Handle.IsPlaying, "real named async retry");
                var asyncResource = ObserveResource();
                namedClip = FindSource().clip;
                Check(asyncResource.RefCount > 0, "async resource kept while playing");
                async.Cancel();
                Check(asyncResource.RefCount == 0, "cancel returns resource ownership");
                Check(asyncResource.Asset == null && asyncResource.State == ResState.Waiting &&
                    (!namedClip || namedClip.loadState == AudioDataLoadState.Unloaded), "cancel unloads actual named audio data");
                var old = AudioKit.PlaySound(Signal, loop); old.Stop();
                var current = AudioKit.PlaySound(Signal, loop); old.Stop();
                Check(current.IsPlaying, "pool reuse rejects old generation Stop"); current.Stop();
                AudioKit.Shutdown();
                Check(AudioKit.SoundStats.OwnedSourceCount == 0, "Shutdown releases owned Source pool");
                mExitProvider = new Provider { Clip = Signal };
                mExitHandle = AudioKit.PlaySoundNamed(new AudioClipAddress(mExitProvider, "active"), loop);
                mExitPending = AudioKit.PlayVoiceAsync(new AudioClipAddress(mExitProvider, "pending"));
                Application.quitting += VerifyQuit;
            }
            finally
            {
                if (MixerConfig) AudioMixerControl.TryClearFloat(MixerConfig.Mixer, "MusicVolume");
                Time.timeScale = scale;
                for (var i = 0; i < 6; i++)
                    if (!existed[i]) PlayerPrefs.DeleteKey(keys[i]);
                    else if (i < 3) PlayerPrefs.SetInt(keys[i], (int)values[i]);
                    else PlayerPrefs.SetFloat(keys[i], values[i]);
            }
        }

        private void VerifyQuit()
        {
            Application.quitting -= VerifyQuit;
            var rejected = false;
            try { AudioKit.PlaySound(Signal); } catch (InvalidOperationException) { rejected = true; }
            var passed = rejected && !mExitHandle.IsValid && mExitPending.IsDone &&
                mExitPending.EndReason == AudioPlayEnd.Shutdown && mExitProvider.Owner.Releases == 1 && AudioKit.SoundStats.OwnedSourceCount == 0;
            if (passed) Debug.Log("AUDIO45 QUIT_PASS");
            else
            {
                Result = "FAILED: real quit cleanup";
                Debug.LogError("AUDIO45 QUIT_FAILED");
                // quitting 已不可撤销；Mono fixture 的失败必须以非零码结束，不能被先前 Quit(0) 覆盖。
                if (!Application.isEditor) Environment.Exit(1);
            }
        }
        private IRes ObserveResource()
        {
            // 仅在已有播放持有资源时临时取得/归还观察租约，不给验收程序集增加内部 API 特权。
            var loader = ResLoader.Allocate();
            var key = ResKey.Allocate(ResourceKey, null, typeof(AudioClip));
            try { return loader.LoadResSync(key); }
            finally { key.RecycleToCache(); loader.RecycleToCache(); }
        }
        private static AudioSource FindSource(AudioClip clip = null)
        {
            foreach (var source in FindObjectsOfType<AudioSource>()) if ((!clip || source.clip == clip) && source.isPlaying) return source;
            return null;
        }
        private IEnumerator Measure(string label, Action<double> consume, float duration = .2f)
        {
            // Listener 混音输出不等于 OS loopback，更不等于人工听感。
            Stage = label;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-audio45-listen") >= 0) duration = Mathf.Max(duration, .8f);
            yield return new WaitForSecondsRealtime(.15f);
            double energy = 0; var count = 0;
            var deadline = Time.realtimeSinceStartup + duration;
            do
            {
                AudioListener.GetOutputData(mSamples, 0);
                foreach (var sample in mSamples) energy += sample * sample;
                count += mSamples.Length;
                yield return null;
            } while (Time.realtimeSinceStartup < deadline);
            var rms = Math.Sqrt(energy / count);
            Debug.Log("AUDIO45 OUTPUT " + label + " rms=" + rms.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            consume(rms);
        }
        private void OnGUI() => GUI.Box(new Rect(10, 10, 900, 45), "AUDIO45 " + Stage + " / " + Result);
        private static IEnumerator Wait(Func<bool> ready, string label)
        {
            var deadline = Time.realtimeSinceStartup + 15;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Check(ready(), label);
        }
        private static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Audio45 contract: " + label);
            Debug.Log("AUDIO45 CHECK " + label);
        }
    }
}
