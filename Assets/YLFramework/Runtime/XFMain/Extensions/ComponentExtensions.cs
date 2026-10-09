using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public static class ComponentExtensions
    {

        // Fix编码


        /// <summary>
        /// 判断组件是否被销毁
        /// </summary>
        /// <param name="component"></param>
        /// <returns></returns> 
        public static bool IsDestroy(this Component component)
        {  
            return !component;
        }

    }

}

