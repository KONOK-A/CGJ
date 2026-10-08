using AudioKit = YLFramework.Services.Audio.AudioKit;
using YLFramework.Services.Audio;
using UnityEngine;
using XFABManager;
using XFGameFramework;

public class GameOverPanel : Panel
{
    private GameController GameController => Module.LoadController<GameController>();
    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        var clip = AssetBundleManager.LoadAsset<AudioClip>(Module.ModuleName, "挑战失败");
        if (clip)
        {
            var options = AudioSoundOptions.Defaults;
            options.PlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
            AudioKit.PlaySound(clip, options);
        }
        System.GC.Collect();
    }

    public void OnBtnRestartClick()
    {
        GameController.Restart();
    }
    

    public void OnBtnMainMenuClick()
    {
        GameController.SetGameState(GameState.Start);
    }
}
