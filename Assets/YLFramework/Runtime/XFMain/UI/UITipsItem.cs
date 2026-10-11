using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    public enum UITipPos { Middle, LeftTop, LeftDown, RightTop, RightDown }
    public enum UITipResult { Completed, Canceled, HostEnded }

    /// <summary>一次提示的取消句柄。结束后不再持有调用方回调，也不操作后续复用会话。</summary>
    public sealed class UITipHandle : IDisposable
    {
        public UITipsItem Item { get; }
        public bool IsDone { get; private set; }
        public UITipResult Result { get; private set; } = UITipResult.Canceled;
        internal object Session;
        internal Action<UITipHandle> Callback;
        internal UITipHandle(UITipsItem item, Action<UITipHandle> callback) { Item = item; Callback = callback; }
        public void Dispose() => Finish(UITipResult.Canceled);
        internal void Finish(UITipResult result)
        {
            if (IsDone) return;
            Result = result;
            if (Item && ReferenceEquals(Item.Session, Session)) Item.Close();
        }
        internal void End() => IsDone = true;
        internal void Notify()
        {
            var callback = Callback;
            Callback = null;
            callback?.Invoke(this);
        }
    }

    /// <summary>正式短提示呈现项；每次显示沿 Module/View 会话退出，池归还仅在框架收尾后发生。</summary>
    public sealed class UITipsItem : Panel
    {
        [SerializeField] private Image bg;
        [SerializeField] private Text text;
        private Sprite originalSprite;
        private Color originalTextColor;
        private Color originalBgColor;
        private CanvasGroup group;
        private bool prepared;
        private float elapsed;
        private float duration;
        private bool fade;
        private UITipHandle handle;
        private UITipsPresenter presenter;
        public override PanelType PanelType => PanelType.Overlay;

        public override void OnLoaded(LoadViewArgs args)
        {
            if (!bg || !text) throw new InvalidOperationException("UITipsItem requires background and text bindings.");
            if (!prepared)
            {
                originalSprite = bg.sprite;
                originalTextColor = text.color;
                originalBgColor = bg.color;
                group = GetComponent<CanvasGroup>();
                if (!group) group = gameObject.AddComponent<CanvasGroup>();
                prepared = true;
            }
            presenter = (UITipsPresenter)args.GetObject("presenter");
            handle = (UITipHandle)args.GetObject("handle");
            handle.Session = Session;
            duration = args.GetFloat("duration");
            elapsed = 0;
            fade = args.GetBool("fade");
            text.text = args.GetString("content");
            text.color = args.GetObject("textColor") is Color color ? color : originalTextColor;
            bg.sprite = args.GetObject("sprite") as Sprite;
            if (!bg.sprite) bg.sprite = originalSprite;
            bg.color = originalBgColor;
            text.raycastTarget = bg.raycastTarget = false;
            group.blocksRaycasts = false;
            group.alpha = fade ? 0 : 1;
        }

        private void Update()
        {
            if (!IsLoaded || handle == null || handle.IsDone) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= duration) { handle.Finish(UITipResult.Completed); return; }
            if (fade) group.alpha = Mathf.Clamp01(Mathf.Min(elapsed, duration - elapsed) / (duration * 0.25f));
        }

        public override void OnRemove()
        {
            handle?.End();
            if (prepared && this && bg && text)
            {
                text.text = string.Empty;
                text.color = originalTextColor;
                bg.sprite = originalSprite;
                bg.color = originalBgColor;
                group.alpha = 1;
            }
        }

        protected override void OnClosed()
        {
            var previous = handle;
            var host = presenter;
            handle = null;
            presenter = null;
            if (host) host.Return(this, previous);
            else previous?.Notify();
        }
    }
}
