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

            if (node != null && (!controller || !controller.states.Contains(node))) node = null;
            this.node = node;
            this.controller = controller;
            this.grap = node != null ? grap : null;
            if (node == null)
            {
                if (Selection.activeObject == this) Selection.activeObject = null;
                return;
            }

            Selection.activeObject = this;

        }

    }

}
