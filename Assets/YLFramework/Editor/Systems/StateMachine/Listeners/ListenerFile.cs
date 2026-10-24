using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    internal class ListenerFile
    {


        [InitializeOnLoadMethod]
        private static void InitOnLoad()
        {
            //Debug.Log("Init");
            Selection.selectionChanged -= OnSelectionChange;
            Selection.selectionChanged += OnSelectionChange;


            EditorApplication.quitting -= OnEditorApplicationQuitting;
            EditorApplication.quitting += OnEditorApplicationQuitting;
        }

        private static void OnSelectionChange()
        {
            RuntimeFsmController controller = Selection.activeObject as RuntimeFsmController;
            if (controller != null)
            {
                // 把InstanceID设置为空
                Context.Instance.FsmControllerInstanceId = 0;
                // 设置给Context 保存起来
                string assetPath = AssetDatabase.GetAssetPath(controller);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                Context.Instance.RuntimeFsmControllerGuid = guid;
            }
            else
            {
                GameObject gameObj = Selection.activeGameObject;

                if (gameObj == null)
                {
                    return;
                }

                if (gameObj.activeInHierarchy == false)
                {
                    string path = AssetDatabase.GetAssetPath(gameObj);
                    if (string.IsNullOrEmpty(path)) // 路径不为空说明是预制体
                    {
                        return;
                    }
                }
                // 判断有没有FSMController组件
                FsmController fsmController = gameObj.GetComponent<FsmController>();
                if (FsmControllerIsEmpty(fsmController))
                {
                    return;
                }

                for (int i = 0; i < fsmController.RuntimeFsmController.Count; i++)
                {
                    if (fsmController.RuntimeFsmController[i] != null)
                    {
                        Context.Instance.FsmControllerIndex = i;
                        break;
                    }
                }

                Context.Instance.FsmControllerInstanceId = gameObj.GetInstanceID();
            }

            // 刷新
            Context.Instance.RefreshRuntimeFsmControllerGuid();
        }


        private static bool FsmControllerIsEmpty(FsmController fsmController)
        {
            if (fsmController == null)
            {
                return true;
            }

            if (fsmController.RuntimeFsmController == null)
            {
                return true;
            }

            if (fsmController.RuntimeFsmController.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < fsmController.RuntimeFsmController.Count; i++)
            {
                if (fsmController.RuntimeFsmController[i] != null)
                {
                    return false;
                }
            }

            return true;
        }

        private static void OnEditorApplicationQuitting()
        {
            Context.Instance.FsmControllerIndex = 0;
            Context.Instance.FsmControllerInstanceId = 0;
        }

        [UnityEditor.Callbacks.OnOpenAsset(0)]
        private static bool OnOpenAsset(int insId, int line)
        {
#if UNITY_6000_2_OR_NEWER
            RuntimeFsmController obj = EditorUtility.EntityIdToObject(insId) as RuntimeFsmController;
#else
            RuntimeFsmController obj = EditorUtility.InstanceIDToObject(insId) as RuntimeFsmController;
#endif


            if (obj != null)
            {
                obj.RefreshStateScripts();
                EditorWindow.GetWindow<FsmEditorWindow>().Show();
            }
            return obj != null;
        }
    }
}
