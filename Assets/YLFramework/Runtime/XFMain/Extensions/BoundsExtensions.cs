using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core.Extensions
{
    public static class BoundsExtensions
    {

        /// <summary>
        /// 判断一个Bounds是否包含另外一个Bounds
        /// </summary>
        /// <param name="b"></param>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public static bool Contains(this Bounds b, Bounds bounds) 
        {
            if (b.min.x <= bounds.min.x && b.min.y <= bounds.min.y && b.min.z <= bounds.min.z &&
                b.max.x >= bounds.max.x && b.max.y >= bounds.max.y && b.max.z >= bounds.max.z)
            {
                return true;
            }

            return false;
        }

    }

}

