
using UnityEngine;

namespace YLFramework.Overlap
{
    /// <summary>
    /// 重叠检测某一个点
    /// </summary>
    public class OverlapPoint2D : OverlapBase2D
    {
        // Fix编码
        public override void CheckOverlap()
        {
            if (!TryBeginCheck())
            {
                return;
            }

            try
            {
                ClearResults();

#if UNITY_6000_0_OR_NEWER
                ContactFilter2D contactFilter = CreateLegacyFilter(layerMask.value, float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.defaultPhysicsScene.OverlapPoint(transform.position, contactFilter, ResultBuffer);
#else
                int count = Physics2D.OverlapPointNonAlloc(transform.position, ResultBuffer, layerMask.value);
#endif
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
