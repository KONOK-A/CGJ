using System;
using System.Collections.Generic;

namespace YLFramework.DataAssets
{
    // 保留原配表的ID查询/范围算法；先完整校验，再发布不可变索引。
    internal sealed class DataAssetIdIndex
    {
        private readonly Dictionary<int, int> rows = new Dictionary<int, int>();
        private readonly int min;
        private readonly int max;

        internal DataAssetIdIndex(IReadOnlyList<int?> ids, string owner)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var first = true;
            for (var i = 0; i < ids.Count; i++)
            {
                if (!ids[i].HasValue) continue;
                var id = ids[i].Value;
                if (rows.TryGetValue(id, out var previous))
                    throw new InvalidOperationException($"DataAssets '{owner}': duplicate ID {id} at rows {previous} and {i}.");
                rows.Add(id, i);
                if (first || id < min) min = id;
                if (first || id > max) max = id;
                first = false;
            }
        }

        internal bool TryGetRow(int id, out int row) => rows.TryGetValue(id, out row);

        internal bool TryGetRange(out int minimum, out int maximum)
        {
            minimum = min;
            maximum = max;
            return rows.Count != 0;
        }

        internal int NextId()
        {
            if (max < int.MaxValue) return Math.Max(1, max + 1);
            for (var id = 1; id < int.MaxValue; id++)
                if (!rows.ContainsKey(id)) return id;
            throw new InvalidOperationException("No positive DataAssets ID is available.");
        }
    }
}
