using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class FSMAboutWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.LabelField("YL Asset FSM", EditorStyles.boldLabel);
        }
    }
}
