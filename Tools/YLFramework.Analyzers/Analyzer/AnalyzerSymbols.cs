using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace YLFramework.Analyzers
{
    internal sealed class AnalyzerSymbols
    {
        internal readonly IAssemblySymbol Core;
        internal readonly INamedTypeSymbol[] Markers;
        internal readonly Compilation Compilation;
        internal readonly INamedTypeSymbol ObjectPool;
        internal readonly INamedTypeSymbol View;
        private readonly IFieldSymbol poolOwner;
        private readonly IFieldSymbol requestOwner;
        private readonly bool[] inherited;
        private readonly ConcurrentDictionary<ISymbol, ImmutableArray<AttributeData>>[] attributes;
        private readonly ConcurrentDictionary<AttributeData, ImmutableArray<string>> strings = new ConcurrentDictionary<AttributeData, ImmutableArray<string>>();

        private AnalyzerSymbols(IAssemblySymbol core, Compilation compilation)
        {
            Core = core;
            Compilation = compilation;
            View = core.GetTypeByMetadataName("YLFramework.Core.View");
            var names = new[] { "MustCallBase", "RestrictedCall", "WarnInClasses", "DisableNew", "MethodLocalOnly", "ProtectedHierarchy" };
            Markers = names.Select(n => core.GetTypeByMetadataName("YLFramework.Core.RoslynAnalyzers." + n + "Attribute")).ToArray();
            inherited = Markers.Select(m => m == null || !m.GetAttributes().Any(a =>
                a.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute" && a.NamedArguments.Any(p => p.Key == "Inherited" && Equals(p.Value.Value, false)))).ToArray();
            attributes = names.Select(_ => new ConcurrentDictionary<ISymbol, ImmutableArray<AttributeData>>(SymbolEqualityComparer.Default)).ToArray();
            var pool = compilation.GetTypeByMetadataName("YLFramework.Core.Pool.ObjectPool`1");
            if (pool?.ContainingAssembly.Name == "YLFramework.Foundation.Runtime" && !pool.Locations.Any(l => l.IsInSource)) ObjectPool = pool;
            var args = core.GetTypeByMetadataName("YLFramework.Core.LoadViewArgs");
            var candidates = args?.GetMembers("Pool").OfType<IFieldSymbol>().ToArray();
            var field = candidates?.Length == 1 ? candidates[0] : null;
            if (field != null && field.DeclaredAccessibility == Accessibility.Private && field.IsStatic && field.IsReadOnly &&
                field.Type is INamedTypeSymbol fieldType && SymbolEqualityComparer.Default.Equals(fieldType.OriginalDefinition, ObjectPool) &&
                fieldType.TypeArguments.Length == 1 && SymbolEqualityComparer.Default.Equals(fieldType.TypeArguments[0], args) && !Attributes(args, 4).IsEmpty) poolOwner = field;
            var request = core.GetTypeByMetadataName("YLFramework.Core.LoadViewRequest`1");
            candidates = request?.GetMembers("args").OfType<IFieldSymbol>().ToArray();
            field = candidates?.Length == 1 ? candidates[0] : null;
            if (request?.Arity == 1 && field != null && field.DeclaredAccessibility == Accessibility.Private && !field.IsStatic &&
                SymbolEqualityComparer.Default.Equals(field.Type, args)) requestOwner = field;
        }

        internal static AnalyzerSymbols Create(Compilation compilation)
        {
            var candidates = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
            if (compilation.Assembly.Name == "YLFramework.Core.Runtime") candidates.Add(compilation.Assembly);
            foreach (var reference in compilation.References)
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly && assembly.Name == "YLFramework.Core.Runtime") candidates.Add(assembly);
            return candidates.Count == 1 ? new AnalyzerSymbols(candidates.Single(), compilation) : null;
        }

        internal bool IsOwner(ISymbol member) => member is IFieldSymbol field &&
            (SymbolEqualityComparer.Default.Equals(field.OriginalDefinition, poolOwner) || SymbolEqualityComparer.Default.Equals(field.OriginalDefinition, requestOwner));

        internal (INamedTypeSymbol type, AttributeData attribute) Marked(ITypeSymbol type, int rule, CancellationToken token)
        {
            foreach (var candidate in ClassTypes(type))
            {
                token.ThrowIfCancellationRequested();
                var effective = Attributes(candidate, rule);
                if (effective.IsEmpty) continue;
                for (var owner = candidate; owner != null; owner = owner.BaseType)
                {
                    token.ThrowIfCancellationRequested();
                    if (owner.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, Markers[rule]) && !HasError(a))) return (owner, effective[0]);
                }
            }
            return (null, null);
        }

        private static IEnumerable<INamedTypeSymbol> ClassTypes(ITypeSymbol type)
        {
            var pending = new Stack<ITypeSymbol>();
            var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            if (type != null) pending.Push(type);
            while (pending.Count != 0)
            {
                var current = pending.Pop();
                if (!seen.Add(current) || current.TypeKind == TypeKind.Error) continue;
                if (current is ITypeParameterSymbol parameter)
                {
                    foreach (var constraint in parameter.ConstraintTypes)
                        if (constraint.TypeKind == TypeKind.Class || constraint.TypeKind == TypeKind.TypeParameter) pending.Push(constraint);
                }
                else if (current is INamedTypeSymbol named) yield return named;
            }
        }

        internal ImmutableArray<AttributeData> Attributes(ISymbol symbol, int rule)
        {
            if (symbol == null || Markers[rule] == null) return ImmutableArray<AttributeData>.Empty;
            if (symbol is IMethodSymbol method) symbol = (method.ReducedFrom ?? method).OriginalDefinition;
            return attributes[rule].GetOrAdd(symbol, key =>
            {
                var result = ImmutableArray.CreateBuilder<AttributeData>();
                for (var current = key; current != null; current = Parent(current))
                {
                    result.AddRange(current.GetAttributes().Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, Markers[rule]) && !HasError(a)));
                    if (!inherited[rule] || (rule != 2 && result.Count != 0)) break;
                }
                return result.ToImmutable();
            });
        }

        private static ISymbol Parent(ISymbol symbol)
            => symbol is IMethodSymbol method ? method.OverriddenMethod : (symbol as INamedTypeSymbol)?.BaseType;

        private static bool HasError(AttributeData attribute) => attribute.ConstructorArguments.Any(Error);
        private static bool Error(TypedConstant constant)
            => constant.Kind == TypedConstantKind.Error || (constant.Kind == TypedConstantKind.Array && !constant.IsNull && constant.Values.Any(Error));

        internal ImmutableArray<string> Strings(AttributeData attribute)
            => strings.GetOrAdd(attribute, a => a.ConstructorArguments.SelectMany(Flatten).Select(v => v.Value as string)
                .Where(s => !string.IsNullOrEmpty(s)).Distinct(StringComparer.Ordinal).ToImmutableArray());

        internal static IEnumerable<TypedConstant> Flatten(TypedConstant constant)
        {
            if (constant.Kind == TypedConstantKind.Array)
            {
                if (!constant.IsNull)
                    foreach (var value in constant.Values)
                        foreach (var leaf in Flatten(value)) yield return leaf;
            }
            else yield return constant;
        }

        internal static IEnumerable<INamedTypeSymbol> Types(AttributeData attribute)
            => attribute.ConstructorArguments.SelectMany(Flatten).Where(v => v.Kind == TypedConstantKind.Type)
                .Select(v => v.Value).OfType<INamedTypeSymbol>();

        internal static bool Matches(ITypeSymbol type, INamedTypeSymbol target)
        {
            if (type == null || type.TypeKind == TypeKind.Error || target.TypeKind == TypeKind.Error) return false;
            bool Same(INamedTypeSymbol candidate) => candidate != null && SymbolEqualityComparer.Default.Equals(
                target.IsUnboundGenericType ? candidate.OriginalDefinition : candidate,
                target.IsUnboundGenericType ? target.OriginalDefinition : target);
            for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
                if (Same(current)) return true;
            return type.AllInterfaces.Any(Same);
        }

        internal static string Display(ISymbol symbol) => symbol?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) ?? "<未知>";
        internal string Reason(AttributeData attribute) => string.Join("；", Strings(attribute));
    }
}
