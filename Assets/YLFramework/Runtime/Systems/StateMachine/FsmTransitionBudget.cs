using System.Collections.Generic;

namespace YLFramework.StateMachine
{
    internal sealed class FsmTransitionBudget
    {
        private readonly Dictionary<string, int> _visits = new Dictionary<string, int>();
        private int _frame = -1;
        private int _total;
        private bool _isBlocked;
        public bool TryEnter(int frameCount, string state)
        {
            if (_frame != frameCount)
            {
                Clear();
                _frame = frameCount;
            }
            if (_isBlocked)
            {
                return false;
            }

            _visits.TryGetValue(state, out int count);
            _visits[state] = ++count;
            _isBlocked = ++_total > 256 || count > 32;
            return !_isBlocked;
        }
        public void Clear()
        {
            _visits.Clear();
            _frame = -1;
            _total = 0;
            _isBlocked = false;
        }
    }
}
