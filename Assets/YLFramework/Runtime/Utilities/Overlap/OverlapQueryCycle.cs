namespace YLFramework.Overlap
{
    // 同一组件的回调不能重新覆盖当前快照；其他组件不共享查询状态。
    internal sealed class OverlapQueryCycle
    {
        private bool _isChecking;
        private bool _hasReportedCapacity;
        public bool IsInvalidated
        {
            get; private set;
        }

        public bool TryBegin()
        {
            if (_isChecking)
            {
                return false;
            }

            _isChecking = true;
            IsInvalidated = false;
            return true;
        }

        public void End() => _isChecking = false;

        public void Invalidate()
        {
            IsInvalidated = true;
            _hasReportedCapacity = false;
        }

        public bool ReportCapacity(int count, int capacity)
        {
            if (count < capacity || _hasReportedCapacity)
            {
                return false;
            }

            _hasReportedCapacity = true;
            return true;
        }
    }
}
