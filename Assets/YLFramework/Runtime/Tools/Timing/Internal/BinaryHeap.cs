/****************************************************************************
 * Copyright (c) 2017 snowcold
 * Copyright (c) 2017 liangxie
 * 
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 ****************************************************************************/

using System;

namespace YLFramework.Utilities.Timing
{
    /// <summary>
    /// 以 1 为起始下标的二叉堆优先队列：按 <see cref="IBinaryHeapElement.SortScore"/> 排序，
    /// 元素通过 <see cref="IBinaryHeapElement.HeapIndex"/> 记录自身位置，分数变化后可原位修复顺序。
    /// 分数相同时不保证 FIFO 顺序。
    /// </summary>
    public class BinaryHeap<T> where T : IBinaryHeapElement
    {
        /// <summary>从下标一开始存储元素的数组。</summary>
        protected T[] mArray;
        /// <summary>自动扩容比例。</summary>
        protected float mGrowthFactor = 1.6f;
        /// <summary>最后一个有效元素的下标；0 表示空堆。</summary>
        protected int mLastChildIndex;
        /// <summary>当前排序方向。</summary>
        protected BinaryHeapSortMode mSortMode;

        /// <summary>创建指定初始容量及排序方向的空堆；容量下限为 2。</summary>
        public BinaryHeap(int minSize, BinaryHeapSortMode sortMode)
        {
            mSortMode = sortMode;
            mArray = new T[Math.Max(2, minSize)];
        }

        /// <summary>通过自底向上算法从数组创建堆。</summary>
        /// <exception cref="ArgumentNullException">dataArray 为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">数组内含 null 元素时抛出。</exception>
        public BinaryHeap(T[] dataArray, BinaryHeapSortMode sortMode)
            : this(dataArray == null ? 10 : dataArray.Length + 1, sortMode)
        {
            Insert(dataArray, BinaryHeapBuildMode.kN);
        }

        /// <summary>当前元素数量。</summary>
        public int Count => mLastChildIndex;

        /// <summary>清除所有元素并将其堆索引归零。</summary>
        public void Clear()
        {
            for (var i = 1; i <= mLastChildIndex; i++) mArray[i].HeapIndex = 0;
            Array.Clear(mArray, 0, mArray.Length);
            mLastChildIndex = 0;
        }

        /// <summary>按指定算法批量插入数组中的元素：kN 先追加再自底向上建堆，kNLog 逐个插入并上浮。</summary>
        /// <exception cref="ArgumentNullException">dataArray 为 null 时抛出。</exception>
        /// <exception cref="ArgumentException">数组内含 null 元素时抛出。</exception>
        public void Insert(T[] dataArray, BinaryHeapBuildMode buildMode)
        {
            if (dataArray == null) throw new ArgumentNullException(nameof(dataArray));
            foreach (var element in dataArray)
                if (element == null) throw new ArgumentException("Heap elements cannot be null.", nameof(dataArray));
            var totalLength = checked(mLastChildIndex + dataArray.Length + 1);
            if (mArray.Length < totalLength) ResizeArray(totalLength);
            if (buildMode == BinaryHeapBuildMode.kNLog)
            {
                foreach (var element in dataArray) Insert(element);
            }
            else
            {
                foreach (var element in dataArray)
                {
                    mArray[++mLastChildIndex] = element;
                    element.HeapIndex = mLastChildIndex;
                }
                SortAsCurrentMode();
            }
        }

        /// <summary>插入一个元素并上浮至正确位置。</summary>
        public void Insert(T element)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (mLastChildIndex + 1 >= mArray.Length) ResizeArray();
            mArray[++mLastChildIndex] = element;
            PercolateUp(mLastChildIndex);
        }

        /// <summary>移除并返回堆顶，空堆返回默认值。</summary>
        public T Pop()
        {
            if (!HasValue()) return default;
            var result = mArray[1];
            RemoveAt(1);
            return result;
        }

        /// <summary>读取堆顶，空堆返回默认值。</summary>
        public T Top() => HasValue() ? mArray[1] : default;

        /// <summary>切换排序方向并重新建堆。</summary>
        public void Sort(BinaryHeapSortMode sortMode)
        {
            if (mSortMode == sortMode) return;
            mSortMode = sortMode;
            SortAsCurrentMode();
        }

        /// <summary>修复指定位置分数变化后的堆顺序；无效索引不处理。</summary>
        public void RebuildAtIndex(int index)
        {
            if (index < 1 || index > mLastChildIndex) return;
            var parentIndex = index >> 1;
            if (parentIndex > 0 && ComesBefore(mArray[index], mArray[parentIndex])) PercolateUp(index);
            else PercolateDown(index);
        }

        /// <summary>删除指定位置的元素并修复堆顺序；被删元素的 HeapIndex 归零，无效索引不处理。</summary>
        public void RemoveAt(int index)
        {
            if (index < 1 || index > mLastChildIndex) return;
            mArray[index].HeapIndex = 0;
            var last = mArray[mLastChildIndex];
            mArray[mLastChildIndex--] = default;
            if (index > mLastChildIndex) return;
            mArray[index] = last;
            last.HeapIndex = index;
            RebuildAtIndex(index);
        }

        /// <summary>按堆位置读取元素，无效索引返回默认值。</summary>
        public T GetElement(int index) => index < 1 || index > mLastChildIndex ? default : mArray[index];

        /// <summary>判断堆内是否仍有元素。</summary>
        public bool HasValue() => mLastChildIndex > 0;

        /// <summary>扩容内部数组；newSize 小于 0 时按默认增长比例或 +4 自动扩容。</summary>
        /// <exception cref="InvalidOperationException">目标容量超过 2^30 时抛出。</exception>
        protected void ResizeArray(int newSize = -1)
        {
            if (newSize < 0) newSize = Math.Max(mArray.Length + 4, (int)Math.Round(mArray.Length * mGrowthFactor));
            if (newSize > 1 << 30) throw new InvalidOperationException("Binary heap capacity exceeds 2^30.");
            Array.Resize(ref mArray, newSize);
        }

        private bool ComesBefore(T a, T b) => mSortMode == BinaryHeapSortMode.kMin
            ? a.SortScore < b.SortScore : a.SortScore > b.SortScore;

        private void SortAsCurrentMode()
        {
            for (var i = mLastChildIndex >> 1; i > 0; --i) PercolateDown(i);
        }

        /// <summary>使用空穴上浮算法恢复指定位置的堆顺序。</summary>
        protected void PercolateUp(int index)
        {
            var element = mArray[index];
            var parentIndex = index >> 1;
            while (parentIndex >= 1 && ComesBefore(element, mArray[parentIndex]))
            {
                mArray[index] = mArray[parentIndex];
                mArray[index].HeapIndex = index;
                index = parentIndex;
                parentIndex = index >> 1;
            }
            mArray[index] = element;
            element.HeapIndex = index;
        }

        /// <summary>使用空穴下沉算法恢复指定位置的堆顺序。</summary>
        protected void PercolateDown(int index)
        {
            var element = mArray[index];
            var childIndex = index << 1;
            while (childIndex <= mLastChildIndex)
            {
                if (childIndex < mLastChildIndex && ComesBefore(mArray[childIndex + 1], mArray[childIndex])) childIndex++;
                if (!ComesBefore(mArray[childIndex], element)) break;
                mArray[index] = mArray[childIndex];
                mArray[index].HeapIndex = index;
                index = childIndex;
                childIndex = index << 1;
            }
            mArray[index] = element;
            element.HeapIndex = index;
        }
    }
}
