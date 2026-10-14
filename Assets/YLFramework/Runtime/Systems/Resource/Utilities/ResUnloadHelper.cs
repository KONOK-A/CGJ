using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源卸载辅助：集中区分 Unity 缓存资产与运行时对象的不同释放方式。
    /// </summary>
    public static class ResUnloadHelper
    {
        /// <summary>
        /// 卸载缓存资产：非 GameObject 走 Resources.UnloadAsset；
        /// GameObject 不能使用 Resources.UnloadAsset（Unity 不允许），直接跳过，由调用方清空引用。
        /// </summary>
        public static void UnLoadRes(Object asset)
        {
            if (asset is GameObject)
            {
            }
            else
            {
                Resources.UnloadAsset(asset);
            }
        }

        /// <summary>
        /// 销毁运行时创建的对象（Object.Destroy），例如 Instantiate 出来的实例。
        /// </summary>
        public static void DestroyObject(Object asset)
        {
            Object.Destroy(asset);
        }
    }
}