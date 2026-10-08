using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFFSM;
using XFGameFramework;

public class GameSetting : FSMState
{
    private Module _module;
    private GameSettingPanel _settingPanel;
    public override void OnEnter()
    {
        base.OnEnter();
        _module = userData as Module;
        if (_module == null)
        {
            throw new Exception("参数传递异常!");
        }
        //加载开始场景
        if (SceneManager.GetActiveScene().name != "Setting")
        {
            _module.LoadController<SceneController>().LoadScene("Setting",() =>
            {
                //加载开始游戏界面
                _settingPanel = _module.LoadPanel<GameSettingPanel>();
            });
        }
        else
        {
            //加载开始游戏界面
            _settingPanel = _module.LoadPanel<GameSettingPanel>();
        }
        
       
    }

    public override void OnExit()
    {
        base.OnExit();
        if (_settingPanel != null)
        {
            _settingPanel.Close();
            _settingPanel = null;
        }
    } 
}
