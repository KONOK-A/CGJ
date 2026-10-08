using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// 单例触发器：任意场景卸载时注销容器内的所有句柄。
    /// 实际语义（与上游 QFramework 一致）：监听 <see cref="SceneManager.sceneUnloaded"/>，
    /// 任何场景卸载都会触发，包括 additive 加载的场景；不区分 active scene，也不区分句柄注册时所在场景。
    /// 实例常驻（DontDestroyOnLoad）并由 MonoSingletonProperty 管理；仅运行时可用。
    /// 如需按具体场景过滤，请直接监听 SceneManager.sceneUnloaded 自行管理。
    /// </summary>
    public class OnCurSceneUnLoadUnRegisterTrigger : UnRegisterTriggerBase, ISingleton
    {
        /// <summary>
        /// 全局唯一实例；Play 模式下首次访问时自动创建。
        /// </summary>
        public static OnCurSceneUnLoadUnRegisterTrigger Instance =>
            MonoSingletonProperty<OnCurSceneUnLoadUnRegisterTrigger>.Instance;

        /// <inheritdoc />
        public void OnSingletonInit() { }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            hideFlags = HideFlags.HideInHierarchy;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            TriggerUnRegisters();
        }
    }
}
