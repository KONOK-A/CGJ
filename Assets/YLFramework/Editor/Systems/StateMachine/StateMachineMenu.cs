using YLFramework.Core.Editor;

using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class StateMachineMenu
    {
        // 这里不使用 CreateAssetMenu 来创建配置的原因，是因为创建完成之后，需要对配置添加一些默认数据
        // 所以这里自定义了这个逻辑
        [MenuItem("Assets/Create/YLFramework/Asset FSM/FSMController")]
        static void CreateFSM() {

            RuntimeFSMControllerCreator runtimeFSMControllerCreator = ScriptableObject.CreateInstance<RuntimeFSMControllerCreator>();

            string fileName = GetName();

            GUIContent content = EditorGUIUtility.IconContent("d_ScriptableObject Icon");

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(runtimeFSMControllerCreator.GetInstanceID(), runtimeFSMControllerCreator, fileName, (Texture2D)content.image, null);
        }

        internal static string GetName(string templateName = "New FSMController", string suffix = "asset") {

            string name;
            string[] files;

            int i = 0;

            do
            {
                name = string.Format("{0}{1}", templateName, i == 0 ? string.Empty : i.ToString());
                files = AssetDatabase.FindAssets(name);

                i++;
            } while (files != null && files.Length != 0);

            return string.Format("{0}.{1}", name, suffix);
        }

        [MenuItem("Window/YLFramework/Asset FSM/StateMachineWindow",false,4000 )]
        static void StateMachineWindow()
        {
            FSMEditorWindow window = EditorWindow.GetWindow<FSMEditorWindow>();
            window.Show();
        }

        [MenuItem("Assets/Create/YLFramework/Asset FSM/FSMState")]
        static void CreateFSMState() {
            FSMStateCreator stateCreator = ScriptableObject.CreateInstance<FSMStateCreator>();

            string fileName = GetName("NewState", "cs");

            GUIContent content = EditorGUIUtility.IconContent("icons/processed/cs script icon.asset");

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(stateCreator.GetInstanceID(), stateCreator, fileName, (Texture2D)content.image, string.Empty);
        }

        [MenuItem("Window/YLFramework/Asset FSM/About",false,5000)]
        static void About()
        {
            //Debug.Log("About");
            Rect rect = new Rect(0, 0, 550, 350);
            FSMAboutWindow window = EditorWindow.GetWindowWithRect<FSMAboutWindow>(rect, true, "About YL Asset FSM");
            window.Show();
        }

    }

}
