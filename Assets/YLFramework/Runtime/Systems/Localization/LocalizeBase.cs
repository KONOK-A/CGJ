using System;
using UnityEngine;
using YLFramework.Events;

namespace YLFramework.Localization
{
    [DisallowMultipleComponent]
    public abstract class LocalizeBase : MonoBehaviour
    {
        [SerializeField] private int id;
        private IUnregister _subscription;
        public int Id
        {
            get => id; set
            {
                if (id == value)
                {
                    return;
                }
                id = value;
                Refresh();
            }
        }

        protected virtual void OnEnable()
        {
            _subscription?.Unregister();
            _subscription = LocalizationManager.Changed.Register(Refresh);
            Refresh();
        }

        protected virtual void OnDisable()
        {
            _subscription?.Unregister();
            _subscription = null;
        }

        /// <summary>直接更新显示，不切换GameObject活动状态；停用时不加载或回写UI。</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            try
            {
                OnLanguageValueChange();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        protected virtual void OnLanguageValueChange()
        {
        }
    }
}
