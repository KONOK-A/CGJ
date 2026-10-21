using UnityEngine;
using YLFramework.Events;

namespace YLFramework.ReddotSystem
{
    /// <summary>只切换显式指示器，不批量启停业务子物体，也不持有业务路径来源。</summary>
    [AddComponentMenu("YLFramework/Reddot")]
    public sealed class Reddot : MonoBehaviour
    {
        [SerializeField, Tooltip("按段匹配，例如 Mail/Gift；空路径不显示。")]
        private string _path;
        [SerializeField, Tooltip("只控制此指示器。不能是本物体或其祖先，避免关闭监听宿主。")]
        private GameObject _indicator;
        private IUnRegister _subscription;

        public string Path { get => _path; set { _path = value; Refresh(); } }
        public GameObject Indicator { get => _indicator; set { _indicator = value; Refresh(); } }

        private void OnEnable()
        {
            _subscription?.UnRegister();
            _subscription = ReddotManager.RegisterChanged(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            _subscription?.UnRegister();
            _subscription = null;
        }

        private void OnDestroy() => OnDisable();

        public void Refresh()
        {
            if (!_indicator || _indicator == gameObject || transform.IsChildOf(_indicator.transform)) return;
            var active = isActiveAndEnabled && ReddotManager.ShouldBeDisplay(_path);
            if (_indicator.activeSelf != active) _indicator.SetActive(active);
        }

        /// <summary>仅移除普通集合路径；独立来源须由业务owner自行释放。</summary>
        public void Remove() => ReddotManager.RemovePath(_path);
    }
}
