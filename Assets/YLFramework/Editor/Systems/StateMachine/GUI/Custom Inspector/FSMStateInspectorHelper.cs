using YLFramework.Core.Editor;

using UnityEditor;

namespace YLFramework.StateMachine
{
    public class FSMStateInspectorHelper : ScriptableObjectSingleton<FSMStateInspectorHelper>
    {
        //private static FSMStateInspectorHelper _instance;
        //public static FSMStateInspectorHelper Instance {
        //    get {
        //        if (_instance == null) {
        //            _instance = ScriptableObject.CreateInstance<FSMStateInspectorHelper>();
        //        }
        //        return _instance;
        //    }
        //}

        public FSMStateNodeData node;
        public RuntimeFSMController controller;

        public FSMStateGraphView grap;

        public void Inspect(RuntimeFSMController controller, FSMStateNodeData node, FSMStateGraphView grap) {

            if (node == null)
            {
                Selection.activeObject = null;
                return;
            }

            this.node = node;
            this.controller = controller;
            this.grap = grap;
            Selection.activeObject = this;

        }

    }

}
