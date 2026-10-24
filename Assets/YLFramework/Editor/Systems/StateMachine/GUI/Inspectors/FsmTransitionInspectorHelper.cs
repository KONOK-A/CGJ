using UnityEditor;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    public class FsmTransitionInspectorHelper : ScriptableObjectSingleton<FsmTransitionInspectorHelper>
    {

        public FsmTransitionData transition;
        public RuntimeFsmController controller;

        public void Inspect(RuntimeFsmController controller, FsmTransitionData transition)
        {
            if (transition != null && (!controller || !controller.transitions.Contains(transition)))
            {
                transition = null;
            }

            this.transition = transition;
            this.controller = controller;
            if (transition == null)
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
