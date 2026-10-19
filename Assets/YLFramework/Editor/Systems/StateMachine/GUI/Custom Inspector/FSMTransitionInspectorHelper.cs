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
            if (transition == null)
            {
                Selection.activeObject = null;
                return;
            }

            this.transition = transition;
            this.controller = controller;

            Selection.activeObject = this;
        }


    }

}
