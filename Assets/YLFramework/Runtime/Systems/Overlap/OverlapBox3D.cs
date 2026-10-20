using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Overlap
{

    /// <summary>
    /// 重叠检测立方体3D
    /// </summary>
    public class OverlapBox3D : OverlapBase3D
    {
        #region 字段


        [SerializeField]
        private Vector3 size = Vector3.one;

        #endregion

        #region 属性

        /// <summary>
        /// 大小
        /// </summary>
        public Vector3 Size
        {
            get {
                return size;
            }
            set {
                size = value;
            }
        }

        #endregion



        // Fix编码
        public override void CheckOverlap()
        {
            if (!TryBeginCheck()) return;
            try
            {
                Vector3 lossy = transform.lossyScale;
                Vector3 s = new Vector3( Mathf.Abs( size.x * lossy.x * 0.5f), Mathf.Abs( size.y * lossy.y * 0.5f),Mathf.Abs(  size.z * lossy.z * 0.5f));

                int count = Physics.OverlapBoxNonAlloc(transform.position, s, results, transform.rotation, layerMask.value);
                ClearResults();

                CheckCapacity(count);
                for (int i = 0; i < count && !CheckInvalidated; i++)
                {
                    if (!results[i] || results[i].isTrigger) continue;
                    AddResults(results[i],transform.position);
                }

                OverlapEnd();
            }
            finally { EndCheck(); }
        }


    }

}
