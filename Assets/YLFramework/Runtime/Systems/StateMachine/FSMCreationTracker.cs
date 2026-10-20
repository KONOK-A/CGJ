namespace YLFramework.StateMachine
{
    internal sealed class FSMCreationTracker
    {
        private readonly bool[] created;
        public FSMCreationTracker(int count) => created = new bool[count];
        public bool IsCreated(int index) => created[index];
        public bool TryMarkCreated(int index)
        {
            if (created[index]) return false;
            created[index] = true;
            return true;
        }
    }
}
