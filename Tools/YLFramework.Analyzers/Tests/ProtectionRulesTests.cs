using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class ProtectionRulesTests
    {
        private const string Model = "[ProtectedHierarchy(typeof(C))]class A{public int x;public string s;public int P{get;set;}public int this[int i]{get=>0;set{}}public static int Static;public event Action E;public S value;public R reference;public int[] array;public List<int> list;}struct S{public int x;}class R{public int x;}";
        public static IEnumerable<TestCaseData> Cases()
        {
            yield return Case("Write_AssignmentAndCompounds", Model + "class C{void M(A a){ {|YL0006:a.x|}=1; {|YL0006:a.x|}+=1; {|YL0006:a.x|}%=2; {|YL0006:a.x|}<<=1; {|YL0006:a.x|}|=1; {|YL0006:a.s|}??=\"x\";}}");
            yield return Case("Write_AllRemainingCompounds", Model + "class C{void M(A a){ {|YL0006:a.x|}-=1; {|YL0006:a.x|}*=2; {|YL0006:a.x|}/=2; {|YL0006:a.x|}>>=1; {|YL0006:a.x|}&=1; {|YL0006:a.x|}^=1;}}");
            yield return Case("Write_IncrementPropertyIndexer", Model + "class C{void M(A a){ {|YL0006:a.x|}++; --{|YL0006:a.x|}; {|YL0006:a.P|}=2; {|YL0006:a[0]|}=1;}}");
            yield return Case("Write_DeconstructionLeaves", Model + "class C{void M(A a){({|YL0006:a.x|},({|YL0006:a.P|},{|YL0006:a[0]|}))=(1,(2,3));}}");
            yield return Case("Write_ObjectInitializer", Model + "class C{A M()=>new A{{|YL0006:x|}=1,{|YL0006:P|}=2};}");
            yield return Case("Write_RefOutNotIn", Model + "class C{void M(A a){F(ref {|YL0006:a.x|});G(out {|YL0006:a.x|});H(in a.x);}void F(ref int x){}void G(out int x){x=0;}void H(in int x){}}");
            yield return Case("Write_RefLocalReturnAndReassignment", Model + "class C{void M(A a){ref int r=ref {|YL0006:a.x|};r=ref {|YL0006:a.x|};r=1;}ref int Return(A a)=>ref {|YL0006:a.x|};}");
            yield return Case("Write_RefReadonlyOnlyReads", Model + "class C{void M(A a){ref readonly int r=ref a.x;var x=r;}ref readonly int Return(A a)=>ref a.x;}");
            yield return Case("Write_StructChainAndReferenceBoundary", Model + "class C{void M(A a){ {|YL0006:a.value.x|}=1;a.reference.x=1;a.array[0]=1;a.list[0]=1;a.list.Add(1);}}");
            yield return Case("Write_ReadStaticAndUnrelated", Model + "class C{void M(A a){var x=a.x;var y=a.P;A.Static=1;Action h=()=>{};a.E+=h;a.E-=h;}}class D{void M(A a){a.x=1;}}");
            yield return Case("Write_ProtectedFamilyOwnsState", "[ProtectedHierarchy(typeof(A))]class A{public int x;void M(A a){a.x=1;}}class B:A{void M(A a){a.x=1;}}");
            yield return Case("Write_OpenGenericAndNestedForbidden", "[ProtectedHierarchy(typeof(Outer.C<>))]class A{public int x;}class Outer{public class C<T>{void M(A a){ {|YL0006:a.x|}=1;}}}");
            yield return Case("Write_ClosedGenericExact", "[ProtectedHierarchy(typeof(F<int>))]class A{public int x;}class F<T>{}class C:F<int>{void M(A a){ {|YL0006:a.x|}=1;}}class D:F<string>{void M(A a){a.x=1;}}");
            yield return Case("Write_ReceiverClassConstraint", Model + "class C{void M<T>(T a)where T:A{ {|YL0006:a.x|}=1;}}");
            yield return Case("Write_MarkedDerivedReceiverInheritedMember", "class B{public int x;}[ProtectedHierarchy(typeof(C))]class A:B{}class C{void M(A a){ {|YL0006:a.x|}=1;}}");
            yield return Case("Write_ForeignMarkerIgnored", "class ProtectedHierarchyAttribute:Attribute{public ProtectedHierarchyAttribute(Type t){}}[ProtectedHierarchy(typeof(C))]class A{public int x;}class C{void M(A a){a.x=1;}}");
        }

        private static TestCaseData Case(string name, string source) => new TestCaseData(source).SetName(name);
        [TestCaseSource(nameof(Cases))]
        public Task Contract(string source) => AnalyzerFixture.Check(source);
    }
}
