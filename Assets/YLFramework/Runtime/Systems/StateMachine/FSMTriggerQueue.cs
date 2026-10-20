using System.Collections.Generic;

namespace YLFramework.StateMachine
{
    internal sealed class FSMTriggerQueue
    {
        private readonly Dictionary<int, int> pending = new Dictionary<int, int>();
        public void Enqueue(int key)
        {
            int count = GetCount(key);
            if (count < int.MaxValue) pending[key] = count + 1;
        }
        public int GetCount(int key) => pending.TryGetValue(key, out int count) ? count : 0;
        public bool TryTake(int key, bool unset)
        {
            int count = GetCount(key);
            if (!unset || count == 0) return false;
            if (count == 1) pending.Remove(key);
            else pending[key] = count - 1;
            return true;
        }
        public void CopyKeysTo(List<int> keys) { keys.Clear(); keys.AddRange(pending.Keys); }
        public void Reset(int key) => pending.Remove(key);
        public void Clear() => pending.Clear();
    }
}
