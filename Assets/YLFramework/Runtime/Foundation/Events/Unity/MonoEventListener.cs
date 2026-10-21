using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Events
{
    /// <summary>
    /// 将本对象的原生生命周期转发到 Inspector 事件，不持有全局状态。
    /// 需要观察 Awake/首次 OnEnable 时，应在 Prefab 或未激活对象上预先绑定。
    /// </summary>
    [AddComponentMenu("YLFramework/Events/Mono Event Listener")]
    public class MonoEventListener : MonoBehaviour
    {
        public UnityEvent onAwake = new UnityEvent();
        public UnityEvent onStart = new UnityEvent();
        public UnityEvent onEnable = new UnityEvent();
        public UnityEvent onDisable = new UnityEvent();
        public UnityEvent onDestroy = new UnityEvent();

        private void Awake() => onAwake?.Invoke();
        private void Start() => onStart?.Invoke();
        private void OnEnable() => onEnable?.Invoke();
        private void OnDisable() => onDisable?.Invoke();
        private void OnDestroy() => onDestroy?.Invoke();
    }
}
