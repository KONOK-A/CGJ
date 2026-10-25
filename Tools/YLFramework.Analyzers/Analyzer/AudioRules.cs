using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YLFramework.Analyzers
{
    internal static class AudioRules
    {
        internal static void Yield(SyntaxNodeAnalysisContext context, AnalyzerSymbols symbols)
        {
            var statement = (YieldStatementSyntax)context.Node;
            var expression = statement.Expression;
            if (expression == null ||
                !(context.SemanticModel.GetEnclosingSymbol(statement.SpanStart, context.CancellationToken) is IMethodSymbol method) ||
                method.ReturnType.SpecialType != SpecialType.System_Collections_IEnumerator ||
                !SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type, symbols.AudioRequest))
            {
                return;
            }

            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[9], expression.GetLocation(),
                "AudioPlayRequest 是普通对象，标准 Unity 协程不会等待其开始或结束。等开始使用 WaitUntil(() => request.HasStarted || request.IsDone)，等结束使用 WaitUntil(() => request.IsDone)；循环播放可能不结束，自定义调度器可局部抑制 YL0010。"));
        }
    }
}
