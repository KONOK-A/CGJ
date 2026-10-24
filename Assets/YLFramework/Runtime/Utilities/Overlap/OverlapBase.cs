using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Overlap
{

    public enum OverlapUpdateMode
    {
        [Tooltip("普通模式")]
        Normal,
        [Tooltip("忽略时间缩放")]
        UnscaledTime
    }

    [Serializable]
    public struct OverlapHit<T>
    {
        public T collider;
        public Vector2 point;
    }




    public abstract class OverlapBase<T, Event> : MonoBehaviour where Event : UnityEvent<OverlapHit<T>>
    {

        #region 查询状态

        internal protected readonly T[] ResultBuffer = new T[100];
        private readonly OverlapQueryCycle _queryCycle = new OverlapQueryCycle();
        protected bool IsCheckInvalidated => _queryCycle.IsInvalidated;

        #endregion


        #region 字段

        [SerializeField]
        protected LayerMask layerMask;

        [Range(0.01f, 100)]
        [SerializeField]
        [Tooltip("每秒检测次数(默认:10)")]
        protected float detectionNumber = 10;

        private float _timer;

        [SerializeField]
        [Tooltip("更新模式")]
        protected OverlapUpdateMode updateModel;


        protected bool IsQueryRunning = false;

        [SerializeField]
        [Tooltip("默认运行")]
        protected bool runOnAwake = true;

        /// <summary>
        /// 保存检测结果
        /// </summary>
        protected List<OverlapHit<T>> LastResults = new List<OverlapHit<T>>();

        #endregion

        #region 属性

        /// <summary>
        /// 检测层级
        /// </summary>
        public LayerMask LayerMask
        {
            get
            {
                return layerMask;
            }
            set
            {
                layerMask = value;
            }
        }

        /// <summary>
        /// 每秒检测次数
        /// </summary>
        public float DetectionNumber
        {
            get
            {
                return detectionNumber;
            }
            set
            {
                detectionNumber = Mathf.Clamp(value, 0.01f, 100);
            }
        }

        /// <summary>
        /// 更新模式
        /// </summary>
        public OverlapUpdateMode UpdateMode
        {
            get
            {
                return updateModel;
            }
            set
            {
                updateModel = value;
            }
        }

        /// <summary>
        /// 是否正在运行中或检测中
        /// </summary>
        public bool IsRunning
        {
            get
            {
                return IsQueryRunning;
            }
            set
            {
                IsQueryRunning = value;
            }
        }

        [global::UnityEngine.Serialization.FormerlySerializedAs("Results")]
        [Tooltip("检测结果集合")]
        [HideInInspector]
        public List<OverlapHit<T>> results = new List<OverlapHit<T>>();

        #endregion

        #region 生命周期

        protected virtual void Awake()
        {
            if (runOnAwake)
            {
                IsQueryRunning = true;
            }
        }

        protected virtual void OnDisable()
        {
            _queryCycle.Invalidate();
            _timer = 0;
            results.Clear();
            LastResults.Clear();
        }


        protected virtual void Update()
        {
            if (!IsQueryRunning)
            {
                return;
            }

            switch (updateModel)
            {
                case OverlapUpdateMode.Normal:
                    _timer += Time.deltaTime;
                    break;
                case OverlapUpdateMode.UnscaledTime:
                    _timer += Time.unscaledDeltaTime;
                    break;
            }

            if (_timer > 1.0f / detectionNumber)
            {
                CheckOverlap();
                _timer = 0;
            }

        }

        #endregion

        #region 方法

        public abstract void CheckOverlap();

        protected bool TryBeginCheck() => _queryCycle.TryBegin();

        protected void EndCheck()
        {
            Array.Clear(ResultBuffer, 0, ResultBuffer.Length);
            _queryCycle.End();
        }

        protected void CheckCapacity(int count)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_queryCycle.ReportCapacity(count, ResultBuffer.Length))
            {
                Debug.LogWarning($"{GetType().Name}: query reached capacity {ResultBuffer.Length}; results may be truncated (可能截断).", this);
            }
#endif
        }

        #endregion


        #region 事件

        public Event onColliderEnter;

        public Event onColliderExit;

        #endregion


        #region 生命周期

        protected virtual void Reset()
        {
        }

        #endregion


        #region 方法

        protected void ClearResults()
        {
            LastResults.Clear();
            LastResults.AddRange(results);
            results.Clear();
        }

        protected void AddResults(T collider, Vector3 origin)
        {
            if (IsCheckInvalidated || (collider is UnityEngine.Object unityObject && !unityObject))
            {
                return;
            }

            if (Contains(results, collider))
            {
                return;
            }

            OverlapHit<T> hit = new OverlapHit<T>();
            hit.collider = collider;

            if (collider is Collider2D)
            {
                Collider2D collider2D = collider as Collider2D;
                hit.point = collider2D.ClosestPoint(origin);
            }
            else if (collider is Collider)
            {
                Collider collider3D = collider as Collider;
                hit.point = collider3D.ClosestPoint(origin);
            }

            results.Add(hit);

            if (!Contains(LastResults, collider))
            {
                onColliderEnter?.Invoke(hit);
            }
        }


        protected void OverlapEnd()
        {
            for (int i = 0; i < LastResults.Count && !IsCheckInvalidated; i++)
            {
                var item = LastResults[i];
                if (!Contains(results, item.collider))
                {
                    onColliderExit?.Invoke(item);
                }
            }
        }


        protected bool Contains(List<OverlapHit<T>> list, T collider)
        {
            foreach (var item in list)
            {
                if (EqualityComparer<T>.Default.Equals(item.collider, collider))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion


    }
}
