using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using XFABManager;
using XFFSM;
using XFGameFramework;
using Random = UnityEngine.Random;

public class Gaming : FSMState
{
    private Module _module;
    private CinemachineVirtualCamera _cinemachineVirtualCamera;
    private SnakeHead _shakeHead;
    private float _timer;
    private int _waves;
    private Coroutine _coroutine;
    private GamePanel _gamePanel;
    private AudioPlayer _audioPlayer;
    private SceneController SceneController => _module.LoadController<SceneController>();
    private GameController GameController => _module.LoadController<GameController>();
    private bool IsMax { get; set; }
    private WaitForSeconds _waitForSeconds;
    private readonly Queue<EnemyInfo> _enemyQueue = new();
    private bool _isStart;
    
    private struct EnemyInfo
    {
        public Vector2 Pos;
        public bool IsItem;
        public int Type;
    }
    
    public override void OnEnter()
    {
        base.OnEnter();
        _module = userData as Module;
        if (_module == null)
        {
            throw new Exception("参数传递异常!");
        }
        SceneController.LoadScene("Game",OnLoaded);
        _timer = 0;
        _waves = 0;
        IsMax = false;
        _coroutine = null;
        _enemyQueue.Clear();
        _waitForSeconds = new WaitForSeconds(0.2f);
        GameController.SetEnemyCount(0);
        _isStart = true;
        
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        //随时间生成敌人
        _timer += Time.deltaTime;
        if (_timer >= SettingConst.GenerateEnemyTime + 3f * _waves)
        {
            GenerateEnemy();
            _timer = 0;
        }
        //控制镜头缩放
        float mouseCenter = Input.GetAxis("Mouse ScrollWheel");
        if (mouseCenter < 0)
        {
            if (_cinemachineVirtualCamera.m_Lens.OrthographicSize < SettingConst.CameraLensMaxSize)
            {
                _cinemachineVirtualCamera.m_Lens.OrthographicSize +=
                    SettingConst.CameraLensSizeChangedSpeed * Time.deltaTime;
                if (_cinemachineVirtualCamera.m_Lens.OrthographicSize >= SettingConst.CameraLensMaxSize)
                {
                    _cinemachineVirtualCamera.m_Lens.OrthographicSize = SettingConst.CameraLensMaxSize;
                }
            }
        }
        else if(mouseCenter > 0)
        {
            if (_cinemachineVirtualCamera.m_Lens.OrthographicSize > SettingConst.CameraLensMinSize)
            {
                _cinemachineVirtualCamera.m_Lens.OrthographicSize -=
                    SettingConst.CameraLensSizeChangedSpeed * Time.deltaTime;
                if (_cinemachineVirtualCamera.m_Lens.OrthographicSize < SettingConst.CameraLensMinSize)
                {
                    _cinemachineVirtualCamera.m_Lens.OrthographicSize = SettingConst.CameraLensMinSize;
                }
            }
        }
    }
    public override void OnExit()
    {
        base.OnExit();
        _isStart = false;
        if (_coroutine != null)
        {
            CoroutineStarter.Stop(_coroutine);
            _enemyQueue.Clear();
            _coroutine = null;
        }
        if (_gamePanel != null)
        {
            _gamePanel.Close();
            _gamePanel = null;
        }
        if (_audioPlayer) _audioPlayer.Stop(StopType.Play);
        _audioPlayer = null;
    }
    private void OnLoaded()
    {
        var scene = SceneManager.GetActiveScene();
        _cinemachineVirtualCamera = scene.FindRootGameObject("Virtual Camera").GetComponent<CinemachineVirtualCamera>();
        _audioPlayer = _cinemachineVirtualCamera.GetComponent<AudioPlayer>();
        //加载主角
        _shakeHead = _module.LoadView<SnakeHead>("Snake");
        _shakeHead.transform.position = Vector3.zero;
        //设置相机追随
        _cinemachineVirtualCamera.Follow = _shakeHead.transform;
        //初始生成一波敌人
        GenerateEnemy();
        //开启协程
        _coroutine = CoroutineStarter.Start(GlobalGenerateCoroutine());
        //生成GameUI
        _gamePanel = _module.LoadPanel<GamePanel>().SetPlayer(_shakeHead);
        _audioPlayer.Play();
    }
    private void GenerateEnemy()
    {
        _waves++;
        //实际生成逻辑
        int count;
        if (!IsMax)
        {
            float t = Mathf.Pow(_waves, GameController.GenerateParam);
            count = (int)(3 * t);
            if (count >= GameController.MaxEnemyGenerateCount)
            {
                count = Mathf.Clamp(count, 3, GameController.MaxEnemyGenerateCount);
                IsMax = true;
            }
        }
        else
        {
            count = GameController.MaxEnemyGenerateCount;
        }
        GenerateCalculate(count);
    }
    
    private void GenerateCalculate(int count)
    {
        int itemEnemy = count / 3;
        int counter = 0;
        for (int i = 0; i < count; i++)
        {
            var pos = _shakeHead.MidSegment.position;
            float c = (_shakeHead.Segments.Count + 25f) * SettingConst.Interval / 2.0f;
            int xory = Random.Range(0, 2);
            var posR = Vector3.zero;
            if (xory == 0)
            {
                posR.y = Random.Range(0, 2) == 0 ? pos.y + c : pos.y - c;
                posR.x = Random.Range(pos.x - c, pos.x + c);
            }
            else if(xory == 1)
            {
                posR.x = Random.Range(0, 2) == 0 ? pos.x + c : pos.x - c;
                posR.y = Random.Range(pos.y - c, pos.y + c);
            }
            int type = Random.Range(1, 5);
            bool isItem = false;
            if (counter < itemEnemy)
            {
                isItem = true;
                counter += 1;
            }
            _enemyQueue.Enqueue(new EnemyInfo
            {
                IsItem = isItem,
                Pos = posR,
                Type = type
            });
        }
    }

    private IEnumerator GlobalGenerateCoroutine()
    {
        while (_isStart)
        {
            if (!GameController.IsEnemyFull)
            {
                if (_enemyQueue.Count > 0)
                {
                    var info = _enemyQueue.Dequeue();
                    yield return RealGenerateEnemy(info);
                    yield return _waitForSeconds;
                }
            }

            yield return null;
        }
    }

    private IEnumerator RealGenerateEnemy(EnemyInfo info)
    {
        
        switch (info.Type)
        {
            case 1:
                yield return LoadEnemy<Enemy_MachineGun>(info);
                break;
            case 2: 
                yield return LoadEnemy<Enemy_ShotGun>(info);
                break;
            case 3:
                yield return LoadEnemy<Enemy_RocketGun>(info);
                break;
            case 4:
                yield return LoadEnemy<Enemy_Shield>(info);
                break;
        }
    }
    private IEnumerator LoadEnemy<T>(EnemyInfo info) where T : EnemyBase
    {
        var p = 1 + (IsMax
            ? GameController.HpAddParam * 2 * _shakeHead.Segments.Count
            : GameController.HpAddParam * _shakeHead.Segments.Count);
        var request = _module.LoadViewAsync<T>(null, p,_shakeHead.transform,info.IsItem,info.Pos);
        yield return request;
        GameController.AddEnemyCount();
    }
}
