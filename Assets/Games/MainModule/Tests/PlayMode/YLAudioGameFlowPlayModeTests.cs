using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XFGameFramework;
using XFFSM;
using YLFramework.Services.Audio;
using Object = UnityEngine.Object;

public class YLAudioGameFlowPlayModeTests
{
    private bool mOwnsSession;
    private float mTimeScale;
    private int mFrameRate;
    [UnityTest]
    public IEnumerator MainStartSettingsGameExitAndReentryUseFrameworkAudio()
    {
        Assert.IsNull(ModuleManager.GetModule<MainModule>(), "The fixture must own its module session.");
        Assert.IsNull(FSMController.GetFSM("GameState"), "The fixture must own its FSM session.");
        const string musicKey = "KEY_AUDIO_MANAGER_MUSIC_ON";
        const string volumeKey = "KEY_AUDIO_MANAGER_MUSIC_VOLUME";
        const string legacyKey = "AudioConfigVolume:Music";
        var hadMusic = PlayerPrefs.HasKey(musicKey);
        var musicValue = PlayerPrefs.GetInt(musicKey);
        var hadVolume = PlayerPrefs.HasKey(volumeKey);
        var volumeValue = PlayerPrefs.GetFloat(volumeKey);
        var hadLegacy = PlayerPrefs.HasKey(legacyKey);
        var legacyValue = PlayerPrefs.GetFloat(legacyKey);
        var timeScale = Time.timeScale;
        var frameRate = Application.targetFrameRate;
        mTimeScale = timeScale;
        mFrameRate = frameRate;
        mOwnsSession = true;
        try
        {
            AudioKit.Shutdown();
            AudioKit.Settings.IsMusicOn.SetValueWithoutCallback(true);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitUntil(() => Object.FindFirstObjectByType<StartPanel>() && AudioKit.Music.IsPlaying, "Main -> StartPanel / BGM");
            var module = ModuleManager.GetModule<MainModule>();
            Assert.NotNull(module);
            var menuMusic = AudioKit.Music;
            InvokeButton(Object.FindFirstObjectByType<StartPanel>(), "OnBtnStartClicked");
            yield return WaitUntil(() => Object.FindFirstObjectByType<GameSettingPanel>(), "Start -> game settings");
            Assert.IsFalse(menuMusic.IsValid);
            Assert.IsFalse(AudioKit.Music.IsValid);

            for (var round = 0; round < 2; round++)
            {
                var settings = Object.FindFirstObjectByType<GameSettingPanel>();
                settings.OnGenerateParamInputFieldSubmit("1");
                settings.OnMaxEnemyGenerateCountInputFieldSubmit("150");
                settings.OnMaxEnemyCountInputFieldSubmit("150");
                settings.OnHpAddParamInputFieldSubmit("0.01");
                InvokeButton(settings, "OnBtnEnterGame");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" &&
                    Object.FindFirstObjectByType<GamePanel>() && AudioKit.Music.IsPlaying, "settings -> Gaming / BGM");
                Assert.NotNull(GameObject.Find("Virtual Camera").GetComponent<AudioMusicPlayer>());
                var gameMusic = AudioKit.Music;
                module.LoadController<GameController>().SetGameState(round == 0 ? GameState.Start : GameState.GameOver);
                if (round == 0)
                {
                    yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Start" &&
                        Object.FindFirstObjectByType<StartPanel>() && AudioKit.Music.IsPlaying, "Gaming -> menu");
                    Assert.IsFalse(gameMusic.IsValid);
                    InvokeButton(Object.FindFirstObjectByType<StartPanel>(), "OnBtnStartClicked");
                    yield return WaitUntil(() => Object.FindFirstObjectByType<GameSettingPanel>(), "menu -> settings again");
                }
                else
                {
                    yield return WaitUntil(() => Object.FindFirstObjectByType<GameOverPanel>(), "Gaming -> game over");
                    Assert.IsFalse(gameMusic.IsValid);
                    Assert.IsFalse(AudioKit.Music.IsValid);
                }
            }
        }
        finally
        {
            FSMController.RemoveFSM("GameState");
            AudioKit.Shutdown();
            Time.timeScale = timeScale;
            Application.targetFrameRate = frameRate;
            if (hadMusic) PlayerPrefs.SetInt(musicKey, musicValue); else PlayerPrefs.DeleteKey(musicKey);
            if (hadVolume) PlayerPrefs.SetFloat(volumeKey, volumeValue); else PlayerPrefs.DeleteKey(volumeKey);
            if (hadLegacy) PlayerPrefs.SetFloat(legacyKey, legacyValue); else PlayerPrefs.DeleteKey(legacyKey);
        }
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator ReleaseOwnedModuleAfterFsmExit()
    {
        if (!mOwnsSession) yield break;
        FSMController.RemoveFSM("GameState");
        // FSM OnDestroy exits its state, which must precede disposal of the state's module/views.
        yield return null;
        if (ModuleManager.GetModule<MainModule>() != null) ModuleManager.CloseModule<MainModule>();
        AudioKit.Shutdown();
        Time.timeScale = mTimeScale;
        Application.targetFrameRate = mFrameRate;
        mOwnsSession = false;
        yield return null;
    }

    private static IEnumerator WaitUntil(Func<bool> ready, string stage)
    {
        var deadline = Time.realtimeSinceStartup + 25f;
        while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsTrue(ready(), "Timed out: " + stage);
    }
    private static void InvokeButton(Component panel, string method)
    {
        foreach (var button in panel.GetComponentsInChildren<Button>(true))
        {
            for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == panel && button.onClick.GetPersistentMethodName(i) == method)
                {
                    button.onClick.Invoke();
                    return;
                }
            }
        }
        Assert.Fail("Missing real button event: " + method);
    }
}
