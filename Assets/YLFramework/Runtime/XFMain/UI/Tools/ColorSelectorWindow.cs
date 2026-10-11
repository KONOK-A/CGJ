using System;
using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    /// <summary>正式取色窗口。Module 为业务 owner，窗口和内容均不创建全局 UI Manager。</summary>
    public sealed class ColorSelectorWindow : Panel
    {
        [SerializeField] private ColorSelector selector;
        [SerializeField] private Button closeButton;
        public Color CurrentColor => selector.CurrentColor;

        public static LoadViewArgs Arguments(Color initial, Action<Color> changed = null)
        {
            var args = LoadViewArgs.New();
            args.SetObject("initialColor", initial);
            args.SetObject("colorChanged", changed);
            return args;
        }

        public override void OnLoaded(LoadViewArgs args) => Configure(args);
        public override void OnWindowUpdated(LoadViewArgs args) => Configure(args);
        private void Configure(LoadViewArgs args)
        {
            if (!selector || !closeButton) throw new InvalidOperationException("ColorSelectorWindow requires selector and close button bindings.");
            selector.BeginSession(args.GetObject("initialColor") is Color initial ? initial : Color.white,
                args.GetObject("colorChanged") as Action<Color>);
        }

        private void CloseFromButton() { if (IsLoaded && !IsInputBlocked) Close(); }
        protected override void OnViewEnabled() => closeButton.onClick.AddListener(CloseFromButton);
        protected override void OnViewDisabled() { if (closeButton) closeButton.onClick.RemoveListener(CloseFromButton); }
        public override void OnRemove() { if (selector) selector.EndSession(); }
    }
}
