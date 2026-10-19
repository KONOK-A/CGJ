namespace YLFramework.Overlap
{
    // 同一组件的回调不能重新覆盖当前快照；其他组件不共享查询状态。
    internal sealed class OverlapQueryCycle
    {
        private bool checking;
        private bool capacityReported;
        public bool Invalidated { get; private set; }

        public bool TryBegin()
        {
            if (checking) return false;
            checking = true;
            Invalidated = false;
            return true;
        }

        public void End() => checking = false;

        public void Invalidate()
        {
            Invalidated = true;
            capacityReported = false;
        }

        public bool ReportCapacity(int count, int capacity)
        {
            if (count < capacity || capacityReported) return false;
            capacityReported = true;
            return true;
        }
    }
}
