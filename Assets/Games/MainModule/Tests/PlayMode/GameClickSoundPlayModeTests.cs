using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XFGameFramework;

public class GameClickSoundPlayModeTests
{
    [UnityTest]
    public IEnumerator LateOldPanelDisableAndDestroyCannotStopReplacementMusicOwner()
    {
        yield return GameTestResources.Ready();
        Assert.IsNull(AudioPlayer.GetAuidoController("Music"));
        using var preferences = new GameAudioPreferences();
        preferences.UseAudibleDefaults();
        var module = new MainModule();
        module.LoadController<AudioController>(false);
        StartPanel old = null;
        GameObject replacement = null;
        var clip = AudioClip.Create("Replacement music owner", 441000, 1, 44100, false);
        try
        {
            old = module.LoadPanel<StartPanel>();
            var original = old.GetComponentsInChildren<AudioPlayer>(true).Single(p => p.ControllerName == "Music");
            replacement = Object.Instantiate(original.gameObject);
            var next = replacement.GetComponent<AudioPlayer>();
            next.Clips[0].clips = new List<AudioClip> { clip };
            next.Play();
            var music = AudioPlayer.GetAuidoController("Music").AudioSource;
            Assert.AreSame(clip, music.clip);
            old.gameObject.SetActive(false);
            Assert.IsTrue(music.isPlaying);
            Assert.AreSame(clip, music.clip);
            Object.Destroy(old.gameObject);
            old = null;
            yield return null;
            Assert.IsTrue(music.isPlaying);
            Assert.AreSame(clip, music.clip);
        }
        finally
        {
            if (old) { old.Close(); Object.Destroy(old.gameObject); }
            if (replacement) Object.Destroy(replacement);
            module.RemoveController<AudioController>();
            Object.Destroy(clip);
        }
    }

    [UnityTest]
    public IEnumerator RealButtonUsesSharedXfOneShotsAndSurvivesPanelDisable()
    {
        yield return GameTestResources.Ready();
        Assert.IsNull(AudioPlayer.GetAuidoController("Music"));
        using var preferences = new GameAudioPreferences();
        preferences.UseAudibleDefaults();
        var module = new MainModule();
        module.LoadController<AudioController>(false);
        StartPanel panel = null;
        try
        {
            panel = module.LoadPanel<StartPanel>();
            Assert.IsNotNull(panel);
            var player = panel.GetComponentsInChildren<AudioPlayer>(true).Single(p => p.ControllerName == "Sound");
            Button target = null;
            var index = -1;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    if (button.onClick.GetPersistentTarget(i) == player) { target = button; index = i; break; }
            Assert.IsNotNull(target);
            for (var i = 0; i < target.onClick.GetPersistentEventCount(); i++)
                target.onClick.SetPersistentListenerState(i, i == index ? UnityEventCallState.RuntimeOnly : UnityEventCallState.Off);
            var sound = AudioPlayer.GetAuidoController("Sound");
            var data = (ICollection)typeof(XFGameFramework.AudioController).GetField("AudioDatas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sound);
            var before = data.Count;
            target.onClick.Invoke();
            target.onClick.Invoke();
            Assert.AreEqual(before + 2, data.Count);
            Assert.IsTrue(sound.AudioSource.isPlaying);
            panel.gameObject.SetActive(false);
            Assert.AreEqual(before + 2, data.Count);
            Assert.IsTrue(sound.AudioSource.isPlaying);
            Assert.IsNull(player.GetComponent<AudioSource>());
        }
        finally
        {
            if (panel) { panel.Close(); Object.Destroy(panel.gameObject); }
            module.RemoveController<AudioController>();
        }
        yield return null;
    }
}
