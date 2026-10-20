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




    public abstract class OverlapBase<T,Event> : MonoBehaviour where Event : UnityEvent<OverlapHit<T>>
    {

        #region 查询状态

        internal protected readonly T[] results = new T[100];
        private readonly OverlapQueryCycle queryCycle = new OverlapQueryCycle();
        protected bool CheckInvalidated => queryCycle.Invalidated;

        #endregion


        #region 字段

        [SerializeField]
        protected LayerMask layerMask;

        [Range(0.01f,100)]
        [SerializeField]
        [Tooltip("每秒检测次数(默认:10)")]
        protected float detectionNumber = 10;

        private float timer;

        [SerializeField]
        [Tooltip("更新模式")]
        protected OverlapUpdateMode updateModel;


        protected bool isRunning = false;

        [SerializeField]
        [Tooltip("默认运行")]
        protected bool runOnAwake = true;

        /// <summary>
        /// 保存检测结果
        /// </summary>
        protected List<OverlapHit<T>> last_results = new List<OverlapHit<T>>();

        #endregion

        #region 属性

        /// <summary>
        /// 检测层级
        /// </summary>
        public LayerMask LayerMask
        {
            get {
                return layerMask;
            }
            set {
                layerMask = value;
            }
        }

        /// <summary>
        /// 每秒检测次数
        /// </summary>
        public float DetectionNumber
        {
            get {
                return detectionNumber;
            }
            set {
                detectionNumber = Mathf.Clamp(value, 0.01f, 100);
            }
        }

        /// <summary>
        /// 更新模式
        /// </summary>
        public OverlapUpdateMode UpdateMode
        {
            get {
                return updateModel;
            }
            set {
                updateModel = value;
            }
        }

        /// <summary>
        /// 是否正在运行中或检测中
        /// </summary>
        public bool Running
        {
            get {
                return isRunning;
            }
            set {
                isRunning = value;
            }
        }

        [Tooltip("检测结果集合")]
        [HideInInspector]
        public List<OverlapHit<T>> Results = new List<OverlapHit<T>>();

        #endregion

        #region 生命周期

        protected virtual void Awake() {
            if(runOnAwake)
                isRunning = true;
        }

        protected virtual void OnDisable()
        {
            queryCycle.Invalidate();
            timer = 0;
            Results.Clear();
            last_results.Clear();
        }


        protected virtual void Update()
        {
            if (!isRunning)
                return;

            switch (updateModel)
            {
                case OverlapUpdateMode.Normal:
                    timer += Time.deltaTime;
                    break;
                case OverlapUpdateMode.UnscaledTime:
                    timer += Time.unscaledDeltaTime;
                    break;
            }

            if (timer > 1.0f / detectionNumber)
            {
                CheckOverlap();
                timer = 0;
            }

        }

        #endregion

        #region 方法

        public abstract void CheckOverlap();

        protected bool TryBeginCheck() => queryCycle.TryBegin();

        protected void EndCheck()
        {
            Array.Clear(results, 0, results.Length);
            queryCycle.End();
        }

        protected void CheckCapacity(int count)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (queryCycle.ReportCapacity(count, results.Length))
                Debug.LogWarning($"{GetType().Name}: query reached capacity {results.Length}; results may be truncated (可能截断).", this);
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
            last_results.Clear();
            last_results.AddRange(Results);
            Results.Clear();
        }

        protected void AddResults(T collider,Vector3 origin)
        {
            if (CheckInvalidated || (collider is UnityEngine.Object unityObject && !unityObject))
                return;
            if (IsContains(Results, collider))
                return;

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

            Results.Add(hit);

            if (!IsContains(last_results, collider))
                onColliderEnter?.Invoke(hit);

        }


        protected void OverlapEnd()
        {
            for (int i = 0; i < last_results.Count && !CheckInvalidated; i++)
            {
                var item = last_results[i];
                if (!IsContains(Results, item.collider))
                    onColliderExit?.Invoke(item);
            }
        }


        protected bool IsContains(List<OverlapHit<T>> list, T collider)
        {
            foreach (var item in list)
            {
                if(EqualityComparer<T>.Default.Equals(item.collider, collider))
                    return true;
            }
            return false;
        }

        #endregion


    }
}
