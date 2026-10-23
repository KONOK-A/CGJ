using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core
{
    /// <summary>业务已解析的资源规则。完整地址或 Prefab 引用二选一，不是必须注册的窗口定义。</summary>
    public readonly struct PanelRule
    {
        /// <summary>严格区分大小写的完整地址；直接规则为 null。</summary>
        public string AssetName
        {
            get;
        }
        /// <summary>直接 Prefab 引用；地址规则为 null，不以 InstanceID 代替存活引用。</summary>
        public GameObject Prefab
        {
            get;
        }
        /// <summary>该身份匹配时逐字段应用的覆盖值。</summary>
        public PanelOverrides Overrides
        {
            get;
        }
        /// <summary>创建地址规则；绑定 Module 时统一校验并复制，不触发资源加载。</summary>
        public PanelRule(string assetName, PanelOverrides overrides)
        {
            AssetName = assetName;
            Prefab = null;
            Overrides = overrides;
        }
        /// <summary>创建直接来源规则；绑定时拒绝空引用、销毁引用和重复规则。</summary>
        public PanelRule(GameObject prefab, PanelOverrides overrides)
        {
            AssetName = null;
            Prefab = prefab;
            Overrides = overrides;
        }
    }

    // 绑定完整构造成功后才由 Module 替换；后续请求只持有这份不可变数据。
    internal sealed class PanelRuleSnapshot
    {
        internal static readonly PanelRuleSnapshot Empty = new PanelRuleSnapshot(Array.Empty<PanelRule>());
        private readonly PanelRule[] _rows;
        private PanelRuleSnapshot(PanelRule[] rows)
        {
            this._rows = rows;
        }

        internal static PanelRuleSnapshot Capture(IEnumerable<PanelRule> rules)
        {
            if (rules == null)
            {
                return Empty;
            }

            var values = new List<PanelRule>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rules)
            {
                row.Overrides.Validate();
                bool named = row.AssetName != null;
                if (named ? string.IsNullOrWhiteSpace(row.AssetName) || !ReferenceEquals(row.Prefab, null) : ReferenceEquals(row.Prefab, null) || !row.Prefab)
                {
                    throw new ArgumentException("PanelRule 必须指定非空完整地址或存活 Prefab，且只能指定一种身份。");
                }

                if (named && !names.Add(row.AssetName))
                {
                    throw new ArgumentException("重复 Panel 地址规则：" + row.AssetName);
                }

                if (!named)
                {
                    foreach (var other in values)
                    {
                        if (ReferenceEquals(other.Prefab, row.Prefab))
                        {
                            throw new ArgumentException("重复 Panel Prefab 规则。");
                        }
                    }
                }

                values.Add(row);
            }
            return values.Count == 0 ? Empty : new PanelRuleSnapshot(values.ToArray());
        }

        internal int Match(string assetName, GameObject prefab)
        {
            int match = -1;
            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                if (!(row.AssetName != null ? StringComparer.Ordinal.Equals(row.AssetName, assetName) :
                    !ReferenceEquals(prefab, null) && ReferenceEquals(row.Prefab, prefab)))
                {
                    continue;
                }

                if (match >= 0)
                {
                    throw new ArgumentException("地址与 Prefab 命中同一资源的多条 Panel 规则。");
                }

                match = i;
            }
            return match;
        }
        internal PanelOverrides Resolve(string assetName, GameObject prefab)
        {
            int index = Match(assetName, prefab);
            return index < 0 ? default : _rows[index].Overrides;
        }
    }
}
