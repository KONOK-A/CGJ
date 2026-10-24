using System;
using UnityEngine;
using UnityEngine.Events;


namespace YLFramework.Events
{
    [Serializable]
    public class AnimationEvent : UnityEvent<string>
    {
    }

    [AddComponentMenu("YLFramework/Events/Animation Event Listener")]
    public class AnimationEventListener : MonoBehaviour
    {
        [Tooltip("由 AnimationClip 的 OnAnimationEvent(string) 事件传入名称；不监听 Animator 状态变化。")]
        public AnimationEvent animationEvent = new AnimationEvent();

        public void OnAnimationEvent(string name)
        {
            animationEvent?.Invoke(name);
        }
    }
}
