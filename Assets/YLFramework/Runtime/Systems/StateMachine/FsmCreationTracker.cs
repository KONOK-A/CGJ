namespace YLFramework.StateMachine
{
    internal sealed class FsmCreationTracker
    {
        private readonly bool[] _created;
        public FsmCreationTracker(int count) => _created = new bool[count];
        public bool IsCreated(int index) => _created[index];
        public bool TryMarkCreated(int index)
        {
            if (_created[index])
            {
                return false;
            }

            _created[index] = true;
            return true;
        }
    }
}
