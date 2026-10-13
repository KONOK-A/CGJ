using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class AnalysisBoundaryTests
    {
        [Test]
        public async Task ParallelCompilationsKeepStorageShapesAndConstructedSymbolsIsolated()
        {
            var analyzer = new CoreUsageAnalyzer();
            var inputs = new[] {
                "class Box<T>{T value;}class C{Box<YLFramework.Core.LoadViewArgs> held;Box<int> plain;}",
                "class Box<T>{static T value;}class C{Box<YLFramework.Core.LoadViewArgs> phantom;}"
            };
            var results = await Task.WhenAll(inputs.Select(s => AnalyzerFixture.Compilation(AnalyzerFixture.Prefix + s)
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(analyzer)).GetAnalyzerDiagnosticsAsync()));
            Assert.That(results[0].Select(d=>d.Id), Is.EqualTo(new[]{"YL0005"}));
            Assert.That(results[1], Is.Empty);
        }

        [Test]
        public async Task CanceledDriverCanAnalyzeAgainWithoutLeakingPartialState()
        {
            var compilation = AnalyzerFixture.Compilation(AnalyzerFixture.Prefix + "class Box<T>{T value;}class C{Box<YLFramework.Core.LoadViewArgs> held;}");
            var driver = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer()));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.CatchAsync<System.OperationCanceledException>(async () => await driver.GetAnalyzerDiagnosticsAsync(canceled.Token));
            var results = await driver.GetAnalyzerDiagnosticsAsync();
            Assert.That(results.Select(d=>d.Id), Is.EqualTo(new[]{"YL0005"}));
        }

        [Test]
        public async Task IncompleteGenericAndUnboundWriteHaveNoAnalyzerException()
        {
            var compilation = AnalyzerFixture.Compilation(AnalyzerFixture.Prefix + "class Box<T>{Missing<T> value;}class C:YLFramework.Core.View{Box<YLFramework.Core.LoadViewArgs> held;void M(){missing.X=1;}}");
            Assert.That(compilation.GetDiagnostics().Any(d=>d.Severity == DiagnosticSeverity.Error), Is.True);
            var results = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer())).GetAnalyzerDiagnosticsAsync();
            Assert.That(results, Is.Empty, string.Join("\n", results));
        }
    }
}
