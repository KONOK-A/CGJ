using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.StateMachine
{
    public class FsmAboutWindow : EditorWindow
    {
        private void OnGUI()
        {
            EditorGUILayout.LabelField("YL Asset FSM", EditorStyles.boldLabel);
        }
    }
}
