using UnityEngine;

namespace YLFramework.Utilities.Extensions
{

    public static class ColorExtensions
    {

        /// <summary>
        /// 颜色反向插值
        /// </summary>
        public static float InverseLerp(Color from, Color to, Color current)
        {

            Vector4 fromColor = new Vector4(from.r, from.g, from.b, from.a);
            Vector4 toColor = new Vector4(to.r, to.g, to.b, to.a);
            Vector4 currentColor = new Vector4(current.r, current.g, current.b, current.a);

            float lerp = Mathf.InverseLerp(fromColor.x, toColor.x, currentColor.x);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(currentColor, Vector4.Lerp(fromColor, toColor, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            lerp = Mathf.InverseLerp(fromColor.y, toColor.y, currentColor.y);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(currentColor, Vector4.Lerp(fromColor, toColor, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            lerp = Mathf.InverseLerp(fromColor.z, toColor.z, currentColor.z);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(currentColor, Vector4.Lerp(fromColor, toColor, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            lerp = Mathf.InverseLerp(fromColor.w, toColor.w, currentColor.w);

            if (lerp > 0 && lerp <= 1)
            {
                if (Vector4.Distance(currentColor, Vector4.Lerp(fromColor, toColor, lerp)) <= 0.01f)
                {
                    return lerp;
                }
            }

            return 0;
        }

    }

}
