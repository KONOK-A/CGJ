using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [CustomEditor(typeof(RuntimeFsmController))]
    internal sealed class FsmConfigurationInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (RuntimeFsmController)target;
            if (GUILayout.Button("Open YL Asset FSM Graph"))
            {
                controller.RefreshStateScripts();
                Context.Instance.RuntimeFsmControllerGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(controller));
                Context.Instance.FsmControllerInstanceId = 0;
                EditorWindow.GetWindow<FsmEditorWindow>().Show();
            }
            DrawDefaultInspector();
            if (!controller.ValidateConfiguration(out string error))
            {
                EditorGUILayout.HelpBox($"Asset '{controller.name}': {error}", MessageType.Error);
            }
        }
    }
}
