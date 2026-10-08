using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using XFGameFramework;

/// <summary>
/// 蛇头
/// </summary>
public class SnakeHead : SnakeBase
{
    #region 字段

    [SerializeField] private float speed = 5;
    [SerializeField] private float rotateSpeed = 5;
    [SerializeField] private float maxSpeedMultiplier;
    [SerializeField] private float minSpeedMultiplier;
    [SerializeField] private float multiplierChangeSpeed = 1;
    private float _angle;
    private float _multiplier = 1;
    private float _inputX;
    private float _inputY;
    private int _currentFrameCount;
    public List<SnakeBase> Segments { get; private set; }
    private bool _isTouchWall;
    private float _distance;
    private Vector2 _lastPos;
    private Vector2 _lastDir;
    private float _timer;
    //public List<Vector2> _positionHistory = new();
    
    
    public List<Transform> SegmentsTrans => Segments.Select(seg => seg.transform).ToList();
    public Transform MidSegment => SegmentsTrans[Segments.Count / 2];
    
    
    #endregion

    #region 属性

    public bool InputDisable { get; set; }
    public float Speed => speed * _multiplier;
    private GameController GameController => Module.LoadController<GameController>();

    #endregion

    #region 生命周期

    public override void OnLoaded(params object[] param)
    {
        base.OnLoaded(param);
        _multiplier = 1;
        Segments = new List<SnakeBase> { this };
        sprites.Clear();
        sprites.AddRange(GetComponentsInChildren<SpriteRenderer>());
        //_positionHistory = new List<Vector2>();
        sprites.ForEach(s =>
        {
            if (s != null)
            {
                s.color = Color.white;
            }
        });
    }

    private void Awake()
    {
        Segments = new List<SnakeBase> { this };
    }
    protected override void OnEnable()
    {
        base.OnEnable();
        Segments.Clear();
    }

    protected override void Start()
    {
        base.Start();
        Index = 0;
        Type = SegmentType.Head;
        _isTouchWall = false;
        _lastPos = Position2D;
        _lastDir = Vector2.up;
        _distance = 0;
    }

    private void Update()
    {
        PlayerInput();

        _timer += Time.deltaTime;
        if (_timer >= 10)
        {
            _timer = 0;
            Hp += 15;
            if (Hp > hp)
            {
                Hp = hp;
            }
        }
    }

    protected override void FixedUpdate()
    {
        //移动方式1
        Move();
        _distance += (rigidBody.velocity * Time.fixedDeltaTime).magnitude;
        if (_distance >= SettingConst.Interval / 2 + 0.15f)
        {
            if (!_isTouchWall)
            {
                MoveBody();
            }
            _lastDir = Direction;
            _lastPos = Position2D;
            _distance = 0;
        }
        //移动方式2
        // _positionHistory.Insert(0,rigidBody.position);
        // Move();
        // int index = 0;
        // for (int i = 1; i < Segments.Count; i++)
        // {
        //     Vector2 point = _positionHistory[Mathf.Clamp(index * Gap, 0, _positionHistory.Count - 1)];
        //     Vector2 dir = point - Segments[i].rigidBody.position;
        //     Segments[i].MovePos(Segments[i].rigidBody.position + dir.normalized * (Speed * Time.fixedDeltaTime));
        //     Vector2 dirToHead = rigidBody.position - Segments[i].rigidBody.position;
        //     Segments[i].SetDirection(dirToHead.normalized);
        //     index++;
        // }
    }

    private void OnCollisionEnter2D(Collision2D other)
    {
        if (other.collider.gameObject.CompareTag("CanNotTouch"))
        {
            _isTouchWall = true;
            //游戏结束
            GameController.SetGameState(GameState.GameOver);
        }
    }

    private void OnCollisionExit2D(Collision2D other)
    {
        _isTouchWall = false;
    }

    #endregion
    
    private void PlayerInput()
    {
        if (InputDisable)
        {
            _inputX = 0;
            _inputY = 0;
            return;
        }

        _inputX = Input.GetAxisRaw("Horizontal");
        _inputY = Input.GetAxisRaw("Vertical");
        //旋转方向
        _angle += _inputX * rotateSpeed * Time.deltaTime;
        var deltaDir = Vector2.up * Mathf.Cos(_angle) + Vector2.right * Mathf.Sin(_angle);
        sprite.up = deltaDir;
        SetDirection(deltaDir);
        //速度乘区
        _multiplier += _inputY * multiplierChangeSpeed * Time.deltaTime;
        _multiplier = Mathf.Clamp(_multiplier, minSpeedMultiplier, maxSpeedMultiplier);
    }

    private void Move()
    {
        //蛇身移动逻辑 这里怎么都处理不流畅
        MoveDelta(Speed);
    }

    private void MoveBody()
    {
        for (int i = Segments.Count - 1; i > 0; i--)
        {
            if (i != 1)
            {
                Segments[i].SetDirection(Segments[i - 1].Direction);
                Segments[i].MovePos(Segments[i - 1].Position2D);
            }
            else
            {
                Segments[i].SetDirection(_lastDir);
                Segments[i].MovePos(_lastPos);
            }
        }
    }
    public void Grow(SegmentType segmentType,string equipName = "")
    {
        SnakeBase seg = null;
        switch (segmentType)
        {
            case SegmentType.Head:
                
                return;
            case SegmentType.Normal:
                seg = Module.LoadView<NormalSegment>();
                break;
            case SegmentType.Functional:
                seg = Module.LoadView<FunctionalSegment>(null,equipName);
                break;
        }
        if(seg == null )return;
        seg.HeadSegment = this;
        seg.Index = 1;
        seg.SetDirection(Direction);
        seg.transform.position = Position2D - Direction.normalized * SettingConst.Interval;
        Segments.Insert(1, seg);
        for (int i = Segments.Count - 1; i >= 2; i--)
        {
            var segment = Segments[i];
            segment.Index = i;
            segment.transform.position -= (Vector3)(Direction.normalized * SettingConst.Interval);
        }
    }

    public void CutOffBody(int index)
    {
        if (Segments.Count <= index)
        {
            return;
        }
        //脱离死亡节点之后的所有节点
        for (int i = index; i < Segments.Count; i++)
        {
            var seg = Segments[i];
            TimerManager.DelayInvoke(() => { seg.OnFallOff(); }, 0.5f);
        }
        
        Segments.RemoveRange(index, Segments.Count - index);
    }

    protected override void OnDead()
    {
        //头部死亡游戏结束
        GameController.SetGameState(GameState.GameOver);
    }
}
