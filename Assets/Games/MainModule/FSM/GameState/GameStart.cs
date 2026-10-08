using System;
using UnityEngine.SceneManagement;
using XFFSM;
using XFGameFramework;

public class GameStart : FSMState
{
    private Module _module;
    private StartPanel _startPanel;
    public override void OnEnter()
    {
        base.OnEnter();
        _module = userData as Module;
        if (_module == null)
        {
            throw new Exception("参数传递异常!");
        }
        //加载开始场景
        if (SceneManager.GetActiveScene().name != "Start")
        {
            _module.LoadController<SceneController>().LoadScene("Start",() =>
            {
                //加载开始游戏界面
                _startPanel = _module.LoadPanel<StartPanel>();
            });
        }
        else
        {
            //加载开始游戏界面
            _startPanel = _module.LoadPanel<StartPanel>();
        }
        
       
    }

    public override void OnExit()
    {
        base.OnExit();
        if (_startPanel != null)
        {
            _startPanel.Close();
            _startPanel = null;
        }
    } 
}
