#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Reflection;
using UnityEditor;
using XFGameFramework;

public class GameAudioWiringTests
{
    [TestCase("Views/Enemy/Enemy_MachineGun/Enemy_MachineGun.prefab", "EnemyWeapon")]
    [TestCase("Views/Enemy/Enemy_ShotGun/Enemy_ShotGun.prefab", "EnemyWeapon")]
    [TestCase("Views/Enemy/Enemy_RocketGun/Enemy_RocketGun.prefab", "EnemyWeapon")]
    [TestCase("Views/Player/Equip/Weapon/MachineGun/MachineGun.prefab", "PlayerWeapon")]
    [TestCase("Views/Player/Equip/Weapon/ShotGun/ShotGun.prefab", "PlayerWeapon")]
    [TestCase("Views/Player/Equip/Weapon/RocketGun/RocketGun.prefab", "PlayerWeapon")]
    [TestCase("Views/Player/Segments/Snake.prefab", "PlayerWeapon")]
    public void RealWeaponPrefabsCacheTheirActualOwnerForSoundAdmission(string relativePath, string expected)
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Games/MainModule/" + relativePath);
        try
        {
            var weapons = root.GetComponentsInChildren<WeaponBase>(true);
            Assert.IsNotEmpty(weapons);
            foreach (var weapon in weapons)
            {
                weapon.OnLoaded();
                Assert.AreEqual(expected, typeof(WeaponBase).GetField("_shotGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon).ToString());
                Assert.AreEqual(weapon.GetInstanceID(), typeof(WeaponBase).GetField("_soundEmitterId", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon));
                weapon.OnLoaded();
                Assert.AreEqual(expected, typeof(WeaponBase).GetField("_shotGroup", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(weapon).ToString());
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static readonly (string Path, int ClickTargets)[] PanelPrefabs =
    {
        ("Assets/Games/MainModule/UI/StartPanel/StartPanel.prefab", 4),
        ("Assets/Games/MainModule/UI/PausePanel/PausePanel.prefab", 4),
        ("Assets/Games/MainModule/UI/GameOverPanel/GameOverPanel.prefab", 2),
        ("Assets/Games/MainModule/UI/GameSettingPanel/GameSettingPanel.prefab", 2),
        ("Assets/Games/MainModule/UI/Description/Description.prefab", 1),
        ("Assets/Games/MainModule/UI/GamePanel/GamePanel.prefab", 1),
        ("Assets/Games/MainModule/UI/SettingPanel/SettingPanel.prefab", 1)
    };

    [Test]
    public void CalibratedRocketLaunchKeepsAtLeastOneDecibelOfHeadroom()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Games/MainModule/Art/解限蛇音效/火箭发射.mp3");
        Assert.IsNotNull(clip);
        var samples = new float[clip.samples * clip.channels];
        Assert.IsTrue(clip.GetData(samples, 0));
        Assert.LessOrEqual(samples.Max(sample => Mathf.Abs(sample)), Mathf.Pow(10, -1f / 20),
            "Rocket launch must retain headroom after Unity import instead of clipping at full scale.");
    }

    [TestCase("02.wav", -22f)]
    [TestCase("03.wav", -22f)]
    [TestCase("挑战失败.mp3", -19f)]
    public void MusicAndFailureAssetsStayBelowTheirCalibratedRmsCeiling(string file, float ceilingDb)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Games/MainModule/Art/解限蛇音效/" + file);
        Assert.IsNotNull(clip);
        var samples = new float[clip.samples * clip.channels];
        Assert.IsTrue(clip.GetData(samples, 0));
        double energy = 0;
        foreach (var sample in samples) energy += (double)sample * sample;
        Assert.LessOrEqual(10 * System.Math.Log10(energy / samples.Length), ceilingDb,
            "Music and failure cues must not regain their original excessive level after import.");
    }

    [Test]
    public void ClickPlayerPrefabUsesXfOneShotGroup()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/Art/Prefabs/ClickPlayer.prefab");
        Assert.NotNull(prefab, "ClickPlayer.prefab 不存在");
        var player = prefab.GetComponent<AudioPlayer>();
        AssertPlayer(player, "Sound", AudioPlayType.PlayOneShot, false);
        var clips = player.Clips[0].clips;
        Assert.AreEqual(2, clips.Count);
        Assert.AreEqual("0ef6464264076e64a81e6ca0646b8f9d", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(clips[0])));
        Assert.AreEqual("006c81f033745d744b14dc35af43bef3", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(clips[1])));
        Assert.IsNull(prefab.GetComponent<AudioSource>(), "点击音借用共享 Sound，不能随 UI 销毁而截断");
    }

    [Test]
    public void StartPanelPrefabUsesXfMusicComponentAndExplicitOwnerReference()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/UI/StartPanel/StartPanel.prefab");
        Assert.NotNull(prefab, "StartPanel.prefab 不存在");
        var player = prefab.GetComponentsInChildren<AudioPlayer>(true).SingleOrDefault(p => p.ControllerName == "Music");
        AssertPlayer(player, "Music", AudioPlayType.Play, true);
        Assert.AreEqual("a26bcad52dad4804fa209206f51dc433", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(player.Clips[0].clips.Single())));
        using var serialized = new SerializedObject(prefab.GetComponent<StartPanel>());
        Assert.AreSame(player, serialized.FindProperty("musicPlayer").objectReferenceValue);
        Assert.IsNull(player.GetComponent<AudioSource>());
    }

    [Test]
    public void PanelClickEventsKeepTheirSharedXfPrefabTargets()
    {
        foreach (var entry in PanelPrefabs)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);
            Assert.NotNull(prefab, entry.Path);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<AudioPlayer>(true).Count(p => p.ControllerName == "Sound"));

            var clickTargets = 0;
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
            {
                var onClick = button.onClick;
                for (var i = 0; i < onClick.GetPersistentEventCount(); i++)
                {
                    var target = onClick.GetPersistentTarget(i);
                    if (target is AudioPlayer player)
                    {
                        Assert.AreEqual("Sound", player.ControllerName);
                        Assert.AreEqual("Play", onClick.GetPersistentMethodName(i), entry.Path + " -> " + button.name);
                        var source = PrefabUtility.GetCorrespondingObjectFromSource(player);
                        Assert.AreEqual("Assets/Games/MainModule/Art/Prefabs/ClickPlayer.prefab", AssetDatabase.GetAssetPath(source));
                        using (var serialized = new UnityEditor.SerializedObject(button))
                        {
                            var call = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(i);
                            Assert.AreEqual("XFGameFramework.AudioPlayer, XFGameFramework",
                                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue, entry.Path + " -> " + button.name);
                        }
                        clickTargets++;
                    }
                }
            }

            Assert.AreEqual(entry.ClickTargets, clickTargets, entry.Path + " 的 XF 点击事件数量不符");
        }
    }

    [Test]
    public void UnusedSpatialTemplateUsesNativeSpatialSource()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/Art/Prefabs/3DAudioPlayer.prefab");
        Assert.NotNull(prefab);
        var player = prefab.GetComponent<AudioPlayer>();
        AssertPlayer(player, "Sound", AudioPlayType.Play, false);
        Assert.IsEmpty(player.Clips[0].clips);
        var source = prefab.GetComponent<AudioSource>();
        Assert.IsNotNull(source);
        Assert.AreEqual(1, source.spatialBlend);
        Assert.AreEqual(AudioRolloffMode.Linear, source.rolloffMode);
        Assert.AreEqual(1, source.minDistance);
        Assert.AreEqual(50, source.maxDistance);
        Assert.IsFalse(source.loop);
    }

    [Test]
    public void GameCameraUsesExplicitlyStartedFrameworkMusicPlayer()
    {
        for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            Assert.IsFalse(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Asset wiring verification requires saved scenes.");
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Games/MainModule/Scenes/Game.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            GameObject camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "Virtual Camera") camera = root;
            }
            Assert.NotNull(camera, "Game 场景缺少 Gaming 获取的 Virtual Camera");
            var player = camera.GetComponent<AudioPlayer>();
            AssertPlayer(player, "Music", AudioPlayType.Play, false);
            Assert.AreEqual("400ef5481e980d242978b2a794305170", UnityEditor.AssetDatabase.AssetPathToGUID(
                UnityEditor.AssetDatabase.GetAssetPath(player.Clips[0].clips.Single())));
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded && scene.isActive))
                UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
        }
    }

    private static void AssertPlayer(AudioPlayer player, string controller, AudioPlayType playType, bool automatic)
    {
        Assert.IsNotNull(player, "Missing native XF AudioPlayer");
        Assert.AreEqual("403c24cad33f2944ead6f9acd0524ea2", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(player))));
        Assert.AreEqual(controller, player.ControllerName);
        Assert.AreEqual(AudioAssetType.AudioClip, player.AudioAssetType);
        Assert.IsEmpty(player.AssetNames);
        Assert.AreEqual(1, player.Clips.Count);
        Assert.AreEqual("Default", player.Clips[0].name);
        Assert.AreEqual(playType, player.Clips[0].playType);
        Assert.AreEqual(automatic, player.Clips[0].playOnAwake);
    }
}
#endif
