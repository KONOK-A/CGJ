// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/TimerManager/TimerObject.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using UnityEngine;
using UnityEngine.Events; 

namespace YLFramework.Core
{ 
    public class TimerObject : MonoBehaviour
    {
        // Fix编码
        [Tooltip("延迟时间")]
        public float delay = 2;

        [Tooltip("忽略时间缩放")]
        public bool ignoreTimeScale = true;

        [Tooltip("事件回调")]
        public UnityEvent Action;



        [Tooltip("中断回调")]
        public UnityEvent Abort;

        private Timer timer;

        private void OnEnable()
        {
            if (timer == null)
                timer = new Timer();

            TimerManager.DelayInvokeNonAlloc(timer,gameObject,OnAction, delay, ignoreTimeScale, OnAbort);
        }

        

        private void OnAction()
        {
            Action?.Invoke();
        }


        private void OnAbort() { 
            Abort?.Invoke();
        }

    } 
}