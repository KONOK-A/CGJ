using System;
using UnityEngine;
using XFFSM;
using XFGameFramework;

public class GameOver : FSMState
{
    private Module _module;
    private GameOverPanel _gameOverPanel;
    public override void OnEnter()
    {
        base.OnEnter();
        _module = userData as Module;
        if (_module == null)
        {
            throw new Exception("参数传递异常!");
        }
        //游戏结束界面
        _gameOverPanel = _module.LoadPanel<GameOverPanel>();
        Time.timeScale = 0;
    }

    public override void OnExit()
    {
        base.OnExit();
        if (_gameOverPanel != null)
        {
            _gameOverPanel.Close();
            _gameOverPanel = null;
        }

        Time.timeScale = 1;
    } 
}