using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Extensions
{
    public class TimeExtensions
    {
        /// <summary>
        /// 获取DeltaTime
        /// </summary>
        /// <param name="ignoreTimeScale"></param>
        /// <returns></returns>
        public static float GetDeltaTime(bool ignoreTimeScale)
        {
            if (ignoreTimeScale)
            {
                // 大于 0.2 说明这个值有点不正常 使用0.02
                return Time.unscaledDeltaTime > 0.2f ? 0.02f : Time.unscaledDeltaTime;
            }

            return Time.deltaTime;
        }

    }
}
