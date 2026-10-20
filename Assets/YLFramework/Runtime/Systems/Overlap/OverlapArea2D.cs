using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Overlap
{
    public enum OverlapAreaMode {

        Position ,
        Transform
    }

    /// <summary>
    /// 区域检测2D
    /// </summary>
    public class OverlapArea2D : OverlapBase2D
    {

        #region 字段

        [SerializeField]
        [Tooltip("检测坐标方式")]
        private OverlapAreaMode _mode = OverlapAreaMode.Position;

        [SerializeField]
        [Tooltip("检测区域坐标")]
        protected List<Vector3> positions = new List<Vector3>();

        [SerializeField]
        [Tooltip("检测区域位置")]
        protected List<Transform> transforms = new List<Transform>();


        #endregion


        #region 属性

        /// <summary>
        /// 检测坐标模式
        /// </summary>
        public OverlapAreaMode Mode
        {
            get {
                return _mode;
            }

            set {
                _mode = value;
            }

        }

        /// <summary>
        /// 检测区域坐标
        /// </summary>
        public List<Vector3> Positions => positions;

        /// <summary>
        /// 检测区域位置
        /// </summary>
        public List<Transform> Transforms => transforms;

        #endregion


        #region 方法

        public override void CheckOverlap()
        {
            if (!TryBeginCheck()) return;
            try
            {
                ClearResults();

                switch (Mode)
                {
                    case OverlapAreaMode.Position:

                        if (positions == null || positions.Count < 2)
                            break;

                        for (int i = 0; i < positions.Count - 1 && !CheckInvalidated; i++)
                        {
                            Vector3 start = transform.localToWorldMatrix.MultiplyPoint(positions[i]);
                            Vector3 to = transform.localToWorldMatrix.MultiplyPoint(positions[i + 1]);

    #if UNITY_6000_0_OR_NEWER
                            ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                            int count = Physics2D.defaultPhysicsScene.OverlapArea(start, to, contactFilter, results);
    #else
                            int count = Physics2D.OverlapAreaNonAlloc(start, to, results, layerMask.value);
    #endif

                            CheckCapacity(count);
                            for (int j = 0; j < count && !CheckInvalidated; j++)
                            {
                                if (!results[j] || results[j].isTrigger) continue;

                                AddResults(results[j], Vector3.Lerp(start, to, 0.5f));
                            }

                        }

                        break;
                    case OverlapAreaMode.Transform:


                        if (transforms == null || transforms.Count < 2)
                            break;

                        for (int i = 0; i < transforms.Count - 1 && !CheckInvalidated; i++)
                        {
                            if (transforms[i] == null || transforms[i + 1] == null)
                                continue;

                            Vector3 start = transforms[i].position;
                            Vector3 to = transforms[i + 1].position;

    #if UNITY_6000_0_OR_NEWER
                            ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                            int count = Physics2D.defaultPhysicsScene.OverlapArea(start, to, contactFilter, results);

    #else
                            int count = Physics2D.OverlapAreaNonAlloc(start, to, results, layerMask.value);
    #endif

                            CheckCapacity(count);
                            for (int j = 0; j < count && !CheckInvalidated; j++)
                            {
                                if (!results[j] || results[j].isTrigger) continue;

                                AddResults(results[j], Vector3.Lerp(start, to, 0.5f));
                            }
                        }

                        break;
                }

                OverlapEnd();
            }
            finally { EndCheck(); }
        }


#endregion




    }

}
