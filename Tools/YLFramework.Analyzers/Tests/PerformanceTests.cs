using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    [NonParallelizable]
    public class PerformanceTests
    {
        private sealed class Sample
        {
            public double Milliseconds
            {
                get; set;
            }
            public long AllocatedBytes
            {
                get; set;
            }
            public int YlDiagnostics
            {
                get; set;
            }
        }

        private static string Corpus(int count, int ordinaryCalls)
        {
            var text = new StringBuilder(AnalyzerFixture.Prefix);
            text.Append("class Box<T>{T value;}\n");
            for (int i = 0; i < count; i++)
            {
                text.Append("class C").Append(i).Append(":YLFramework.Core.View{\nBox<Box<YLFramework.Core.LoadViewArgs>> held;\n");
                text.Append("[RestrictedCall(\"Awake\")]static void F(){}\nvoid Awake(){F();RegisterEvent(()=>{});}\n");
                text.Append("protected override void Start(){base.Start();}\nvoid OnEnable(){}\nvoid Wrong(){F();\n");
                for (int j = 0; j < ordinaryCalls; j++)
                    text.Append("Math.Abs(-1);\n");
                text.Append("}}\n");
            }
            return text.ToString();
        }

        private static async Task<Sample> Measure(SyntaxTree tree, bool enabled, int expected)
        {
            var allocation = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            var compilation = CSharpCompilation.Create("Measured", new[] { tree },
                AnalyzerFixture.Platform.Concat(new[] { AnalyzerFixture.Core }),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var diagnostics = enabled
                ? await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer())).GetAllDiagnosticsAsync()
                : compilation.GetDiagnostics();
            watch.Stop();
            var bytes = GC.GetTotalAllocatedBytes(true) - allocation;
            Assert.That(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error || d.Id == "AD0001" || d.Id == "CS8032"), Is.False,
                string.Join("\n", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.Id == "AD0001")));
            var count = diagnostics.Count(d => d.Id.StartsWith("YL", StringComparison.Ordinal));
            Assert.That(count, Is.EqualTo(enabled ? expected : 0));
            return new Sample { Milliseconds = watch.Elapsed.TotalMilliseconds, AllocatedBytes = bytes, YlDiagnostics = count };
        }

        [Test]
        public async Task FixedCorpusColdAndWarmAnalysisRecordsEvidenceWithoutAbsoluteTimingThreshold()
        {
            var results = new List<object>();
            // 元数据/解析准备不计入比较；每轮创建同树同引用的新 compilation，避免复用已缓存的 driver 结果。
            _ = AnalyzerFixture.Core;
            foreach (var configuration in new[] { (name: "normal", count: 24, calls: 3), (name: "pressure", count: 350, calls: 20) })
            {
                var source = Corpus(configuration.count, configuration.calls);
                var tree = CSharpSyntaxTree.ParseText(source, AnalyzerFixture.Parse, "Measured.cs");
                var coldOff = await Measure(tree, false, 0);
                var coldOn = await Measure(tree, true, configuration.count * 3);
                var off = new List<Sample>();
                var on = new List<Sample>();
                for (int iteration = 0; iteration < 3; iteration++)
                {
                    off.Add(await Measure(tree, false, 0));
                    on.Add(await Measure(tree, true, configuration.count * 3));
                }
                results.Add(new
                {
                    configuration.name,
                    configuration.count,
                    configuration.calls,
                    Characters = source.Length,
                    Lines = source.Count(c => c == '\n') + 1,
                    Nodes = tree.GetRoot().DescendantNodesAndSelf().Count(),
                    ColdWithoutAnalyzer = coldOff,
                    ColdWithAnalyzer = coldOn,
                    WarmWithoutAnalyzer = off,
                    WarmWithAnalyzer = on,
                    WarmMedianOffMs = off.Select(s => s.Milliseconds).OrderBy(x => x).ElementAt(1),
                    WarmMedianOnMs = on.Select(s => s.Milliseconds).OrderBy(x => x).ElementAt(1),
                    WarmMedianOffBytes = off.Select(s => s.AllocatedBytes).OrderBy(x => x).ElementAt(1),
                    WarmMedianOnBytes = on.Select(s => s.AllocatedBytes).OrderBy(x => x).ElementAt(1)
                });
            }
            var directory = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestResults");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "performance.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
