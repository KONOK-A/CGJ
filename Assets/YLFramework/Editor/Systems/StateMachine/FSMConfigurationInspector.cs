using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [CustomEditor(typeof(RuntimeFSMController))]
    internal sealed class FSMConfigurationInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (RuntimeFSMController)target;
            if (GUILayout.Button("Open YL Asset FSM Graph"))
            {
                controller.RefreshStateScripts();
                Context.Instance.RuntimeFSMControllerGUID = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller));
                Context.Instance.FSMControllerInstanceID = 0;
                EditorWindow.GetWindow<FSMEditorWindow>().Show();
            }
            DrawDefaultInspector();
            if (!controller.ValidateConfiguration(out string error)) EditorGUILayout.HelpBox($"Asset '{controller.name}': {error}", MessageType.Error);
        }
    }
}
