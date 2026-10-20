// Derived from XF Localization 1.0.9. Copyright (c) 2024 弦风课堂. MIT; see LICENSE.txt in this directory.
using System;
using UnityEngine;
using YLFramework.Events;

namespace YLFramework.Localization
{
    [DisallowMultipleComponent]
    public abstract class LocalizeBase : MonoBehaviour
    {
        [SerializeField] private int id;
        private IUnRegister subscription;
        public int ID { get => id; set { if (id == value) return; id = value; Refresh(); } }

        protected virtual void OnEnable()
        {
            subscription?.UnRegister();
            subscription = LocalizationManager.Changed.Register(Refresh);
            Refresh();
        }

        protected virtual void OnDisable()
        {
            subscription?.UnRegister();
            subscription = null;
        }

        /// <summary>直接更新显示，不切换GameObject活动状态；停用时不加载或回写UI。</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled) return;
            try { OnLanguageValueChange(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }

        protected virtual void OnLanguageValueChange() { }
    }
}
