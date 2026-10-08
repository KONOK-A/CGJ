using System.Collections;
using UnityEngine;
using YLFramework.Core.Singleton;


namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源服务入口（Singleton&lt;ResManager&gt; 仅作为模块门面，不持有运行时状态）。
    /// <para>
    /// 必须先调用 <see cref="Init"/> 或 <see cref="InitAsync"/> 再加载资源；
    /// 两者幂等，初始化失败后可重试（完成标志只在成功后提交，见 ServiceManager）。
    /// 仅运行时可用；编辑器下开启 SimulationMode 时会自动初始化。
    /// </para>
    /// </summary>
    public class ResManager : Singleton<ResManager>
    {
        private ResManager() { }
        
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod]
        private static void CheckAutoInit()
        {
            if (PlatformCheck.IsEditor && AssetBundleHelper.SimulationMode)
            {
                Init();
            }
        }
#endif

        /// <summary>
        /// 初始化资源服务模块（同步）。重复调用幂等，失败后可重试。
        /// </summary>
        public static void Init()
        {
            ServiceManager.Init();
        }

        /// <summary>
        /// 异步初始化资源服务模块（WebGL 平台只支持异步初始化）。重复调用幂等，失败后可重试。
        /// </summary>
        /// <returns>初始化协程，供 StartCoroutine 或 yield return 等待。</returns>
        public static IEnumerator InitAsync()
        {
            yield return ServiceManager.InitAsync();
        }
        
        
    }
}
