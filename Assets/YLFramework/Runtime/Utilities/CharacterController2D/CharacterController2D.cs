using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Extensions;

namespace YLFramework.Character2D
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterController2D : MonoBehaviour
    {
        #region 常量
        /// <summary>
        /// 这个值不能再小了,否则会影响检测!!!
        /// </summary>
        private const float Distance = 0.1F;
        #endregion


        #region 字段

        internal readonly RaycastHit2D[] Hits = new RaycastHit2D[10];

        internal readonly ContactPoint2D[] Contacts = new ContactPoint2D[10];
        private bool _isColliderReady;
        private bool HasValidColliders => _isColliderReady && IsUsable(Top) && IsUsable(Bottom) && IsUsable(Left) && IsUsable(Right);
        internal int PhysicsVersion
        {
            get; private set;
        }
        internal bool IsPhysicsCurrent(int version) => this && isActiveAndEnabled && HasValidColliders && version == PhysicsVersion;
        private static bool IsUsable(Collider2D collider) => collider && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy;

        private bool EnsureColliders()
        {
            if (HasValidColliders)
            {
                return true;
            }

            if (_isColliderReady)
            {
                _isColliderReady = false;
                ReleaseGravityAndResetState();
            }
            return false;
        }

        private int _contactCount;

        internal LayerMask CheckLayerMask;

        private int _currentLayer = 0;

        private Rigidbody2D _rigidbody2d;

        internal int CheckCount; // 测试用
#if UNITY_EDITOR
        private int _lastSecond;
#endif

        internal Collider2D Top = null;
        internal Collider2D Bottom = null;
        internal Collider2D Left = null;
        internal Collider2D Right = null;

        [Tooltip("是否自动旋转?如果开启，当前角色会根据地面倾斜角度自动旋转,始终保持与地面垂直")]
        public bool autoRotation = false;

        // 是否在空中 用于自动控制旋转角度
        private bool _isInTheAir = false;

        [SerializeField]
        [Tooltip("是否绘制调试的线条?如果开启，可以在场景中看到接触点的法线，角度等信息")]
        private bool showDebugLine = true;
        public bool ShowDebugLine => showDebugLine;

        [Tooltip("是否检测角色当前是否在地面上(默认检测)")]
        [SerializeField]
        private bool isCheckGround = true;

        [Tooltip("是否检测角色当前是否接触墙面(默认不检测)")]
        [SerializeField]
        private bool isCheckWall = false;

        [Tooltip(" 是否检测角色头顶是否碰到游戏物体(默认不检测)")]
        [SerializeField]
        private bool isCheckTop = false;

        private Vector2 _moveDelta;

        private Vector2 _lastPosition;

        internal float SpeedVertical;

        [Tooltip("当前角色受到的重力大小(仅在开启检测地面时有效,如果未开启检测地面,当前角色不会受到重力影响!)")]
        public float gravityScale = 1;
        [global::UnityEngine.Serialization.FormerlySerializedAs("MaxFallingVelocity")]
        [Tooltip("下落的最大速度(默认:20)")]
        [Range(1, 100)]
        public float maxFallingVelocity = 20;

        private float _rigidbodyGravityScale = 0;

        private CheckGround _checkGround;

        private CheckWall _checkWall;

        private CheckTop _checkTop;

        [Tooltip("地面倾斜角度,0代表水平无倾斜,如果为30,则是倾斜角度在30以内的地面,才会被认为是地面!")]
        [Range(1, 45)]
        public float groundAngle = 35;

        [Tooltip("墙面倾斜角度,0代表竖直向上,如果为30,则是竖直向上往左或往右偏移30度以内认为是墙面!")]
        [Range(1, 45)]
        public float wallAngle = 20;


        [Tooltip("角色头顶碰到的游戏物体的倾斜角度,在该角度范围内，认为碰到游戏物体!")]
        [Range(1, 60)]
        public float topAngle = 45;

        private bool _isGrounded;

        private bool _isTouchingWall;

        private bool _isTouchingCeiling;

        private Vector2 _velocity;

        [global::UnityEngine.Serialization.FormerlySerializedAs("MaxJumpCount")]
        [Tooltip("角色最多连续跳跃次数")]
        public int maxJumpCount = 1;

        private int _currentJumpCount = 0;

        private bool _isJumping = false;
        private float _jumpAbortSpeedReduction = 0;
        #endregion

        #region 属性

        /// <summary>
        /// 是否在地面上
        /// </summary>
        public bool IsGrounded
        {
            get
            {
                if (SpeedVertical > 0)
                {
                    _isGrounded = false;
                    return false;
                }

                return _isGrounded;
            }
            internal protected set
            {
                bool changed = _isGrounded != value;
                _isGrounded = value;
                if (changed)
                {
                    onGroundValueChange?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// 与地面的接触信息(比如 接触点 法线 等)
        /// </summary>
        public RaycastHit2D GroundInfo
        {
            get; internal protected set;
        }

        /// <summary>
        /// 是否接触到墙面
        /// </summary>
        public bool IsTouchingWall
        {
            get
            {
                return _isTouchingWall;
            }
            internal protected set
            {
                bool changed = _isTouchingWall != value;
                _isTouchingWall = value;
                if (changed)
                {
                    onTouchWallValueChange?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// 与墙面的接触信息(比如 接触点 法线 等)
        /// </summary>
        public RaycastHit2D WallInfo
        {
            get; internal protected set;
        }

        /// <summary>
        /// 头顶是否接触到游戏物体
        /// </summary>
        public bool IsTouchingCeiling
        {
            get
            {
                return _isTouchingCeiling;
            }
            internal protected set
            {

                bool changed = _isTouchingCeiling != value;
                _isTouchingCeiling = value;
                if (changed)
                {
                    onTopValueChange?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// 与顶部的接触信息(比如 接触点 法线 等)
        /// </summary>
        public RaycastHit2D TopInfo
        {
            get; internal protected set;
        }


        /// <summary>
        /// 移动速度
        /// </summary>
        public Vector2 Velocity
        {
            get
            {
                return _velocity;
            }
            internal protected set
            {
                bool changed = _velocity != value;
                _velocity = value;
                if (changed)
                {
                    onVelocityValueChange?.Invoke(value);
                }
            }
        }

        public Rigidbody2D Rigidbody
        {
            get
            {
                if (_rigidbody2d == null)
                {
                    _rigidbody2d = GetComponent<Rigidbody2D>();
                }

                return _rigidbody2d;
            }
        }


        /// <summary>
        /// 是否检测角色当前是否在地面上(默认检测)
        /// </summary>
        public bool ChecksGround
        {
            get
            {
                return isCheckGround;
            }
            set
            {
                isCheckGround = value;
                if (isCheckGround == false)
                {
                    IsGrounded = false;
                    ResetSpeedVertical();
                    Rigidbody.gravityScale = _rigidbodyGravityScale;
                }
            }
        }

        /// <summary>
        /// 是否检测角色当前是否接触墙面(默认不检测)
        /// </summary>
        public bool ChecksWalls
        {
            get
            {
                return isCheckWall;
            }
            set
            {
                isCheckWall = value;
                if (isCheckWall == false)
                {
                    IsTouchingWall = false;
                }
            }
        }

        /// <summary>
        /// 是否检测角色头顶是否碰到游戏物体(默认不检测)
        /// </summary>
        public bool ChecksCeiling
        {
            get
            {
                return isCheckTop;
            }
            set
            {
                isCheckTop = value;
                if (isCheckTop == false)
                {
                    IsTouchingCeiling = false;
                }
            }
        }

        /// <summary>
        /// 当前已经跳跃的数量
        /// </summary>
        public int CurrentJumpCount
        {
            get
            {
                return _currentJumpCount;
            }

            set
            {
                bool changed = _currentJumpCount != value;
                _currentJumpCount = value;
                if (changed)
                {
                    onJumpCountValueChange?.Invoke(value);
                }
            }

        }

        #endregion


        #region 事件

        [Tooltip("是否在地面的值发生改变时触发")]
        [Header("是否在地面的值发生改变时触发")]
        public BoolUnityEvent onGroundValueChange;
        [Tooltip("是否接触墙面的值发生改变时触发")]
        [Header("是否接触墙面的值发生改变时触发")]
        public BoolUnityEvent onTouchWallValueChange;
        [Tooltip("头顶是否接触游戏物体的值发生改变时触发")]
        [Header("头顶是否接触游戏物体的值发生改变时触发")]
        public BoolUnityEvent onTopValueChange;
        [Tooltip("移动速度改变时触发")]
        [Header("移动速度改变时触发")]
        public Vector2UnityEvent onVelocityValueChange;
        [Tooltip("已跳跃的数量发生改变时触发")]
        [Header("已跳跃的数量发生改变时触发")]
        public IntUnityEvent onJumpCountValueChange;

        #endregion

        #region 生命周期

        private void Awake()
        {
            if (Rigidbody == null)
            {
                throw new System.Exception("未查询到刚体组件!");
            }

            _rigidbodyGravityScale = Rigidbody.gravityScale;

            _currentLayer = gameObject.layer;
            CheckLayerMask.Physics2DSetting(gameObject.layer);
        }

        private void Reset()
        {
            if (Rigidbody == null)
            {
                throw new System.Exception("未查询到刚体组件!");
            }

            gravityScale = Rigidbody.gravityScale;
        }

        private void FixedUpdate()
        {
            if (!EnsureColliders())
            {
                return;
            }

            int version = PhysicsVersion;
            UpdateSpeedVertical();
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            Move();
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            JumpHeld();
        }

        private void OnEnable()
        {
            _rigidbodyGravityScale = Rigidbody.gravityScale;
            RefreshColliders();
            _lastPosition = Rigidbody.position;
        }

        private void OnDisable()
            => ReleaseGravityAndResetState();

        private void ReleaseGravityAndResetState()
        {
            PhysicsVersion++;
            if (Rigidbody)
            {
                Rigidbody.gravityScale = _rigidbodyGravityScale;
            }
            // 停用不发送额外业务事件，也不把上次输入或接触缓存带入下一次启用。
            _moveDelta = Vector2.zero;
            _velocity = Vector2.zero;
            SpeedVertical = 0;
            _isJumping = false;
            _jumpAbortSpeedReduction = 0;
            _currentJumpCount = 0;
            _isGrounded = _isTouchingWall = _isTouchingCeiling = false;
            _checkGround = null;
            _checkWall = null;
            _checkTop = null;
            GroundInfo = WallInfo = TopInfo = default;
            Array.Clear(Hits, 0, Hits.Length);
            Array.Clear(Contacts, 0, Contacts.Length);
        }

        private void Update()
        {
            if (!EnsureColliders())
            {
                return;
            }

            if (autoRotation)
            {
                // 修改当前人物旋转角度
                AutoRotation();
            }

            if (_currentLayer != gameObject.layer)
            {
                CheckLayerMask.Physics2DSetting(gameObject.layer);
                _currentLayer = gameObject.layer;
            }

            PhysicsCheck();


        }


        #endregion


        #region 方法

        /// <summary>
        /// 移动(请在FixedUpdate中调用)
        /// </summary>
        /// <param name="delta">移动的增量</param>
        public void Move(Vector2 delta)
        {
            if (!isActiveAndEnabled || !HasValidColliders)
            {
                return;
            }

            if (IsIgnoreMoveX(delta))
            {
                delta.x = 0;
            }

            _moveDelta += delta;
        }


        /// <summary>
        /// 水平方向移动(请在FixedUpdate中调用)
        /// </summary>
        /// <param name="speed"></param>
        public void MoveHorizontal(float speed)
        {
            if (IsGrounded && ChecksGround)
            {
                Vector3 dir = Vector3.Cross(GroundInfo.normal, Vector3.forward);
                Move(dir.normalized * speed * Time.fixedDeltaTime);
            }
            else
            {
                Move(Vector2.right * speed * Time.fixedDeltaTime);
            }
        }

        private void Move()
        {
            int version = PhysicsVersion;
            if (Mathf.Abs(_moveDelta.x) <= 0.01f && Mathf.Abs(_moveDelta.y) <= 0.01f)
            {
                Velocity = Vector2.zero;
                if (!IsPhysicsCurrent(version))
                {
                    return;
                }

                _moveDelta = Vector2.zero;
                _lastPosition = Rigidbody.position;
                return;
            }

            // 移动到目标位置
            Rigidbody.MovePosition(Rigidbody.position + _moveDelta);

            Velocity = (Rigidbody.position - _lastPosition) / Time.fixedDeltaTime;
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            _lastPosition = Rigidbody.position;
            _moveDelta = Vector2.zero;

            if (ChecksCeiling)
            {
                if (IsTouchingCeiling && SpeedVertical > 0)
                {
                    ResetSpeedVertical();
                }
            }
            else
            {
                if (_checkTop == null)
                {
                    _checkTop = new CheckTop();
                }

                int count = Rigidbody.GetContacts(Contacts);
                _checkTop.Check(Contacts, this, count, topAngle);
                if (!IsPhysicsCurrent(version))
                {
                    return;
                }

                if (_checkTop.IsTopByContact && SpeedVertical > 0)
                {
                    ResetSpeedVertical();
                }
            }


        }

        /// <summary>
        /// 跳跃
        /// </summary>
        /// <param name="speed">跳跃初速度,值越大跳的越高</param>
        public void Jump(float speed)
        {
            if (!isActiveAndEnabled || !HasValidColliders)
            {
                return;
            }

            if (!ChecksGround)
            {
                Debug.LogWarning("是否检测地面的选项未开启，无法跳跃!");
                return;
            }

            if (CurrentJumpCount >= maxJumpCount)
            {
                return;
            }

            int version = PhysicsVersion;
            CurrentJumpCount++;
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            SpeedVertical = speed;
            if (SpeedVertical > 0)
            {
                IsGrounded = false;
            }

            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            // 重置速度
            Velocity = Vector2.zero;
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            _moveDelta = Vector2.zero;
            _lastPosition = Rigidbody.position;
        }

        /// <summary>
        /// 跳跃(可提前中断,中断后跳跃高度会降低,适用于长按跳的更高的情况)
        /// </summary>
        /// <param name="speed">跳跃初速度,值越大跳的越高 </param>
        /// <param name="jumpAbortSpeedReduction">跳跃中断后的减速度, 值越大减速越快,跳的越低</param>
        public void JumpStart(float speed, float jumpAbortSpeedReduction)
        {
            if (!isActiveAndEnabled || !HasValidColliders || !ChecksGround)
            {
                return;
            }

            if (CurrentJumpCount >= maxJumpCount)
            {
                return;
            }

            int version = PhysicsVersion;
            Jump(speed);
            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            _isJumping = true;

            if (jumpAbortSpeedReduction < 0)
            {
                jumpAbortSpeedReduction = 0;
            }

            this._jumpAbortSpeedReduction = jumpAbortSpeedReduction;
        }

        private void JumpHeld()
        {
            if (_jumpAbortSpeedReduction == 0)
            {
                return;
            }

            if (!_isJumping && SpeedVertical > 0)
            {
                SpeedVertical -= _jumpAbortSpeedReduction * Time.fixedDeltaTime;
            }

            if (SpeedVertical <= 0)
            {
                _jumpAbortSpeedReduction = 0;
                _isJumping = false;
            }

        }

        /// <summary>
        /// 中断跳跃
        /// </summary>
        public void JumpEnd()
        {
            _isJumping = false;
        }

        /// <summary>
        /// 重置竖直方向速度
        /// </summary>
        public void ResetSpeedVertical()
        {
            SpeedVertical = 0;
        }

        private void UpdateSpeedVertical()
        {
            // 如果不检测地面 则不需要控制竖直方向速度
            if (!ChecksGround)
            {
                return;
            }

            if (_checkGround != null && _checkGround.IsGroundedByContacts && SpeedVertical <= 0)
            {
                SpeedVertical = 0;
                Rigidbody.gravityScale = _rigidbodyGravityScale;
            }
            else
            {
                Rigidbody.gravityScale = 0;

                // 如果下方有碰撞体 没有必要往下落
                if (_checkGround != null)
                {
                    _contactCount = Rigidbody.GetContacts(Contacts);
                    if (_checkGround.HasColliderBelow(Contacts, _contactCount) && SpeedVertical < 0)
                    {
                        return;
                    }
                }

                SpeedVertical = CharacterMotorMath.IntegrateGravity(SpeedVertical, Physics2D.gravity.y, gravityScale, Time.fixedDeltaTime, maxFallingVelocity);

                // 竖直方向的移动
                Move(Vector2.up * SpeedVertical * Time.fixedDeltaTime);
            }
        }

        /// <summary>重新选择本刚体的四向碰撞体；运行时添加或移除碰撞体后显式调用。</summary>
        public bool RefreshColliders()
        {
            PhysicsVersion++;
            Top = Bottom = Left = Right = null;
            List<Collider2D> colliders = new List<Collider2D>();
            Rigidbody.GetAttachedColliders(colliders);

            for (int i = 0; i < colliders.Count; i++)
            {
                Collider2D collider = colliders[i];
                if (!IsUsable(collider))
                {
                    continue;
                }

                bool iscollider2d = !(collider is BoxCollider2D);
                bool iscirclecollider2d = !(collider is CircleCollider2D);
                bool iscapsulecollider2d = !(collider is CapsuleCollider2D);

                if (iscollider2d && iscirclecollider2d && iscapsulecollider2d)
                {
                    continue;
                }

                if (Top == null)
                {
                    Top = collider;
                }
                else
                {
                    if (collider.bounds.max.y > Top.bounds.max.y)
                    {
                        Top = collider;
                    }
                }

                if (Bottom == null)
                {
                    Bottom = collider;
                }
                else
                {
                    if (collider.bounds.min.y < Bottom.bounds.min.y)
                    {
                        Bottom = collider;
                    }
                }

                if (Right == null)
                {
                    Right = collider;
                }
                else
                {
                    if (collider.bounds.max.x > Right.bounds.max.x)
                    {
                        Right = collider;
                    }
                }

                if (Left == null)
                {
                    Left = collider;
                }
                else
                {
                    if (collider.bounds.min.x < Left.bounds.min.x)
                    {
                        Left = collider;
                    }
                }


            }

            _isColliderReady = Left && Right && Bottom && Top;
            if (!_isColliderReady)
            {
                ReleaseGravityAndResetState();
                Debug.LogError($"{nameof(CharacterController2D)} on '{name}': Rigidbody2D requires an attached, non-trigger BoxCollider2D, CircleCollider2D or CapsuleCollider2D. Add one and call RefreshColliders().", this);
            }
            return _isColliderReady;

        }

        private void PhysicsCheck()
        {
            int version = PhysicsVersion;
            // 获取与当前刚体接触的游戏物体

            if (ChecksGround || ChecksWalls || ChecksCeiling)
            {
                _contactCount = Rigidbody.GetContacts(Contacts);
            }

            if (ChecksGround)
            {
                if (_checkGround == null)
                {
                    _checkGround = new CheckGround();
                }

                _checkGround.Check(Contacts, this, _contactCount, groundAngle);

                if (IsGrounded)
                {
                    CurrentJumpCount = 0;
                }
            }

            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            if (ChecksWalls)
            {
                if (_checkWall == null)
                {
                    _checkWall = new CheckWall();
                }

                _checkWall.Check(Contacts, this, _contactCount, wallAngle);
            }

            if (!IsPhysicsCurrent(version))
            {
                return;
            }

            if (ChecksCeiling)
            {
                if (_checkTop == null)
                {
                    _checkTop = new CheckTop();
                }

                _checkTop.Check(Contacts, this, _contactCount, topAngle);
            }

#if UNITY_EDITOR
            if (Mathf.FloorToInt(Time.time) != _lastSecond)
            {
                _lastSecond = Mathf.FloorToInt(Time.time);
                CheckCount = 0;
            }
#endif

        }

        internal static ContactFilter2D CreateLegacyFilter(int layerMask, float minDepth, float maxDepth)
        {
            ContactFilter2D result = default(ContactFilter2D);
            result.useTriggers = Physics2D.queriesHitTriggers;
            result.SetLayerMask(layerMask);
            result.SetDepth(minDepth, maxDepth);
            return result;
        }


        internal int Raycast(CircleCollider2D circle, Vector2 dir)
        {

            Vector2 point = circle.bounds.center;

            float r = circle.radius * Mathf.Max(Mathf.Abs(circle.transform.lossyScale.x), Mathf.Abs(circle.transform.lossyScale.y));

#if UNITY_6000_0_OR_NEWER
            ContactFilter2D contactFilter = CreateLegacyFilter(CheckLayerMask.value, float.NegativeInfinity, float.PositiveInfinity);
            return Physics2D.defaultPhysicsScene.CircleCast(point, r, dir, DISTANCE, contactFilter, Hits);

#else
            return Physics2D.CircleCastNonAlloc(point, r, dir, Hits, Distance, CheckLayerMask.value);
#endif
        }

        internal int Raycast(BoxCollider2D box, Vector2 dir)
        {
            Vector2 point = box.bounds.center;
            Vector2 size = CharacterMotorMath.ScaledColliderSize(box.size, box.transform.lossyScale);
            float angle = GetColliderCastAngle(box);

#if UNITY_6000_0_OR_NEWER
            ContactFilter2D contactFilter = CreateLegacyFilter(CheckLayerMask.value, float.NegativeInfinity, float.PositiveInfinity);
            return Physics2D.defaultPhysicsScene.BoxCast(point, size, angle, dir, DISTANCE, contactFilter, Hits);
#else
            return Physics2D.BoxCastNonAlloc(point, size, angle, dir, Hits, Distance, CheckLayerMask.value);
#endif

        }

        internal int Raycast(CapsuleCollider2D capsule, Vector2 dir)
        {
            Vector2 point = capsule.bounds.center;
            Vector2 size = CharacterMotorMath.ScaledColliderSize(capsule.size, capsule.transform.lossyScale);
            float angle = GetColliderCastAngle(capsule);
            CapsuleDirection2D d = capsule.direction;

#if UNITY_6000_0_OR_NEWER
            ContactFilter2D contactFilter = CreateLegacyFilter(CheckLayerMask.value, float.NegativeInfinity, float.PositiveInfinity);
            return Physics2D.defaultPhysicsScene.CapsuleCast(point, size, d, angle, dir, DISTANCE, contactFilter, Hits);
#else
            return Physics2D.CapsuleCastNonAlloc(point, size, d, angle, dir, Hits, Distance, CheckLayerMask.value);
#endif
        }

        internal static float GetColliderCastAngle(Collider2D collider)
        {
            float angle = collider.transform.eulerAngles.z;
            var body = collider.attachedRigidbody;
            // 刚体物理位姿可先于Transform更新；保留子Collider的相对旋转，不能只读旧Transform。
            return body ? angle + Mathf.DeltaAngle(body.transform.eulerAngles.z, body.rotation) : angle;
        }

        private void AutoRotation()
        {

            if (!ChecksGround)
            {
                return;
            }

            if (IsGrounded)
            {
                _isInTheAir = false;
                float angle = -Vector2.SignedAngle(GroundInfo.normal, Vector2.up);

                if (transform.right.x < 0)
                {
                    angle = -angle;
                }

                transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, angle);
            }
            else
            {
                if (!_isInTheAir)
                {
                    transform.eulerAngles = new Vector3(transform.eulerAngles.x, transform.eulerAngles.y, 0);
                }

                _isInTheAir = true;
            }

        }

        private bool IsIgnoreMoveX(Vector2 delta)
        {
            if (!ChecksWalls)
            {
                return false;
            }

            if (!ChecksGround)
            {
                return false;
            }

            if (IsTouchingWall && !IsGrounded)
            {
                // 判断法线方向和移动方向是否相反 不能往墙上移动
                if (CharacterMotorMath.BlocksHorizontal(delta.x, WallInfo.normal.x))
                {
                    return true;
                }
            }

            if (!IsTouchingWall && !IsGrounded)
            {
                int count = Rigidbody.GetContacts(Contacts);
                for (int i = 0; i < count; i++)
                {
                    if (Contacts[i].collider == null)
                    {
                        continue;
                    }

                    if (Contacts[i].collider.isTrigger)
                    {
                        continue;
                    }

                    if (CharacterMotorMath.BlocksHorizontal(delta.x, Contacts[i].normal.x))
                    {
                        return true;
                    }
                }
            }


            return false;
        }

        #endregion

    }
}
