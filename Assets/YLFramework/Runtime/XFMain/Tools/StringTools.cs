using UnityEngine;


// Fix 编码

namespace YLFramework.Core
{
    /// <summary>
    /// 字符串工具类
    /// </summary>
    public class StringTools
    {

        private const int kb = 1024;     
        private const int mb = kb * 1024;
        private const int gb = mb * 1024;
        private const long tb = gb * (long)1024;


        /// <summary>
        /// 格式化字节 例如: 1024 = 1kb
        /// </summary>
        /// <param name="length">字节长度</param>
        public static string FormatByte(long length) {
            if (length < kb) 
                return string.Format("{0}b", length);
             else if (length >= kb && length < mb )
                return string.Format("{0:N2}kb", length / 1024.0f);
            else if (length >= mb && length < gb)
                return string.Format("{0:N2}mb", length / 1024.0f / 1024.0f);
            else if (length >= gb && length < tb)
                return string.Format("{0:N2}gb", length / 1024.0f / 1024.0f / 1024.0f);

            return "";
        }



        /// <summary>
        /// 把 0 -1 转换成 0% - 100%
        /// </summary>
        /// <returns></returns>
        public static string FormatProgress(float progress) 
        {
            return string.Format("{0}%", Mathf.Lerp(0, 100, progress));
        }
    
    }

}

