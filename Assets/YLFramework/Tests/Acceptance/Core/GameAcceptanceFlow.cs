using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AudioKit = YLFramework.Audio.AudioKit;
using Object = UnityEngine.Object;
using YLFramework.UI;

namespace YLFramework.Core.Tests
{
    /// <summary>仅在验收 Player 的 -ui34-game 模式使用原 Main/资源链，不替换游戏业务。</summary>
    internal static class GameAcceptanceFlow
    {
        internal static IEnumerator Run(GameObject colorPrefab)
        {
            var floatKeys = new[] { "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_SOUND_VOLUME", "AudioConfigVolume:Music" };
            var intKeys = new[] { "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_SOUND_ON" };
            var exists = new bool[floatKeys.Length + intKeys.Length];
            var values = new float[exists.Length];
            for (int i = 0; i < floatKeys.Length; i++)
            {
                exists[i] = PlayerPrefs.HasKey(floatKeys[i]);
                values[i] = PlayerPrefs.GetFloat(floatKeys[i]);
            }
            for (int i = 0; i < intKeys.Length; i++)
            {
                int n = floatKeys.Length + i;
                exists[n] = PlayerPrefs.HasKey(intKeys[i]);
                values[n] = PlayerPrefs.GetInt(intKeys[i]);
            }
            float scale = Time.timeScale;
            int frameRate = Application.targetFrameRate;
            OverlayModule uiOwner = null;
            GameObject ownedRoot = null;
            Check(XFGameFramework.ModuleManager.GetModule<MainModule>() == null && XFFSM.FSMController.GetFSM("GameState") == null,
                "owns fresh original game module and FSM session");
            try
            {
                AudioKit.Settings.IsMusicOn.Value = true;
                AudioKit.Settings.IsSoundOn.Value = true;
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                var wait = Wait(() => Object.FindObjectOfType<StartPanel>() && AudioKit.Music.IsPlaying, "Main -> Start/BGM");
                while (wait.MoveNext())
                {
                    yield return wait.Current;
                }

                var module = XFGameFramework.ModuleManager.GetModule<MainModule>();
                var system = EventSystem.current;
                Check(module != null && system && system.currentInputModule is StandaloneInputModule, "original XF module and Legacy input unchanged");
                var menu = Object.FindObjectOfType<StartPanel>();
                int soundCount = AudioKit.SoundCount;
                Button(menu, "OnBtnSetting");
                Check(AudioKit.SoundCount > soundCount, "real menu button retains YL click audio");
                var setting = Object.FindObjectOfType<SettingPanel>();
                Check(setting, "menu setting panel loaded through original XF/XFAB");
                foreach (var slider in setting.GetComponentsInChildren<Slider>(true))
                {
                    for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
                    {
                        if (slider.onValueChanged.GetPersistentTarget(i) == setting)
                        {
                            var method = slider.onValueChanged.GetPersistentMethodName(i);
                            slider.value = method == "OnSliderMusicValueChange" ? 0.37f : 0.61f;
                        }
                    }
                }

                Check(Mathf.Abs(AudioKit.Settings.MusicVolume.Value - 0.37f) < 0.001f &&
                    Mathf.Abs(AudioKit.Settings.SoundVolume.Value - 0.61f) < 0.001f, "persistent setting sliders update framework audio");
                Button(setting, "Close");
                var music = AudioKit.Music;
                uiOwner = new OverlayModule();
                yield return ModuleManager.StartUpModule(uiOwner);
                var rootPrefab = Resources.Load<GameObject>("YLFramework/Core/Canvas");
                Check(rootPrefab && rootPrefab.GetComponent<UIRootContext>(), "migrated lightweight root is available");
                ownedRoot = Object.Instantiate(rootPrefab);
                ownedRoot.name = "YLUI.Coexist.OwnedRoot";
                var window = uiOwner.LoadPanel<ColorSelectorWindow>(colorPrefab, ColorSelectorWindow.Arguments(Color.cyan), ownedRoot.GetComponent<UIRootContext>());
                Check(EventSystem.current == system && music.IsPlaying, "new YL UI borrows original EventSystem without disturbing BGM");
                uiOwner.Close();
                Object.Destroy(ownedRoot);
                ownedRoot = null;
                yield return null;
                yield return null;
                Check(EventSystem.current == system && system.currentInputModule is StandaloneInputModule && music.IsPlaying,
                    "YL root exit preserves borrowed XF input and audio");
                for (int round = 0; round < 2; round++)
                {
                    Button(Object.FindObjectOfType<StartPanel>(), "OnBtnStartClicked");
                    wait = Wait(() => Object.FindObjectOfType<GameSettingPanel>(), "menu -> game settings");
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    var options = Object.FindObjectOfType<GameSettingPanel>();
                    options.generateParamInputField.onEndEdit.Invoke("1");
                    options.maxEnemyGenerateCountInputField.onEndEdit.Invoke("150");
                    options.maxEnemyCountInputField.onEndEdit.Invoke("150");
                    options.hpAddParamInputField.onEndEdit.Invoke("0.01");
                    yield return null;
                    Button(options, "OnBtnEnterGame");
                    wait = Wait(() => SceneManager.GetActiveScene().name == "Game" && Object.FindObjectOfType<GamePanel>() && AudioKit.Music.IsPlaying, "settings -> game/BGM");
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    var gameMusic = AudioKit.Music;
                    Button(Object.FindObjectOfType<GamePanel>(), "OnBtnPauseClicked");
                    var pause = Object.FindObjectOfType<PausePanel>();
                    Check(pause && Time.timeScale == 0, "real pause button and paused audio/UI lifetime");
                    Button(pause, "OnBtnMenuClick");
                    wait = Wait(() => SceneManager.GetActiveScene().name == "Start" && Object.FindObjectOfType<StartPanel>() && AudioKit.Music.IsPlaying, "pause -> menu/BGM");
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    Check(!gameMusic.IsValid, "old scene music handle invalidated");
                }
            }
            finally
            {
                try
                {
                    uiOwner?.Close();
                }
                finally
                {
                    if (ownedRoot)
                    {
                        Object.Destroy(ownedRoot);
                    }

                    XFFSM.FSMController.RemoveFSM("GameState");
                    AudioKit.Shutdown();
                    Time.timeScale = scale;
                    Application.targetFrameRate = frameRate;
                    for (int i = 0; i < floatKeys.Length; i++)
                    {
                        if (exists[i])
                        {
                            PlayerPrefs.SetFloat(floatKeys[i], values[i]);
                        }
                        else
                        {
                            PlayerPrefs.DeleteKey(floatKeys[i]);
                        }
                    }
                    for (int i = 0; i < intKeys.Length; i++)
                    {
                        int n = floatKeys.Length + i;
                        if (exists[n])
                        {
                            PlayerPrefs.SetInt(intKeys[i], (int)values[n]);
                        }
                        else
                        {
                            PlayerPrefs.DeleteKey(intKeys[i]);
                        }
                    }
                }
            }
        }

        private static IEnumerator Wait(Func<bool> ready, string stage)
        {
            float deadline = Time.realtimeSinceStartup + 25;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Check(ready(), stage);
        }

        private static void Button(Component panel, string method)
        {
            Check(panel, "button panel exists: " + method);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == panel && button.onClick.GetPersistentMethodName(i) == method)
                    {
                        Check(button.IsInteractable(), "button interactable: " + method);
                        button.onClick.Invoke();
                        return;
                    }
                }
            }

            throw new InvalidOperationException("Missing real game button: " + method);
        }

        private static void Check(bool condition, string contract)
        {
            if (!condition)
            {
                throw new InvalidOperationException("UI34 game contract: " + contract);
            }

            Debug.Log("UI34 GAME_CHECK " + contract);
        }

        private sealed class OverlayModule : Module
        {
            public override void OnStart()
            {
            }
        }
    }
}
