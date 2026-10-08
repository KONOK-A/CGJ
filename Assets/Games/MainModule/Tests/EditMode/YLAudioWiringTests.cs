#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using YLFramework.Services.Audio;

public class YLAudioWiringTests
{
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
    public void ClickPlayerPrefabUsesYlComponent()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/Art/Prefabs/ClickPlayer.prefab");
        Assert.NotNull(prefab, "ClickPlayer.prefab 不存在");
        var emitter = prefab.GetComponent<AudioEmitter>();
        Assert.NotNull(emitter, "ClickPlayer 应挂载 AudioEmitter");
        Assert.AreEqual("e0f9382a5a3344d4a979ae028b52cb9c", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.MonoScript.FromMonoBehaviour(emitter))));
        var serialized = new UnityEditor.SerializedObject(emitter);
        Assert.AreEqual(2, serialized.FindProperty("clips").arraySize);
        Assert.AreEqual("0ef6464264076e64a81e6ca0646b8f9d", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(serialized.FindProperty("clips").GetArrayElementAtIndex(0).objectReferenceValue)));
        Assert.AreEqual("006c81f033745d744b14dc35af43bef3", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(serialized.FindProperty("clips").GetArrayElementAtIndex(1).objectReferenceValue)));
        Assert.AreEqual(1f, serialized.FindProperty("volume").floatValue);
        Assert.AreEqual(1f, serialized.FindProperty("pitch").floatValue);
        Assert.AreEqual((int)AudioEmitter.PositionMode.TwoDimensional, serialized.FindProperty("positionMode").enumValueIndex);
        Assert.AreEqual((int)AudioKit.PlaySoundModes.EveryOne, serialized.FindProperty("playSoundMode").enumValueIndex);
        Assert.IsFalse(serialized.FindProperty("useGlobalPlaySoundMode").boolValue, "旧 Prefab 应保留显式 EveryOne，不继承全局去重");
        Assert.IsNull(prefab.GetComponent<AudioSource>(), "Emitter 不应自行拥有 AudioSource");
        Assert.IsNull(prefab.GetComponent<XFGameFramework.AudioPlayer>(), "ClickPlayer 不应再挂载 XF AudioPlayer");
    }

    [Test]
    public void StartPanelPrefabUsesYlBgmComponent()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/UI/StartPanel/StartPanel.prefab");
        Assert.NotNull(prefab, "StartPanel.prefab 不存在");
        var player = prefab.GetComponentInChildren<AudioMusicPlayer>(true);
        Assert.NotNull(player, "StartPanel 应挂载 AudioMusicPlayer");
        Assert.AreEqual("a02644b3809d0e34aa3ad1a21593f647", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(UnityEditor.MonoScript.FromMonoBehaviour(player))));
        var serialized = new UnityEditor.SerializedObject(player);
        Assert.AreEqual("a26bcad52dad4804fa209206f51dc433", UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath(serialized.FindProperty("clip").objectReferenceValue)));
        Assert.IsTrue(serialized.FindProperty("loop").boolValue);
        Assert.IsTrue(serialized.FindProperty("playOnAwake").boolValue);
        Assert.AreEqual(1f, serialized.FindProperty("volume").floatValue);
        Assert.IsFalse(serialized.FindProperty("crossFadeOnPlay").boolValue);
        Assert.AreEqual((int)AudioOutputRoute.RouteMode.Inherit, serialized.FindProperty("outputRoute").FindPropertyRelative("Mode").enumValueIndex);
        Assert.IsNull(prefab.GetComponentInChildren<XFGameFramework.AudioPlayer>(true), "StartPanel 不应再挂载 XF AudioPlayer");
    }

    [Test]
    public void PanelClickEventsTargetFrameworkAudioEmitter()
    {
        foreach (var entry in PanelPrefabs)
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);
            Assert.NotNull(prefab, entry.Path);
            Assert.NotNull(prefab.GetComponentInChildren<AudioEmitter>(true), entry.Path + " 缺少 AudioEmitter");

            var clickTargets = 0;
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
            {
                var onClick = button.onClick;
                for (var i = 0; i < onClick.GetPersistentEventCount(); i++)
                {
                    var target = onClick.GetPersistentTarget(i);
                    Assert.IsFalse(target is XFGameFramework.AudioPlayer, entry.Path + " -> " + button.name + " 仍指向 XF AudioPlayer");
                    if (target is AudioEmitter)
                    {
                        Assert.AreEqual("Play", onClick.GetPersistentMethodName(i), entry.Path + " -> " + button.name);
                        clickTargets++;
                    }
                }
            }

            Assert.AreEqual(entry.ClickTargets, clickTargets, entry.Path + " 的 YL 点击事件数量不符");
        }
    }

    [Test]
    public void UnusedSpatialTemplateUsesEmptyFixedPositionEmitter()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Games/MainModule/Art/Prefabs/3DAudioPlayer.prefab");
        Assert.NotNull(prefab);
        var emitter = prefab.GetComponent<AudioEmitter>();
        Assert.NotNull(emitter);
        Assert.IsNull(prefab.GetComponent<XFGameFramework.AudioPlayer>());
        Assert.IsNull(prefab.GetComponent<AudioSource>());
        var serialized = new UnityEditor.SerializedObject(emitter);
        Assert.AreEqual(0, serialized.FindProperty("clips").arraySize);
        Assert.AreEqual((int)AudioEmitter.PositionMode.FixedPosition, serialized.FindProperty("positionMode").enumValueIndex);
        Assert.IsFalse(serialized.FindProperty("playOnAwake").boolValue);
        Assert.IsFalse(serialized.FindProperty("useGlobalPlaySoundMode").boolValue);
        Assert.AreEqual(1f, serialized.FindProperty("minDistance").floatValue);
        Assert.AreEqual(50f, serialized.FindProperty("maxDistance").floatValue);
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
            var player = camera.GetComponent<AudioMusicPlayer>();
            Assert.NotNull(player, "Gaming 相机应挂载 AudioMusicPlayer");
            Assert.IsNull(camera.GetComponent<XFGameFramework.AudioPlayer>());
            var serialized = new UnityEditor.SerializedObject(player);
            Assert.AreEqual("400ef5481e980d242978b2a794305170", UnityEditor.AssetDatabase.AssetPathToGUID(
                UnityEditor.AssetDatabase.GetAssetPath(serialized.FindProperty("clip").objectReferenceValue)));
            Assert.IsTrue(serialized.FindProperty("loop").boolValue);
            Assert.IsFalse(serialized.FindProperty("playOnAwake").boolValue, "Game BGM 应由 Gaming.OnLoaded 启动");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
#endif
