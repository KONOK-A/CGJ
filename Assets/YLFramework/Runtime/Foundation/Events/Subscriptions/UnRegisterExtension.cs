using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Events
{
    /// <summary>
    /// 注销句柄的生命周期绑定扩展：把注册得到的 <see cref="IUnRegister"/> 挂到 GameObject 生命周期上自动注销。
    /// </summary>
    public static class UnRegisterExtension
    {
        /// <summary>
        /// 当指定 GameObject 被销毁时自动注销；Unity 仅保证曾 active 的对象收到 OnDestroy。
        /// 从未 active 的宿主须显式清理；已经销毁或为 null 的宿主立即取消。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="gameObject">生命周期宿主。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenGameObjectDestroyed(this IUnRegister unRegister, GameObject gameObject)
        {
            if (unRegister == null) return null;
            if (!gameObject)
            {
                unRegister.UnRegister();
                return unRegister;
            }
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
            return unRegister.UnRegisterWhenGameObjectDestroyed(component ? component.gameObject : null);
        }

        /// <summary>
        /// 下一次实际 OnDisable 时注销，不在绑定 inactive 对象时立即取消，也不在 Enable 时自动重订。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <param name="gameObject">生命周期宿主。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenDisabled(this IUnRegister unRegister, GameObject gameObject)
        {
            if (unRegister == null) return null;
            if (!gameObject)
            {
                unRegister.UnRegister();
                return unRegister;
            }
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
            return unRegister.UnRegisterWhenDisabled(component ? component.gameObject : null);
        }

        /// <summary>
        /// 任意场景卸载时自动注销（包括 additive 场景；不区分 active scene）。
        /// 运行时创建常驻单例触发器，语义详见 <see cref="OnCurSceneUnLoadUnRegisterTrigger"/>。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <returns>传入的注销句柄。</returns>
        public static IUnRegister UnRegisterWhenAnySceneUnloaded(this IUnRegister unRegister)
        {
            if (unRegister == null) return null;
            return OnCurSceneUnLoadUnRegisterTrigger.Instance.AddUnRegister(unRegister);
        }

        /// <summary>
        /// 仅在指定的已加载 Scene 卸载时注销，捕获其 handle，不随 active scene 或 GameObject 迁移改变。
        /// 无效或已卸载的 Scene 立即取消；不创建场景宿主。
        /// </summary>
        public static IUnRegister UnRegisterWhenSceneUnloaded(this IUnRegister unRegister, Scene scene)
        {
            if (unRegister == null) return null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                unRegister.UnRegister();
                return unRegister;
            }
            return OnCurSceneUnLoadUnRegisterTrigger.Instance.AddUnRegister(unRegister, scene);
        }
    }
}
