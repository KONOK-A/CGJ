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
        sliderSound.value = AudioController.SoundVolume;
        sliderMusic.value = AudioController.MusicVolume;
    }

    public void OnSliderSoundValueChange(float v)
    {
        AudioController.SoundVolume = v;
    }
    public void OnSliderMusicValueChange(float v)
    {
        AudioController.MusicVolume = v;
    }
}
