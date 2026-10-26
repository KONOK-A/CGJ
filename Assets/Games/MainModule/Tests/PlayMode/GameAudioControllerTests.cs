using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using XFGameFramework;
using XFAudio = XFGameFramework.AudioPlayer;

public class GameAudioControllerTests
{
    private const string MigrationKey = "MainModule.Audio.XFPreferencesMigrated.v1";
    private static readonly string[] FloatKeys = { "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_SOUND_VOLUME", "AudioConfigVolume:Music", "AudioConfigVolume:Sound" };
    private static readonly string[] IntKeys = { "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_SOUND_ON", "AudioConfigMute:Music", "AudioConfigMute:Sound", MigrationKey };
    private static readonly string[] Sounds = { "机炮3", "机炮4", "霰弹1", "火箭发射", "机体爆炸1", "火箭爆炸1" };
    private bool[] hadFloats, hadInts;
    private float[] floats;
    private int[] ints;
    private MainModule module;
    private AudioController controller;

    [UnitySetUp]
    public IEnumerator SavePreferences()
    {
        hadFloats = null;
        hadInts = null;
        yield return GameTestResources.Ready();
        Assert.IsNull(XFAudio.GetAuidoController("Music"), "The fixture must own the native audio session.");
        Assert.IsNull(XFAudio.GetAuidoController("Sound"));
        hadFloats = FloatKeys.Select(PlayerPrefs.HasKey).ToArray();
        floats = FloatKeys.Select(key => PlayerPrefs.GetFloat(key)).ToArray();
        hadInts = IntKeys.Select(PlayerPrefs.HasKey).ToArray();
        ints = IntKeys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
        foreach (var key in FloatKeys.Concat(IntKeys)) PlayerPrefs.DeleteKey(key);
    }

    [TearDown]
    public void ReleaseOnlyOwnedSession()
    {
        controller?.OnExit();
        module?.RemoveController<AudioController>();
        controller = null;
        module = null;
        if (hadFloats == null) return;
        for (var i = 0; i < FloatKeys.Length; i++)
        {
            if (hadFloats[i]) PlayerPrefs.SetFloat(FloatKeys[i], floats[i]); else PlayerPrefs.DeleteKey(FloatKeys[i]);
        }
        for (var i = 0; i < IntKeys.Length; i++)
        {
            if (hadInts[i]) PlayerPrefs.SetInt(IntKeys[i], ints[i]); else PlayerPrefs.DeleteKey(IntKeys[i]);
        }
        PlayerPrefs.Save();
    }

    private void StartController()
    {
        module = new MainModule();
        controller = module.LoadController<AudioController>(false);
        Assert.IsNotNull(XFAudio.GetAuidoController("Music"), "Game audio must register XF Music before panels load.");
        Assert.IsNotNull(XFAudio.GetAuidoController("Sound"), "Game audio must register XF Sound.");
        Assert.IsTrue((bool)typeof(AudioController).GetField("_running", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller));
        Assert.That(Voices().Count(source => source && source.isPlaying), Is.Zero,
            string.Join(", ", Voices().Select(source => source ? source.name + ":" + (source.clip ? source.clip.name : "null") + ":" + source.isPlaying : "missing")));
    }

    private static AudioSource Source(int slot) => XFAudio.GetAuidoController($"MainModule.Combat.{slot:00}")?.AudioSource;
    private static AudioSource[] Voices() => Enumerable.Range(0, 48).Select(Source).ToArray();
    private static int Playing(string clip = null) => Voices().Count(source => source && source.isPlaying && (clip == null || source.clip.name == clip));

    private bool Play(string name, double now, bool important = false)
    {
        return PlayGroup(name, now, "Explosion", important: important);
    }

    private bool PlayGroup(string name, double now, string group, int emitter = 0, bool important = false)
    {
        var groupType = typeof(AudioController).GetNestedType("SoundGroup");
        Assert.IsNotNull(groupType, "Player, enemy, explosions and feedback need independent admission groups.");
        var method = typeof(AudioController).GetMethod("TryPlayCombatSound", BindingFlags.Instance | BindingFlags.NonPublic);
        var priority = Enum.ToObject(method.GetParameters()[1].ParameterType, important ? 1 : 0);
        return (bool)method.Invoke(controller, new[] { (object)name, priority, now, Enum.Parse(groupType, group), emitter });
    }

    [Test]
    public void EnemyGunCannotDebouncePlayerOrAnotherPlayerWeapon()
    {
        StartController();
        Assert.IsTrue(PlayGroup(Sounds[0], 1, "EnemyWeapon", 100));
        Assert.IsTrue(PlayGroup(Sounds[0], 1.001, "PlayerWeapon", 1));
        Assert.IsTrue(PlayGroup(Sounds[0], 1.002, "PlayerWeapon", 2));
        Assert.IsFalse(PlayGroup(Sounds[0], 1.003, "PlayerWeapon", 1));
        Assert.IsFalse(PlayGroup(Sounds[0], 1.004, "EnemyWeapon", 101), "Enemy crowds must still aggregate identical shots.");
    }

    [Test]
    public void ExpiredEmitterHistoryIsReclaimedWithoutForgettingRecentShots()
    {
        StartController();
        var admissions = (IDictionary)typeof(AudioController)
            .GetField("_lastPlayed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.IsTrue(PlayGroup(Sounds[0], 1, "PlayerWeapon", 1));
        Assert.IsTrue(PlayGroup(Sounds[0], 1.99, "PlayerWeapon", 2));
        Assert.IsTrue(PlayGroup(Sounds[0], 2, "PlayerWeapon", 3));
        Assert.IsFalse(PlayGroup(Sounds[0], 2.01, "PlayerWeapon", 2), "A cleanup must retain the current cooldown window.");
        Assert.AreEqual(2, admissions.Count, "Expired instances must not remain for the whole module session.");
        for (var round = 0; round < 20; round++)
        {
            for (var i = 0; i < 8; i++)
                Assert.IsTrue(PlayGroup(Sounds[0], 10 + round * 2 + i * .001, "PlayerWeapon", 100 + round * 8 + i));
            Assert.LessOrEqual(admissions.Count, 8, "Changing weapon instances must not accumulate historical emitters.");
        }
    }

    [Test]
    public void SaturatedEnemiesAndExplosionsCannotUsePlayerOrFeedbackBudgets()
    {
        StartController();
        for (var i = 0; i < 12; i++) Assert.IsTrue(PlayGroup(Sounds[i % Sounds.Length], 10 + i * .1, "EnemyWeapon"));
        for (var i = 0; i < 16; i++) Assert.IsTrue(PlayGroup(Sounds[i % Sounds.Length], 10 + i * .1, "Explosion", important: true));
        Assert.IsTrue(PlayGroup(Sounds[0], 20, "PlayerWeapon", 1));
        Assert.IsTrue(PlayGroup(Sounds[0], 20, "Feedback", important: true));
        Assert.AreEqual(30, Enumerable.Range(0, 48).Count(i => Source(i) && Source(i).isPlaying));
        Assert.IsTrue(Source(16).isPlaying, "The first player slot must remain available despite enemy/explosion saturation.");
        Assert.IsTrue(Source(44).isPlaying, "Player feedback must have its own slots.");
    }

    [Test]
    public void PlayerFireRefreshesItsOwnOldTailRatherThanDroppingTheNextAttack()
    {
        StartController();
        for (var i = 0; i < 8; i++) Assert.IsTrue(PlayGroup(Sounds[0], 10 + i * .1, "PlayerWeapon", 1));
        Assert.IsTrue(PlayGroup(Sounds[0], 11, "PlayerWeapon", 1));
        Assert.AreEqual(8, Enumerable.Range(16, 16).Count(i => Source(i) && Source(i).isPlaying));
        Assert.IsFalse(Source(0).isPlaying, "Refreshing player tails cannot borrow explosion slots.");
    }

    [Test]
    public void SeventhEnemyShotRefreshesItsTailWithoutExceedingSixSameClipVoices()
    {
        StartController();
        for (var i = 0; i < 7; i++) Assert.IsTrue(PlayGroup(Sounds[0], 10 + i * .1, "EnemyWeapon", i + 1));
        Assert.AreEqual(6, Enumerable.Range(32, 12).Count(i => Source(i).isPlaying));
        Assert.AreEqual(6, Playing(Sounds[0]));
    }

    [TestCase("PlayerWeapon", 16, 16)]
    [TestCase("EnemyWeapon", 32, 12)]
    public void NormalGunfireCannotReplaceImportantVoicesInItsOwnGroup(string group, int first, int count)
    {
        StartController();
        for (var i = 0; i < count; i++) Assert.IsTrue(PlayGroup(Sounds[i % Sounds.Length], 10 + i * .1, group, i + 1, true));
        var before = Enumerable.Range(first, count).Select(i => Source(i).clip).ToArray();
        Assert.IsFalse(PlayGroup("挑战失败", 20, group, 100));
        CollectionAssert.AreEqual(before, Enumerable.Range(first, count).Select(i => Source(i).clip));
    }

    [Test]
    public void MixedCombatHasFortyEightBoundedSlotsAndEnoughRealVoices()
    {
        StartController();
        foreach (var group in new[] { (Name: "Explosion", Count: 16), (Name: "PlayerWeapon", Count: 16), (Name: "EnemyWeapon", Count: 12), (Name: "Feedback", Count: 4) })
            for (var i = 0; i < group.Count; i++) Assert.IsTrue(PlayGroup(Sounds[i % Sounds.Length], 10 + i * .1, group.Name, i + 1));
        Assert.AreEqual(48, Enumerable.Range(0, 48).Count(i => Source(i) && Source(i).isPlaying));
        Assert.IsNull(XFAudio.GetAuidoController("MainModule.Combat.48"));
        Assert.GreaterOrEqual(AudioSettings.GetConfiguration().numRealVoices, 64);
        for (var i = 0; i < 48; i++) Assert.AreEqual(i < 16 ? 128 : i < 32 ? 64 : i < 44 ? 192 : 16, Source(i).priority);
        Assert.IsTrue(PlayGroup("挑战失败", 20, "Explosion", important: true));
        Assert.AreEqual(80, Source(0).priority);
        Assert.IsTrue(PlayGroup("挑战失败", 20, "EnemyWeapon", important: true));
        Assert.AreEqual(144, Source(32).priority);
    }

    [UnityTest]
    public IEnumerator FeedbackRemainsRealDuringFullCombatWithMusicAndUi()
    {
        StartController();
        GameObject ownedListener = null;
        StartPanel panel = null;
        try
        {
            if (!UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(listener => listener.enabled))
                ownedListener = new GameObject("Combat voice priority test", typeof(AudioListener));
            panel = module.LoadPanel<StartPanel>();
            panel.GetComponentsInChildren<AudioPlayer>(true).Single(player => player.ControllerName == "Sound").Play();
            foreach (var group in new[] { (Name: "Explosion", Count: 16), (Name: "PlayerWeapon", Count: 16), (Name: "EnemyWeapon", Count: 12), (Name: "Feedback", Count: 4) })
                for (var i = 0; i < group.Count; i++)
                    Assert.IsTrue(PlayGroup(group.Name == "Feedback" ? Sounds[4] : Sounds[i % Sounds.Length], 10 + i * .1, group.Name, i + 1));
            Assert.AreEqual(48, Playing());
            var music = XFAudio.GetAuidoController("Music").AudioSource;
            var ui = XFAudio.GetAuidoController("Sound").AudioSource;
            Assert.IsTrue(music.isPlaying);
            Assert.IsTrue(ui.isPlaying);
            Assert.AreEqual(24, music.priority);
            Assert.AreEqual(32, ui.priority);
            var dsp = AudioSettings.dspTime;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.Greater(AudioSettings.dspTime, dsp, "Virtualization assertions require an active audio device.");
            for (var i = 44; i < 48; i++)
            {
                Assert.IsTrue(Source(i).isPlaying);
                Assert.AreEqual(16, Source(i).priority);
                Assert.IsFalse(Source(i).isVirtual, "Feedback must retain a real voice under mixed combat load.");
            }
        }
        finally
        {
            if (panel) { panel.Close(); UnityEngine.Object.DestroyImmediate(panel.gameObject); }
            if (ownedListener) UnityEngine.Object.DestroyImmediate(ownedListener);
        }
    }

    private void Fill(bool important = false)
    {
        for (var i = 0; i < 16; i++) Assert.IsTrue(Play(Sounds[i % Sounds.Length], 10 + i * .1, important));
        Assert.AreEqual(16, Playing(), "The test needs a functioning audio context and a full explosion group.");
    }

    [Test]
    public void RegistersBoundedNativeControllersAndReleasesThemIdempotently()
    {
        StartController();
        Assert.IsTrue(XFAudio.GetAuidoController("Music").AudioSource.loop);
        Assert.IsFalse(XFAudio.GetAuidoController("Sound").AudioSource.loop);
        foreach (var source in Voices())
        {
            Assert.IsNotNull(source);
            Assert.IsFalse(source.loop);
            Assert.AreEqual(0, source.spatialBlend);
            Assert.AreEqual(1, source.volume);
        }
        controller.OnExit();
        controller.OnExit();
        Assert.IsNull(XFAudio.GetAuidoController("Music"));
        Assert.IsNull(XFAudio.GetAuidoController("Sound"));
        for (var i = 0; i < 48; i++) Assert.IsNull(XFAudio.GetAuidoController($"MainModule.Combat.{i:00}"));
        module.RemoveController<AudioController>();
        controller = module.LoadController<AudioController>(false);
        Assert.IsNotNull(Source(47));
    }

    [Test]
    public void RepeatedSameFrameRequestsAndRejectedRequestsDoNotMoveCooldown()
    {
        StartController();
        Assert.IsTrue(Play(Sounds[0], 1));
        for (var i = 0; i < 1000; i++) Assert.IsFalse(Play(Sounds[0], 1.079));
        Assert.AreEqual(1, Playing());
        Assert.IsTrue(Play(Sounds[0], 1.08));
        Assert.AreEqual(2, Playing());
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void CooldownUsesTimeRatherThanFrameCount(int fps)
    {
        StartController();
        var accepted = 0;
        for (var frame = 0; frame <= fps / 5; frame++)
            if (Play(Sounds[0], (double)frame / fps)) accepted++;
        Assert.That(accepted, Is.InRange(2, 3));
        Assert.LessOrEqual(Playing(), 3);
    }

    [Test]
    public void ImportantCanUpgradeNormalButRepeatedImportantIsThrottled()
    {
        StartController();
        Assert.IsTrue(Play(Sounds[0], 1));
        Assert.IsTrue(Play(Sounds[0], 1.001, true));
        Assert.IsFalse(Play(Sounds[0], 1.002, true));
        Assert.IsFalse(Play(Sounds[0], 1.003));
        Assert.AreEqual(2, Playing());
    }

    [TestCase("机炮3", 10f)]
    [TestCase("机炮4", 12f)]
    public void ConfiguredGunCadenceKeepsEveryShotBeforeTheFirstClipFinishes(string sound, float shotsPerSecond)
    {
        StartController();
        var clip = XFABManager.AssetBundleManager.LoadAsset<AudioClip>(module.ProjectName, sound);
        Assert.IsNotNull(clip);
        var overlaps = Mathf.CeilToInt(clip.length * shotsPerSecond);
        Assert.That(overlaps, Is.InRange(5, 8), "Use the actual gun clips and configured firing cadence, not synthetic short sounds.");
        // 此窗口内首发还没结束，按真实发射间隔提交，不应因单音效额度漏掉第五/六发。
        for (var shot = 0; shot < overlaps; shot++)
            Assert.IsTrue(PlayGroup(sound, 10 + shot / (double)shotsPerSecond, "PlayerWeapon", 1), sound + " dropped shot " + (shot + 1));
        Assert.AreEqual(overlaps, Playing(sound));
    }

    [Test]
    public void SameClipCapIsRetainedWhenImportantReplacesNormal()
    {
        StartController();
        for (var i = 0; i < 8; i++) Assert.IsTrue(Play(Sounds[0], 10 + i * .1));
        Assert.IsFalse(Play(Sounds[0], 10.8));
        Assert.IsTrue(Play(Sounds[0], 10.801, true));
        Assert.AreEqual(8, Playing(Sounds[0]));
        Assert.AreEqual(8, Playing());
    }

    [Test]
    public void RareImportantReplacesOldestNormalInSaturatedExplosionGroup()
    {
        StartController();
        Fill();
        Assert.IsFalse(Play("挑战失败", 20));
        Assert.IsTrue(Play("挑战失败", 20, true));
        Assert.AreEqual("挑战失败", Source(0).clip.name);
        Assert.AreEqual(16, Playing());
        Assert.IsFalse(Play(Sounds[0], 20.1));
        Assert.AreEqual("挑战失败", Source(0).clip.name);
    }

    [Test]
    public void NewImportantReplacesOldestImportantWhenEveryVoiceIsImportant()
    {
        StartController();
        Fill(true);
        Assert.IsTrue(Play("挑战失败", 20, true));
        Assert.AreEqual("挑战失败", Source(0).clip.name);
        Assert.AreEqual(16, Playing());
    }

    [Test]
    public void VolumeAndMuteFollowSoundWithoutPerVoicePreferenceWrites()
    {
        StartController();
        Assert.IsTrue(Play(Sounds[0], 1));
        controller.SoundVolume = .25f;
        XFAudio.GetAuidoController("Sound").Mute = true;
        for (var i = 0; i < 48; i++)
        {
            Assert.AreEqual(.25f, Source(i).volume);
            Assert.IsTrue(Source(i).mute);
            Assert.IsFalse(PlayerPrefs.HasKey($"AudioConfigVolume:MainModule.Combat.{i:00}"));
            Assert.IsFalse(PlayerPrefs.HasKey($"AudioConfigMute:MainModule.Combat.{i:00}"));
        }
    }

    [TestCase("Sound", "sliderSound")]
    [TestCase("Music", "sliderMusic")]
    public void VolumeSliderCanRecoverPersistedMuteWithoutChangingItOnPanelLoad(string channel, string field)
    {
        PlayerPrefs.SetFloat("AudioConfigVolume:" + channel, .161909044f);
        PlayerPrefs.SetInt("AudioConfigMute:" + channel, 1);
        StartController();
        SettingPanel panel = null;
        try
        {
            panel = module.LoadPanel<SettingPanel>();
            Assert.IsNotNull(panel);
            var slider = (Slider)typeof(SettingPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var native = XFAudio.GetAuidoController(channel);
            Assert.AreEqual(.161909044f, native.Volume, "Opening settings must preserve the stored volume.");
            Assert.IsTrue(native.Mute, "Opening settings is not an unmute gesture.");
            Assert.AreEqual(0, slider.value, "A muted category must not display a misleading positive audible volume.");
            slider.value = .5f;
            Assert.AreEqual(.5f, native.Volume);
            Assert.IsFalse(native.Mute, "An explicit positive slider change must recover audible output.");
            Assert.IsFalse(native.AudioSource.mute);
            if (channel == "Sound")
                foreach (var voice in Voices())
                {
                    Assert.IsFalse(voice.mute);
                    Assert.AreEqual(.5f, voice.volume);
                }
            slider.value = 0;
            Assert.AreEqual(0, native.Volume);
            Assert.IsTrue(native.Mute);
            slider.value = .25f;
            Assert.IsFalse(native.Mute);
            Assert.AreEqual(.25f, native.Volume);
        }
        finally
        {
            if (panel) { panel.Close(); UnityEngine.Object.DestroyImmediate(panel.gameObject); }
        }
    }

    [UnityTest]
    public IEnumerator SoundSliderRestoresRealCombatOutputFromPersistedMute()
    {
        PlayerPrefs.SetFloat("AudioConfigVolume:Sound", .5f);
        PlayerPrefs.SetInt("AudioConfigMute:Sound", 1);
        StartController();
        GameObject ownedListener = null;
        SettingPanel panel = null;
        try
        {
            if (!UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(listener => listener.enabled))
                ownedListener = new GameObject("Game sound output test", typeof(AudioListener));
            panel = module.LoadPanel<SettingPanel>();
            controller.PlaySound("火箭爆炸1");
            double muted = 0, audible = 0;
            yield return MeasureOutput(value => muted = value);
            Assert.Less(muted, .00001, "The persisted Sound mute must really silence the listener output.");
            var slider = (Slider)typeof(SettingPanel).GetField("sliderSound", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            slider.value = .5f;
            controller.PlaySound("火箭爆炸1");
            yield return MeasureOutput(value => audible = value);
            Assert.Greater(audible, .0001, "Restoring Sound through the real slider must produce nonzero combat PCM, not only isPlaying=true.");
        }
        finally
        {
            if (panel) { panel.Close(); UnityEngine.Object.DestroyImmediate(panel.gameObject); }
            if (ownedListener) UnityEngine.Object.DestroyImmediate(ownedListener);
        }
    }

    private static IEnumerator MeasureOutput(Action<double> completed)
    {
        var samples = new float[512];
        var until = Time.realtimeSinceStartup + .35f;
        double maximum = 0;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
            AudioListener.GetOutputData(samples, 0);
            double sum = 0;
            foreach (var sample in samples) sum += sample * sample;
            maximum = Math.Max(maximum, Math.Sqrt(sum / samples.Length));
        }
        completed(maximum);
    }

    [Test]
    public void MigrationPreservesExistingZeroAndInvertsOldOnFlagsWithoutDeletingThem()
    {
        PlayerPrefs.SetFloat("AudioConfigVolume:Music", 0);
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_MUSIC_VOLUME", .7f);
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_SOUND_VOLUME", .3f);
        PlayerPrefs.SetInt("KEY_AUDIO_MANAGER_MUSIC_ON", 0);
        PlayerPrefs.SetInt("KEY_AUDIO_MANAGER_SOUND_ON", 1);
        StartController();
        Assert.AreEqual(0, controller.MusicVolume);
        Assert.AreEqual(.3f, controller.SoundVolume);
        Assert.IsTrue(XFAudio.GetAuidoController("Music").Mute);
        Assert.IsFalse(XFAudio.GetAuidoController("Sound").Mute);
        Assert.AreEqual(.7f, PlayerPrefs.GetFloat("KEY_AUDIO_MANAGER_MUSIC_VOLUME"));
        Assert.AreEqual(1, PlayerPrefs.GetInt(MigrationKey));
        controller.OnExit();
        module.RemoveController<AudioController>();
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_SOUND_VOLUME", .9f);
        controller = module.LoadController<AudioController>(false);
        Assert.AreEqual(.3f, controller.SoundVolume, "Migration cannot resynchronize development preferences.");
    }

    [Test]
    public void InvalidLegacyPreferencesUseNativeDefaults()
    {
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_MUSIC_VOLUME", float.NaN);
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_SOUND_VOLUME", float.PositiveInfinity);
        PlayerPrefs.SetInt("KEY_AUDIO_MANAGER_MUSIC_ON", -1);
        StartController();
        Assert.AreEqual(1, controller.MusicVolume);
        Assert.AreEqual(1, controller.SoundVolume);
        Assert.IsFalse(XFAudio.GetAuidoController("Music").Mute);
    }

    [Test]
    public void MigrationMarkerRejectsLegacyPreferencesCreatedAfterTheMigration()
    {
        StartController();
        Assert.IsFalse(PlayerPrefs.HasKey("AudioConfigVolume:Sound"));
        controller.OnExit();
        module.RemoveController<AudioController>();
        PlayerPrefs.SetFloat("KEY_AUDIO_MANAGER_SOUND_VOLUME", .2f);
        PlayerPrefs.SetInt("KEY_AUDIO_MANAGER_SOUND_ON", 0);
        controller = module.LoadController<AudioController>(false);
        Assert.AreEqual(1, controller.SoundVolume);
        Assert.IsFalse(XFAudio.GetAuidoController("Sound").Mute);
        Assert.IsFalse(PlayerPrefs.HasKey("AudioConfigVolume:Sound"));
        Assert.IsFalse(PlayerPrefs.HasKey("AudioConfigMute:Sound"));
    }

    [Test]
    public void MissingClipCannotEvictPlayingVoiceOrConsumeCooldown()
    {
        StartController();
        Fill();
        var before = Voices().Select(source => source.clip).ToArray();
        Assert.IsFalse(Play("__GameAudioMissingClip__", 11, true));
        CollectionAssert.AreEqual(before, Voices().Select(source => source.clip));
        var admissions = (System.Collections.IDictionary)typeof(AudioController)
            .GetField("_lastPlayed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
        Assert.IsFalse(admissions.Contains((AudioController.SoundGroup.Explosion, "__GameAudioMissingClip__", 0)));
    }

    [Test]
    public void ReusingNaturallyStoppedSourceDoesNotAccumulateXfPlaybackReferences()
    {
        StartController();
        for (var i = 0; i < 100; i++)
        {
            Source(0).Stop();
            Assert.IsTrue(Play(Sounds[0], i * .1));
            var data = (System.Collections.ICollection)typeof(XFGameFramework.AudioController)
                .GetField("AudioDatas", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(XFAudio.GetAuidoController("MainModule.Combat.00"));
            Assert.AreEqual(1, data.Count);
        }
    }

    [Test]
    public void InitializationCollisionNeverTakesOrRemovesSomeoneElsesController()
    {
        XFAudio.RegisterAudioController("Music", true);
        var foreign = XFAudio.GetAuidoController("Music");
        try
        {
            module = new MainModule();
            Assert.Throws<InvalidOperationException>(() => module.LoadController<AudioController>(false));
            Assert.AreSame(foreign, XFAudio.GetAuidoController("Music"));
            Assert.IsNull(XFAudio.GetAuidoController("Sound"));
            Assert.IsFalse(PlayerPrefs.HasKey(MigrationKey));
        }
        finally { XFAudio.RemoveAudioController("Music"); }
    }
}
