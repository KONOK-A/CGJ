using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Utilities.Extensions
{

    public static class RandomExtensions 
    {

        /// <summary>
        /// 按概率返回 true /false 的随机方法
        /// </summary>
        /// <param name="probability">概率(0-1)</param>
        /// <returns></returns>
        public static bool Random(float probability)
        {
            if (probability >= 1) return true;
            if (probability <= 0) return false;

            int p = Mathf.RoundToInt( probability * 100); 
            return UnityEngine.Random.Range(1, 101) <= p;
        }
    }

}
