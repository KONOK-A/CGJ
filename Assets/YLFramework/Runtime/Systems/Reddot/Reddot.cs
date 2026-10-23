using UnityEngine;
using YLFramework.Events;

namespace YLFramework.ReddotSystem
{
    /// <summary>只切换显式指示器，不批量启停业务子物体，也不持有业务路径来源。</summary>
    [AddComponentMenu("YLFramework/Reddot")]
    public sealed class Reddot : MonoBehaviour
    {
        [global::UnityEngine.Serialization.FormerlySerializedAs("_path")]
        [SerializeField, Tooltip("按段匹配，例如 Mail/Gift；空路径不显示。")]
        private string path;
        [global::UnityEngine.Serialization.FormerlySerializedAs("_indicator")]
        [SerializeField, Tooltip("只控制此指示器。不能是本物体或其祖先，避免关闭监听宿主。")]
        private GameObject indicator;
        private IUnregister _subscription;

        public string Path
        {
            get => path; set
            {
                path = value;
                Refresh();
            }
        }
        public GameObject Indicator
        {
            get => indicator; set
            {
                indicator = value;
                Refresh();
            }
        }

        private void OnEnable()
        {
            _subscription?.Unregister();
            _subscription = ReddotManager.RegisterChanged(Refresh);
            Refresh();
        }

        private void OnDisable()
        {
            _subscription?.Unregister();
            _subscription = null;
        }

        private void OnDestroy() => OnDisable();

        public void Refresh()
        {
            if (!indicator || indicator == gameObject || transform.IsChildOf(indicator.transform))
            {
                return;
            }

            var active = isActiveAndEnabled && ReddotManager.ShouldBeDisplayed(path);
            if (indicator.activeSelf != active)
            {
                indicator.SetActive(active);
            }
        }

        /// <summary>仅移除普通集合路径；独立来源须由业务owner自行释放。</summary>
        public void Remove() => ReddotManager.RemovePath(path);
    }
}
