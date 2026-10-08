using UnityEngine;

namespace YLFramework.Events
{
    /// <summary>
    /// 生命周期注销触发器基类：收集需要随某个生命周期自动注销的句柄，
    /// 由子类在 OnDestroy / OnDisable / 场景卸载时调用 <see cref="TriggerUnRegisters"/>。
    /// 触发采用“快照 + 先清空再执行”的方式：重复触发幂等，回调中增删句柄不会破坏遍历，
    /// 回调中新增的句柄留到下一次触发处理。
    /// </summary>
    public abstract class UnRegisterTriggerBase : MonoBehaviour
    {
        private readonly SubscriptionSet<IUnRegister> _unRegisterContainer = new();

        /// <summary>
        /// 加入一个注销句柄，生命周期触发时统一注销；同一句柄重复加入只保留一份（去重）。
        /// </summary>
        /// <param name="unRegister">待托管的注销句柄。</param>
        /// <returns>传入的句柄，便于链式使用。</returns>
        public IUnRegister AddUnRegister(IUnRegister unRegister)
        {
            _unRegisterContainer.Add(unRegister);
            return unRegister;
        }

        /// <summary>
        /// 从容器中移除一个注销句柄（仅解除托管，不会触发注销）。
        /// </summary>
        /// <param name="unRegister">要移除的注销句柄。</param>
        public void RemoveUnRegister(IUnRegister unRegister)
        {
            _unRegisterContainer.Remove(unRegister);
        }

        /// <summary>
        /// 立即触发容器内所有注销器；重复调用安全。
        /// </summary>
        public void UnRegister()
        {
            TriggerUnRegisters();
        }

        /// <summary>
        /// 触发全部注销器：先取快照并清空容器，再逐个注销。
        /// 这样嵌套触发不会重复注销，回调中新增的句柄会保留到下一次触发。
        /// 若某个注销器抛异常，异常立即向上传播，本次快照中剩余的句柄不再注销；
        /// 容器已被清空，不会在下次触发时重复注销旧句柄。
        /// </summary>
        protected void TriggerUnRegisters()
        {
            if (_unRegisterContainer.Count == 0) return;

            var snapshot = _unRegisterContainer.DetachAll();
            foreach (var unRegister in snapshot)
            {
                unRegister?.UnRegister();
            }
        }
    }
}
