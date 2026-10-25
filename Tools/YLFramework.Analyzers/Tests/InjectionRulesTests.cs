using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class InjectionRulesTests
    {
        [TestCase("[Inject] static object {|YL0008:Bad|};", "static")]
        [TestCase("[Inject] static object {|YL0008:Bad|}{get;set;}", "static")]
        [TestCase("[Inject] const string {|YL0008:Bad|}=null;", "const")]
        [TestCase("[Inject] readonly object {|YL0008:Bad|};", "readonly")]
        [TestCase("[Inject] object {|YL0008:Bad|}{get;}", "setter")]
        [TestCase("[Inject] object {|YL0008:this|}[int i]{get=>null;set{}}", "indexer")]
        public Task UnsupportedDeclarationsExplainTheReason(string member, string reason)
            => AnalyzerFixture.Check("using YLFramework.DependencyInjection;class C{" + member + "}", reason);

        [Test]
        public Task MutableNonPublicAndPrivateSetterMembersAreAllowed()
            => AnalyzerFixture.Check(@"using YLFramework.DependencyInjection;
class C {
 [Inject] public object Field;
 [Inject(""main"")] public object Property{get;private set;}
 [Inject] private object Hidden;
 [Inject] private object WriteOnly{set{}}
 [field:Inject] public object Backing{get;set;}
}");

        [Test]
        public Task AliasAndDerivedAttributeStillReferToRealInject()
            => AnalyzerFixture.Check(@"using InjectAlias=YLFramework.DependencyInjection.InjectAttribute;
class SpecialInjectAttribute:InjectAlias{}
class C {
 [InjectAlias] readonly object {|YL0008:First|};
 [SpecialInject] readonly object {|YL0008:Second|};
}");

        [Test]
        public Task ExplicitBackingFieldAttributeChecksItsActualReadonlyField()
            => AnalyzerFixture.Check(@"class C {
 [field:YLFramework.DependencyInjection.Inject] public object {|YL0008:Value|}{get;}
}", "readonly");

        [Test]
        public Task ExplicitStaticEventFieldIsUnsupportedInjection()
            => AnalyzerFixture.Check(@"class C {
 [field:YLFramework.DependencyInjection.Inject] static event System.Action {|YL0008:Changed|};
}", "static");

        [Test]
        public Task ExplicitRecordParameterFieldChecksItsReadonlyBackingField()
            => AnalyzerFixture.Check(@"record Consumer([field:YLFramework.DependencyInjection.Inject] object {|YL0008:Service|});", "readonly");

        [Test]
        public Task InstanceEventFieldsAndRecordPropertiesAreNotReadonlyFieldContracts()
            => AnalyzerFixture.Check(@"class C {
 [field:YLFramework.DependencyInjection.Inject] event System.Action Changed;
}
record Consumer([property:YLFramework.DependencyInjection.Inject] object Service);");

        [Test]
        public Task ExplicitGeneratedMembersKeepTheDefaultGeneratedCodeExclusion()
            => AnalyzerFixture.Check(@"class C {
 [System.CodeDom.Compiler.GeneratedCode(""generator"",""1"")]
 [field:YLFramework.DependencyInjection.Inject] public object Value{get;}
 [System.CodeDom.Compiler.GeneratedCode(""generator"",""1"")]
 [field:YLFramework.DependencyInjection.Inject] static event System.Action Changed;
}");

        [Test]
        public Task EventFieldListsReportOncePerMemberAndIgnoreUnrelatedAttributes()
            => AnalyzerFixture.Check(@"class C {
 [field:YLFramework.DependencyInjection.Inject] static event System.Action {|YL0008:First|}, {|YL0008:Second|};
}
class InjectAttribute:System.Attribute{}
class Other{[field:Inject] static event System.Action Ignored;}");

        [Test]
        public Task MultipleVariablesAndReasonsProduceOneDiagnosticPerMember()
            => AnalyzerFixture.Check(@"using YLFramework.DependencyInjection;class C {
 [Inject] static readonly object {|YL0008:First|}, {|YL0008:Second|};
}");

        [Test]
        public Task LookalikeAttributeIsNotAnInjectionContract()
            => AnalyzerFixture.Check(@"class InjectAttribute:System.Attribute{}
class C{[Inject] static readonly object Value;}");

        [Test]
        public async Task SourceRuntimeAndMissingOptionalInjectRemainIndependent()
        {
            var source = CSharpSyntaxTree.ParseText(
                "class C{[YLFramework.DependencyInjection.Inject] public static object Bad;}", AnalyzerFixture.Parse);
            var diagnostics = await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation().AddSyntaxTrees(source));
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "YL0008" }));

            var withoutInject = AnalyzerFixture.CoreCompilation(includeInject: false).AddSyntaxTrees(
                CSharpSyntaxTree.ParseText("class C:YLFramework.Core.View{protected override void Start(){}}", AnalyzerFixture.Parse));
            diagnostics = await AnalyzerFixture.Diagnostics(withoutInject);
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "YL0001" }));
        }

        [Test]
        public async Task GeneratedAndIncompleteDeclarationsDoNotCrash()
        {
            var generated = AnalyzerFixture.Compilation(
                "class C{[YLFramework.DependencyInjection.Inject] static object Bad;}", "Consumer.g.cs");
            Assert.That(await AnalyzerFixture.Diagnostics(generated), Is.Empty);
            var incomplete = AnalyzerFixture.Compilation(
                "class C{[YLFramework.DependencyInjection.Inject(Missing)] object Bad{get;}}");
            var diagnostics = await incomplete.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer())).GetAnalyzerDiagnosticsAsync();
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public async Task ConcurrentCompilationsDoNotShareMemberState()
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AnalyzerFixture.Check(
                "class C{[YLFramework.DependencyInjection.Inject] readonly object {|YL0008:Bad|};}")));
        }
    }
}
