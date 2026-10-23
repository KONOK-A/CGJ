using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace YLFramework.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CoreUsageAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly ImmutableArray<DiagnosticDescriptor> Rules = ImmutableArray.Create(
            Rule("YL0001", "覆写方法缺少基类调用"), Rule("YL0002", "受限方法调用位置不合法"),
            Rule("YL0003", "指定类型中的方法调用警告"), Rule("YL0004", "类型应通过正式工厂创建"),
            Rule("YL0005", "成员持有仅限方法借用的类型"), Rule("YL0006", "禁止修改受保护实例"),
            Rule("YL0007", "View 框架消息被遮蔽", "YLFramework.Lifecycle"));

        private static DiagnosticDescriptor Rule(string id, string title, string category = "YLFramework.Usage")
            => new DiagnosticDescriptor(id, title, "{0}", category, DiagnosticSeverity.Warning, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(start =>
            {
                var symbols = AnalyzerSymbols.Create(start.Compilation);
                if (symbols == null)
                {
                    return;
                }

                if (symbols.View != null)
                {
                    start.RegisterSymbolAction(c => ViewLifecycleRules.Message(c, symbols), SymbolKind.Method);
                }

                if (symbols.Markers[0] != null)
                {
                    start.RegisterSyntaxNodeAction(c => CallRules.MustCallBase(c, symbols), SyntaxKind.MethodDeclaration);
                }

                if (symbols.Markers[1] != null || symbols.Markers[2] != null)
                {
                    start.RegisterOperationAction(c => CallRules.Invocation(c, symbols), OperationKind.Invocation);
                }

                if (symbols.Markers[3] != null)
                {
                    start.RegisterOperationAction(c => OwnershipRules.Creation(c, symbols), OperationKind.ObjectCreation, OperationKind.TypeParameterObjectCreation);
                }

                if (symbols.Markers[4] != null)
                {
                    var storage = new StorageModel(symbols);
                    start.RegisterSymbolAction(c => OwnershipRules.Member(c, symbols, storage), SymbolKind.Field, SymbolKind.Property);
                    start.RegisterSyntaxNodeAction(c => OwnershipRules.RecordParameter(c, symbols, storage), SyntaxKind.Parameter);
                }
                if (symbols.Markers[5] != null)
                {
                    start.RegisterOperationAction(c => ProtectionRules.Write(c, symbols), OperationKind.SimpleAssignment,
                        OperationKind.CompoundAssignment, OperationKind.CoalesceAssignment, OperationKind.Increment,
                        OperationKind.Decrement, OperationKind.DeconstructionAssignment, OperationKind.Argument);
                    start.RegisterSyntaxNodeAction(c => ProtectionRules.Reference(c, symbols), SyntaxKind.RefExpression);
                }
            });
        }
    }
}
