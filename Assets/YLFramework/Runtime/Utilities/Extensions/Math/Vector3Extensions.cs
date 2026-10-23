using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Utilities.Extensions
{
    public static class Vector3Extensions
    {

        /// <summary>
        /// 反向插值
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="current"></param>
        /// <returns></returns>
        public static float InverseLerp(Vector3 from, Vector3 to, Vector3 current)
        {
            float lerp = Mathf.InverseLerp(from.x, to.x, current.x);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector3.Distance(current, Vector3.Lerp(from, to, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            lerp = Mathf.InverseLerp(from.y, to.y, current.y);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector3.Distance(current, Vector3.Lerp(from, to, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            lerp = Mathf.InverseLerp(from.z, to.z, current.z);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector3.Distance(current, Vector3.Lerp(from, to, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            return 0;
        }


        /// <summary>
        /// 旋转 Vector3
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="axis"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static Vector3 Rotate(Vector3 direction, Vector3 axis, float angle)
        {
            Quaternion q = Quaternion.AngleAxis(angle, axis);
            return q * direction;
        }

    }

}
