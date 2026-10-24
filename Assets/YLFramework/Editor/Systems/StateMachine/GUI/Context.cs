using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using YLFramework.StateMachine;

namespace YLFramework.Editor.StateMachine
{
    internal class Context
    {

        #region 字段

        private RuntimeFsmController _runtimeFsmController;

        public List<FsmStateNodeData> SelectNodes = new List<FsmStateNodeData>();

        public FsmTransitionData SelectTransition;

        public bool IsPreviewingTransition = false;
        public FsmStateNodeData FromState = null;
        public FsmStateNodeData HoverState = null;

        private FsmController _fsmController;

        //private LayoutMatrix4x4Info _matrixInfo;

        private static Context _instance;

        //private List<FSMStateNodeData> current_show_state_node_data = new List<FSMStateNodeData>();
        //private List<FSMTransitionData> current_show_transition_data = new List<FSMTransitionData>();



        #endregion

        #region Layers

        private List<string> _emptyLayers = new List<string>();

        public List<string> Layers
        {
            get
            {

                if (RuntimeFsmController != null)
                {
                    return RuntimeFsmController.layers;
                }

                return _emptyLayers;
            }
        }

        public string LayerParent
        {
            get
            {
                if (Layers.Count == 0)
                {
                    return string.Empty;
                }

                return Layers[Layers.Count - 1];
            }
        }

        public void AddLayer(string layer)
        {
            if (RuntimeFsmController == null)
            {
                return;
            }

            RuntimeFsmController.AddLayer(layer);
        }

        public void RemoveLayer(int index, int count)
        {
            if (RuntimeFsmController == null)
            {
                return;
            }

            RuntimeFsmController.RemoveLayer(index, count);
        }

        public void RemoveLast(string name)
        {
            if (RuntimeFsmController == null)
            {
                return;
            }

            int index = RuntimeFsmController.layers.Count - 1;
            if (index >= 0 && index < RuntimeFsmController.layers.Count)
            {
                string last = RuntimeFsmController.layers[index];
                if (!last.Equals(name))
                {
                    return;
                }

                RuntimeFsmController.RemoveLayer(RuntimeFsmController.layers.Count - 1, 1);
            }
        }

        #endregion

        #region 属性

        // 这个逻辑修改为如果只有一个,逻辑不变,
        // 如果有多个,判断当前显示的是不是在多个中,如果在不做处理,如果不在设置为第一个
        public RuntimeFsmController RuntimeFsmController
        {
            get
            {
                if (!IsEmpty(RuntimeFsmControllers))
                {
                    if (FsmControllerIndex < 0 || FsmControllerIndex >= RuntimeFsmControllers.Count)
                    {
                        FsmControllerIndex = 0;
                    }

                    RuntimeFsmController controller = RuntimeFsmControllers[FsmControllerIndex];

                    if (controller != null && !string.IsNullOrEmpty(controller.originGuid) && _runtimeFsmControllerGuid != controller.originGuid)
                    {
                        EditorPrefs.SetString("YL.AssetFSMRuntimeFSMControllerGUID", controller.originGuid);
                        _runtimeFsmControllerGuid = controller.originGuid;
                        //Debug.LogFormat("保存的GUID:{0}", _runtimeFSMControllerGUID);
                    }

                    return controller;
                }

                if (_runtimeFsmController == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(RuntimeFsmControllerGuid);
                    _runtimeFsmController = AssetDatabase.LoadAssetAtPath<RuntimeFsmController>(path);
                }

                return _runtimeFsmController;
            }
            internal set
            {
                if (_runtimeFsmController == value)
                {
                    return;
                }

                _runtimeFsmController = value;
            }
        }

        public FsmController FsmController
        {
            get
            {

                if (_fsmController == null)
                {

                    UnityEngine.Object obj = null;

#if UNITY_6000_2_OR_NEWER
                    obj = EditorUtility.EntityIdToObject(FsmControllerInstanceId);
#else
                    obj = EditorUtility.InstanceIDToObject(FsmControllerInstanceId);
#endif

                    GameObject gameObj = obj as GameObject;
                    if (gameObj == null)
                    {
                        return null;
                    }

                    _fsmController = gameObj.GetComponent<FsmController>();
                }

                return _fsmController;
            }
        }


        private List<RuntimeFsmController> _currentFsmControllers = new List<RuntimeFsmController>();

        public List<RuntimeFsmController> RuntimeFsmControllers
        {
            get
            {
                if (FsmController != null)
                {
                    return FsmController.RuntimeFsmController;
                }
                _currentFsmControllers.Clear();

                if (_runtimeFsmController == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(RuntimeFsmControllerGuid);
                    _runtimeFsmController = AssetDatabase.LoadAssetAtPath<RuntimeFsmController>(path);
                }

                if (_runtimeFsmController != null)
                {
                    _currentFsmControllers.Add(_runtimeFsmController);
                }

                return _currentFsmControllers;
            }
        }

        public static Context Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Context();
                }

                return _instance;
            }
        }

        internal int FsmControllerInstanceId
        {
            get
            {
                return EditorPrefs.GetInt("YL.AssetFSMControllerInstanceID", 0);
            }
            set
            {
                EditorPrefs.SetInt("YL.AssetFSMControllerInstanceID", value);

                UnityEngine.Object obj = null;

#if UNITY_6000_2_OR_NEWER
                obj = EditorUtility.EntityIdToObject(FsmControllerInstanceId);
#else
                obj = EditorUtility.InstanceIDToObject(FsmControllerInstanceId);
#endif

                GameObject gameObj = obj as GameObject;
                if (gameObj != null && gameObj.GetComponent<FsmController>() != null)
                {
                    _fsmController = gameObj.GetComponent<FsmController>();
                }
                else
                {
                    _fsmController = null;
                }
            }
        }

        private string _runtimeFsmControllerGuid;

        internal string RuntimeFsmControllerGuid
        {
            get
            {
                return EditorPrefs.GetString("YL.AssetFSMRuntimeFSMControllerGUID", string.Empty);
            }
            set
            {
                //Debug.LogFormat("修改GUID:{0}",value);
                EditorPrefs.SetString("YL.AssetFSMRuntimeFSMControllerGUID", value);
                string path = AssetDatabase.GUIDToAssetPath(value);
                RuntimeFsmController = AssetDatabase.LoadAssetAtPath<RuntimeFsmController>(path);
            }
        }

        internal int FsmControllerIndex
        {
            get
            {
                return EditorPrefs.GetInt("FSMControllerIndex", 0);
            }
            set
            {
                if (FsmControllerIndex == value)
                {
                    return;
                }

                EditorPrefs.SetInt("FSMControllerIndex", value);

                // 刷新RuntimeGUID
                RefreshRuntimeFsmControllerGuid();
            }
        }

        #endregion

        #region 方法

        private Context()
        {
        }

        public void ClearSelections()
        {
            // 如果不是 InspectorWindow 此时清空 Inspector
            this.SelectNodes.Clear();
            SelectTransition = null;
            Selection.activeObject = null;
            //Selection.activeObject = this.RuntimeFSMController;
        }

        /// <summary>
        /// 获取当前显示状态节点
        /// </summary>
        /// <returns></returns>
        public List<FsmStateNodeData> GetCurrentShowStateNodeData()
        {

            if (RuntimeFsmController != null)
            {
                return RuntimeFsmController.GetCurrentShowStateNodeData(LayerParent);
            }

            return null;
        }

        public List<FsmTransitionData> GetCurrentShowTransitionData()
        {
            if (RuntimeFsmController != null)
            {
                return RuntimeFsmController.GetCurrentShowTransitionData(LayerParent);
            }

            return null;
        }


        public bool IsEmpty(List<RuntimeFsmController> controllers)
        {
            if (controllers == null || controllers.Count == 0)
            {
                return true;
            }

            foreach (var item in controllers)
            {
                if (item != null)
                {
                    return false;
                }
            }

            return true;
        }




        #endregion

        #region 运行时刷新选中的GUID

        // 运行时选中某一个FSMController 组件，当取消运行，这个游戏物体被销毁时
        // 仍选中这个状态配置

        private List<RuntimeFsmControllerLayer> _tempLayers = new List<RuntimeFsmControllerLayer>();

        public void RefreshRuntimeFsmControllerGuid()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (FsmController == null)
            {
                return;
            }

            if (IsEmpty(FsmController.RuntimeFsmController))
            {
                return;
            }

            // 通过反射拿到源配置
            Type type = typeof(FsmController);
            FieldInfo fieldInfo = type.GetField("runtimeFsmControllerLayers", BindingFlags.NonPublic | BindingFlags.Instance);
            List<RuntimeFsmControllerLayer> layers = fieldInfo.GetValue(FsmController) as List<RuntimeFsmControllerLayer>;

            if (layers == null || layers.Count == 0)
            {
                return;
            }

            _tempLayers.Clear();
            foreach (var layer in layers)
            {
                if (layer.controller == null)
                {
                    continue;
                }

                _tempLayers.Add(layer);
            }

            if (FsmControllerIndex < 0 || FsmControllerIndex >= _tempLayers.Count)
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(_tempLayers[FsmControllerIndex].controller);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            RuntimeFsmControllerGuid = guid;

        }

        #endregion

    }
}
