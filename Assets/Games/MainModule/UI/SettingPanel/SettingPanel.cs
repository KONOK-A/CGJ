using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using XFGameFramework;

public class SettingPanel : Panel
{
    [SerializeField]
    private Slider sliderSound;
    [SerializeField]
    private Slider sliderMusic;

    private AudioController AudioController => Module.LoadController<AudioController>();

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        var audio = AudioController;
        // 初始化只反映有效音量，不触发回调改写既有静音偏好。
        sliderSound.SetValueWithoutNotify(AudioPlayer.GetAuidoController(global::AudioController.SoundName).Mute ? 0 : audio.SoundVolume);
        sliderMusic.SetValueWithoutNotify(AudioPlayer.GetAuidoController(global::AudioController.MusicName).Mute ? 0 : audio.MusicVolume);
    }

    public void OnSliderSoundValueChange(float v)
    {
        AudioController.SoundVolume = v;
        AudioPlayer.GetAuidoController(global::AudioController.SoundName).Mute = v <= 0;
    }
    public void OnSliderMusicValueChange(float v)
    {
        AudioController.MusicVolume = v;
        AudioPlayer.GetAuidoController(global::AudioController.MusicName).Mute = v <= 0;
    }
}
