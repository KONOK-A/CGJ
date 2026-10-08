using UnityEngine;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Events
{
    /// <summary>
    /// 注销句柄的生命周期绑定扩展：把注册得到的 <see cref="IUnRegister"/> 挂到 GameObject 生命周期上自动注销。
    /// </summary>
    public static class UnRegisterExtension
    {
        /// <summary>
        /// 当指定 GameObject 被销毁时自动注销。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="gameObject">生命周期宿主。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenGameObjectDestroyed(this IUnRegister unRegister, GameObject gameObject)
        {
            return gameObject.GetOrAddComponent<OnDestroyUnRegisterTrigger>().AddUnRegister(unRegister);
        }

        /// <summary>
        /// 当指定组件的 GameObject 被销毁时自动注销。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="component">生命周期宿主组件。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenGameObjectDestroyed<T>(this IUnRegister unRegister, T component)
            where T : Component
        {
            return unRegister.UnRegisterWhenGameObjectDestroyed(component.gameObject);
        }

        /// <summary>
        /// 当指定 GameObject 被禁用（OnDisable）时自动注销。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="gameObject">生命周期宿主。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenDisabled(this IUnRegister unRegister, GameObject gameObject)
        {
            return gameObject.GetOrAddComponent<OnDisableUnRegisterTrigger>().AddUnRegister(unRegister);
        }

        /// <summary>
        /// 当指定组件的 GameObject 被禁用（OnDisable）时自动注销。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="component">生命周期宿主组件。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenDisabled<T>(this IUnRegister unRegister, T component)
            where T : Component
        {
            return unRegister.UnRegisterWhenDisabled(component.gameObject);
        }

        /// <summary>
        /// 任意场景卸载时自动注销（包括 additive 场景；不区分 active scene）。
        /// 运行时创建常驻单例触发器，语义详见 <see cref="OnCurSceneUnLoadUnRegisterTrigger"/>。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenCurrentSceneUnloaded(this IUnRegister unRegister)
        {
            return OnCurSceneUnLoadUnRegisterTrigger.Instance.AddUnRegister(unRegister);
        }
    }
}
