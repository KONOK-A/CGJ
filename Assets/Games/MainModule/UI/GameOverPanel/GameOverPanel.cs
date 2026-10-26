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
            AudioPlayer.Play(AudioController.SoundName, clip, true);
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
