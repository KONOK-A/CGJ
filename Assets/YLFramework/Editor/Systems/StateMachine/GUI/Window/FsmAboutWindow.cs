using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FsmAboutWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.LabelField("YL Asset FSM", EditorStyles.boldLabel);
        }
    }
}
