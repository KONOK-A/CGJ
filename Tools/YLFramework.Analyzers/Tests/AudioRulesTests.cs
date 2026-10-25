using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class AudioRulesTests
    {
        [TestCase("request")]
        [TestCase("(request)")]
        [TestCase("Get(request)")]
        public Task DirectRequestExpressionsAreNotUnityWaitInstructions(string expression)
            => AnalyzerFixture.Check(@"using System.Collections;using YLFramework.Audio;
class C {
 AudioPlayRequest Get(AudioPlayRequest request)=>request;
 IEnumerator Run(AudioPlayRequest request){yield return {|YL0010:" + expression + @"|};}
}", "IsDone", "HasStarted");

        [Test]
        public Task AliasesAndLocalRequestsKeepTheirRealStaticType()
            => AnalyzerFixture.Check(@"using Co=System.Collections.IEnumerator;using Request=YLFramework.Audio.AudioPlayRequest;
class C{Co Run(Request input){var local=input;yield return {|YL0010:local|};}}");

        [Test]
        public Task DataIteratorsErasureAndExplicitWaitsStayQuiet()
            => AnalyzerFixture.Check(@"using System.Collections;using System.Collections.Generic;using YLFramework.Audio;
class C {
 IEnumerable<AudioPlayRequest> Data(AudioPlayRequest r){yield return r;}
 IEnumerator<AudioPlayRequest> DataCursor(AudioPlayRequest r){yield return r;}
 IEnumerable UntypedData(AudioPlayRequest r){yield return r;}
 IEnumerator Run(AudioPlayRequest r){
  object boxed=r;yield return boxed;yield return (object)r;yield return r as object;
  yield return new UnityEngine.WaitUntil(()=>r.IsDone);
  yield return new UnityEngine.WaitUntil(()=>r.HasStarted||r.IsDone);
  while(!r.IsDone){yield return null;}yield break;
 }
}");

        [Test]
        public Task LocalFunctionUsesItsOwnIteratorContract()
            => AnalyzerFixture.Check(@"using System.Collections;using System.Collections.Generic;using YLFramework.Audio;
class C{IEnumerator Run(AudioPlayRequest r){
 IEnumerable<AudioPlayRequest> Data(){yield return r;}
 IEnumerator Coroutine(){yield return {|YL0010:r|};}
 yield return Data();yield return Coroutine();
}}");

        [Test]
        public Task LookalikeRequestWithTheSameFullNameStaysQuiet()
            => AnalyzerFixture.Check(@"namespace YLFramework.Audio{public class AudioPlayRequest{}}
class C{System.Collections.IEnumerator Run(YLFramework.Audio.AudioPlayRequest r){yield return r;}}");

        [Test]
        public async Task SourceRuntimeAlsoReportsDirectRequests()
        {
            var source = CSharpSyntaxTree.ParseText(
                "class C{System.Collections.IEnumerator Run(YLFramework.Audio.AudioPlayRequest r){yield return r;}}",
                AnalyzerFixture.Parse);
            var diagnostics = await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation().AddSyntaxTrees(source));
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "YL0010" }));
        }

        [Test]
        public async Task GeneratedAndIncompleteIteratorsDoNotCrash()
        {
            const string valid = "class C{System.Collections.IEnumerator Run(YLFramework.Audio.AudioPlayRequest r){yield return r;}}";
            Assert.That(await AnalyzerFixture.Diagnostics(AnalyzerFixture.Compilation(valid, "Audio.g.cs")), Is.Empty);
            var incomplete = AnalyzerFixture.Compilation(valid.Replace("yield return r", "yield return missing"));
            var diagnostics = await incomplete.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new CoreUsageAnalyzer())).GetAnalyzerDiagnosticsAsync();
            Assert.That(diagnostics, Is.Empty);
        }
    }
}
