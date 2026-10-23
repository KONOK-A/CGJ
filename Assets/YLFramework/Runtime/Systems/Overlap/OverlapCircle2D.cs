
using UnityEngine;

namespace YLFramework.Overlap
{
    /// <summary>
    /// 重叠检测圆形2D
    /// </summary>
    public class OverlapCircle2D : OverlapBase2D
    {
        #region 字段

        [SerializeField]
        private float radius = 0.5f;

        #endregion


        #region 属性
        /// <summary>
        /// 半径
        /// </summary>
        public float Radius
        {
            get
            {
                return radius;
            }
            set
            {
                radius = value;
            }

        }

        #endregion



        // Fix编码
        public override void CheckOverlap()
        {
            if (!TryBeginCheck())
            {
                return;
            }

            try
            {
                Vector3 s = transform.lossyScale;
                float scale = Mathf.Abs(s.x) > Mathf.Abs(s.y) ? Mathf.Abs(s.x) : Mathf.Abs(s.y);

#if UNITY_6000_0_OR_NEWER
                ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.defaultPhysicsScene.OverlapCircle(transform.position, radius * scale, contactFilter, ResultBuffer);
#else
                int count = Physics2D.OverlapCircleNonAlloc(transform.position, radius * scale, ResultBuffer, layerMask.value);
#endif


                ClearResults();
                CheckCapacity(count);
                for (int i = 0; i < count && !IsCheckInvalidated; i++)
                {
                    if (!ResultBuffer[i] || ResultBuffer[i].isTrigger)
                    {
                        continue;
                    }

                    AddResults(ResultBuffer[i], transform.position);
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
