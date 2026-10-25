using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YLFramework.Analyzers
{
    internal static class InjectionRules
    {
        internal static void Member(SymbolAnalysisContext context, AnalyzerSymbols symbols)
        {
            if (context.Symbol.IsImplicitlyDeclared)
            {
                return;
            }

            Check(context.Symbol, context, symbols);
        }

        internal static void BackingFields(SymbolAnalysisContext context, AnalyzerSymbols symbols)
        {
            // 显式 field-target 标记属于实际字段，不依赖属性或 record 参数的语法形态。
            foreach (var field in ((INamedTypeSymbol)context.Symbol).GetMembers().OfType<IFieldSymbol>())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (field.IsImplicitlyDeclared && !(field.AssociatedSymbol is IEventSymbol))
                {
                    Check(field, context, symbols);
                }
            }
        }

        internal static void EventFields(SyntaxNodeAnalysisContext context, AnalyzerSymbols symbols)
        {
            var declaration = (EventFieldDeclarationSyntax)context.Node;
            if (!declaration.Modifiers.Any(SyntaxKind.StaticKeyword) || declaration.ContainsDiagnostics)
            {
                return;
            }
            // 事件 backing field 不由 Roslyn 的公开 GetMembers 暴露；绑定显式 field-target 的真实构造符号。
            var attribute = declaration.AttributeLists.Where(list => list.Target?.Identifier.ValueText == "field")
                .SelectMany(list => list.Attributes).FirstOrDefault(candidate =>
                    context.SemanticModel.GetSymbolInfo(candidate, context.CancellationToken).Symbol is IMethodSymbol constructor &&
                    AnalyzerSymbols.Matches(constructor.ContainingType, symbols.Inject));
            if (attribute == null)
            {
                return;
            }
            foreach (var variable in declaration.Declaration.Variables)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is IEventSymbol member &&
                    member.Type.TypeKind != TypeKind.Error)
                {
                    context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[7], variable.Identifier.GetLocation(),
                        $"事件 {AnalyzerSymbols.Display(member)} 的 field-target Inject 不支持 static backing field；请显式 Resolve 或选择实例依赖，不要仅为消警改变事件生命周期。"));
                }
            }
        }

        private static void Check(ISymbol member, SymbolAnalysisContext context, AnalyzerSymbols symbols)
        {
            var attribute = member.GetAttributes().FirstOrDefault(candidate =>
                candidate.ApplicationSyntaxReference != null && candidate.AttributeConstructor != null &&
                !AnalyzerSymbols.HasError(candidate) && AnalyzerSymbols.Matches(candidate.AttributeClass, symbols.Inject));
            if (attribute == null)
            {
                return;
            }

            string reason = null;
            if (member is IFieldSymbol field && field.Type.TypeKind != TypeKind.Error)
            {
                reason = field.IsConst ? "const 字段" : field.IsStatic ? "static 字段" :
                    field.IsReadOnly ? "readonly 字段" : null;
            }
            else if (member is IPropertySymbol property && property.Type.TypeKind != TypeKind.Error)
            {
                reason = property.IsStatic ? "static 属性" : property.IsIndexer ? "indexer 属性" :
                    property.SetMethod == null ? "无 setter 属性" : null;
            }

            if (reason == null)
            {
                return;
            }

            var location = member.Locations.FirstOrDefault(candidate => candidate.IsInSource) ??
                attribute.ApplicationSyntaxReference.GetSyntax(context.CancellationToken).GetLocation();
            context.ReportDiagnostic(Diagnostic.Create(CoreUsageAnalyzer.Rules[7], location,
                $"成员 {AnalyzerSymbols.Display(member)} 的 Inject 不支持 {reason}；请显式 Resolve、使用构造注入或可写实例成员，不要仅为消警改变所有权设计。"));
        }
    }
}
