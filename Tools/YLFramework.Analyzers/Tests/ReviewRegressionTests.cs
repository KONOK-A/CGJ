using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class ReviewRegressionTests
    {
        [Test]
        public Task LocalInitializersRetainDirectCallerIncludingForAndUsing()
            => AnalyzerFixture.Check("class C{[RestrictedCall(\"Awake\")]static int F()=>1;[RestrictedCall(\"Awake\")]static System.IO.MemoryStream Make()=>null;void Awake(){int value=F();for(int i=F();i<0;i++){}using var stream=Make();using(var other=Make()){} }}");

        [Test]
        public Task ClassConstraintStorageUsesRealCarrierValuesNotInterfaceSignatures()
            => AnalyzerFixture.Check("class Box<T>{public T Value;}class C<T,U,V>where T:Box<YLFramework.Core.LoadViewArgs>where U:T where V:IEnumerable<YLFramework.Core.LoadViewArgs>{T {|YL0005:first|};U {|YL0005:second|};V sequence;}");

        [Test]
        public Task ReadonlyRefRebindingDoesNotExposeMutableState()
            => AnalyzerFixture.Check("[ProtectedHierarchy(typeof(C))]class A{public int x;}class C{void M(A a){ref readonly int read=ref a.x;read=ref a.x;ref int write=ref {|YL0006:a.x|};write=ref {|YL0006:a.x|};}}");

        [TestCase("namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]public class LoadViewArgs{private LoadViewArgs Pool;private LoadViewArgs Pool;}}")]
        [TestCase("namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]public class LoadViewArgs{}public class LoadViewRequest<T>{private LoadViewArgs args;private LoadViewArgs args;}}")]
        public async Task DuplicateCoreOwnerDeclarationsAreCompilerErrorsNotAnalyzerFailures(string source)
        {
            var compilation = AnalyzerFixture.CoreCompilation(source);
            Assert.That(compilation.GetDiagnostics().Any(d=>d.Id=="CS0102"), Is.True);
            var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer())).GetAnalyzerDiagnosticsAsync();
            Assert.That(diagnostics.Where(d=>d.Id=="AD0001"), Is.Empty, string.Join("\n",diagnostics));
        }

        [Test]
        public Task ErasedPartialMessageIsNotAUnityMessageAndImplementedPartialReportsOnce()
            => AnalyzerFixture.Check("partial class C:YLFramework.Core.View{partial void OnEnable();partial void {|YL0007:OnDisable|}();}partial class C{partial void OnDisable(){}} ");

        [Test]
        public Task ExplicitRecordPositionalPropertyIsDiagnosedOnlyOnce()
            => AnalyzerFixture.Check("record R(YLFramework.Core.LoadViewArgs Value){public YLFramework.Core.LoadViewArgs {|YL0005:Value|}{get;init;}=Value;}");
    }
}
