using XFABManager;
using XFFSM;
using XFGameFramework;

public enum GameState
{
    Start,
    GameSetting,
    Game,
    GameOver
}

public class GameController : Controller
{
    private GameModel GetGameModel()
    {
        GameModel model = Module.LoadModel<GameModel>();
        if (model == null)
        {
            model = new GameModel();
            Module.AddModel(model);
        }
        return model;
    }
    public bool IsEnemyFull => CurrentEnemyCount >= MaxEnemyCount;
    public int CurrentEnemyCount => GetGameModel().CurrentEnemies;
    public int MaxEnemyCount => GetGameModel().MaxEnemyCount;
    public int MaxEnemyGenerateCount => GetGameModel().MaxEnemyGenerateCount;
    public float GenerateParam => GetGameModel().GenerateParam;
    public float HpAddParam => GetGameModel().HpAddParam;
    public void AddEnemyCount()
    {
        GetGameModel().CurrentEnemies += 1;
    }

    public void ReduceEnemyCount()
    {
        GetGameModel().CurrentEnemies -= 1;
    }

    public void SetMaxEnemyCount(int count)
    {
        GetGameModel().MaxEnemyCount = count;
    }

    public void SetMaxEnemyGenerateCount(int count)
    {
        GetGameModel().MaxEnemyGenerateCount = count;
    }
    public void SetGenerateParam(float value)
    {
        GetGameModel().GenerateParam = value;
    }
    public void SetHpAddParam(float value)
    {
        GetGameModel().HpAddParam = value;
    }
    public void SetEnemyCount(int count)
    {
        GetGameModel().CurrentEnemies = count;
    }
    /// <summary>
    /// 设置游戏状态
    /// </summary>
    /// <param name="state">游戏状态</param>
    public void SetGameState(GameState state)
    {
        FSMController controller = FSMController.GetFSM("GameState");
        if (controller == null)
        {
            //还没有启动状态机，查询为空
            //启动一个状态机，其不会销毁，全局存在
            var gameState = AssetBundleManager.LoadAsset<RuntimeFSMController>(Module.ProjectName, "GameStateManager");
            //第三个参数把模块传递给状态机
            controller = FSMController.StartupFSM("GameState", gameState, Module);
        }

        //设置状态
        controller.SetInt("State", (int)state);
    }
    
    public void Restart()
    {

        //清空所有数据
            
        //重新进入关卡
        SetGameState(GameState.Game);
        
    }
}
    
