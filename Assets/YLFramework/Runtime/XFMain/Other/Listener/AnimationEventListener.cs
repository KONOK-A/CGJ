// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Listener/AnimationEventListener.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
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

