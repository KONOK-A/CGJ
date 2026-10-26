using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XFGameFramework;
using XFFSM;
using XFGameFramework.Overlap;
using Object = UnityEngine.Object;

public class GameAudioFlowPlayModeTests
{
    private bool mOwnsSession;
    private float mTimeScale;
    private int mFrameRate;
    private GameAudioPreferences mPreferences;
    private static AudioSource Music => AudioPlayer.GetAuidoController("Music")?.AudioSource;
    private static bool MusicPlaying => Music && Music.isPlaying && Music.clip;
    [UnityTest]
    public IEnumerator MainStartSettingsGameExitAndReentryUseFrameworkAudio()
    {
        Assert.IsNull(ModuleManager.GetModule<MainModule>(), "The fixture must own its module session.");
        Assert.IsNull(FSMController.GetFSM("GameState"), "The fixture must own its FSM session.");
        mPreferences = new GameAudioPreferences();
        mPreferences.UseAudibleDefaults();
        var timeScale = Time.timeScale;
        var frameRate = Application.targetFrameRate;
        mTimeScale = timeScale;
        mFrameRate = frameRate;
        mOwnsSession = true;
        try
        {
            Time.timeScale = 1;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return WaitUntil(() => Object.FindFirstObjectByType<StartPanel>() && MusicPlaying, "Main -> StartPanel / BGM");
            var module = ModuleManager.GetModule<MainModule>();
            Assert.NotNull(module);
            InvokeButton(Object.FindFirstObjectByType<StartPanel>(), "OnBtnSetting");
            yield return WaitUntil(() => Object.FindFirstObjectByType<SettingPanel>(), "menu -> audio settings");
            var audioSettings = Object.FindFirstObjectByType<SettingPanel>();
            foreach (var slider in audioSettings.GetComponentsInChildren<Slider>(true)) slider.value = .35f;
            Assert.AreEqual(.35f, module.LoadController<AudioController>().MusicVolume);
            Assert.AreEqual(.35f, module.LoadController<AudioController>().SoundVolume);
            audioSettings.Close();
            InvokeButton(Object.FindFirstObjectByType<StartPanel>(), "OnBtnStartClicked");
            yield return WaitUntil(() => Object.FindFirstObjectByType<GameSettingPanel>(), "Start -> game settings");
            Assert.IsFalse(MusicPlaying);

            for (var round = 0; round < 2; round++)
            {
                var settings = Object.FindFirstObjectByType<GameSettingPanel>();
                settings.OnGenerateParamInputFieldSubmit("1");
                settings.OnMaxEnemyGenerateCountInputFieldSubmit("150");
                settings.OnMaxEnemyCountInputFieldSubmit("150");
                settings.OnHpAddParamInputFieldSubmit("0.01");
                InvokeButton(settings, "OnBtnEnterGame");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Game" &&
                    Object.FindFirstObjectByType<GamePanel>() && MusicPlaying, "settings -> Gaming / BGM");
                Assert.NotNull(GameObject.Find("Virtual Camera").GetComponent<AudioPlayer>());
                var gameMusic = Music.clip;
                yield return VerifyOverlapQueriesAdvance();
                if (round == 0)
                {
                    yield return VerifyWeaponShots(module);
                    InvokeButton(Object.FindFirstObjectByType<GamePanel>(), "OnBtnPauseClicked");
                    yield return WaitUntil(() => Object.FindFirstObjectByType<PausePanel>(), "Gaming -> pause");
                    Assert.AreEqual(0, Time.timeScale);
                    Assert.IsTrue(MusicPlaying, "Native XF music is not paused by Time.timeScale.");
                    InvokeButton(Object.FindFirstObjectByType<PausePanel>(), "OnBtnMenuClick");
                    yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Start" &&
                        Object.FindFirstObjectByType<StartPanel>() && MusicPlaying, "Gaming -> menu");
                    Assert.AreNotSame(gameMusic, Music.clip, "Old scene destruction must not stop the replacement menu owner.");
                    yield return null;
                    Assert.IsTrue(MusicPlaying);
                    InvokeButton(Object.FindFirstObjectByType<StartPanel>(), "OnBtnStartClicked");
                    yield return WaitUntil(() => Object.FindFirstObjectByType<GameSettingPanel>(), "menu -> settings again");
                }
                else
                {
                    var combat = module.LoadController<AudioController>();
                    for (var i = 0; i < 48; i++) AudioPlayer.Stop($"MainModule.Combat.{i:00}");
                    var admit = typeof(AudioController).GetMethod("TryPlayCombatSound", BindingFlags.Instance | BindingFlags.NonPublic);
                    var names = new[] { "机炮3", "机炮4", "霰弹1", "火箭发射", "机体爆炸1", "火箭爆炸1" };
                    var now = Time.unscaledTimeAsDouble + 10;
                    foreach (var group in new[] { (Group: AudioController.SoundGroup.Explosion, Count: 16),
                        (Group: AudioController.SoundGroup.PlayerWeapon, Count: 16), (Group: AudioController.SoundGroup.EnemyWeapon, Count: 12),
                        (Group: AudioController.SoundGroup.Feedback, Count: 4) })
                        for (var i = 0; i < group.Count; i++)
                            Assert.IsTrue((bool)admit.Invoke(combat, new object[] { names[i % names.Length], AudioController.SoundPriority.Normal,
                                now + i * .1, group.Group, i + 1 }));
                    Assert.AreEqual(48, Enumerable.Range(0, 48).Count(i => AudioPlayer.GetAuidoController($"MainModule.Combat.{i:00}").AudioSource.isPlaying));
                    var sound = AudioPlayer.GetAuidoController("Sound");
                    var records = (IEnumerable)typeof(XFGameFramework.AudioController).GetField("AudioDatas", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sound);
                    var before = records.Cast<object>().ToArray();
                    var cue = XFABManager.AssetBundleManager.LoadAsset<AudioClip>(module.ModuleName, "挑战失败");
                    Assert.IsNotNull(cue);
                    module.LoadController<GameController>().SetGameState(GameState.GameOver);
                    yield return WaitUntil(() => Object.FindFirstObjectByType<GameOverPanel>(), "Gaming -> game over");
                    Assert.IsFalse(MusicPlaying);
                    Assert.IsTrue(sound.AudioSource.isPlaying, "Failure feedback must bypass the saturated combat pool.");
                    Assert.IsTrue(records.Cast<object>().Except(before).Any(record =>
                        ReferenceEquals(record.GetType().GetField("clip").GetValue(record), cue)), "Real GameOver must submit the actual failure clip.");
                }
            }
        }
        finally
        {
            FSMController.RemoveFSM("GameState");
            Time.timeScale = timeScale;
            Application.targetFrameRate = frameRate;
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
        mPreferences?.Dispose();
        mPreferences = null;
        Time.timeScale = mTimeScale;
        Application.targetFrameRate = mFrameRate;
        mOwnsSession = false;
        yield return null;
    }

    private static IEnumerator VerifyOverlapQueriesAdvance()
    {
        var deadline = Time.realtimeSinceStartup + 10;
        OverlapBase2D[] overlaps;
        do
        {
            overlaps = Object.FindObjectsByType<OverlapBase2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x && x.isActiveAndEnabled).ToArray();
            if (overlaps.Length == 0) yield return null;
        } while (overlaps.Length == 0 && Time.realtimeSinceStartup < deadline);
        Assert.IsNotEmpty(overlaps);
        Assert.That(overlaps.Any(x => x is OverlapCircle2D), Is.True);
        foreach (var overlap in overlaps)
            Assert.IsTrue(overlap.Running, "XF Overlap must remain automatic in the real game.");
        // 高频实例的 timer 每帧可能都归零；用隔离层真实命中证明自动物理查询，而不是比较计时器。
        var owner = new GameObject("Game XF overlap probe");
        var target = new GameObject("Game XF overlap target");
        try
        {
            owner.transform.position = target.transform.position = new Vector3(-10000, -10000, 0);
            target.layer = 31;
            var collider = target.AddComponent<CircleCollider2D>();
            var probe = owner.AddComponent<OverlapCircle2D>();
            probe.LayerMask = 1 << 31;
            probe.Radius = 1;
            probe.DetectionNumber = 100;
            probe.onColliderEnter = new OverlapEvent2D();
            var hits = 0;
            probe.onColliderEnter.AddListener(hit => { if (hit.collider == collider) hits++; });
            Physics2D.SyncTransforms();
            yield return WaitUntil(() => hits > 0, "automatic XF physics overlap");
        }
        finally
        {
            Object.Destroy(owner);
            Object.Destroy(target);
        }
    }

    private static IEnumerator VerifyWeaponShots(MainModule module)
    {
        var timeScale = Time.timeScale;
        var liveWeapons = Object.FindObjectsByType<WeaponBase>(FindObjectsSortMode.None).Where(w => w.isActiveAndEnabled).ToArray();
        var liveBullets = Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        MachineGun player = null, secondPlayer = null;
        Enemy_MachineGun enemy = null;
        try
        {
            Time.timeScale = 0;
            foreach (var weapon in liveWeapons) weapon.enabled = false;
            player = module.LoadView<MachineGun>("MachineGun");
            secondPlayer = module.LoadView<MachineGun>("MachineGun");
            player.canAttack = secondPlayer.canAttack = false;
            var target = Object.FindFirstObjectByType<SnakeHead>().transform;
            enemy = module.LoadView<Enemy_MachineGun>("Enemy_MachineGun", null, 1f, target, false, (Vector2)target.position);
            enemy.enabled = enemy.fsmController.enabled = false;
            enemy.weapon.canAttack = false;
            enemy.weapon.enabled = true;
            // 让真实 XF Start/LoadOnStart 初始化嵌套武器，不手动调用 OnLoaded。
            yield return null;
            Assert.AreSame(module, enemy.weapon.Module);
            enemy.weapon.enabled = false;
            var shoot = typeof(MachineGun).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            for (var round = 0; round < 2; round++)
            {
                yield return new WaitForSecondsRealtime(.09f);
                for (var i = 0; i < 48; i++) AudioPlayer.Stop($"MainModule.Combat.{i:00}");
                shoot.Invoke(enemy.weapon, new object[] { Vector2.up });
                shoot.Invoke(player, new object[] { Vector2.up });
                shoot.Invoke(secondPlayer, new object[] { Vector2.up });
                shoot.Invoke(enemy.weapon, new object[] { Vector2.up });
                Assert.AreEqual(2, Enumerable.Range(16, 16).Count(i => AudioPlayer.GetAuidoController($"MainModule.Combat.{i:00}").AudioSource.isPlaying),
                    "Real same-clip player weapons need separate emitter cooldowns.");
                Assert.AreEqual(1, Enumerable.Range(32, 12).Count(i => AudioPlayer.GetAuidoController($"MainModule.Combat.{i:00}").AudioSource.isPlaying),
                    "Real enemy shots must use the enemy group and aggregate repeats.");
                Assert.IsTrue(Enumerable.Range(0, 16).All(i => !AudioPlayer.GetAuidoController($"MainModule.Combat.{i:00}").AudioSource.isPlaying),
                    "Shoot must not fall back to the default explosion group.");
                foreach (var slot in new[] { 16, 17, 32 })
                    Assert.AreEqual("机炮3", AudioPlayer.GetAuidoController($"MainModule.Combat.{slot:00}").AudioSource.clip.name);
                if (round == 0)
                {
                    enemy.Close();
                    var reused = module.LoadView<Enemy_MachineGun>("Enemy_MachineGun", null, 1f, target, false, (Vector2)target.position);
                    Assert.AreSame(enemy, reused, "The second shot pass must cover the reused enemy root.");
                    enemy.weapon.enabled = false;
                }
            }
        }
        finally
        {
            foreach (var bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Except(liveBullets)) bullet.Close();
            foreach (var view in new View[] { player, secondPlayer, enemy })
                if (view) { view.Close(); Object.Destroy(view.gameObject); }
            foreach (var weapon in liveWeapons) if (weapon) weapon.enabled = true;
            Time.timeScale = timeScale;
        }
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
