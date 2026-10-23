using UnityEditor;
using YLFramework.Core.Editor;

namespace YLFramework.StateMachine
{
    public class FsmStateInspectorHelper : ScriptableObjectSingleton<FsmStateInspectorHelper>
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

        public FsmStateNodeData node;
        public RuntimeFsmController controller;

        public FsmStateGraphView grap;

        public void Inspect(RuntimeFsmController controller, FsmStateNodeData node, FsmStateGraphView grap)
        {

            if (node != null && (!controller || !controller.states.Contains(node)))
            {
                node = null;
            }

            this.node = node;
            this.controller = controller;
            this.grap = node != null ? grap : null;
            if (node == null)
            {
                if (Selection.activeObject == this)
                {
                    Selection.activeObject = null;
                }

                return;
            }

            Selection.activeObject = this;

        }

    }

}
