using System.Collections.Generic;

namespace YLFramework.StateMachine
{
    internal sealed class FsmTriggerQueue
    {
        private readonly Dictionary<int, int> _pending = new Dictionary<int, int>();
        public void Enqueue(int key)
        {
            int count = GetCount(key);
            if (count < int.MaxValue)
            {
                _pending[key] = count + 1;
            }
        }
        public int GetCount(int key) => _pending.TryGetValue(key, out int count) ? count : 0;
        public bool TryTake(int key, bool unset)
        {
            int count = GetCount(key);
            if (!unset || count == 0)
            {
                return false;
            }

            if (count == 1)
            {
                _pending.Remove(key);
            }
            else
            {
                _pending[key] = count - 1;
            }

            return true;
        }
        public void CopyKeysTo(List<int> keys)
        {
            keys.Clear();
            keys.AddRange(_pending.Keys);
        }
        public void Reset(int key) => _pending.Remove(key);
        public void Clear() => _pending.Clear();
    }
}
