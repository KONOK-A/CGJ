using YLFramework.Core.Editor;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;


namespace YLFramework.StateMachine
{
    public class FSMStateCreator : EndNameEditAction
    {

        static string regex = "^[a-zA-Z][a-zA-Z0-9_]*$";

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            CreateFSMState(pathName);
        }


        internal static bool CreateFSMState(string pathName,bool errorTip = true, RuntimeFSMController controller = null, FSMStateNodeData node = null)
        {

            string fileName = Path.GetFileNameWithoutExtension(pathName);
            // 判断文件是否已经存在 如果已经存在则不能创建
            if (File.Exists(pathName))
            {
                if (errorTip) EditorUtility.DisplayDialog("提示", $"Script file already exists: {pathName}", "确定");
                return false;
            }

            string[] guids = AssetDatabase.FindAssets(fileName);

            foreach (var item in guids)
            {
                string asset_path = AssetDatabase.GUIDToAssetPath(item);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(asset_path);
                if (script == null || script.GetClass() == null) continue;
                if (script.GetClass().Name.Equals(fileName))
                {
                    if (errorTip)
                    {
                        string message = string.Format("脚本名称:{0}已经存在!", fileName);
                        //FSMEditorWindow.ShowNotification(string.Format("脚本名称:{0}已经存在!", fileName));
                        EditorUtility.DisplayDialog("提示", message, "确定");
                    }
                    return false;
                }
            }

            if (!Regex.Match(fileName, regex).Success)
            {
                EditorUtility.DisplayDialog("提示", string.Format("文件名:{0}不可用!", fileName), "确定");
                return false;
            }

            TextAsset template = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/YLFramework/Editor/Systems/StateMachine/Template/FSMStateTemplate.txt");
            if (!template) { Debug.LogError("YL Asset FSM: FSMStateTemplate.txt is missing from the Editor/Systems/StateMachine/Template directory."); return false; }
            string content = template.text;


            content = content.Replace("{0}", fileName);

            if (!FSMStateFileWriter.TryWriteNew(pathName, content, out string error))
            {
                if (errorTip) EditorUtility.DisplayDialog("提示", error, "确定");
                return false;
            }
            if (controller && node != null) PendingFSMStateBindings.Schedule(controller, node, pathName);
            AssetDatabase.Refresh();
            return true;
        }

    }

}
