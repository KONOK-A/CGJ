using YLFramework.Core.Editor;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class Context
    {

        #region 字段

        private RuntimeFSMController _runtimeFSMController;

        public List<FSMStateNodeData> SelectNodes = new List<FSMStateNodeData>();

        public FSMTransitionData selectTransition;

        public bool isPreviewTransition = false;
        public FSMStateNodeData fromState = null;
        public FSMStateNodeData hoverState = null;

        private FSMController _FSMController;

        //private LayoutMatrix4x4Info _matrixInfo;

        private static Context _instance;

        //private List<FSMStateNodeData> current_show_state_node_data = new List<FSMStateNodeData>();
        //private List<FSMTransitionData> current_show_transition_data = new List<FSMTransitionData>();



        #endregion

        #region Layers

        private List<string> empty_layers = new List<string>();

        public List<string> Layers
        {
            get {

                if(RuntimeFSMController != null)
                    return RuntimeFSMController.Layers;

                return empty_layers;
            }
        }

        public string LayerParent
        {
            get
            {
                if (Layers.Count == 0)
                    return string.Empty;
                return Layers[Layers.Count - 1];
            }
        }

        public void AddLayer(string layer)
        {
            if (RuntimeFSMController == null) return;
            RuntimeFSMController.AddLayer(layer);
        }

        public void RemoveLayer(int index, int count)
        {
            if (RuntimeFSMController == null) return;
            RuntimeFSMController.RemoveLayer(index,count);
        }

        public void RemoveLast(string name)
        {
            if (RuntimeFSMController == null) return;
            int index = RuntimeFSMController.Layers.Count - 1;
            if (index >= 0 && index < RuntimeFSMController.Layers.Count)
            {
                string last = RuntimeFSMController.Layers[index];
                if (!last.Equals(name))
                    return;
                RuntimeFSMController.RemoveLayer(RuntimeFSMController.Layers.Count - 1, 1);
            }
        }

        #endregion

        #region 属性

        // 这个逻辑修改为如果只有一个,逻辑不变,
        // 如果有多个,判断当前显示的是不是在多个中,如果在不做处理,如果不在设置为第一个
        public RuntimeFSMController RuntimeFSMController {
            get
            {
                if (!IsEmpty(RuntimeFSMControllers) )
                {
                    if (FSMControllerIndex < 0 || FSMControllerIndex >= RuntimeFSMControllers.Count)
                        FSMControllerIndex = 0;

                    RuntimeFSMController controller = RuntimeFSMControllers[FSMControllerIndex];

                    if (controller != null && !string.IsNullOrEmpty(controller.originGUID) && _runtimeFSMControllerGUID != controller.originGUID)
                    {
                        EditorPrefs.SetString("YL.AssetFSMRuntimeFSMControllerGUID", controller.originGUID);
                        _runtimeFSMControllerGUID = controller.originGUID;
                        //Debug.LogFormat("保存的GUID:{0}", _runtimeFSMControllerGUID);
                    }

                    return controller;
                }

                if (_runtimeFSMController == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(RuntimeFSMControllerGUID);
                    _runtimeFSMController = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(path);
                }

                return _runtimeFSMController;
            }
            internal set
            {
                if (_runtimeFSMController == value) return;
                _runtimeFSMController = value;
            }
        }

        public FSMController FSMController {
            get {

                if (_FSMController == null)
                {

                    UnityEngine.Object obj = null;

#if UNITY_6000_2_OR_NEWER
                    obj = EditorUtility.EntityIdToObject(FSMControllerInstanceID);
#else
                    obj = EditorUtility.InstanceIDToObject(FSMControllerInstanceID);
#endif

                    GameObject gameObj = obj as GameObject;
                    if (gameObj == null) return null;
                    _FSMController = gameObj.GetComponent<FSMController>();
                }

                return _FSMController;
            }
        }


        private List<RuntimeFSMController> currentFSMControllers = new List<RuntimeFSMController> ();

        public List<RuntimeFSMController> RuntimeFSMControllers
        {
            get
            {
                if (FSMController != null)
                {
                    return FSMController.RuntimeFSMController;
                }
                currentFSMControllers.Clear();

                if (_runtimeFSMController == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(RuntimeFSMControllerGUID);
                    _runtimeFSMController = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(path);
                }

                if (_runtimeFSMController != null)
                    currentFSMControllers.Add(_runtimeFSMController);
                return currentFSMControllers;
            }
        }

        public static Context Instance
        {
            get {
                if(_instance == null)
                    _instance = new Context();
                return _instance;
            }
        }

        internal int FSMControllerInstanceID
        {
            get
            {
                return EditorPrefs.GetInt("YL.AssetFSMControllerInstanceID",0);
            }
            set {
                EditorPrefs.SetInt("YL.AssetFSMControllerInstanceID", value);

                UnityEngine.Object obj = null;

#if UNITY_6000_2_OR_NEWER
                obj = EditorUtility.EntityIdToObject(FSMControllerInstanceID);
#else
                obj = EditorUtility.InstanceIDToObject(FSMControllerInstanceID);
#endif

                GameObject gameObj = obj as GameObject;
                if (gameObj != null && gameObj.GetComponent<FSMController>() != null) {
                    _FSMController = gameObj.GetComponent<FSMController>();
                }
                else
                    _FSMController = null;

            }
        }

        private string _runtimeFSMControllerGUID;

        internal string RuntimeFSMControllerGUID
        {
            get
            {
                return EditorPrefs.GetString("YL.AssetFSMRuntimeFSMControllerGUID",string.Empty);
            }
            set
            {
                //Debug.LogFormat("修改GUID:{0}",value);
                EditorPrefs.SetString("YL.AssetFSMRuntimeFSMControllerGUID", value);
                string path  = AssetDatabase.GUIDToAssetPath(value);
                RuntimeFSMController = AssetDatabase.LoadAssetAtPath<RuntimeFSMController>(path);
            }
        }

        internal int FSMControllerIndex
        {
            get {
                return EditorPrefs.GetInt("FSMControllerIndex", 0);
            }
            set
            {
                if (FSMControllerIndex == value) return;
                EditorPrefs.SetInt("FSMControllerIndex", value);

                // 刷新RuntimeGUID
                RefreshRuntimeFSMControllerGUID();
            }
        }

#endregion

        #region 方法

        private Context() {
        }

        public void ClearSelections()
        {
            // 如果不是 InspectorWindow 此时清空 Inspector
            this.SelectNodes.Clear();
            selectTransition = null;
            Selection.activeObject = null;
            //Selection.activeObject = this.RuntimeFSMController;
        }

        /// <summary>
        /// 获取当前显示状态节点
        /// </summary>
        /// <returns></returns>
        public List<FSMStateNodeData> GetCurrentShowStateNodeData() {

            if (RuntimeFSMController != null)
                return RuntimeFSMController.GetCurrentShowStateNodeData(LayerParent);

            return null;
        }

        public List<FSMTransitionData> GetCurrentShowTransitionData()
        {
            if (RuntimeFSMController != null)
                return RuntimeFSMController.GetCurrentShowTransitionData(LayerParent);

            return null;
        }


        public bool IsEmpty(List<RuntimeFSMController> controllers)
        {
            if (controllers == null || controllers.Count == 0)
                return true;

            foreach (var item in controllers)
            {
                if (item != null) return false;
            }

            return true;
        }




        #endregion

        #region 运行时刷新选中的GUID

        // 运行时选中某一个FSMController 组件，当取消运行，这个游戏物体被销毁时
        // 仍选中这个状态配置

        private List<RuntimeFSMControllerLayer> tempLayers = new List<RuntimeFSMControllerLayer>();

        public void RefreshRuntimeFSMControllerGUID()
        {
            if (!Application.isPlaying) return;
            if (FSMController == null) return;
            if (IsEmpty(FSMController.RuntimeFSMController)) return;

            // 通过反射拿到源配置
            Type type = typeof(FSMController);
            FieldInfo fieldInfo = type.GetField("_runtimeFSMControllerLayer", BindingFlags.NonPublic | BindingFlags.Instance);
            List<RuntimeFSMControllerLayer> layers = fieldInfo.GetValue(FSMController) as List<RuntimeFSMControllerLayer>;

            if (layers == null || layers.Count == 0) return;

            tempLayers.Clear();
            foreach (var layer in layers)
            {
                if (layer.controller == null) continue;
                tempLayers.Add(layer);
            }

            if (FSMControllerIndex < 0 || FSMControllerIndex >= tempLayers.Count) return;

            string asset_path = AssetDatabase.GetAssetPath(tempLayers[FSMControllerIndex].controller);
            string guid = AssetDatabase.AssetPathToGUID(asset_path);
            if (string.IsNullOrEmpty(guid)) return;
            RuntimeFSMControllerGUID = guid;

        }

        #endregion

    }
}
