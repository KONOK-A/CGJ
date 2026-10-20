using YLFramework.Core.Editor;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class ListenerFile
    {


        [InitializeOnLoadMethod]
        private static void InitOnLoad() {
            //Debug.Log("Init");
            Selection.selectionChanged -= OnSelectionChange;
            Selection.selectionChanged += OnSelectionChange;


            EditorApplication.quitting -= OnEditorApplicationQuitting;
            EditorApplication.quitting += OnEditorApplicationQuitting;
        }

        private static void OnSelectionChange()
        {
            RuntimeFSMController controller = Selection.activeObject as RuntimeFSMController;
            if (controller != null)
            {
                // 把InstanceID设置为空
                Context.Instance.FSMControllerInstanceID = 0;
                // 设置给Context 保存起来
                string assetPath = AssetDatabase.GetAssetPath(controller);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Context.Instance.RuntimeFSMControllerGUID = guid;
            }
            else
            {
                GameObject gameObj = Selection.activeGameObject;

                if (gameObj == null) return;

                if (gameObj.activeInHierarchy == false)
                {
                    string path = AssetDatabase.GetAssetPath(gameObj);
                    if(string.IsNullOrEmpty(path)) // 路径不为空说明是预制体
                        return;
                }
                // 判断有没有FSMController组件
                FSMController fsmController = gameObj.GetComponent<FSMController>();
                if(FSMControllerIsEmpty(fsmController))
                    return;

                for (int i = 0;i<fsmController.RuntimeFSMController.Count;i++)
                {
                    if (fsmController.RuntimeFSMController[i] != null)
                    {
                        Context.Instance.FSMControllerIndex = i;
                        break;
                    }
                }

                Context.Instance.FSMControllerInstanceID = gameObj.GetInstanceID();
            }

            // 刷新
            Context.Instance.RefreshRuntimeFSMControllerGUID();
        }


        private static bool FSMControllerIsEmpty(FSMController fsmController) {
            if (fsmController == null)
                return true;

            if (fsmController.RuntimeFSMController == null)
                return true;

            if (fsmController.RuntimeFSMController.Count == 0)
                return true;

            for (int i = 0; i < fsmController.RuntimeFSMController.Count; i++) {
                if (fsmController.RuntimeFSMController[i] != null)
                    return false;
            }

            return true;
        }

        private static void OnEditorApplicationQuitting() {
            Context.Instance.FSMControllerIndex = 0;
            Context.Instance.FSMControllerInstanceID = 0;
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OnOpenAsset(int insId, int line)
        {
#if UNITY_6000_2_OR_NEWER
            RuntimeFSMController obj = EditorUtility.EntityIdToObject(insId) as RuntimeFSMController;
#else
            RuntimeFSMController obj = EditorUtility.InstanceIDToObject(insId) as RuntimeFSMController;
#endif


            if (obj != null)
            {
                obj.RefreshStateScripts();
                EditorWindow.GetWindow<FSMEditorWindow>().Show();
            }
            return obj != null;
        }
    }
}
