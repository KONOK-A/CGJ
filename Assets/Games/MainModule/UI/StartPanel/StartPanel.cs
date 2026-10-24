using AudioKit = YLFramework.Audio.AudioKit;
using UnityEngine;
using XFGameFramework;

public class StartPanel : Panel
{
   private GameController GameController => Module.LoadController<GameController>();
   public void OnBtnStartClicked()
   {
      GameController.SetGameState(GameState.GameSetting);
      AudioKit.StopMusic();
   }

   public void OnBtnMakerClicked()
   {
      
   }

   public void OnBtnSetting()
   {
      Module.LoadPanel<SettingPanel>();
   }

   public void OnBtnQuit()
   {
      Application.Quit();
   }
}
