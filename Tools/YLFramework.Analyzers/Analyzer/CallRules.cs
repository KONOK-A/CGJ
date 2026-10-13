using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YLFramework.Analyzers
{
    internal static class CallRules
    {
        internal static void MustCallBase(SyntaxNodeAnalysisContext context, AnalyzerSymbols symbols)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var declaration = (MethodDeclarationSyntax)context.Node;
            if (declaration.Body == null && declaration.ExpressionBody == null) return;
            var method = context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) as IMethodSymbol;
            if (method?.OverriddenMethod == null) return;
            var markers = symbols.Attributes(method.OverriddenMethod, 0);
            if (markers.IsEmpty) return;
            foreach (var call in declaration.DescendantNodes(n => !(n is AnonymousFunctionExpressionSyntax) && !(n is LocalFunctionStatementSyntax)).OfType<InvocationExpressionSyntax>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (call.Expression is MemberAccessExpressionSyntax access && access.Expression is BaseExpressionSyntax &&
                    context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol is IMethodSymbol target &&
                    SymbolEqualityComparer.Default.Equals(target.OriginalDefinition, method.OverriddenMethod.OriginalDefinition)) return;
            }
            var fix = method.OverriddenMethod.IsAbstract
                ? "被覆写层是抽象方法，不能直接 base 调用；请调整标记或覆写层级以保留可调用实现。"
                : "请在当前方法直接调用 base." + method.Name + "(...)；局部函数、匿名函数和方法组不算当前方法的调用。";
            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[0], declaration.Identifier.GetLocation(),
                "覆写方法 '" + AnalyzerSymbols.Display(method) + "' 缺少基类 '" + AnalyzerSymbols.Display(method.OverriddenMethod) + "' 的直接调用。" + fix + symbols.Reason(markers[0])));
        }

        internal static void Invocation(OperationAnalysisContext context, AnalyzerSymbols symbols)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var operation = (IInvocationOperation)context.Operation;
            var restricted = symbols.Attributes(operation.TargetMethod, 1);
            var warnings = symbols.Attributes(operation.TargetMethod, 2);
            if (restricted.IsEmpty && warnings.IsEmpty) return;
            var model = context.Compilation.GetSemanticModel(operation.Syntax.SyntaxTree);
            if (!restricted.IsEmpty)
            {
                var caller = Caller(operation.Syntax, model, context);
                var allowed = symbols.Strings(restricted[0]);
                if (caller.method == null || !Selectors(caller.method).Any(s => allowed.Contains(s, StringComparer.Ordinal)))
                    context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[1], operation.Syntax.GetLocation(),
                        "调用 '" + AnalyzerSymbols.Display(operation.TargetMethod) + "' 的位置为" + caller.description +
                        "；仅允许直接调用方法 [" + (allowed.IsEmpty ? "无有效调用者" : string.Join(", ", allowed)) +
                        "]。请将受限调用移至允许的方法本体；传入 callback 不受限，但 callback 内部的调用独立检查。"));
            }
            if (warnings.IsEmpty) return;
            var typeSyntax = operation.Syntax.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            var type = typeSyntax == null ? null : model.GetDeclaredSymbol(typeSyntax, context.CancellationToken) as INamedTypeSymbol;
            if (type == null) return;
            var reasons = warnings.Where(a => AnalyzerSymbols.Types(a).Any(t => AnalyzerSymbols.Matches(type, t)))
                .Select(a => symbols.Reason(a)).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (reasons.Length == 0) return;
            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[2], operation.Syntax.GetLocation(),
                "类型 '" + AnalyzerSymbols.Display(type) + "' 中调用 '" + AnalyzerSymbols.Display(operation.TargetMethod) +
                "' 违反指定类型的调用约束。请改用该类型的受管入口，避免绕过其生命周期。" + string.Join("；", reasons)));
        }

        private static (IMethodSymbol method, string description) Caller(SyntaxNode syntax, SemanticModel model, OperationAnalysisContext context)
        {
            foreach (var node in syntax.Ancestors())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (node is AnonymousFunctionExpressionSyntax) return (null, "匿名函数独立调用体");
                if (node is LocalFunctionStatementSyntax local) return (null, "局部函数 '" + local.Identifier.ValueText + "' 独立调用体");
                IMethodSymbol method = null;
                if (node is BaseMethodDeclarationSyntax || node is AccessorDeclarationSyntax)
                    method = model.GetDeclaredSymbol(node, context.CancellationToken) as IMethodSymbol;
                else if (node is ArrowExpressionClauseSyntax arrow && arrow.Parent is BasePropertyDeclarationSyntax property)
                    method = (model.GetDeclaredSymbol(property, context.CancellationToken) as IPropertySymbol)?.GetMethod;
                if (method != null) return (method, " '" + AnalyzerSymbols.Display(method) + "' (" + method.MethodKind + ")");
                if (node is EqualsValueClauseSyntax initializer &&
                    (initializer.Parent is PropertyDeclarationSyntax ||
                     initializer.Parent is VariableDeclaratorSyntax variable && variable.Parent?.Parent is BaseFieldDeclarationSyntax))
                    return (null, "字段或属性初始化器");
                if (node is TypeDeclarationSyntax) break;
            }
            return (null, "无稳定成员方法身份的调用体");
        }

        private static IEnumerable<string> Selectors(IMethodSymbol method)
        {
            var type = method.ContainingType;
            if (type == null) yield break;
            var names = new List<string>();
            for (var current = type; current != null; current = current.ContainingType) names.Insert(0, current.Name);
            var ns = type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".";
            var qualified = ns + string.Join(".", names);
            var name = method.MethodKind == MethodKind.Constructor || method.MethodKind == MethodKind.StaticConstructor || method.AssociatedSymbol != null
                ? method.MetadataName : method.Name;
            yield return name;
            yield return type.Name + "." + name;
            yield return qualified + "." + name;
            if (method.AssociatedSymbol is IPropertySymbol property)
            {
                yield return type.Name + "." + property.MetadataName;
                yield return qualified + "." + property.MetadataName;
            }
        }
    }
}
