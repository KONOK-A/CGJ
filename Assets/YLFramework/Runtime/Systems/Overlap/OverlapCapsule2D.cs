using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Overlap
{
    /// <summary>
    /// 重叠检测胶囊2D
    /// </summary>
    public class OverlapCapsule2D : OverlapBox2D
    {

        #region 字段
        [SerializeField]
        private CapsuleDirection2D direction;

        #endregion

        #region 属性

        /// <summary>
        /// 方向
        /// </summary>
        public CapsuleDirection2D Direction
        {
            get
            {
                return direction;
            }
            set
            {
                direction = value;
            }
        }

        #endregion


        public override void CheckOverlap()
        {
            if (!TryBeginCheck())
            {
                return;
            }

            try
            {
                //base.CheckOverlap();
                Vector3 center = transform.localToWorldMatrix.MultiplyPoint(offset);

                ClearResults();

                Vector2 s = size * transform.lossyScale;

                s.Set(Mathf.Abs(s.x), Mathf.Abs(s.y));

#if UNITY_6000_0_OR_NEWER
                ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.defaultPhysicsScene.OverlapCapsule(center, s, direction, transform.eulerAngles.z, contactFilter, ResultBuffer);
#else
                int count = Physics2D.OverlapCapsuleNonAlloc(center, s, direction, transform.eulerAngles.z, ResultBuffer, layerMask.value);
#endif


                CheckCapacity(count);
                for (int i = 0; i < count && !IsCheckInvalidated; i++)
                {
                    if (!ResultBuffer[i] || ResultBuffer[i].isTrigger)
                    {
                        continue;
                    }

                    AddResults(ResultBuffer[i], center);
                }

                OverlapEnd();
            }
            finally
            {
                EndCheck();
            }
        }



    }

}
