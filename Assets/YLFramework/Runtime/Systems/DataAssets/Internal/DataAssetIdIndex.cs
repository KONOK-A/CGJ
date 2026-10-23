using System;
using System.Collections.Generic;

namespace YLFramework.DataAssets
{
    // 保留原配表的ID查询/范围算法；先完整校验，再发布不可变索引。
    internal sealed class DataAssetIdIndex
    {
        private readonly Dictionary<int, int> _rows = new Dictionary<int, int>();
        private readonly int _min;
        private readonly int _max;

        internal DataAssetIdIndex(IReadOnlyList<int?> ids, string owner)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            var first = true;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!ids[i].HasValue)
                {
                    continue;
                }

                var id = ids[i].Value;
                if (_rows.TryGetValue(id, out var previous))
                {
                    throw new InvalidOperationException($"DataAssets '{owner}': duplicate ID {id} at rows {previous} and {i}.");
                }

                _rows.Add(id, i);
                if (first || id < _min)
                {
                    _min = id;
                }

                if (first || id > _max)
                {
                    _max = id;
                }

                first = false;
            }
        }

        internal bool TryGetRow(int id, out int row) => _rows.TryGetValue(id, out row);

        internal bool TryGetRange(out int minimum, out int maximum)
        {
            minimum = _min;
            maximum = _max;
            return _rows.Count != 0;
        }

        internal int NextId()
        {
            if (_max < int.MaxValue)
            {
                return Math.Max(1, _max + 1);
            }

            for (var id = 1; id < int.MaxValue; id++)
            {
                if (!_rows.ContainsKey(id))
                {
                    return id;
                }
            }

            throw new InvalidOperationException("No positive DataAssets ID is available.");
        }
    }
}
