using AudioKit = YLFramework.Audio.AudioKit;
using UnityEngine;
using XFABManager;
using XFGameFramework;

public class AudioController : Controller
{
    private const string LegacyMusicVolumeKey = "AudioConfigVolume:Music";

    public override void OnInit()
    {
        base.OnInit();
        // 一次性迁移原 XF 音乐音量到 AudioKit 的持久化音量。
        if (PlayerPrefs.HasKey(LegacyMusicVolumeKey))
        {
            AudioKit.Settings.MusicVolume.Value = PlayerPrefs.GetFloat(LegacyMusicVolumeKey, 1f);
            PlayerPrefs.DeleteKey(LegacyMusicVolumeKey);
        }
    }

    public float MusicVolume
    {
        get => AudioKit.Settings.MusicVolume.Value;
        set => AudioKit.Settings.MusicVolume.Value = value;
    }

    public float SoundVolume
    {
        get => AudioKit.Settings.SoundVolume.Value;
        set => AudioKit.Settings.SoundVolume.Value = value;
    }

    public void PlayBGM(string assetName)
    {
        var audioClip = AssetBundleManager.LoadAsset<AudioClip>(Module.ProjectName, assetName);
        if (audioClip == null) return;
        AudioKit.PlayMusic(audioClip, true);
    }

    public void PlaySound(string assetName)
    {
        var audioClip = AssetBundleManager.LoadAsset<AudioClip>(Module.ProjectName, assetName);
        if (audioClip == null) return;
        AudioKit.PlaySound(audioClip);
    }
}
