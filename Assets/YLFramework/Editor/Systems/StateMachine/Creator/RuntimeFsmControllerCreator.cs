using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{
    public class RuntimeFsmControllerCreator : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            RuntimeFsmController runtimeFsm = ScriptableObject.CreateInstance<RuntimeFsmController>();
            AssetDatabase.CreateAsset(runtimeFsm, pathName);
            Selection.activeObject = runtimeFsm;


            Rect rect = new Rect(0, 100, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);
            FsmStateNodeFactory.CreateStateNode(runtimeFsm, FsmConst.AnyState, rect, false, false, null, true, FsmConst.AnyState);
            rect = new Rect(0, 300, FsmConst.StateNodeWith, FsmConst.StateNodeHeight);
            FsmStateNodeFactory.CreateStateNode(runtimeFsm, FsmConst.EntryState, rect, false, false, null, true, FsmConst.EntryState);
            AssetDatabase.SaveAssetIfDirty(runtimeFsm);
        }
    }

}
