using UnityEngine;
using XFGameFramework;

public class StartPanel : Panel
{
   [SerializeField] private AudioPlayer musicPlayer;

   private GameController GameController => Module.LoadController<GameController>();
   public void OnBtnStartClicked()
   {
      musicPlayer.Stop(StopType.Play);
      GameController.SetGameState(GameState.GameSetting);
   }

   protected override void OnDisable()
   {
      if (musicPlayer) musicPlayer.Stop(StopType.Play);
      base.OnDisable();
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
