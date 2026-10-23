using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>
    /// 用于回收View的组件
    /// </summary>
    [RequireComponent(typeof(View))]
    public class CloseView : MonoBehaviour
    {
        [Header("持续时间")]
        [Tooltip("持续时间")]
        public float duration = 1;

        [Header("忽略时间缩放")]
        [Tooltip("忽略时间缩放")]
        public bool ingoreTimeScale;

        private float _timer = 0;


        private View _view;

        private void Awake()
        {
            _view = GetComponent<View>();
        }

        private void OnEnable()
        {
            _timer = 0;
        }

        private void Update()
        {
            _timer += ingoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

            if (_timer >= duration)
            {
                _view.Close();
                _timer = 0;
            }
        }
    }
}
