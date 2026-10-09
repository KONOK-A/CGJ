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