using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.GraphicsBuffer;

namespace YLFramework.Platform2D
{


    public class Moveable : MonoBehaviour
    {
        #region 字段

        [SerializeField]
        [Tooltip("移动速度")]
        private float speed = 4;

        [SerializeField]
        [Tooltip("更新模式,Normal:普通模式 UnscaledTime:忽略时间缩放")]
        private UpdateMode updateMode = UpdateMode.Normal;

        [SerializeField]
        [Tooltip("移动类型")]
        private MoveType moveType;

        [SerializeField]
        [Tooltip("路径类型,Positions:使用坐标点作为路径 Transforms:使用transform作为路径")]
        private PositionType positionType;

        [SerializeField]
        private List<Transform> transforms = new List<Transform>();
        [SerializeField]
        private List<Vector3> positions = new List<Vector3>();

        private readonly MoveablePath path = new MoveablePath();
        private readonly List<Vector3> pathPoints = new List<Vector3>();
        private bool hasStarted;
        private int pathVersion;

        private float timer;

        [SerializeField]
        [Tooltip("延迟时间")]
        private float delay = 0;
        private float delayTimer = 0;

        [SerializeField]
        [Tooltip("是否自动运行?")]
        private bool runOnAwake = true;

        private bool isRunning = false;

        #endregion

        #region 属性

        /// <summary>
        /// 移动速度
        /// </summary>
        public float Speed
        {
            get {
                return speed;
            }
            set {
                speed = value;
            }
        }

        /// <summary>
        /// 移动类型
        /// </summary>
        public MoveType MoveType
        {
            get {
                return moveType;
            }
            set {
                moveType = value;
            }
        }

        /// <summary>
        /// 路径点
        /// </summary>
        public List<Transform> Transforms => transforms;
        /// <summary>
        /// 路径点
        /// </summary>
        public List<Vector3> Positions => positions;

        public PositionType PositionType { get => positionType; set => positionType = value; }
        public UpdateMode UpdateMode { get => updateMode; set => updateMode = value; }

        /// <summary>
        /// 延迟时间
        /// </summary>
        public float Delay
        {
            get {
                return delay;
            }
            set {
                delay = value;
            }
        }

        /// <summary>
        /// 是否正在移动中
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 路径总长度
        /// </summary>
        public float Distance
        {
            get
            {
                if (!TryGetPathPoints(out _)) return 0;
                float distance = 0;
                for (int i = 1; i < pathPoints.Count; i++) distance += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
                return distance;
            }
        }

        /// <summary>
        /// 从起点移动到终点所需要的时间
        /// </summary>
        public float Time => speed > 0 && !float.IsInfinity(speed) ? Distance / speed : 0;


        #endregion

        #region 事件

        [Header("开始移动时触发")]
        [Tooltip("开始移动时触发")]
        public UnityEvent onStartRun;

        [Header("移动时触发")]
        [Tooltip("移动时触发")]
        public Vector3UnityEvent onMove;

        [Header("暂停时或取消暂停时触发")]
        [Tooltip("暂停时或取消暂停时触发")]
        public BoolUnityEvent onPause;

        [Header("结束移动时触发")]
        [Tooltip("结束移动时触发")]
        public UnityEvent onStop;

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            if (runOnAwake)
                Run();
        }

        protected virtual void FixedUpdate()
        {
            if (!isRunning) return;

            if (delayTimer >= delay)
                Move();
            else {

                switch (updateMode)
                {
                    case UpdateMode.Normal:
                        delayTimer += UnityEngine.Time.fixedDeltaTime;
                        break;
                    case UpdateMode.UnscaledTime:
                        delayTimer += UnityEngine.Time.fixedUnscaledDeltaTime;
                        break;
                }


            }
        }



        #endregion

        #region 方法

        public bool ValidatePath(out string error)
        {
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed)) { error = "Speed must be finite and greater than zero."; return false; }
            if (delay < 0 || float.IsNaN(delay) || float.IsInfinity(delay)) { error = "Delay must be finite and non-negative."; return false; }
            return TryGetPathPoints(out error);
        }

        private bool TryGetPathPoints(out string error)
        {
            pathPoints.Clear();
            int count = positionType == PositionType.Positions ? positions?.Count ?? 0 : transforms?.Count ?? 0;
            if (count == 0) { error = "Path has no nodes (index 0)."; return false; }
            for (int i = 0; i < count; i++)
            {
                if (positionType == PositionType.Transforms && !transforms[i]) { error = $"Missing Transform at path node {i}."; return false; }
                var point = positionType == PositionType.Positions ? positions[i] : transforms[i].position;
                if (!MoveablePath.IsFinite(point)) { error = $"Path node {i} contains NaN/Infinity."; return false; }
                pathPoints.Add(point);
            }
            error = null;
            return true;
        }

        private bool Init()
        {
            pathVersion++;
            if (ValidatePath(out string error) && path.TryBuild(pathPoints, out error)) return true;
            isRunning = hasStarted = false;
            Debug.LogError($"{GetType().Name} on '{name}': {error}", this);
            return false;
        }


        private void Move()
        {
            if (speed <= 0 || float.IsNaN(speed) || float.IsInfinity(speed)) { Stop(); return; }

            // 向目标位置移动
            switch (updateMode)
            {
                case UpdateMode.Normal:
                    timer += UnityEngine.Time.fixedDeltaTime;
                    break;
                case UpdateMode.UnscaledTime:
                    timer += UnityEngine.Time.fixedUnscaledDeltaTime;
                    break;
            }

            float t = MoveablePath.Progress(timer, path.Distance / speed, moveType);

            Vector3 target = GetPosition(t);
            int version = pathVersion;
            onMove?.Invoke(target);
            if (!this || !isActiveAndEnabled || !isRunning || version != pathVersion) return;
            Move(target);
            if ((moveType == MoveType.Once && t >= 1) || path.Distance == 0) Stop();
        }

        /// <summary>
        /// 移动时触发
        /// </summary>
        /// <param name="target"></param>
        protected virtual void Move(Vector3 target)
        {
            transform.position = target;
        }

        /// <summary>
        /// 开始移动
        /// </summary>
        public void Run()
        {
            // 正在运行中 不需要再次运行
            if (isRunning)
                return;

            timer = 0;
            delayTimer = 0;
            if (!Init()) return;
            hasStarted = true;
            isRunning = true;
            onStartRun?.Invoke();
        }

        /// <summary>
        /// 暂停移动
        /// </summary>
        /// <param name="pause"></param>
        public void Pause(bool pause)
        {
            if (!hasStarted || isRunning == !pause) return;
            isRunning = !pause;
            onPause?.Invoke(pause);
        }

        /// <summary>
        /// 停止移动
        /// </summary>
        public void Stop()
        {
            if (!hasStarted && !isRunning) return;
            pathVersion++;
            hasStarted = false;
            isRunning = false;
            delayTimer = 0;
            timer = 0;
            onStop?.Invoke();
        }

        /// <summary>
        /// 添加一个路径点
        /// </summary>
        /// <param name="position"></param>
        public void AddPosition(Vector3 position)
        {
            positions.Add(position);
            Init();
        }

        /// <summary>
        /// 添加一个路径点
        /// </summary>
        /// <param name="transform"></param>
        public void AddPosition(Transform transform)
        {
            transforms.Add(transform);
            Init();
        }

        /// <summary>
        /// 移除一个路径点
        /// </summary>
        /// <param name="index">路径的下标</param>
        public void RemovePosition(int index)
        {
            if (positionType == PositionType.Positions)
            {
                if(index >= 0 && index < positions.Count)
                    positions.RemoveAt(index);
            }
            else
            {
                if (index >= 0 && index < transforms.Count)
                    transforms.RemoveAt(index);
            }

            Init();
        }

        /// <summary>
        /// 清空所有的路径点
        /// </summary>
        public void ClearPosition()
        {
            if (positionType == PositionType.Positions)
                positions.Clear();
            else
                transforms.Clear();

            Init();
        }

        private Vector3 GetPosition(float t)
            => path.Evaluate(t);

        #endregion

    }

}
