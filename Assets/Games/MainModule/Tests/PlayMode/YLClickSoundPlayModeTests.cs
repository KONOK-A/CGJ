using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XFGameFramework;
using AudioKit = YLFramework.Services.Audio.AudioKit;
using YLFramework.Services.Audio;

public class YLClickSoundPlayModeTests
{
    [UnityTest]
    public IEnumerator ButtonClickInvokesFrameworkAudioEmitter()
    {
        var module = new MainModule();
        var soundOn = AudioKit.Settings.IsSoundOn.Value;
        AudioKit.Settings.IsSoundOn.Value = true;
        StartPanel panel = null;
        try
        {
            panel = module.LoadPanel<StartPanel>();
            Assert.NotNull(panel, "StartPanel 加载失败");

            var emitter = panel.GetComponentInChildren<AudioEmitter>(true);
            Assert.NotNull(emitter, "StartPanel 未包含 AudioEmitter");

            Button clickButton = null;
            var clickIndex = -1;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var buttonEvent = button.onClick;
                for (var i = 0; i < buttonEvent.GetPersistentEventCount(); i++)
                {
                    if (ReferenceEquals(buttonEvent.GetPersistentTarget(i), emitter))
                    {
                        clickButton = button;
                        clickIndex = i;
                        break;
                    }
                }
                if (clickButton != null) break;
            }
            Assert.NotNull(clickButton, "未找到指向 AudioEmitter 的按钮事件");

            var onClick = clickButton.onClick;
            for (var i = 0; i < onClick.GetPersistentEventCount(); i++)
                onClick.SetPersistentListenerState(i, i == clickIndex ? UnityEventCallState.RuntimeOnly : UnityEventCallState.Off);

            var before = AudioKit.SoundCount;
            onClick.Invoke();
            onClick.Invoke();
            Assert.AreEqual(before + 2, AudioKit.SoundCount, "UI 点击应显式 EveryOne，允许同帧重叠");
            panel.gameObject.SetActive(false);
            Assert.AreEqual(before + 2, AudioKit.SoundCount, "关闭面板不应截断已播放的 one-shot");
        }
        finally
        {
            AudioKit.StopAllSound();
            AudioKit.Settings.IsSoundOn.Value = soundOn;
            if (panel != null)
            {
                panel.Close();
                Object.Destroy(panel.gameObject);
            }
        }
        yield return null;
    }

    [TestCase(AudioEmitter.PositionMode.TwoDimensional)]
    [TestCase(AudioEmitter.PositionMode.FixedPosition)]
    public void OneShotsOverlapAndSurviveDisable(AudioEmitter.PositionMode mode)
    {
        var soundOn = AudioKit.Settings.IsSoundOn.Value;
        var owner = new GameObject("Emitter one-shot test");
        var clip = AudioClip.Create("Emitter test", 441000, 1, 44100, false);
        var emitter = owner.AddComponent<AudioEmitter>();
        SetField(emitter, "clips", new List<AudioClip> { null, clip });
        SetField(emitter, "positionMode", mode);
        AudioKit.Settings.IsSoundOn.Value = true;
        try
        {
            var before = AudioKit.SoundCount;
            emitter.Play();
            emitter.Play();
            Assert.AreEqual(before + 2, AudioKit.SoundCount);
            owner.SetActive(false);
            Assert.AreEqual(before + 2, AudioKit.SoundCount);
            emitter.Stop();
            Assert.AreEqual(before, AudioKit.SoundCount);
            Assert.IsNull(owner.GetComponent<AudioSource>());
        }
        finally
        {
            emitter.Stop();
            AudioKit.Settings.IsSoundOn.Value = soundOn;
            Object.Destroy(owner);
            Object.Destroy(clip);
        }
    }

    [TestCase(AudioEmitter.PositionMode.TwoDimensional)]
    [TestCase(AudioEmitter.PositionMode.FixedPosition)]
    [TestCase(AudioEmitter.PositionMode.FollowTransform)]
    public void LoopReplayReplacesOwnedPlaybackAndDisableStopsIt(AudioEmitter.PositionMode mode)
    {
        var soundOn = AudioKit.Settings.IsSoundOn.Value;
        var owner = new GameObject("Emitter loop test");
        var clip = AudioClip.Create("Emitter loop", 441000, 1, 44100, false);
        var emitter = owner.AddComponent<AudioEmitter>();
        SetField(emitter, "clips", new List<AudioClip> { clip });
        SetField(emitter, "positionMode", mode);
        SetField(emitter, "loop", true);
        AudioKit.Settings.IsSoundOn.Value = true;
        try
        {
            var before = AudioKit.SoundCount;
            emitter.Play();
            var first = AudioKit.GetPlayingSounds();
            emitter.Play();
            Assert.AreEqual(before + 1, AudioKit.SoundCount);
            var stopped = 0;
            foreach (var handle in first)
            {
                if (!handle.IsValid) stopped++;
            }
            Assert.AreEqual(1, stopped, "循环重播只能停止此组件的原句柄");
            owner.SetActive(false);
            Assert.AreEqual(before, AudioKit.SoundCount);
        }
        finally
        {
            emitter.Stop();
            AudioKit.Settings.IsSoundOn.Value = soundOn;
            Object.Destroy(owner);
            Object.Destroy(clip);
        }
    }

    [Test]
    public void DisableStopsAllFollowingOneShots()
    {
        var soundOn = AudioKit.Settings.IsSoundOn.Value;
        var owner = new GameObject("Emitter follow test");
        var clip = AudioClip.Create("Emitter follow", 441000, 1, 44100, false);
        var emitter = owner.AddComponent<AudioEmitter>();
        SetField(emitter, "clips", new List<AudioClip> { clip });
        SetField(emitter, "positionMode", AudioEmitter.PositionMode.FollowTransform);
        AudioKit.Settings.IsSoundOn.Value = true;
        try
        {
            var before = AudioKit.SoundCount;
            emitter.Play();
            emitter.Play();
            Assert.AreEqual(before + 2, AudioKit.SoundCount);
            owner.SetActive(false);
            Assert.AreEqual(before, AudioKit.SoundCount);
        }
        finally
        {
            emitter.Stop();
            AudioKit.Settings.IsSoundOn.Value = soundOn;
            Object.Destroy(owner);
            Object.Destroy(clip);
        }
    }

    [Test]
    public void DisablingMusicComponentDoesNotStopReplacementMusic()
    {
        var musicOn = AudioKit.Settings.IsMusicOn.Value;
        var owner = new GameObject("Music component test");
        var clip = AudioClip.Create("Music component", 441000, 1, 44100, false);
        var player = owner.AddComponent<AudioMusicPlayer>();
        SetField(player, "clip", clip);
        SetField(player, "playOnAwake", false);
        AudioKit.Settings.IsMusicOn.Value = true;
        AudioHandle replacement = default;
        try
        {
            player.Play();
            var ownedField = typeof(AudioMusicPlayer).GetField("_owned", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(ownedField, "MusicPlayer should track its own leases, including crossfade tails.");
            var ownedHandles = (List<AudioHandle>)ownedField.GetValue(player);
            Assert.AreEqual(1, ownedHandles.Count);
            var owned = ownedHandles[0];
            Assert.AreEqual(owned, AudioKit.Music);
            Assert.IsTrue(owned.IsValid);
            replacement = AudioKit.PlayMusic(clip);
            Assert.IsFalse(owned.IsValid);
            Assert.IsTrue(replacement.IsValid);
            owner.SetActive(false);
            player.Stop();
            Assert.IsTrue(replacement.IsValid, "旧组件不可停止已经替换的全局音乐");
            Assert.IsNull(owner.GetComponent<AudioSource>());
        }
        finally
        {
            player.Stop();
            replacement.Stop();
            AudioKit.Settings.IsMusicOn.Value = musicOn;
            Object.Destroy(owner);
            Object.Destroy(clip);
        }
    }

    private static void SetField(Object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field, name);
        field.SetValue(target, value);
    }
}
