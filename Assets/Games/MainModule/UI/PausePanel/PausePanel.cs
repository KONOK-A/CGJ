using UnityEngine;
using XFGameFramework;

public class PausePanel : Panel
{
    protected override void OnEnable()
    {
        base.OnEnable();
        Time.timeScale = 0;
        System.GC.Collect();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Time.timeScale = 1;
    }

    public void OnBtnBackClick()
    {
        Close();
    }

    public void OnBtnMenuClick()
    {
        Close();
        Module.LoadController<GameController>().SetGameState(GameState.Start);
    }

    public void OnBtnSettingClick()
    {
        Module.LoadPanel<SettingPanel>();
    }

    public void OnBtnMenuBookClick()
    {
        Module.LoadPanel<Description>();
    }
}