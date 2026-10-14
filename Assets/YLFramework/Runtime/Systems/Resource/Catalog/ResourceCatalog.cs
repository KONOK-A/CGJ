using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace YLFramework.Services.Resource
{
    /// <summary>canonical 资产与显式 alias；不携带持久化 Schema 或构建配置。</summary>
    public sealed class ResourceCatalogEntry
    {
        public string AssetId { get; }
        public ResourceAddress Address { get; }
        public ResourceLocation Location { get; }
        public IReadOnlyList<ResourceAddress> Aliases { get; }
        public IReadOnlyList<string> Dependencies { get; }

        public ResourceCatalogEntry(string assetId, ResourceAddress address, ResourceLocation location,
            IEnumerable<ResourceAddress> aliases = null, IEnumerable<string> dependencies = null)
        {
            ResourceAddress.ValidateId(assetId, nameof(assetId));
            if (!address.IsValid) throw new ArgumentException("A valid address is required.", nameof(address));
            AssetId = assetId;
            Address = address;
            Location = location ?? throw new ArgumentNullException(nameof(location));
            Aliases = ResourceCatalog.Copy(aliases);
            Dependencies = ResourceCatalog.Copy(dependencies);
        }
    }

    /// <summary>有界依赖节点/边。此批只验证图，不预先定义 Bundle 持久化字段或执行依赖加载。</summary>
    public sealed class ResourceDependencyNode
    {
        public string Id { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public ResourceDependencyNode(string id, IEnumerable<string> dependencies = null)
        {
            ResourceAddress.ValidateId(id, nameof(id));
            Id = id;
            Dependencies = ResourceCatalog.Copy(dependencies);
        }
    }

    /// <summary>不可变、只读内存快照；解析仅认已登记的地址，构建/编码规范留待真实构建闭环确定。</summary>
    public sealed class ResourceCatalog
    {
        internal const int MaximumItems = 16384;
        private readonly Dictionary<ResourceAddress, ResourceCatalogEntry> addresses = new();
        public Guid SnapshotId { get; } = Guid.NewGuid();
        public string ProjectId { get; }
        public IReadOnlyList<ResourceCatalogEntry> Entries { get; }
        public IReadOnlyList<ResourceDependencyNode> DependencyNodes { get; }

        public ResourceCatalog(string projectId, IEnumerable<ResourceCatalogEntry> entries,
            IEnumerable<ResourceDependencyNode> dependencyNodes = null)
        {
            ResourceAddress.ValidateId(projectId, nameof(projectId));
            ProjectId = projectId;
            Entries = Copy(entries ?? throw new ArgumentNullException(nameof(entries)));
            DependencyNodes = Copy(dependencyNodes);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var foldedAddresses = new HashSet<(string, string)>(new FoldedAddressComparer());
            var locations = new HashSet<(ResourceLocation, string)>();
            foreach (var entry in Entries)
            {
                if (entry == null || !ids.Add(entry.AssetId)) throw new ArgumentException("Null or duplicate canonical asset identity.", nameof(entries));
                if (!locations.Add((entry.Location, entry.Address.SubAsset)))
                    throw new ArgumentException($"Duplicate source identity: {entry.Location}; use an explicit alias.", nameof(entries));
                AddAddress(entry.Address, entry, foldedAddresses);
                foreach (var alias in entry.Aliases) AddAddress(alias, entry, foldedAddresses);
            }
            ValidateGraph();
        }

        internal static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
        {
            var copy = new List<T>();
            if (values != null)
                foreach (var value in values)
                {
                    if (copy.Count == MaximumItems) throw new ArgumentException("The in-memory catalog collection limit was exceeded.");
                    copy.Add(value);
                }
            return copy.AsReadOnly();
        }

        private void AddAddress(ResourceAddress address, ResourceCatalogEntry entry, HashSet<(string, string)> folded)
        {
            if (!address.IsValid || address.ProjectId != ProjectId ||
                address.SubAsset != entry.Address.SubAsset || !folded.Add((address.Path, address.SubAsset)))
                throw new ArgumentException($"Invalid, duplicate or case-folded address conflict: {address}.");
            if (addresses.Count >= MaximumItems) throw new ArgumentException("The catalog address limit was exceeded.");
            addresses.Add(address, entry);
        }

        private sealed class FoldedAddressComparer : IEqualityComparer<(string, string)>
        {
            public bool Equals((string, string) left, (string, string) right) =>
                StringComparer.OrdinalIgnoreCase.Equals(left.Item1, right.Item1) && StringComparer.OrdinalIgnoreCase.Equals(left.Item2, right.Item2);
            public int GetHashCode((string, string) value) => StringComparer.OrdinalIgnoreCase.GetHashCode(value.Item1) ^
                (value.Item2 == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(value.Item2));
        }

        private void ValidateGraph()
        {
            var nodes = new Dictionary<string, ResourceDependencyNode>(StringComparer.Ordinal);
            foreach (var node in DependencyNodes)
                if (node == null || nodes.ContainsKey(node.Id)) throw new ArgumentException("Null or duplicate dependency node.");
                else nodes.Add(node.Id, node);
            var incoming = new Dictionary<string, int>(StringComparer.Ordinal);
            var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var edges = 0;
            foreach (var node in DependencyNodes)
            {
                incoming.Add(node.Id, node.Dependencies.Count);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (var dependency in node.Dependencies)
                {
                    if (dependency == null || !nodes.ContainsKey(dependency) || !unique.Add(dependency))
                        throw new ArgumentException($"Missing or duplicate dependency of '{node.Id}': '{dependency}'.");
                    if (++edges > MaximumItems * 4) throw new ArgumentException("The catalog edge limit was exceeded.");
                    if (!dependents.TryGetValue(dependency, out var list)) dependents.Add(dependency, list = new List<string>());
                    list.Add(node.Id);
                }
            }
            foreach (var entry in Entries)
            {
                var unique = new HashSet<string>(StringComparer.Ordinal);
                foreach (var dependency in entry.Dependencies)
                    if (dependency == null || !nodes.ContainsKey(dependency) || !unique.Add(dependency))
                        throw new ArgumentException($"Missing or duplicate dependency of '{entry.AssetId}': '{dependency}'.");
            }
            // Kahn 遍历避免长链递归溢出；剩余节点中沿依赖边给出真实环路径。
            var ready = new Queue<string>();
            foreach (var pair in incoming) if (pair.Value == 0) ready.Enqueue(pair.Key);
            var visited = 0;
            while (ready.Count != 0)
            {
                var id = ready.Dequeue();
                visited++;
                if (dependents.TryGetValue(id, out var list))
                    foreach (var dependent in list) if (--incoming[dependent] == 0) ready.Enqueue(dependent);
            }
            if (visited == nodes.Count) return;
            string cursor = null;
            foreach (var pair in incoming) if (pair.Value > 0) { cursor = pair.Key; break; }
            var path = new List<string>();
            var positions = new Dictionary<string, int>(StringComparer.Ordinal);
            while (!positions.ContainsKey(cursor))
            {
                positions.Add(cursor, path.Count);
                path.Add(cursor);
                foreach (var dependency in nodes[cursor].Dependencies)
                    if (incoming[dependency] > 0) { cursor = dependency; break; }
            }
            var cycle = path.GetRange(positions[cursor], path.Count - positions[cursor]);
            cycle.Add(cursor);
            throw new ArgumentException("Dependency cycle: " + string.Join(" -> ", cycle));
        }

        public ResourceCatalogEntry Resolve(ResourceAddress address)
        {
            if (!addresses.TryGetValue(address, out var entry)) throw new ArgumentException($"Unknown catalog address: {address}.", nameof(address));
            return entry;
        }
    }
}
