using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YLFramework.Analyzers.Tests
{
    public class ViewMessageRulesTests
    {
        public static IEnumerable<TestCaseData> Cases()
        {
            yield return Case("ViewMessages_ThreePrivateDeclarations", "class C:YLFramework.Core.View{private void {|YL0007:OnEnable|}(){}private void {|YL0007:OnDisable|}(){}private void {|YL0007:OnDestroy|}(){}}", "C");
            yield return Case("ViewMessages_VirtualOverrideEachOwnDeclaration", "class B:YLFramework.Core.View{public virtual void {|YL0007:OnEnable|}(){}}class C:B{public override void {|YL0007:OnEnable|}(){base.OnEnable();}}", "OnViewEnabled");
            yield return Case("ViewMessages_NewDeclaration", "class C:YLFramework.Core.View{public new void {|YL0007:OnDisable|}(){}}", "OnViewDisabled");
            yield return Case("ViewMessages_InheritedNotRepeated", "class B:YLFramework.Core.View{public void {|YL0007:OnDestroy|}(){}}class C:B{}class D:C{}", "OnClosed");
            yield return Case("ViewMessages_IndirectGenericNestedDerived", "class B<T>:YLFramework.Core.View{}class Outer{class C:B<int>{void {|YL0007:OnEnable|}(){}}class Helper{void OnEnable(){}}}", "Outer.C");
            yield return Case("ViewMessages_AliasRealIdentity", "using Real=core::YLFramework.Core.View;class C:Real{void {|YL0007:OnDestroy|}(){}}", "OnClosed");
            yield return Case("ViewMessages_StaticGenericOverloadsWrongReturn", "class C:YLFramework.Core.View{static void OnEnable(){}void OnDisable<T>(){}void OnDisable(int x){}int OnDestroy()=>1;}");
            yield return Case("ViewMessages_OtherUnityMessagesAndHooksAllowed", "class C:YLFramework.Core.View{void Awake(){RegisterEvent(()=>{});}void Update(){}protected override void OnViewEnabled(){}protected override void OnViewDisabled(){}protected override void OnClosed(){}protected override void Start(){base.Start();}}");
            yield return Case("ViewMessages_PropertyAndLocalFunctionNotMessages", "class C:YLFramework.Core.View{int OnEnable=>1;void M(){void OnDisable(){}OnDisable();}}");
            yield return Case("ViewMessages_OrdinaryMonoLikeNotView", "class MonoBehaviour{}class C:MonoBehaviour{void OnEnable(){}void OnDestroy(){}}");
            yield return Case("ViewMessages_ForeignFullName", "namespace YLFramework.Core{class View{}}class C:YLFramework.Core.View{void OnEnable(){}void OnDisable(){}}");
            yield return Case("ViewMessages_ProtectedHelpersOutsideHierarchy", "class C:YLFramework.Core.View{class Helper{public void OnDestroy(){}}}");
        }

        private static TestCaseData Case(string name, string source, params string[] parts) => new TestCaseData(source, parts).SetName(name);
        [TestCaseSource(nameof(Cases))]
        public Task Contract(string source, string[] parts) => AnalyzerFixture.Check(source, parts);
    }
}
