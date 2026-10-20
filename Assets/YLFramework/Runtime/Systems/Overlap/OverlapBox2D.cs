using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Overlap
{
    /// <summary>
    /// 重叠检测矩形2D
    /// </summary>
    public class OverlapBox2D : OverlapBase2D
    {

        #region 字段
        [SerializeField]
        protected Vector2 offset;
        [SerializeField]
        protected Vector2 size = Vector2.one;

        #endregion


        #region 属性

        /// <summary>
        /// 偏移
        /// </summary>
        public Vector2 Offset {
            get {
                return offset;
            }
            set {
                offset = value;
            }
        }

        /// <summary>
        /// 大小
        /// </summary>
        public Vector2 Size {
            get {
                return size;
            }
            set {
                size = value;
            }
        }

        #endregion


        // Fix编码

        #region 方法

        public override void CheckOverlap()
        {
            if (!TryBeginCheck()) return;
            try
            {
                Vector3 center = transform.localToWorldMatrix.MultiplyPoint(offset);


                ClearResults();

                Vector2 s = size * transform.lossyScale;

                s.Set(Mathf.Abs(s.x), Mathf.Abs(s.y));

    #if UNITY_6000_0_OR_NEWER
                ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.defaultPhysicsScene.OverlapBox(center, s, transform.eulerAngles.z, contactFilter, results);
    #else
                int count = Physics2D.OverlapBoxNonAlloc(center, s, transform.eulerAngles.z, results, layerMask.value);
    #endif

                CheckCapacity(count);
                for (int i = 0; i < count && !CheckInvalidated; i++)
                {
                    if (!results[i] || results[i].isTrigger) continue;
                    AddResults(results[i],center);
                }

                OverlapEnd();
            }
            finally { EndCheck(); }

        }

#endregion


    }

}
