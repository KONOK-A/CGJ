using UnityEngine;
using YLFramework;
using YLFramework.Singletons;

namespace YLFramework.Actions
{
    // 每次 Update 处理动作的延迟回收，不承载通用全局服务。
    internal sealed class ActionRecycleDriver : MonoSingletonHost<ActionRecycleDriver>
    {
        protected override Transform HostParent => YLFrameworkRoot.Instance.GetModuleRoot("Action");

        // 测试与会话清理入口：先把待回收项清空（保证旧池对象不跨会话滞留），再清空单例缓存引用。
        internal static void ResetSession()
        {
            try
            {
                ActionQueue.Drain();
            }
            finally
            {
                ResetHostReference();
            }
        }

        // 播放中首次产生回收项时创建驱动器；关停期间或当前不允许创建宿主时跳过，等下一次回收请求再试。
        internal static void EnsureCreated()
        {
            if (HasInstance || ActionKit.IsShuttingDown || !CanCreateHost())
            {
                return;
            }

            _ = Instance;
        }

        private void Update()
        {
            ActionQueue.Drain();
        }

        // 模块级释放：销毁所有驱动器实例并清空缓存引用，不处理队列内容（Shutdown 会先 Drain）。
        internal static void ReleaseAll()
        {
            foreach (var instance in FindObjectsOfType<ActionRecycleDriver>(true))
            {
                instance.ReleaseHost();
            }
            ResetHostReference();
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            // 退出时不能再等 Update，立即清空一次回收队列。
            ActionQueue.Drain();
        }

        protected override void OnHostRelease()
        {
            if (!IsCurrentHost)
            {
                return;
            }
            // 被替换或销毁的驱动器在释放钩子里兜底清空队列，避免回收项无人处理。
            ActionQueue.Drain();
        }
    }
}
