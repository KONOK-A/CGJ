using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YLFramework.Analyzers
{
    internal static class OwnershipRules
    {
        internal static void Creation(OperationAnalysisContext context, AnalyzerSymbols symbols)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var type = context.Operation.Type;
            var marker = symbols.Marked(type, 3, context.CancellationToken);
            if (marker.attribute == null)
            {
                return;
            }

            if (SymbolEqualityComparer.Default.Equals((type as INamedTypeSymbol)?.OriginalDefinition, marker.type.OriginalDefinition))
            {
                for (var current = context.ContainingSymbol.ContainingType; current != null; current = current.ContainingType)
                {
                    if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, marker.type.OriginalDefinition))
                    {
                        return;
                    }
                }
            }

            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[3], context.Operation.Syntax.GetLocation(),
                "类型 '" + AnalyzerSymbols.Display(type) + "' 的构造受 '" + AnalyzerSymbols.Display(marker.type) +
                "' 约束；请使用正式静态工厂，不要在外部直接 new，也不要以派生类型绕过生命周期。" + symbols.Reason(marker.attribute)));
        }

        internal static void Member(SymbolAnalysisContext context, AnalyzerSymbols symbols, StorageModel storage)
        {
            if (context.Symbol is IFieldSymbol field && field.IsImplicitlyDeclared)
            {
                return;
            }

            if (context.Symbol is IPropertySymbol property && property.DeclaringSyntaxReferences.Any(r => r.GetSyntax(context.CancellationToken) is ParameterSyntax))
            {
                return;
            }

            var diagnostic = Check(context.Symbol, symbols, storage, context.CancellationToken);
            if (diagnostic != null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        internal static void RecordParameter(SyntaxNodeAnalysisContext context, AnalyzerSymbols symbols, StorageModel storage)
        {
            var parameter = (ParameterSyntax)context.Node;
            if (!(parameter.Parent?.Parent is RecordDeclarationSyntax record))
            {
                return;
            }

            var type = context.SemanticModel.GetDeclaredSymbol(record, context.CancellationToken) as INamedTypeSymbol;
            var property = type?.GetMembers(parameter.Identifier.ValueText).OfType<IPropertySymbol>().FirstOrDefault();
            if (property == null || !property.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == parameter.SyntaxTree && r.Span == parameter.Span))
            {
                return;
            }

            var diagnostic = Check(property, symbols, storage, context.CancellationToken);
            if (diagnostic != null)
            {
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static Diagnostic Check(ISymbol member, AnalyzerSymbols symbols, StorageModel storage, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (member == null || symbols.IsOwner(member))
            {
                return null;
            }

            var type = member is IFieldSymbol field ? field.Type : (member as IPropertySymbol)?.Type;
            var witness = storage.Find(type, token);
            if (witness == null)
            {
                return null;
            }

            var location = member.Locations.FirstOrDefault(l => l.IsInSource);
            if (location == null)
            {
                return null;
            }

            return Diagnostic.Create(CoreUsageAnalyzer.Rules[4], location,
                "成员 '" + AnalyzerSymbols.Display(member) + "' 持有仅限方法借用的类型。存储路径：" +
                AnalyzerSymbols.Display(member) + " → " + witness + "。请仅在方法借用窗口使用，不保存到字段或属性，回收后不再使用旧引用。");
        }
    }
}
