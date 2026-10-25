using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YLFramework.Analyzers
{
    internal static class PanelLoadingRules
    {
        internal static void Invocation(OperationAnalysisContext context, AnalyzerSymbols symbols)
        {
            var invocation = (IInvocationOperation)context.Operation;
            var method = invocation.TargetMethod;
            if (method.Arity != 1 || (method.Name != "LoadView" && method.Name != "LoadViewAsync") ||
                !SymbolEqualityComparer.Default.Equals(method.ContainingType, symbols.Module))
            {
                return;
            }

            foreach (var type in AnalyzerSymbols.ClassTypes(method.TypeArguments[0]))
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!AnalyzerSymbols.Matches(type, symbols.Panel))
                {
                    continue;
                }

                var replacement = method.Name == "LoadViewAsync" ? "LoadPanelAsync" : "LoadPanel";
                context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[8], invocation.Syntax.GetLocation(),
                    $"{AnalyzerSymbols.Display(method.TypeArguments[0])} 确定属于 Panel，不能通过 {method.Name} 加载；请使用 Module.{replacement}。Transform parent 与 UIRootContext root 不等价，请显式选择根。"));
                return;
            }
        }
    }
}
