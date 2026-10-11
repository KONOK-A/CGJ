using UnityEngine;
using UnityEngine.UI;

namespace YLFramework.Core
{
    public enum PanelType
    {
        /// <summary>不透明且完整覆盖所属根的页面，可遮挡同一上下文内较低的页面。</summary>
        Panel = 1,
        /// <summary>普通窗口，不产生全屏渲染遮挡。</summary>
        Window = 2,
        /// <summary>悬浮内容，不参与普通窗口 Top 或焦点候选。</summary>
        Overlay = 3
    }

    public abstract class Panel : View
    {
        private int sortingOrder;

        /// <summary>本窗口控制的 Canvas，或子控件借用的祖先 Panel Canvas。</summary>
        public Canvas Canvas { get; private set; }

        /// <summary>当前会话所属的实际根。不同根与 Scene 的显示策略相互隔离。</summary>
        public Canvas RootCanvas => PanelManager.GetRoot(this);

        /// <summary>逻辑顺序。Module 偏移、平手与 AlwaysInFront 统一生成有效顺序。</summary>
        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                if (!IsMyselfAddCanvas) return;
                PanelManager.SetOrder(this, value);
            }
        }

        /// <summary>实际 Canvas 顺序；借用 Canvas 的子控件没有独立顺序。</summary>
        public int DisplaySortingOrder => Canvas ? Canvas.sortingOrder : 0;

        public virtual PanelType PanelType => Window?.PanelType ?? PanelType.Window;
        public virtual bool AlwaysInFront => false;

        /// <summary>模态意图。PanelManager 派生输入阻挡，配置的 UIRootContext 据此呈现遮罩；不代替输入后端。</summary>
        public virtual bool IsModal => Window?.Modal ?? false;
        public string WindowKey => Window?.Key;
        public int Layer => WindowLayer;

        /// <summary>Loaded、active 且未被同根全屏页面遮挡。</summary>
        public bool IsVisible => PanelManager.IsVisible(this);

        /// <summary>被较高的同根模态窗口阻挡，或当前不可见；不表示已完成输入系统接管。</summary>
        public bool IsInputBlocked => PanelManager.IsInputBlocked(this);

        /// <summary>当前根的有效导航焦点候选，不等于 EventSystem.currentSelectedGameObject。</summary>
        public bool HasFocus => this && PanelManager.GetFocusedPanel(RootCanvas) == this;

        internal bool IsMyselfAddCanvas { get; private set; }
        internal int RequestedOrder => sortingOrder;
        internal void CommitOrder(int value) => sortingOrder = value;
        internal WindowDefinition Window;
        internal int WindowLayer => Window?.Layer ?? 0;
        internal PanelType PolicyType => Window?.PanelType ?? PanelType;
        internal bool PolicyModal => Window?.Modal ?? IsModal;

        /// <summary>唯一入口再次打开已加载窗口时借读新参数，不重复 OnLoaded 或事件登记。</summary>
        public virtual void OnWindowUpdated(LoadViewArgs args) { }

        /// <summary>当前会话的根寿命类型，在业务 OnLoaded 前确定。</summary>
        public UIType UIType { get; internal set; }

        protected virtual void Awake() { }

        internal void InitCanvas()
        {
            var local = GetComponent<Canvas>();
            // 独立窗口可接管自身预置 Canvas；只有没有本地 Canvas 的子 Panel 借用祖先窗口。
            var borrowed = FindBorrowedCanvas();
            if (!local && borrowed)
            {
                Canvas = borrowed;
                IsMyselfAddCanvas = false;
                return;
            }
            Canvas = local ? local : gameObject.AddComponent<Canvas>();
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            if (!local)
            {
                Canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 |
                    AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3 |
                    AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            }
            IsMyselfAddCanvas = true;
            Canvas.overrideSorting = true;
        }

        internal void RestoreCanvas()
        {
            RefreshBorrowedCanvas();
            if (!Canvas) throw new System.InvalidOperationException("Panel Canvas is unavailable.");
            if (IsMyselfAddCanvas) PanelManager.RestoreCanvas(this);
        }

        internal Canvas FindBorrowedCanvas()
        {
            for (var parent = transform.parent; parent; parent = parent.parent)
            {
                var owner = parent.GetComponent<Panel>();
                if (owner && owner.Session != null && !owner.IsRemoving && owner.IsMyselfAddCanvas && owner.Canvas)
                    return owner.Canvas;
            }
            return null;
        }

        internal void RefreshBorrowedCanvas()
        {
            if (!IsMyselfAddCanvas) Canvas = FindBorrowedCanvas();
        }

        /// <summary>有效排序变更通知。自身初始化时抛错会回滚；其他窗口的观察异常单独报告。</summary>
        protected virtual void OnSortingOrderChange(int sortingOrder) { }
        internal void NotifySortingOrder() => OnSortingOrderChange(DisplaySortingOrder);

        /// <summary>被同根的有效全屏页面遮挡；不会 Close、停用对象或清理其他系统。</summary>
        public virtual void Invisible() { }
        /// <summary>从全屏遮挡恢复。</summary>
        public virtual void Visible() { }

        /// <summary>同根、同 Scene 内的有效普通窗口 Top；排除 Overlay、失活与旧会话。</summary>
        public bool IsTop() => this && PanelManager.GetTopPanel(RootCanvas) == this;

        /// <summary>排序层、AlwaysInFront 或模态配置改变后，重新提交本窗口策略。</summary>
        public void RefreshUIState() => PanelManager.Refresh(this);

        internal override void BeforeOnLoaded()
        {
            base.BeforeOnLoaded();
            if (Window != null && Window.Stretch)
            {
                var rect = transform as RectTransform;
                if (!rect) throw new System.InvalidOperationException("Stretch window requires a RectTransform.");
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            InitCanvas();
            PanelManager.Prepare(this);
        }
    }
}
