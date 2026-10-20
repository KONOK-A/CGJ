
using UnityEngine;

namespace YLFramework.Overlap
{
    /// <summary>
    /// 重叠检测球形
    /// </summary>
    public class OverlapSphere3D : OverlapBase3D
    {

        #region 字段

        [SerializeField]
        [Tooltip("半径")]
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
            if (!TryBeginCheck()) return;
            try
            {
                Vector3 scale = transform.lossyScale;
                float r = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) * radius;
                int count = Physics.OverlapSphereNonAlloc(transform.position, r, results, layerMask.value);

                ClearResults();

                CheckCapacity(count);
                for (int i = 0; i < count && !CheckInvalidated; i++)
                {
                    if (!results[i] || results[i].isTrigger) continue;
                    AddResults(results[i], transform.position);
                }

                OverlapEnd();
            }
            finally { EndCheck(); }
        }




    }

}
