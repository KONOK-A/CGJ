using System;

namespace YLFramework.Core
{
    /// <summary>同一 Module、实际根、可证明资源内的实例策略。</summary>
    public enum PanelInstanceMode
    {
        /// <summary>同一作用域复用健康会话，并合流有效在途意图。</summary>
        Single,
        /// <summary>每次打开建立独立会话。</summary>
        Multiple
    }

    /// <summary>Prefab 默认或当前会话的七字段值副本。结构体零值不等于框架默认，请使用 Default。</summary>
    [Serializable]
    public struct PanelConfig
    {
        /// <summary>实际根内的显式层索引，必须非负且存在。</summary>
        public int Layer;
        /// <summary>同层同 Modal 分组内的优先级，不是原生 Canvas 顺序。</summary>
        public int Priority;
        /// <summary>是否阻挡同根排序更低的标准 UI 输入。</summary>
        public bool Modal;
        /// <summary>完整覆盖声明；只允许停绘低于它的同根 Panel，不改变布局。</summary>
        public bool CoversRoot;
        /// <summary>是否参与 Top 查询，不影响 Modal 本身的阻挡资格。</summary>
        public bool ParticipatesInTop;
        /// <summary>同 Module、实际根和可证明来源内的实例模式。</summary>
        public PanelInstanceMode InstanceMode;
        /// <summary>正常结束时是否允许后端缓存；损坏或来源责任不可转移时仍会丢弃。</summary>
        public bool CacheOnClose;

        /// <summary>轻量窗口默认：Layer/Priority 为 0，非模态、不覆盖、参与 Top、单实例且可缓存。</summary>
        public static PanelConfig Default => new PanelConfig { ParticipatesInTop = true, CacheOnClose = true };

        internal static int Compare(PanelConfig a, long aSequence, PanelConfig b, long bSequence)
        {
            int result = a.Layer.CompareTo(b.Layer);
            if (result == 0) result = a.Modal.CompareTo(b.Modal);
            if (result == 0) result = a.Priority.CompareTo(b.Priority);
            return result == 0 ? aSequence.CompareTo(bSequence) : result;
        }

        internal void Validate()
        {
            if (Layer < 0) throw new ArgumentOutOfRangeException(nameof(Layer), "Panel Layer 不能为负数。");
            if (InstanceMode != PanelInstanceMode.Single && InstanceMode != PanelInstanceMode.Multiple)
                throw new ArgumentOutOfRangeException(nameof(InstanceMode));
        }
    }
}
