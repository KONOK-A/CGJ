using System;

namespace YLFramework.Timing.Internal
{
    // 从 YL Tools/Timing/Internal/BinaryHeap 收窄：保留 1-based 索引及局部修复，
    // 只开放内核需要的最小堆操作，增加私有归属检查和 due/order 稳定比较。
    internal sealed class TimerHeap
    {
        private readonly TimerClock _clock;
        private TimerRun[] _array = new TimerRun[10];
        internal int Count
        {
            get; private set;
        }
        internal TimerRun Top => Count == 0 ? null : _array[1];

        internal TimerHeap(TimerClock clock) => _clock = clock;

        internal void Insert(TimerRun run)
        {
            if (run.Clock != _clock || run.Heap != null || run.HeapIndex != 0)
            {
                throw new InvalidOperationException("Timer run already belongs to a queue or uses another clock.");
            }

            if (Count + 1 >= _array.Length)
            {
                Array.Resize(ref _array, checked(Math.Max(_array.Length + 4, (int)Math.Round(_array.Length * 1.6))));
            }

            run.Heap = this;
            _array[++Count] = run;
            PercolateUp(Count);
        }

        internal TimerRun Pop()
        {
            var run = Top;
            if (run != null)
            {
                Remove(run);
            }

            return run;
        }

        internal void Remove(TimerRun run)
        {
            var index = run.HeapIndex;
            if (!ReferenceEquals(run.Heap, this) || index < 1 || index > Count || !ReferenceEquals(_array[index], run))
            {
                throw new InvalidOperationException("Timer heap ownership does not match its index.");
            }

            run.HeapIndex = 0;
            run.Heap = null;
            var last = _array[Count];
            _array[Count--] = null;
            if (index > Count)
            {
                return;
            }

            _array[index] = last;
            last.HeapIndex = index;
            var parent = index >> 1;
            if (parent > 0 && ComesBefore(last, _array[parent]))
            {
                PercolateUp(index);
            }
            else
            {
                PercolateDown(index);
            }
        }

        private bool ComesBefore(TimerRun a, TimerRun b)
        {
            var compare = _clock == TimerClock.RenderedFrame ? a.DueFrame.CompareTo(b.DueFrame) : a.DueSeconds.CompareTo(b.DueSeconds);
            return compare < 0 || compare == 0 && a.Order < b.Order;
        }

        private void PercolateUp(int index)
        {
            var run = _array[index];
            var parent = index >> 1;
            while (parent >= 1 && ComesBefore(run, _array[parent]))
            {
                _array[index] = _array[parent];
                _array[index].HeapIndex = index;
                index = parent;
                parent = index >> 1;
            }
            _array[index] = run;
            run.HeapIndex = index;
        }

        private void PercolateDown(int index)
        {
            var run = _array[index];
            var child = index << 1;
            while (child <= Count)
            {
                if (child < Count && ComesBefore(_array[child + 1], _array[child]))
                {
                    child++;
                }

                if (!ComesBefore(_array[child], run))
                {
                    break;
                }

                _array[index] = _array[child];
                _array[index].HeapIndex = index;
                index = child;
                child = index << 1;
            }
            _array[index] = run;
            run.HeapIndex = index;
        }
    }
}
