using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace YLFramework.Analyzers
{
    internal static class ProtectionRules
    {
        internal static void Write(OperationAnalysisContext context, AnalyzerSymbols symbols)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            IOperation target = null;
            switch (context.Operation)
            {
                case ISimpleAssignmentOperation assignment:
                    if (!assignment.IsRef)
                    {
                        target = assignment.Target;
                    }

                    break;
                case ICompoundAssignmentOperation compound:
                    target = compound.Target;
                    break;
                case ICoalesceAssignmentOperation coalesce:
                    target = coalesce.Target;
                    break;
                case IIncrementOrDecrementOperation increment:
                    target = increment.Target;
                    break;
                case IDeconstructionAssignmentOperation deconstruction:
                    target = deconstruction.Target;
                    break;
                case IArgumentOperation argument:
                    if ((argument.Parameter?.RefKind == RefKind.Ref || argument.Parameter?.RefKind == RefKind.Out) &&
                        !argument.Value.Syntax.DescendantNodesAndSelf().OfType<RefExpressionSyntax>().Any())
                    {
                        target = argument.Value;
                    }

                    break;
            }
            if (target != null)
            {
                Target(target, context.ContainingSymbol.ContainingType, symbols, context.CancellationToken, context.ReportDiagnostic);
            }
        }

        internal static void Reference(SyntaxNodeAnalysisContext context, AnalyzerSymbols symbols)
        {
            var expression = (RefExpressionSyntax)context.Node;
            foreach (var node in expression.Ancestors())
            {
                if (node is AssignmentExpressionSyntax assignment)
                {
                    if (context.SemanticModel.GetOperation(assignment, context.CancellationToken) is ISimpleAssignmentOperation rebind &&
                        rebind.IsRef && rebind.Target is ILocalReferenceOperation local && local.Local.RefKind == RefKind.RefReadOnly)
                    {
                        return;
                    }

                    break;
                }
                if (node is VariableDeclaratorSyntax variable)
                {
                    if ((context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) as ILocalSymbol)?.RefKind == RefKind.RefReadOnly)
                    {
                        return;
                    }

                    break;
                }
                if (node is ReturnStatementSyntax || node is ArrowExpressionClauseSyntax)
                {
                    if ((context.SemanticModel.GetEnclosingSymbol(expression.SpanStart, context.CancellationToken) as IMethodSymbol)?.ReturnsByRefReadonly == true)
                    {
                        return;
                    }

                    break;
                }
            }
            var target = context.SemanticModel.GetOperation(expression.Expression, context.CancellationToken);
            if (target != null)
            {
                Target(target, context.SemanticModel.GetEnclosingSymbol(expression.SpanStart, context.CancellationToken)?.ContainingType,
                symbols, context.CancellationToken, context.ReportDiagnostic);
            }
        }

        private static void Target(IOperation operation, INamedTypeSymbol caller, AnalyzerSymbols symbols, System.Threading.CancellationToken token, Action<Diagnostic> report)
        {
            token.ThrowIfCancellationRequested();
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            if (operation is ITupleOperation tuple)
            {
                foreach (var element in tuple.Elements)
                {
                    Target(element, caller, symbols, token, report);
                }

                return;
            }
            var location = operation.Syntax.GetLocation();
            for (var current = operation; current != null;)
            {
                token.ThrowIfCancellationRequested();
                ISymbol member;
                IOperation instance;
                if (current is IFieldReferenceOperation field)
                {
                    member = field.Field;
                    instance = field.Instance;
                }
                else if (current is IPropertyReferenceOperation property)
                {
                    member = property.Property;
                    instance = property.Instance;
                }
                else
                {
                    return;
                }

                if (member.IsStatic || instance == null)
                {
                    return;
                }

                var marker = symbols.Marked(instance.Type, 5, token);
                if (marker.attribute != null && !OwnHierarchy(caller, marker.type))
                {
                    var forbidden = AnalyzerSymbols.Types(marker.attribute).FirstOrDefault(t => AnalyzerSymbols.Matches(caller, t));
                    if (forbidden != null)
                    {
                        report(Diagnostic.Create(CoreUsageAnalyzer.Rules[5], location,
                            "类型 '" + AnalyzerSymbols.Display(caller) + "' 受禁止层级 '" + AnalyzerSymbols.Display(forbidden) +
                            "' 约束，不能写入或暴露可写引用到 '" + AnalyzerSymbols.Display(member) + "'（保护者 '" +
                            AnalyzerSymbols.Display(marker.type) + "'）。请读取其状态，或通过保护类型自身的受管方法修改。"));
                        return;
                    }
                }
                // 值类型成员修改会写回外层值；引用对象的内部修改不是外层属性写。
                if (instance.Type?.IsValueType != true)
                {
                    return;
                }

                current = instance;
                while (current is IConversionOperation converted)
                {
                    current = converted.Operand;
                }
            }
        }

        private static bool OwnHierarchy(INamedTypeSymbol caller, INamedTypeSymbol owner)
        {
            for (var current = caller; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, owner.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
