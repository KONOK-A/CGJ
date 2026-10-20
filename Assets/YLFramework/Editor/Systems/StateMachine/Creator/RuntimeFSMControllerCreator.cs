using YLFramework.Core.Editor;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class RuntimeFSMControllerCreator : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            RuntimeFSMController runtimeFSM = ScriptableObject.CreateInstance<RuntimeFSMController>();
            AssetDatabase.CreateAsset(runtimeFSM,pathName);
            Selection.activeObject = runtimeFSM;


            Rect rect = new Rect(0, 100, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);
            FSMStateNodeFactory.CreateStateNode(runtimeFSM, FSMConst.anyState, rect, false,false,null,true,FSMConst.anyState);
            rect = new Rect(0, 300, FSMConst.StateNodeWith, FSMConst.StateNodeHeight);
            FSMStateNodeFactory.CreateStateNode(runtimeFSM, FSMConst.entryState, rect, false, false,null, true, FSMConst.entryState);
            AssetDatabase.SaveAssetIfDirty(runtimeFSM);
        }
    }

}
