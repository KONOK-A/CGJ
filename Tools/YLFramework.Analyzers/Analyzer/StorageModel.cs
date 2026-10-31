using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace YLFramework.Analyzers
{
    internal sealed class StorageModel
    {
        private sealed class Summary
        {
            internal readonly HashSet<ITypeParameterSymbol> Slots = new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default);
            internal readonly List<ITypeSymbol> Values = new List<ITypeSymbol>();
            internal readonly HashSet<INamedTypeSymbol> Dependencies = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            internal bool IsComplete;
        }

        private readonly AnalyzerSymbols _symbols;
        private readonly object _gate = new object();
        private readonly Dictionary<INamedTypeSymbol, Summary> _summaries = new Dictionary<INamedTypeSymbol, Summary>(SymbolEqualityComparer.Default);
        private readonly Dictionary<ITypeSymbol, string> _results = new Dictionary<ITypeSymbol, string>(SymbolEqualityComparer.Default);
        private readonly HashSet<INamedTypeSymbol> _profiles = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        internal StorageModel(AnalyzerSymbols symbols)
        {
            _symbols = symbols;
            foreach (var name in new[] {
                "System.Collections.Generic.List`1", "System.Collections.Generic.HashSet`1", "System.Collections.Generic.Queue`1",
                "System.Collections.Generic.Stack`1", "System.Collections.Generic.LinkedList`1", "System.Collections.Generic.SortedSet`1",
                "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.SortedList`2",
                "System.Collections.ObjectModel.Collection`1", "System.Collections.ObjectModel.ReadOnlyCollection`1", "System.Collections.ObjectModel.ObservableCollection`1",
                "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IList`1", "System.Collections.Generic.ISet`1",
                "System.Collections.Generic.IReadOnlyCollection`1", "System.Collections.Generic.IReadOnlyList`1",
                "System.Collections.Generic.IDictionary`2", "System.Collections.Generic.IReadOnlyDictionary`2",
                "System.Collections.Generic.KeyValuePair`2", "System.Threading.Tasks.Task`1", "System.Threading.Tasks.ValueTask`1",
                "System.Memory`1", "System.ReadOnlyMemory`1",
                "System.Collections.Concurrent.ConcurrentQueue`1", "System.Collections.Concurrent.ConcurrentStack`1",
                "System.Collections.Concurrent.ConcurrentBag`1", "System.Collections.Concurrent.ConcurrentDictionary`2"
            }.Concat(Enumerable.Range(1, 8).SelectMany(i => new[] { "System.Tuple`" + i, "System.ValueTuple`" + i })))
            {
                var type = symbols.Compilation.GetTypeByMetadataName(name);
                if (type == null || type.Locations.Any(l => l.IsInSource))
                {
                    continue;
                }
                // Unity 的 .NET Framework/Mono 引用集将 ConcurrentBag<T> 定义在 System.dll 中。
                if (new[] { "mscorlib", "System", "System.Private.CoreLib", "System.Runtime", "netstandard", "System.Collections", "System.ObjectModel", "System.Threading.Tasks", "System.Threading.Tasks.Extensions", "System.Memory", "System.Collections.Concurrent" }
                    .Contains(type.ContainingAssembly.Name, StringComparer.Ordinal))
                {
                    _profiles.Add(type);
                }
            }
            if (symbols.ObjectPool != null)
            {
                _profiles.Add(symbols.ObjectPool);
            }
        }

        internal string Find(ITypeSymbol type, CancellationToken token)
        {
            lock (_gate)
            {
                token.ThrowIfCancellationRequested();
                if (type == null || type.TypeKind == TypeKind.Error)
                {
                    return null;
                }

                if (_results.TryGetValue(type, out var cached))
                {
                    return cached;
                }

                if (!IsCandidate(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), token))
                {
                    _results[type] = null;
                    return null;
                }
                Ensure(type, token);
                var known = true;
                var witness = Witness(type, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), ref known, token);
                if (witness != null || known)
                {
                    _results[type] = witness;
                }

                return witness;
            }
        }

        private bool IsCandidate(ITypeSymbol type, HashSet<ITypeSymbol> seen, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (type == null || type.TypeKind == TypeKind.Error || !seen.Add(type))
            {
                return false;
            }

            if (_symbols.Marked(type, 4, token).attribute != null)
            {
                return true;
            }

            if (type is ITypeParameterSymbol parameter)
            {
                return Constraints(parameter).Any(t => IsCandidate(t, seen, token));
            }

            if (type is IArrayTypeSymbol array)
            {
                return IsCandidate(array.ElementType, seen, token);
            }

            if (!(type is INamedTypeSymbol named) || named.TypeKind == TypeKind.Delegate)
            {
                return false;
            }

            for (var current = named; current != null; current = current.ContainingType)
            {
                if (current.TypeArguments.Any(t => IsCandidate(t, seen, token)))
                {
                    return true;
                }
            }

            return false;
        }

        // Roslyn 3.8 的原始 ValueTuple 定义也标记 IsTupleType，但已无下一层 underlying。
        private static INamedTypeSymbol Normalize(INamedTypeSymbol type) => type.TupleUnderlyingType ?? type;

        private static bool ContainsParameter(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol)
            {
                return true;
            }

            if (type is IArrayTypeSymbol array)
            {
                return ContainsParameter(array.ElementType);
            }

            if (!(type is INamedTypeSymbol named) || named.TypeKind == TypeKind.Delegate)
            {
                return false;
            }

            return named.TypeArguments.Any(ContainsParameter) || (named.ContainingType != null && ContainsParameter(named.ContainingType));
        }

        private void Ensure(ITypeSymbol root, CancellationToken token)
        {
            var pending = new Stack<ITypeSymbol>();
            var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            pending.Push(root);
            var changed = false;
            while (pending.Count != 0)
            {
                token.ThrowIfCancellationRequested();
                var type = pending.Pop();
                if (type == null || !seen.Add(type))
                {
                    continue;
                }

                if (type is ITypeParameterSymbol parameter)
                {
                    foreach (var constraint in Constraints(parameter))
                    {
                        pending.Push(constraint);
                    }

                    continue;
                }
                if (type is IArrayTypeSymbol array)
                {
                    pending.Push(array.ElementType);
                    continue;
                }
                if (!(type is INamedTypeSymbol named) || type.TypeKind == TypeKind.Error || type.TypeKind == TypeKind.Delegate)
                {
                    continue;
                }

                named = Normalize(named);
                foreach (var argument in Arguments(named))
                {
                    pending.Push(argument);
                }

                var definition = named.OriginalDefinition;
                if (_summaries.ContainsKey(definition))
                {
                    continue;
                }

                changed = true;
                var summary = new Summary { IsComplete = definition.Locations.Any(l => l.IsInSource) || _profiles.Contains(definition) };
                _summaries.Add(definition, summary);
                if (_profiles.Contains(definition))
                {
                    summary.Slots.UnionWith(definition.TypeParameters);
                    continue;
                }
                // 仅展开含参数的实例值槽，不为无关非泛型成员搜索整个对象图。
                var fields = definition.GetMembers().OfType<IFieldSymbol>().Where(f => !f.IsStatic).ToArray();
                summary.IsComplete &= !fields.Any(f => HasError(f.Type)) && !HasError(definition.BaseType);
                summary.Values.AddRange(fields.Where(f => ContainsParameter(f.Type)).Select(f => f.Type));
                if (ContainsParameter(definition.BaseType))
                {
                    summary.Values.Add(definition.BaseType);
                }

                foreach (var value in summary.Values)
                {
                    foreach (var dependency in Definitions(value))
                    {
                        summary.Dependencies.Add(dependency);
                    }

                    pending.Push(value);
                }
            }
            if (!changed)
            {
                return;
            }
            // 摘要只增加内容位；定义数量有限，纯环不会把 visiting 当作持有证据。
            do
            {
                changed = false;
                foreach (var summary in _summaries.Values)
                {
                    foreach (var value in summary.Values)
                    {
                        token.ThrowIfCancellationRequested();
                        foreach (var parameter in ContentParameters(value))
                        {
                            changed |= summary.Slots.Add(parameter);
                        }
                    }
                }
            } while (changed);
        }

        private static IEnumerable<ITypeSymbol> Arguments(INamedTypeSymbol named)
        {
            for (var current = named; current != null; current = current.ContainingType)
            {
                foreach (var argument in current.TypeArguments)
                {
                    yield return argument;
                }
            }
        }

        private static IEnumerable<ITypeSymbol> Constraints(ITypeParameterSymbol parameter)
            => parameter.ConstraintTypes.Where(t => t.TypeKind == TypeKind.Class || t.TypeKind == TypeKind.TypeParameter);

        private static bool HasError(ITypeSymbol type)
            => type?.TypeKind == TypeKind.Error || (type is IArrayTypeSymbol array && HasError(array.ElementType)) ||
                (type is INamedTypeSymbol named && (named.TypeArguments.Any(HasError) || HasError(named.ContainingType)));

        private static IEnumerable<INamedTypeSymbol> Definitions(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
            {
                foreach (var definition in Definitions(array.ElementType))
                {
                    yield return definition;
                }
            }
            else if (type is INamedTypeSymbol named && named.TypeKind != TypeKind.Delegate && named.TypeKind != TypeKind.Error)
            {
                yield return Normalize(named).OriginalDefinition;
                foreach (var argument in Arguments(named))
                {
                    foreach (var definition in Definitions(argument))
                    {
                        yield return definition;
                    }
                }
            }
        }

        private static ITypeSymbol Bound(INamedTypeSymbol type, ITypeParameterSymbol parameter)
        {
            for (var current = type; current != null; current = current.ContainingType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, parameter.ContainingSymbol) && parameter.Ordinal < current.TypeArguments.Length)
                {
                    return current.TypeArguments[parameter.Ordinal];
                }
            }

            return null;
        }

        private IEnumerable<ITypeParameterSymbol> ContentParameters(ITypeSymbol type)
        {
            if (type is ITypeParameterSymbol parameter)
            {
                yield return parameter;
            }
            else if (type is IArrayTypeSymbol array)
            {
                foreach (var value in ContentParameters(array.ElementType))
                {
                    yield return value;
                }
            }
            else if (type is INamedTypeSymbol named && named.TypeKind != TypeKind.Delegate)
            {
                named = Normalize(named);
                if (!_summaries.TryGetValue(named.OriginalDefinition, out var summary))
                {
                    yield break;
                }

                foreach (var slot in summary.Slots.ToArray())
                {
                    foreach (var value in ContentParameters(Bound(named, slot)))
                    {
                        yield return value;
                    }
                }
            }
        }

        private bool IsComplete(INamedTypeSymbol definition, HashSet<INamedTypeSymbol> seen)
            => !seen.Add(definition) || (_summaries.TryGetValue(definition, out var summary) && summary.IsComplete && summary.Dependencies.All(d => IsComplete(d, seen)));

        private string Witness(ITypeSymbol type, HashSet<ITypeSymbol> seen, ref bool known, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (type == null || type.TypeKind == TypeKind.Error)
            {
                known = false;
                return null;
            }
            if (!seen.Add(type))
            {
                return null;
            }

            var marker = _symbols.Marked(type, 4, token);
            if (marker.attribute != null)
            {
                return AnalyzerSymbols.Display(type) + "（" + _symbols.Reason(marker.attribute) + "）";
            }

            if (type is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in Constraints(parameter))
                {
                    var leaf = Witness(constraint, seen, ref known, token);
                    if (leaf != null)
                    {
                        return AnalyzerSymbols.Display(type) + " → 类约束 → " + leaf;
                    }
                }
                return null;
            }
            if (type is IArrayTypeSymbol array)
            {
                var leaf = Witness(array.ElementType, seen, ref known, token);
                return leaf == null ? null : AnalyzerSymbols.Display(type) + " → 数组元素 → " + leaf;
            }
            if (!(type is INamedTypeSymbol named) || type.TypeKind == TypeKind.Delegate)
            {
                return null;
            }

            named = Normalize(named);
            if (!_summaries.TryGetValue(named.OriginalDefinition, out var summary))
            {
                known = false;
                return null;
            }
            known &= IsComplete(named.OriginalDefinition, new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default));
            foreach (var slot in summary.Slots.OrderBy(p => AnalyzerSymbols.Display(p.ContainingSymbol), StringComparer.Ordinal).ThenBy(p => p.Ordinal))
            {
                var leaf = Witness(Bound(named, slot), seen, ref known, token);
                if (leaf != null)
                {
                    return AnalyzerSymbols.Display(type) + " → 实例内容位 " + slot.Name + " → " + leaf;
                }
            }
            return null;
        }
    }
}
