using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class OwnershipRulesTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            yield return Case("New_ExplicitAndTargetTyped", "class C{void M(){var a={|YL0004:new YLFramework.Core.LoadViewArgs()|};YLFramework.Core.LoadViewArgs b={|YL0004:new()|};}}", "LoadViewArgs", "Use New");
            yield return Case("New_OwnFactoryAndNestedLambda", "[DisableNew]class A{public static A New()=>new A();class H{public static Func<A> F=()=>new A();}}class B:A{public B():base(){}}class C{object M()=>{|YL0004:new B()|};}");
            yield return Case("New_DerivedDoesNotOwnBaseContract", "[DisableNew]class A{}class B:A{B(){}static B F()=>{|YL0004:new B()|};}");
            yield return Case("New_ClassConstraintClosure", "[DisableNew]class A{}class C{T F<T,U>()where T:U,new()where U:A=>{|YL0004:new T()|};T G<T>()where T:new()=>new T();}", "T");
            yield return Case("New_ForeignMarker", "class DisableNewAttribute:Attribute{}[DisableNew]class A{}class C{A F()=>new A();}");
            yield return Case("Storage_DirectStaticArrayProperty", "class C{static YLFramework.Core.LoadViewArgs {|YL0005:a|};YLFramework.Core.LoadViewArgs[] {|YL0005:array|};public YLFramework.Core.LoadViewArgs {|YL0005:P|}{get;set;}public YLFramework.Core.LoadViewArgs {|YL0005:Computed|}=>null;}", "LoadViewArgs", "Lease reason");
            yield return Case("Storage_SignaturesDelegateLocalNotStorage", "class C{event Action<YLFramework.Core.LoadViewArgs> E;Func<YLFramework.Core.LoadViewArgs> factory;void M(YLFramework.Core.LoadViewArgs a){var type=typeof(YLFramework.Core.LoadViewArgs);Action<YLFramework.Core.LoadViewArgs> local=x=>{};}YLFramework.Core.LoadViewArgs F()=>null;}");
            yield return Case("Storage_BoxBoxAndBoundCache", "class Box<T>{private T value;}class C{Box<Box<YLFramework.Core.LoadViewArgs>> {|YL0005:yes|};Box<Box<int>> no;}", "Box", "LoadViewArgs");
            yield return Case("Storage_OnlySecondParameter", "class Carrier<A,B>{private B value;}class C{Carrier<YLFramework.Core.LoadViewArgs,int> no;Carrier<int,YLFramework.Core.LoadViewArgs> {|YL0005:yes|};}");
            yield return Case("Storage_PhantomStaticOnlyComputed", "class Phantom<T>{public T F()=>default;}class StaticOnly<T>{static T cache;}class C{Phantom<YLFramework.Core.LoadViewArgs> no;StaticOnly<YLFramework.Core.LoadViewArgs> alsoNo;System.Threading.Tasks.Task<Phantom<YLFramework.Core.LoadViewArgs>> task;}");
            yield return Case("Storage_OuterGenericParameter", "class Outer<T>{public class Inner<U>{private T outer;private U inner;}}class C{Outer<YLFramework.Core.LoadViewArgs>.Inner<int> {|YL0005:first|};Outer<int>.Inner<YLFramework.Core.LoadViewArgs> {|YL0005:second|};Outer<int>.Inner<string> no;}");
            yield return Case("Storage_BaseSubstitutionAndAutoBacking", "class Box<T>{public T Value{get;set;}}class Derived<T>:Box<T>{}class C{Derived<YLFramework.Core.LoadViewArgs> {|YL0005:yes|};}");
            yield return Case("Storage_CycleNoLeaf", "class A<T>{private B<T> next;}class B<T>{private A<T> next;}class C{A<YLFramework.Core.LoadViewArgs> no;}");
            yield return Case("Storage_CycleWithLeafBothRoots", "class A<T>{private B<T> next;}class B<T>{private A<T> next;private T value;}class C{A<YLFramework.Core.LoadViewArgs> {|YL0005:a|};B<YLFramework.Core.LoadViewArgs> {|YL0005:b|};}");
            yield return Case("Storage_ExpandingCycleTerminates", "class Empty<T>{Empty<List<T>> next;}class Full<T>{Full<List<T>> next;T value;}class C{Empty<YLFramework.Core.LoadViewArgs> no;Full<YLFramework.Core.LoadViewArgs> {|YL0005:yes|};}");
            yield return Case("Storage_DictionaryKeysAndValues", "class C{Dictionary<YLFramework.Core.LoadViewArgs,int> {|YL0005:key|};Dictionary<int,YLFramework.Core.LoadViewArgs> {|YL0005:value|};}");
            yield return Case("Storage_TupleRestAndResults", "class C{(int,int,int,int,int,int,int,int,YLFramework.Core.LoadViewArgs) {|YL0005:tuple|};System.Threading.Tasks.Task<YLFramework.Core.LoadViewArgs> {|YL0005:task|};System.Threading.Tasks.ValueTask<YLFramework.Core.LoadViewArgs> {|YL0005:valueTask|};KeyValuePair<int,YLFramework.Core.LoadViewArgs> {|YL0005:pair|};}");
            yield return Case("Storage_NullableValueCarrier", "struct Box<T>{public T Value;}struct Phantom<T>{public T Get()=>default;}class C{Box<YLFramework.Core.LoadViewArgs>? {|YL0005:held|};Phantom<YLFramework.Core.LoadViewArgs>? no;int? plain;}", "LoadViewArgs");
            yield return Case("Storage_NullableMarkedStruct", "[MethodLocalOnly]struct Lease{}class C{Lease? {|YL0005:held|};}", "Lease");
            foreach (var container in new[] { "System.ArraySegment", "System.Memory", "System.ReadOnlyMemory",
                "System.Collections.Concurrent.ConcurrentQueue", "System.Collections.Concurrent.ConcurrentStack", "System.Collections.Concurrent.ConcurrentBag" })
                yield return Case("Storage_ValueCarrier_" + container.Substring(container.LastIndexOf('.') + 1),
                    "class Phantom<T>{public T Get()=>default;}class C{" + container + "<YLFramework.Core.LoadViewArgs> {|YL0005:held|};" +
                    container + "<int> plain;" + container + "<Phantom<YLFramework.Core.LoadViewArgs>> phantom;" +
                    container + "<Func<YLFramework.Core.LoadViewArgs>> callbacks;}", "LoadViewArgs");
            yield return Case("Storage_ConcurrentDictionaryKeysAndValues", "class C{System.Collections.Concurrent.ConcurrentDictionary<YLFramework.Core.LoadViewArgs,int> {|YL0005:key|};System.Collections.Concurrent.ConcurrentDictionary<int,YLFramework.Core.LoadViewArgs> {|YL0005:value|};}");
            yield return Case("Storage_PseudoConcurrentCarrierNotSeeded", "namespace System.Collections.Concurrent{class ConcurrentQueue<T>{public static T cache;}}class C{System.Collections.Concurrent.ConcurrentQueue<YLFramework.Core.LoadViewArgs> no;}");
            yield return Case("Storage_HoldingVsEnumerableProtocol", "class C{IReadOnlyDictionary<YLFramework.Core.LoadViewArgs,int> {|YL0005:held|};IEnumerable<YLFramework.Core.LoadViewArgs> sequence;IEnumerator<YLFramework.Core.LoadViewArgs> iterator;IComparer<YLFramework.Core.LoadViewArgs> comparer;}");
            yield return Case("Storage_PseudoBclNameNotSeeded", "namespace System.Collections.Generic{class List<T>{public static T cache;}}class C{System.Collections.Generic.List<YLFramework.Core.LoadViewArgs> no;}");
            yield return Case("Storage_ClassNotInterfaceConstraint", "class C<T,U,V>where T:U where U:YLFramework.Core.LoadViewArgs where V:IEnumerable<YLFramework.Core.LoadViewArgs>{T {|YL0005:held|};V no;}");
            yield return Case("Storage_RecordAutoAndPositional", "record R{public YLFramework.Core.LoadViewArgs {|YL0005:Value|}{get;init;}}record S(YLFramework.Core.LoadViewArgs {|YL0005:Value|});");
            yield return Case("Storage_FakeCoreOwnerNoExemption", "namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]class LoadViewArgs{private static readonly Pool.ObjectPool<LoadViewArgs> {|YL0005:Pool|}=new Pool.ObjectPool<LoadViewArgs>(()=>null);}class LoadViewRequest<T>{private LoadViewArgs {|YL0005:args|};}}");
            yield return Case("Storage_ForeignPoolAndFalseSelfOwner", "class C{private static readonly YLFramework.Core.Pool.ObjectPool<YLFramework.Core.LoadViewArgs> {|YL0005:Pool|};}[MethodLocalOnly]class A{private static readonly YLFramework.Core.Pool.ObjectPool<A> {|YL0005:Pool|};}");
        }

        private static TestCaseData Case(string name, string source, params string[] parts) => new TestCaseData(source, parts).SetName(name);

        [TestCaseSource(nameof(Cases))]
        public Task Contract(string source, string[] parts) => AnalyzerFixture.Check(source, parts);

        [Test]
        public async Task ActualCoreOwnersAndOwnFactoryAreLegal()
            => Assert.That(await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation()), Is.Empty);

        [Test]
        public async Task MergedRuntimeDoesNotGrantOwnershipToAForeignSameNamedPool()
        {
            var foreign = AnalyzerFixture.Emit("ForeignPool", new[] {
                AnalyzerFixture.PoolSource.Replace("private T[] storage", "public T[] storage")
            }, AnalyzerFixture.Platform).WithProperties(new MetadataReferenceProperties(aliases: System.Collections.Immutable.ImmutableArray.Create("foreign")));
            var source = "extern alias foreign;namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]public class LoadViewArgs{" +
                "private static readonly foreign::YLFramework.Core.Pool.ObjectPool<LoadViewArgs> Pool=" +
                "new foreign::YLFramework.Core.Pool.ObjectPool<LoadViewArgs>(()=>null);}}";
            var diagnostics = await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation(source).AddReferences(foreign));
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(new[] { "YL0005" }));
            Assert.That(diagnostics.Single().Location.SourceSpan.Start,
                Is.EqualTo(source.IndexOf(" Pool=", StringComparison.Ordinal) + 1));
        }

        [TestCase("public static readonly", "ObjectPool<LoadViewArgs>", "Pool")]
        [TestCase("private static", "ObjectPool<LoadViewArgs>", "Pool")]
        [TestCase("private static readonly", "ObjectPool<LoadViewArgs>", "OtherPool")]
        public async Task CoreIdentityDoesNotExemptOtherPoolShapes(string modifiers, string type, string name)
        {
            var source = "using YLFramework.Core.Pool;namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]public class LoadViewArgs{" + modifiers + " " + type + " " + name + ";}}";
            var diagnostics = await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation(source));
            Assert.That(diagnostics.Select(d=>d.Id), Is.EqualTo(new[]{"YL0005"}));
            Assert.That(diagnostics.Single().Location.SourceSpan.Start, Is.EqualTo(source.IndexOf(" " + name + ";", System.StringComparison.Ordinal) + 1));
        }

        [TestCase("private", "args", 1, false)]
        [TestCase("public", "args", 1, true)]
        [TestCase("private static", "args", 1, true)]
        [TestCase("private", "other", 1, true)]
        [TestCase("private", "args", 2, true)]
        public async Task RequestOwnerIsOneExactMemberShape(string modifiers, string name, int arity, bool violation)
        {
            var source = "namespace YLFramework.Core{[RoslynAnalyzers.MethodLocalOnly]public class LoadViewArgs{}public class LoadViewRequest<" + (arity==1?"T":"T,U") + ">{" + modifiers + " LoadViewArgs " + name + ";}}";
            var diagnostics = await AnalyzerFixture.Diagnostics(AnalyzerFixture.CoreCompilation(source));
            Assert.That(diagnostics.Select(d=>d.Id), Is.EqualTo(violation?new[]{"YL0005"}:new string[0]));
        }

        [Test]
        public async Task SignatureOnlyMetadataIsUnknownNotGuessedStorage()
        {
            var opaque = AnalyzerFixture.Emit("Opaque", new[]{"public class Opaque<T>{public T Get()=>default;}"}, AnalyzerFixture.Platform);
            var compilation = AnalyzerFixture.Compilation(AnalyzerFixture.Prefix + "class C{Opaque<YLFramework.Core.LoadViewArgs> field;}").AddReferences(opaque);
            Assert.That(await AnalyzerFixture.Diagnostics(compilation), Is.Empty);
        }

        [TestCase("System", true)]
        [TestCase("ThirdPartyContainers", false)]
        public async Task ConcurrentBagProfileRespectsFrameworkAssemblyIdentity(string assemblyName, bool violation)
        {
            var replaced = AnalyzerFixture.Platform.Where(r => Path.GetFileNameWithoutExtension(r.Display) == "System" ||
                Path.GetFileNameWithoutExtension(r.Display) == "System.Collections.Concurrent").ToArray();
            var bag = AnalyzerFixture.Emit(assemblyName, new[] {
                "namespace System.Collections.Concurrent { public class ConcurrentBag<T> { public void Add(T item) {} } }"
            }, AnalyzerFixture.Platform.Except(replaced));
            var source = AnalyzerFixture.Prefix + "class C { System.Collections.Concurrent.ConcurrentBag<YLFramework.Core.LoadViewArgs> held; }";
            var compilation = AnalyzerFixture.Compilation(source).RemoveReferences(replaced).AddReferences(bag);
            var diagnostics = await AnalyzerFixture.Diagnostics(compilation);
            Assert.That(diagnostics.Select(d => d.Id), Is.EqualTo(violation ? new[] { "YL0005" } : new string[0]));
            if (violation)
            {
                Assert.That(diagnostics.Single().Severity, Is.EqualTo(DiagnosticSeverity.Warning));
                Assert.That(diagnostics.Single().Location.SourceSpan.Start, Is.EqualTo(source.IndexOf("held;", StringComparison.Ordinal)));
                Assert.That(diagnostics.Single().Location.SourceSpan.Length, Is.EqualTo(4));
            }
        }

        [Test]
        public async Task TupleRawAndSyntacticFormsShareStorageContract()
        {
            await AnalyzerFixture.Check("class C{(int,YLFramework.Core.LoadViewArgs) {|YL0005:syntax|};System.ValueTuple<int,YLFramework.Core.LoadViewArgs> {|YL0005:raw|};System.Tuple<YLFramework.Core.LoadViewArgs> {|YL0005:reference|};}");
        }
    }
}
