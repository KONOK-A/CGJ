using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YLFramework.Audio;
using AudioKit = YLFramework.Audio.AudioKit;
using Object = UnityEngine.Object;

namespace YLFramework.Core.Tests
{
    /// <summary>阶段 4.5 原 Main/XFAB 音频验收窄探针；不替换游戏业务或修改资源。</summary>
    internal static class AudioGameAcceptanceFlow
    {
        internal static IEnumerator Run()
        {
            Check(XFGameFramework.ModuleManager.GetModule<MainModule>() == null &&
                XFFSM.FSMController.GetFSM("GameState") == null && !AudioKit.Music.IsValid &&
                !AudioKit.Voice.IsValid && AudioKit.SoundCount == 0, "OWNERSHIP", "fresh game/FSM/audio session");
            var floatKeys = new[] { "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_SOUND_VOLUME",
                "KEY_AUDIO_MANAGER_VOICE_VOLUME", "AudioConfigVolume:Music" };
            var intKeys = new[] { "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_SOUND_ON", "KEY_AUDIO_MANAGER_VOICE_ON" };
            var exists = new bool[floatKeys.Length + intKeys.Length];
            var values = new float[floatKeys.Length];
            var switches = new int[intKeys.Length];
            for (int i = 0; i < floatKeys.Length; i++)
            {
                exists[i] = PlayerPrefs.HasKey(floatKeys[i]);
                values[i] = PlayerPrefs.GetFloat(floatKeys[i]);
            }
            for (int i = 0; i < intKeys.Length; i++)
            {
                exists[floatKeys.Length + i] = PlayerPrefs.HasKey(intKeys[i]);
                switches[i] = PlayerPrefs.GetInt(intKeys[i]);
            }
            float scale = Time.timeScale;
            int frameRate = Application.targetFrameRate;
            Action restore = () =>
            {
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
                    if (exists[floatKeys.Length + i])
                    {
                        PlayerPrefs.SetInt(intKeys[i], switches[i]);
                    }
                    else
                    {
                        PlayerPrefs.DeleteKey(intKeys[i]);
                    }
                }
            };
            Exception failure = null;
            Exception cleanupFailure = null;
            bool cleanupDone = false;
            var flow = Session();
            try
            {
                // 展平子流程，使异常在恢复和下一帧模块清理之后再交给调用方。
                while (true)
                {
                    bool next;
                    try
                    {
                        next = flow.MoveNext();
                    }
                    catch (Exception error)
                    {
                        failure = error is InvalidOperationException ? error :
                            new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [GAME_EXCEPTION] original game flow", error);
                        break;
                    }
                    if (!next)
                    {
                        break;
                    }

                    yield return flow.Current;
                }
            }
            finally
            {
                try
                {
                    (flow as IDisposable)?.Dispose();
                    XFFSM.FSMController.RemoveFSM("GameState");
                }
                finally
                {
                    try
                    {
                        AudioKit.Shutdown();
                    }
                    finally
                    {
                        restore();
                        var ownedModule = XFGameFramework.ModuleManager.GetModule<MainModule>();
                        // FSM 的 OnDestroy 才退出状态，必须先让出一帧再关闭状态使用的 Module/Panel。
                        var cleanup = YLFramework.Async.CoroutineStarter.Start(ReleaseModule(ownedModule, restore,
                            error =>
                            {
                                cleanupFailure = error;
                                cleanupDone = true;
                            }));
                        Check(cleanup != null, "CLEANUP", "deferred module cleanup scheduled");
                    }
                }
            }
            while (!cleanupDone)
            {
                yield return null;
            }

            if (failure != null || cleanupFailure != null)
            {
                if (failure != null && cleanupFailure != null)
                {
                    throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [GAME_AND_CLEANUP]",
                        new AggregateException(failure, cleanupFailure));
                }

                throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL " +
                    (failure != null ? "[GAME_FLOW]" : "[CLEANUP]"), failure ?? cleanupFailure);
            }
            Check(true, "COMPLETE", "original game audio flow and preference restoration completed");
        }

        private static IEnumerator Session()
        {
            AudioKit.Shutdown();
            Time.timeScale = 1f;
            AudioKit.Settings.IsOn = true;
            AudioKit.Settings.MusicVolume.Value = 0.83f;
            AudioKit.Settings.SoundVolume.Value = 0.79f;
            PlayerPrefs.SetFloat("AudioConfigVolume:Music", 0.42f);
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            var wait = Wait(() => SceneManager.GetActiveScene().name == "Start" &&
                Object.FindObjectOfType<StartPanel>() && AudioKit.Music.IsPlaying, "Main -> first Start/BGM");
            while (wait.MoveNext())
            {
                yield return wait.Current;
            }

            var mainModule = XFGameFramework.ModuleManager.GetModule<MainModule>();
            Check(mainModule != null && XFFSM.FSMController.GetFSM("GameState") != null,
                "GAME_FLOW", "original Main module/FSM started");
            var audio = mainModule.LoadController<AudioController>();
            Check(!PlayerPrefs.HasKey("AudioConfigVolume:Music") && Mathf.Abs(audio.MusicVolume - 0.42f) < 0.001f &&
                Mathf.Abs(AudioKit.Settings.MusicVolume.Value - 0.42f) < 0.001f &&
                Mathf.Abs(PlayerPrefs.GetFloat("KEY_AUDIO_MANAGER_MUSIC_VOLUME") - 0.42f) < 0.001f,
                "MIGRATION", "original AudioController.OnInit migrated legacy 0.42 and deleted old key");
            Check(AudioKit.PlaySoundMode == AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames &&
                AudioKit.GlobalFrameCountForIgnoreSameSound == 10, "AUDIO_STATE", "original global deduplication unchanged");
            var system = EventSystem.current;
            Check(system && system.currentInputModule is StandaloneInputModule,
                "INPUT", "original Legacy EventSystem retained; no UI overlay");
            var menu = Object.FindObjectOfType<StartPanel>();
            Check(menu.GetComponentInChildren<AudioMusicPlayer>(true), "RESOURCE_WIRING", "first menu BGM component present");
            var menuMusic = AudioKit.Music;
            var output = Output("menu_bgm", "BGM");
            while (output.MoveNext())
            {
                yield return output.Current;
            }

            var settingsButton = FindButton(menu, "OnBtnSetting");
            bool wiredEmitter = false;
            for (int i = 0; i < settingsButton.onClick.GetPersistentEventCount(); i++)
            {
                wiredEmitter |= settingsButton.onClick.GetPersistentTarget(i) is AudioEmitter &&
                    settingsButton.onClick.GetPersistentMethodName(i) == "Play";
            }

            Check(wiredEmitter, "RESOURCE_WIRING", "actual setting button has persistent AudioEmitter.Play");
            var beforeClick = AudioKit.GetPlayingSounds();
            int clickCount = AudioKit.SoundCount;
            Click(settingsButton);
            Click(settingsButton);
            Check(AudioKit.SoundCount == clickCount + 2 && NewPlayingCount(beforeClick) == 2,
                "CLICK_EVERYONE", "two full real Button pointer-click events overlap in the same frame despite global deduplication");
            output = Output("menu_click", "BGM+click; mixed output, not isolated click attribution");
            while (output.MoveNext())
            {
                yield return output.Current;
            }

            var settingPanels = Object.FindObjectsOfType<SettingPanel>();
            Check(settingPanels.Length > 0, "RESOURCE_WIRING", "SettingPanel loaded by original XF/XFAB");
            var setting = settingPanels[0];
            SliderValue(setting, "OnSliderMusicValueChange", 0.37f);
            SliderValue(setting, "OnSliderSoundValueChange", 0.61f);
            Check(Mathf.Abs(audio.MusicVolume - 0.37f) < 0.001f && Mathf.Abs(audio.SoundVolume - 0.61f) < 0.001f &&
                Mathf.Abs(PlayerPrefs.GetFloat("KEY_AUDIO_MANAGER_MUSIC_VOLUME") - 0.37f) < 0.001f &&
                Mathf.Abs(PlayerPrefs.GetFloat("KEY_AUDIO_MANAGER_SOUND_VOLUME") - 0.61f) < 0.001f,
                "PERSISTENT_SLIDERS", "real persistent Slider listeners update Controller/settings/PlayerPrefs");
            // 两次完整按钮事件可能各创建一个窗口，全部通过真实关闭按钮退出。
            foreach (var settingPanel in settingPanels)
            {
                Click(FindButton(settingPanel, "Close"));
            }

            for (int round = 0; round < 2; round++)
            {
                Check(AudioKit.Music.IsPlaying, "AUDIO_STATE", "menu BGM before round " + (round + 1));
                menuMusic = AudioKit.Music;
                Click(FindButton(Object.FindObjectOfType<StartPanel>(), "OnBtnStartClicked"));
                wait = Wait(() => Object.FindObjectOfType<GameSettingPanel>(), "menu -> settings round " + (round + 1));
                while (wait.MoveNext())
                {
                    yield return wait.Current;
                }

                Check(!menuMusic.IsValid && !AudioKit.Music.IsValid, "AUDIO_STATE", "start button stopped old menu BGM");
                var options = Object.FindObjectOfType<GameSettingPanel>();
                options.generateParamInputField.onEndEdit.Invoke("1");
                options.maxEnemyGenerateCountInputField.onEndEdit.Invoke("150");
                options.maxEnemyCountInputField.onEndEdit.Invoke("150");
                options.hpAddParamInputField.onEndEdit.Invoke(0.01f.ToString(System.Globalization.CultureInfo.CurrentCulture));
                yield return null;
                Click(FindButton(options, "OnBtnEnterGame"));
                wait = Wait(() => SceneManager.GetActiveScene().name == "Game" && Object.FindObjectOfType<GamePanel>() &&
                    AudioKit.Music.IsPlaying, "settings -> Game/BGM round " + (round + 1));
                while (wait.MoveNext())
                {
                    yield return wait.Current;
                }

                var camera = GameObject.Find("Virtual Camera");
                Check(camera && camera.GetComponent<AudioMusicPlayer>(), "RESOURCE_WIRING", "original game camera AudioMusicPlayer");
                Check(Mathf.Abs(audio.MusicVolume - 0.37f) < 0.001f && Mathf.Abs(audio.SoundVolume - 0.61f) < 0.001f,
                    "PERSISTENT_SLIDERS", "configured volume retained in game round " + (round + 1));
                var gameMusic = AudioKit.Music;
                if (round == 0)
                {
                    Click(FindButton(Object.FindObjectOfType<GamePanel>(), "OnBtnPauseClicked"));
                    var pause = Object.FindObjectOfType<PausePanel>();
                    Check(pause && Time.timeScale == 0f && gameMusic.IsPlaying && !gameMusic.IsPaused,
                        "PAUSE_BGM", "real Pause button freezes game but not default BGM");
                    float initialTime = gameMusic.Time;
                    wait = Wait(() => gameMusic.IsPlaying && Mathf.Abs(gameMusic.Time - initialTime) > 0.01f,
                        "paused BGM clip time advances", 3f);
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    Check(Time.timeScale == 0f && !gameMusic.IsPaused, "PAUSE_BGM", "clip time advanced " + initialTime +
                        " -> " + gameMusic.Time + " while timeScale stayed zero");
                    output = Output("pause_bgm", "BGM continues at timeScale=0");
                    while (output.MoveNext())
                    {
                        yield return output.Current;
                    }

                    // 冻结游戏后跨过去重窗口，避免原武器随机发声与同 Clip 探针竞争；不改业务去重策略。
                    var assets = new[] { "机炮4", "机炮3", "霰弹1", "火箭发射", "火箭爆炸1", "机体爆炸1", "挑战失败" };
                    foreach (var asset in assets)
                    {
                        int frames = Mathf.Max(AudioKit.GlobalFrameCountForIgnoreSameSound,
                            AudioKit.SoundFrameCountForIgnoreSameSound) + 1;
                        for (int i = 0; i < frames; i++)
                        {
                            yield return null;
                        }

                        var clip = LoadClip(mainModule.ProjectName, asset);
                        var before = AudioKit.GetPlayingSounds();
                        int total = AudioKit.SoundCount;
                        int perClip = AudioKit.GetSoundCount(clip);
                        mainModule.LoadController<AudioController>().PlaySound(asset);
                        Check(AudioKit.SoundCount == total + 1 && AudioKit.GetSoundCount(clip) == perClip + 1 &&
                            NewPlayingCount(before) == 1, "CONTROLLER_SOUND", "XFAB/Controller.PlaySound " + asset +
                            " total=" + AudioKit.SoundCount + " clipCount=" + AudioKit.GetSoundCount(clip) + " newHandle.IsPlaying=true; game paused");
                        output = Output("battle_probe:" + asset, "BGM+Controller sound; paused game, mixed output");
                        while (output.MoveNext())
                        {
                            yield return output.Current;
                        }

                        Check(Time.timeScale == 0f && gameMusic.IsPlaying, "PAUSE_BGM", "default BGM survives controller probe " + asset);
                    }
                    Click(FindButton(pause, "OnBtnMenuClick"));
                    wait = Wait(() => SceneManager.GetActiveScene().name == "Start" && Object.FindObjectOfType<StartPanel>() &&
                        AudioKit.Music.IsPlaying, "pause -> menu/BGM");
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    Check(!gameMusic.IsValid && Time.timeScale == 1f, "REENTRY", "old game BGM invalidated and game time restored at menu");
                }
                else
                {
                    var failureClip = LoadClip(mainModule.ProjectName, "挑战失败");
                    int beforeFailure = AudioKit.GetSoundCount(failureClip);
                    var before = AudioKit.GetPlayingSounds();
                    mainModule.LoadController<GameController>().SetGameState(GameState.GameOver);
                    wait = Wait(() => Object.FindObjectOfType<GameOverPanel>(), "GameController -> GameOverPanel");
                    while (wait.MoveNext())
                    {
                        yield return wait.Current;
                    }

                    Check(!gameMusic.IsValid && !AudioKit.Music.IsValid && Time.timeScale == 0f,
                        "GAME_OVER", "original GameOver stops BGM and freezes game");
                    Check(AudioKit.GetSoundCount(failureClip) > beforeFailure && NewPlayingCount(before) > 0,
                        "GAME_OVER_SOUND", "real settlement panel plays XFAB 挑战失败 with a new playing handle");
                    output = Output("game_over", "settlement sound, no BGM; other surviving one-shots may be mixed");
                    while (output.MoveNext())
                    {
                        yield return output.Current;
                    }

                    Check(!AudioKit.Music.IsValid, "GAME_OVER", "no BGM after settlement output sampling");
                }
            }
        }

        private static IEnumerator ReleaseModule(MainModule owned, Action restore, Action<Exception> finished)
        {
            yield return null;
            Exception failure = null;
            try
            {
                var current = XFGameFramework.ModuleManager.GetModule<MainModule>();
                Check(ReferenceEquals(current, owned), "OWNERSHIP", "deferred cleanup still owns original module");
                if (owned != null)
                {
                    XFGameFramework.ModuleManager.CloseModule<MainModule>();
                }
            }
            catch (Exception error)
            {
                failure = error;
            }
            // Panel OnDisable/销毁也可能修改 timeScale，下一帧再恢复一次最终快照。
            yield return null;
            try
            {
                try
                {
                    AudioKit.Shutdown();
                }
                finally
                {
                    restore();
                }
                Check(XFGameFramework.ModuleManager.GetModule<MainModule>() == null &&
                    XFFSM.FSMController.GetFSM("GameState") == null, "CLEANUP", "owned FSM/module released after state exit");
            }
            catch (Exception error)
            {
                failure = failure == null ? error : new AggregateException(failure, error);
            }
            finished(failure);
        }

        private static AudioClip LoadClip(string project, string asset)
        {
            // 现有验收程序集没有 XFABManager 直接引用；只反射这个已核对的公开非泛型接口。
            var type = Type.GetType("XFABManager.AssetBundleManager, XFABManager");
            var method = type?.GetMethod("LoadAsset", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(string), typeof(string), typeof(Type) }, null);
            Check(method != null, "RESOURCE_XFAB", "public XFAB LoadAsset(string,string,Type) available");
            AudioClip clip;
            try
            {
                clip = method.Invoke(null, new object[] { project, asset, typeof(AudioClip) }) as AudioClip;
            }
            catch (TargetInvocationException error)
            {
                throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [RESOURCE_XFAB] " + project + "/" + asset +
                    "; existing bundle/profile only, no rebuild", error.InnerException ?? error);
            }
            Check(clip && clip.length > 0f, "RESOURCE_XFAB", "loaded AudioClip " + project + "/" + asset + "; no bundle rebuild");
            return clip;
        }

        private static int NewPlayingCount(AudioHandle[] before)
        {
            int count = 0;
            foreach (var handle in AudioKit.GetPlayingSounds())
            {
                if (Array.IndexOf(before, handle) < 0 && handle.IsPlaying)
                {
                    count++;
                }
            }

            return count;
        }

        private static Button FindButton(Component panel, string method)
        {
            Check(panel, "RESOURCE_WIRING", "button panel exists: " + method);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentTarget(i) == panel && button.onClick.GetPersistentMethodName(i) == method)
                    {
                        return button;
                    }
                }
            }

            throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [RESOURCE_WIRING] missing real button: " + method);
        }

        private static void Click(Button button)
        {
            Check(button && button.IsActive() && button.IsInteractable() && EventSystem.current,
                "INPUT", "real button active/interactable: " + (button ? button.name : "missing"));
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left
            }, ExecuteEvents.pointerClickHandler);
        }

        private static void SliderValue(SettingPanel panel, string method, float value)
        {
            foreach (var slider in panel.GetComponentsInChildren<Slider>(true))
            {
                for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
                {
                    if (slider.onValueChanged.GetPersistentTarget(i) == panel &&
                        slider.onValueChanged.GetPersistentMethodName(i) == method)
                    {
                        Check(slider.IsActive() && slider.IsInteractable(), "INPUT", "real Slider active/interactable: " + method);
                        slider.value = value;
                        Check(Mathf.Abs(slider.value - value) < 0.001f, "PERSISTENT_SLIDERS", method + "=" + value);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [RESOURCE_WIRING] missing persistent Slider: " + method);
        }

        private static IEnumerator Wait(Func<bool> ready, string stage, float seconds = 25f)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Check(ready(), "FLOW_TIMEOUT", stage + "; failed waits require resource/game-flow diagnosis, never auto rebuild bundles");
        }

        private static IEnumerator Output(string stage, string mix)
        {
            AudioAcceptanceEntry.Stage = "GAME " + stage;
            var listeners = Object.FindObjectsOfType<AudioListener>();
            Check(Array.Exists(listeners, listener => listener && listener.isActiveAndEnabled) &&
                !AudioListener.pause && AudioListener.volume > 0f, "OUTPUT_ENVIRONMENT", stage + " active unpaused non-muted listener");
            var samples = new float[1024];
            float deadline = Time.realtimeSinceStartup + 2f;
            float nextSample = Time.realtimeSinceStartup;
            int stable = 0;
            int frames = 0;
            double lastRms = 0;
            double minRms = double.MaxValue;
            while (stable < 3 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (Time.realtimeSinceStartup < nextSample)
                {
                    continue;
                }

                nextSample = Time.realtimeSinceStartup + 0.02f;
                AudioListener.GetOutputData(samples, 0);
                double sum = 0;
                foreach (float sample in samples)
                {
                    sum += (double)sample * sample;
                }

                lastRms = Math.Sqrt(sum / samples.Length);
                frames++;
                if (!double.IsNaN(lastRms) && !double.IsInfinity(lastRms) && lastRms > 0.000001)
                {
                    stable++;
                    minRms = Math.Min(minRms, lastRms);
                }
                else
                {
                    stable = 0;
                    minRms = double.MaxValue;
                }
            }
            Debug.Log("AUDIO45 GAME_OUTPUT stage=" + stage + " rms=" + lastRms.ToString("G6", System.Globalization.CultureInfo.InvariantCulture) +
                " stableMinRms=" + (stable > 0 ? minRms : 0).ToString("G6", System.Globalization.CultureInfo.InvariantCulture) +
                " stableFrames=" + stable + " sampledFrames=" + frames + " timeScale=" + Time.timeScale +
                " dspTime=" + AudioSettings.dspTime + " mix=" + mix + "; DSP evidence only, not audibility/listening assessment");
            Check(stable >= 3, "OUTPUT_SIGNAL", stage + " three distinct-frame nonzero RMS samples; audio-disabled/headless output is not a playback-contract failure");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-audio45-listen") >= 0)
            {
                yield return new WaitForSecondsRealtime(.8f);
            }
        }

        private static void Check(bool condition, string category, string contract)
        {
            if (!condition)
            {
                throw new InvalidOperationException("AUDIO45 GAME_CHECK FAIL [" + category + "] " + contract);
            }

            Debug.Log("AUDIO45 GAME_CHECK PASS [" + category + "] " + contract);
        }
    }
}
