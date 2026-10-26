
using System; 
using System.Collections.Generic;
using UnityEngine;
using XFGameFramework;

public class MainModule : Module
{ 
    // 当前模块所使用的资源模块名称
    public override string ProjectName => "MainModule";
     
    // 是否为基础模块，基础模块不可关闭，如果模块能够关闭，请设置为false
    public override bool BaseModule => false;

    // 默认启动的场景的名称，如果不需要启动场景保持默认即可
    public override string DefaultStartUpScene => "Start";
    
    // 当模块启动时触发
    public override void OnStart()
    {
        //触发音频初始化
        LoadController<AudioController>();
        Application.targetFrameRate = 120;
        //模块启动成功后，即将游戏状态切换
        LoadController<GameController>().SetGameState(GameState.Start);
    }
}
