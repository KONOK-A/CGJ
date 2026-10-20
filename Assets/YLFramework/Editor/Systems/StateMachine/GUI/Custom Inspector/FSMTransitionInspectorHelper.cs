using YLFramework.Core.Editor;

using UnityEditor;

namespace YLFramework.StateMachine
{
    public class FSMTransitionInspectorHelper : ScriptableObjectSingleton<FSMTransitionInspectorHelper>
    {

        public FSMTransitionData transition;
        public RuntimeFSMController controller;

        public void Inspect(RuntimeFSMController controller, FSMTransitionData transition)
        {
            if (transition != null && (!controller || !controller.transitions.Contains(transition))) transition = null;
            this.transition = transition;
            this.controller = controller;
            if (transition == null)
            {
                if (Selection.activeObject == this) Selection.activeObject = null;
                return;
            }

            Selection.activeObject = this;
        }


    }

}
