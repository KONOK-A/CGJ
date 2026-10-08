namespace YLFramework.Events
{
    /// <summary>
    /// 挂载后，当自身 GameObject 被禁用（OnDisable）时自动注销容器内的所有句柄。
    /// 通常由 <see cref="UnRegisterExtension.UnRegisterWhenDisabled(IUnRegister, UnityEngine.GameObject)"/> 自动挂载。
    /// </summary>
    public class OnDisableUnRegisterTrigger : UnRegisterTriggerBase
    {
        private void OnDisable()
        {
            TriggerUnRegisters();
        }
    }
}
