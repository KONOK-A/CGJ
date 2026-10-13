using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YLFramework.Analyzers
{
    internal static class ViewLifecycleRules
    {
        internal static void Message(SymbolAnalysisContext context, AnalyzerSymbols symbols)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var method = (IMethodSymbol)context.Symbol;
            if (method.IsImplicitlyDeclared || method.MethodKind != MethodKind.Ordinary || method.IsStatic ||
                method.Arity != 0 || method.Parameters.Length != 0 || !method.ReturnsVoid) return;
            if (method.PartialDefinitionPart != null) return;
            if (method.PartialImplementationPart == null && method.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax(context.CancellationToken) is MethodDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword))) return;
            var hook = method.Name == "OnEnable" ? "OnViewEnabled" : method.Name == "OnDisable" ? "OnViewDisabled" : method.Name == "OnDestroy" ? "OnClosed" : null;
            if (hook == null || SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, symbols.View)) return;
            var derived = false;
            for (var type = method.ContainingType.BaseType; type != null; type = type.BaseType)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, symbols.View)) { derived = true; break; }
            }
            var location = method.Locations.FirstOrDefault(l => l.IsInSource);
            if (!derived || location == null) return;
            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[6], location,
                "声明 '" + AnalyzerSymbols.Display(method) + "' 竞争 View 框架的私有 Unity 消息 '" + method.Name +
                "'，可能绕过正式事件轮次或及时关闭。请覆写 " + hook + "()，不要声明同名 Unity 消息；框架兜底清理不替代原生生命周期。"));
        }
    }
}
