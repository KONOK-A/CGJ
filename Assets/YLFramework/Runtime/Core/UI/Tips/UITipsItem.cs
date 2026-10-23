using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    public enum UITipPos
    {
        Middle,
        LeftTop,
        LeftDown,
        RightTop,
        RightDown
    }
    public enum UITipResult
    {
        Completed,
        Canceled,
        HostEnded
    }

    /// <summary>一次提示的取消句柄。结束后不再持有调用方回调，也不操作后续复用会话。</summary>
    public sealed class UITipHandle : IDisposable
    {
        public UITipsItem Item
        {
            get;
        }
        public bool IsDone
        {
            get; private set;
        }
        public UITipResult Result { get; private set; } = UITipResult.Canceled;
        internal object Session;
        internal Action<UITipHandle> Callback;
        internal UITipHandle(UITipsItem item, Action<UITipHandle> callback)
        {
            Item = item;
            Callback = callback;
        }
        public void Dispose() => Finish(UITipResult.Canceled);
        internal void Finish(UITipResult result)
        {
            if (IsDone)
            {
                return;
            }

            Result = result;
            if (Item && ReferenceEquals(Item.Session, Session))
            {
                Item.Close();
            }
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
        private Sprite _originalSprite;
        private Color _originalTextColor;
        private Color _originalBgColor;
        private CanvasGroup _group;
        private bool _isPrepared;
        private float _elapsed;
        private float _duration;
        private bool _fade;
        private UITipHandle _handle;
        private UITipsPresenter _presenter;

        public override void OnLoaded(LoadViewArgs args)
        {
            if (!bg || !text)
            {
                throw new InvalidOperationException("UITipsItem requires background and text bindings.");
            }

            if (!_isPrepared)
            {
                _originalSprite = bg.sprite;
                _originalTextColor = text.color;
                _originalBgColor = bg.color;
                _group = GetComponent<CanvasGroup>();
                if (!_group)
                {
                    _group = gameObject.AddComponent<CanvasGroup>();
                }

                _isPrepared = true;
            }
            _presenter = (UITipsPresenter)args.GetObject("presenter");
            _handle = (UITipHandle)args.GetObject("handle");
            _handle.Session = Session;
            _duration = args.GetFloat("duration");
            _elapsed = 0;
            _fade = args.GetBool("fade");
            text.text = args.GetString("content");
            text.color = args.GetObject("textColor") is Color color ? color : _originalTextColor;
            bg.sprite = args.GetObject("sprite") as Sprite;
            if (!bg.sprite)
            {
                bg.sprite = _originalSprite;
            }

            bg.color = _originalBgColor;
            text.raycastTarget = bg.raycastTarget = false;
            _group.blocksRaycasts = false;
            _group.alpha = _fade ? 0 : 1;
        }

        private void Update()
        {
            if (!IsLoaded || _handle == null || _handle.IsDone)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed >= _duration)
            {
                _handle.Finish(UITipResult.Completed);
                return;
            }
            if (_fade)
            {
                _group.alpha = Mathf.Clamp01(Mathf.Min(_elapsed, _duration - _elapsed) / (_duration * 0.25f));
            }
        }

        public override void OnRemove()
        {
            _handle?.End();
            if (_isPrepared && this && bg && text)
            {
                text.text = string.Empty;
                text.color = _originalTextColor;
                bg.sprite = _originalSprite;
                bg.color = _originalBgColor;
                _group.alpha = 1;
            }
        }

        protected override void OnClosed()
        {
            var previous = _handle;
            var host = _presenter;
            _handle = null;
            _presenter = null;
            if (host)
            {
                host.Return(this, previous);
            }
            else
            {
                previous?.Notify();
            }
        }
    }
}
