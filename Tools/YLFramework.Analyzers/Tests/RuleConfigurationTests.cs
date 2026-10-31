using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class RuleConfigurationTests
    {
        [Test]
        public async Task ActualEditorConfigParsesOneScopedSeverityWithoutChangingOtherIds()
        {
            var directory = Path.Combine(AnalyzerFixture.Root, "Tools/YLFramework.Analyzers/Tests/Configuration");
            var path = Path.Combine(directory, ".editorconfig");
            var config = AnalyzerConfig.Parse(SourceText.From(File.ReadAllText(path)), path);
            var set = AnalyzerConfigSet.Create(ImmutableArray.Create(config));
            var file = Path.Combine(directory, "EditorConfigFixture.cs");
            var options = set.GetOptionsForSourcePath(file);
            Assert.That(options.TreeOptions["YL0002"], Is.EqualTo(ReportDiagnostic.Suppress));
            Assert.That(set.GetOptionsForSourcePath(Path.Combine(directory, "Other.cs")).TreeOptions.ContainsKey("YL0002"), Is.False);
            var source = AnalyzerFixture.Prefix + "class C:YLFramework.Core.View{protected override void Start(){}void Wrong(){RegisterEvent(()=>{});}}";
            var compilation = AnalyzerFixture.Compilation(source, file);
            var defaults = await AnalyzerFixture.Diagnostics(compilation);
            Assert.That(defaults.Select(d => d.Id).OrderBy(id => id), Is.EqualTo(new[] { "YL0001", "YL0002" }));
            var configured = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(options.TreeOptions));
            var diagnostics = await AnalyzerFixture.Diagnostics(configured);
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "YL0001" }));
            Assert.That(diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
        }
    }
}
