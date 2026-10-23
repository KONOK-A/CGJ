#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;


namespace YLFramework.Foundation
{
    /// <summary>
    /// EditorApplication工具
    /// </summary>
    public class ApplicationTool
    {

        /// <summary>
        /// 判断当前编辑器是否处于运行状态(UnityEngine.Application.isPlaying在停止运行编辑器时触发OnDestroy时仍返回true)
        /// </summary>
        public static bool IsPlaying
        {
            get
            {
#if UNITY_EDITOR
                return EditorApplication.isPlayingOrWillChangePlaymode;  // 如果是编辑器模式 使用这个数据
#else
                return true; // 如果是非编辑器模式一定是true
#endif  
            }

        }

    }
}
