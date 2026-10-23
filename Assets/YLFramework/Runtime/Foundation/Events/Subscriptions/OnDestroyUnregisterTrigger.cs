namespace YLFramework.Events
{
    /// <summary>
    /// 挂载后，当自身 GameObject 被销毁时自动注销容器内的所有句柄。
    /// 通常由 <see cref="UnregisterExtension.UnregisterWhenGameObjectDestroyed(IUnregister, UnityEngine.GameObject)"/> 自动挂载。
    /// </summary>
    public class OnDestroyUnregisterTrigger : UnregisterTriggerBase
    {
    }
}
