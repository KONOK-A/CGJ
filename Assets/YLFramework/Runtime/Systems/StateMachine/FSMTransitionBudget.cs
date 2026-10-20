using System.Collections.Generic;

namespace YLFramework.StateMachine
{
    internal sealed class FSMTransitionBudget
    {
        private readonly Dictionary<string, int> visits = new Dictionary<string, int>();
        private int frame = -1;
        private int total;
        private bool blocked;
        public bool TryEnter(int frameCount, string state)
        {
            if (frame != frameCount) { Clear(); frame = frameCount; }
            if (blocked) return false;
            visits.TryGetValue(state, out int count);
            visits[state] = ++count;
            blocked = ++total > 256 || count > 32;
            return !blocked;
        }
        public void Clear() { visits.Clear(); frame = -1; total = 0; blocked = false; }
    }
}
