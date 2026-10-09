using System; 
using UnityEngine;
using UnityEngine.Events;


namespace YLFramework.Core
{ 
    [Serializable]
    public class AnimationEvent : UnityEvent<string> { }

    public class AnimationEventListener : MonoBehaviour
    {
        // Fix编码
        [Obsolete("已过时,使用CommonSystem中的AnimationEventListener代替!")]
        [Header("已过时,使用CommonSystem中的AnimationEventListener代替!")]
        public AnimationEvent animationEvent;

        public void OnAnimationEvent(string name) {
#pragma warning disable CS0618 // 类型或成员已过时
            animationEvent?.Invoke(name);
#pragma warning restore CS0618 // 类型或成员已过时
        } 
    }
}

