using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.StateMachine
{
    [Serializable]
    public class RuntimeFSMControllerLayer
    {
        /// <summary>
        /// 状态机层的名称
        /// </summary>
        public string name;
        /// <summary>
        /// 状态配置文件
        /// </summary>
        public RuntimeFSMController controller;
    }

    public class FSMController : MonoBehaviour
    {

        #region 字段

        [HideInInspector]
        [SerializeField]
        private List<RuntimeFSMController> _runtimeFSMController = new List<RuntimeFSMController>();

        [SerializeField]
        private List<RuntimeFSMControllerLayer> _runtimeFSMControllerLayer = new List<RuntimeFSMControllerLayer>();

        internal object userData;
        /// <summary>在启动前为直接挂载的控制器设置状态脚本共享数据，不必使用静态宿主。</summary>
        public object UserData { get => userData; set => userData = value; }

        private List<RuntimeFSMControllerInstance> Instances = new List<RuntimeFSMControllerInstance>();
        private readonly List<RuntimeFSMControllerInstance> phaseSnapshot = new List<RuntimeFSMControllerInstance>();
        private readonly Stack<List<RuntimeFSMControllerInstance>> broadcastPool = new Stack<List<RuntimeFSMControllerInstance>>();
        private bool initializing;
        private bool destroying;

        private List<RuntimeFSMController> runtimeControllers = new List<RuntimeFSMController>();

        private List<RuntimeFSMControllerLayer> runtimeFSMControllerLayer = new List<RuntimeFSMControllerLayer>();

        [SerializeField]
        [Tooltip("是否在OnDisable时重置状态机")]
        [Header("是否在OnDisable时重置状态机")]
        private bool resetOnDisable = true;

        #endregion

        #region 事件
        /// <summary>
        /// 初始化完成的回调
        /// </summary>
        public Action onInitFinish;
        /// <summary>
        /// 状态改变的回调 参数1:状态机名称 参数2:当前状态
        /// </summary>
        public Action<string,string> onStateChange;

        #endregion

        #region 属性

        /// <summary>
        /// 当前状态机执行的状态配置文件列表
        /// </summary>
        public List<RuntimeFSMController> RuntimeFSMController
        {
            get
            {
                runtimeControllers.Clear();

                if (Application.isPlaying && (Initialized || initializing))
                {
                    foreach (var item in runtimeFSMControllerLayer)
                    {
                        if (item.controller == null) continue;
                        runtimeControllers.Add(item.controller);
                    }
                }
                else {
                    foreach (var item in _runtimeFSMControllerLayer)
                    {
                        if (item.controller == null) continue;
                        runtimeControllers.Add(item.controller);
                    }
                }

                return runtimeControllers;
            }
        }

        public List<RuntimeFSMControllerLayer> RuntimeFSMControllerLayers
        {
            get
            {
                if (Application.isPlaying && (Initialized || initializing))
                {
                    return runtimeFSMControllerLayer;
                }

                return _runtimeFSMControllerLayer;
            }
        }

        /// <summary>
        /// 状态机是否初始化完成
        /// </summary>
        public bool Initialized { get; private set; } = false;

        #endregion

        #region 生命周期

        private void Start()
        {
            // 不推荐在Awake中初始化,如果在Awake中初始化，
            // 状态机的逻辑执行过早，在状态逻辑中,如果需要使用其他的组件,其他的组件如果没有初始化就会报错
            // 暂时觉得没有必要把初始化的时机配置成选项,固定在Start中初始化没有任何问题,
            // Awake 仅仅只是比Start早了一帧,其他没有任何必要性
            // 在Start之前所有数据保持默认,Start之后由状态机控制行为逻辑!
            if (!Initialized) Init();
        }

        private void Update()
            => Tick(0);

        private void LateUpdate()
            => Tick(1);

        private void FixedUpdate()
            => Tick(2);

        private void Tick(int phase)
        {
            phaseSnapshot.Clear();
            phaseSnapshot.AddRange(Instances);
            try
            {
                foreach (var item in phaseSnapshot)
                {
                    if (!this || !isActiveAndEnabled || destroying) break;
                    if (item.Disposed || !Instances.Contains(item)) continue;
                    if (phase == 0 && !item.Started) StartInstance(item);
                    if (!this || !isActiveAndEnabled || item.Disposed || !Instances.Contains(item)) continue;
                    if (phase == 0) item.Update();
                    else if (phase == 1) item.LateUpdate();
                    else item.FixedUpdate();
                }
            }
            finally { phaseSnapshot.Clear(); }
        }

        private void OnDestroy()
        {
            destroying = true;
            Initialized = false;
            var snapshot = Instances.ToArray();
            Instances.Clear();
            runtimeFSMControllerLayer.Clear();
            try { CloseInstances(snapshot, true); }
            finally
            {
                onInitFinish = null;
                onStateChange = null;
                FSMControllerExtension.Unregister(this);
            }
        }

        private void OnDisable()
        {
            if (resetOnDisable)
            {
                CloseInstances(Instances.ToArray(), false);
            }
        }

        private static void CloseInstances(RuntimeFSMControllerInstance[] snapshot, bool dispose)
        {
            Exception firstError = null;
            foreach (var instance in snapshot)
            {
                try { if (dispose) instance.Dispose(); else instance.Close(); }
                catch (Exception error) { firstError ??= error; }
            }
            // 用户退出回调的异常不吞掉，但必须先归还其余层的资源。
            if (firstError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        #endregion

        #region 方法

        // 初始化
        private void Init()
        {
            Instances.Clear();
            //runtimeControllers.Clear();

            // 兼容
            if (_runtimeFSMControllerLayer.Count == 0 && _runtimeFSMController.Count > 0)
            {
                for (int i = 0; i < _runtimeFSMController.Count; i++)
                {
                    if(_runtimeFSMController[i] == null) continue;
                    RuntimeFSMControllerLayer layer = new RuntimeFSMControllerLayer();
                    layer.name = string.Format("Layer {0}",i);
                    layer.controller = _runtimeFSMController[i];
                    _runtimeFSMControllerLayer.Add(layer);
                }
            }

            runtimeFSMControllerLayer.Clear();

            initializing = true;
            Exception firstError = null;
            try
            {
                foreach (var item in _runtimeFSMControllerLayer.ToArray())
                {
                    if (destroying) break;
                    if (!_runtimeFSMControllerLayer.Contains(item) || !item.controller) continue;
                    try { StartLayer(item); }
                    catch (Exception error) { firstError ??= error; }
                }
            }
            finally { initializing = false; }
            if (!destroying)
            {
                Initialized = true;
                try { onInitFinish?.Invoke(); }
                catch (Exception error) { firstError ??= error; }
            }
            if (firstError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        private void StartLayer(RuntimeFSMControllerLayer source)
        {
            RuntimeFSMControllerInstance instance;
            try { instance = new RuntimeFSMControllerInstance(source.controller, this, source.name); }
            catch (ArgumentException exception) { Debug.LogError(exception.Message, this); return; }
            catch (System.Reflection.TargetInvocationException exception)
            {
                Debug.LogError($"Asset '{source.controller.name}', layer '{source.name}': state constructor failed. {exception.InnerException}", this);
                return;
            }
            // 先登记所有权，再执行用户回调，允许回调移除自身或其他层。
            runtimeFSMControllerLayer.Add(new RuntimeFSMControllerLayer { name = source.name, controller = instance.RuntimeFSMController });
            Instances.Add(instance);
            if (isActiveAndEnabled) StartInstance(instance);
        }

        private void StartInstance(RuntimeFSMControllerInstance instance)
        {
            try { instance.StartUp(); }
            catch (Exception error)
            {
                Instances.Remove(instance);
                runtimeFSMControllerLayer.RemoveAll(layer => ReferenceEquals(layer.controller, instance.RuntimeFSMController));
                try { instance.Dispose(); }
                catch (Exception cleanupError) { Debug.LogException(cleanupError, this); }
                // 首次回调失败后不再驱动半初始化实例；保留原异常和调用栈。
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
            }
        }

        public void SetBool(string name, bool v)
        {
            SetBool(StringToHash(name), v);
        }

        /// <summary>
        /// 设置bool类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <param name="v">值</param>
        public void SetBool(int name_hash, bool v)
            => BroadcastParameter(name_hash, v ? 1 : 0, ParameterType.Bool);

        public void SetFloat(string name, float v)
        {
            SetFloat(StringToHash(name), v);
        }

        /// <summary>
        /// 设置float类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <param name="v">值</param>
        public void SetFloat(int name_hash, float v)
            => BroadcastParameter(name_hash, v, ParameterType.Float);

        public void SetInt(string name, int v)
        {
            SetInt(StringToHash(name), v);
        }

        /// <summary>
        /// 设置Int类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <param name="v">值</param>
        public void SetInt(int name_hash, int v)
            => BroadcastParameter(name_hash, v, ParameterType.Int);

        public void SetTrigger(string name)
        {
            SetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 触发Trigger
        /// </summary>
        /// <param name="name_hash">Trigger名称</param>
        public void SetTrigger(int name_hash)
            => BroadcastParameter(name_hash, 1, ParameterType.Trigger);

        private void BroadcastParameter(int hash, double value, ParameterType type, bool resetTrigger = false)
        {
            if (destroying) return;
            if (!Initialized)
            {
                onInitFinish += () => BroadcastParameter(hash, value, type, resetTrigger);
                return;
            }
            if (!IsContainParameter(hash, type))
            {
                if (!resetTrigger) Debug.LogWarningFormat("未查询到参数:{0} 类型:{1}", HashToString(hash), type);
                return;
            }
            // 参数回调允许递归广播及增删层；借用独立快照，预热后不逐帧分配。
            var snapshot = broadcastPool.Count > 0 ? broadcastPool.Pop() : new List<RuntimeFSMControllerInstance>();
            snapshot.AddRange(Instances);
            try
            {
                foreach (var item in snapshot)
                {
                    if (destroying || item.Disposed || !Instances.Contains(item)) continue;
                    if (type == ParameterType.Bool) item.SetBool(hash, value != 0);
                    else if (type == ParameterType.Float) item.SetFloat(hash, (float)value);
                    else if (type == ParameterType.Int) item.SetInt(hash, (int)value);
                    else if (resetTrigger) item.ResetTrigger(hash);
                    else item.SetTrigger(hash);
                }
            }
            finally { snapshot.Clear(); broadcastPool.Push(snapshot); }
        }

        public void ResetTrigger(string name)
        {
            ResetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 还原Trigger
        /// </summary>
        /// <param name="name_hash">Trigger名称</param>
        public void ResetTrigger(int name_hash)
            => BroadcastParameter(name_hash, 0, ParameterType.Trigger, true);

        public bool GetBool(string name)
        {
            return GetBool(StringToHash(name));
        }

        /// <summary>
        /// 获取Bool类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <returns></returns>
        public bool GetBool(int name_hash)
        {
            foreach (var item in Instances)
            {
                if (item.parameters.TryGetValue(name_hash, out var parameter) && parameter.parameterType == ParameterType.Bool && item.GetParamter(name_hash) == 1)
                    return true;
            }

            return false;
        }

        public float GetFloat(string name)
        {
            return GetFloat(StringToHash(name));
        }

        /// <summary>
        /// 获取Float类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <returns></returns>
        public float GetFloat(int name_hash )
        {
            foreach (var item in Instances)
            {
                if (!item.parameters.TryGetValue(name_hash, out var parameter) || parameter.parameterType != ParameterType.Float) continue;
                float v = item.GetParamter(name_hash);
                if (v != 0)
                    return v;
            }

            return 0;
        }

        public int GetInt(string name)
        {
            return GetInt(StringToHash(name));
        }

        /// <summary>
        /// 获取Int类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <returns></returns>
        public int GetInt(int name_hash)
        {
            foreach (var item in Instances)
            {
                if (!item.parameters.TryGetValue(name_hash, out var parameter) || parameter.parameterType != ParameterType.Int) continue;
                int v = (int)(item.GetParamter(name_hash));
                if (v != 0)
                    return v;
            }

            return 0;
        }

        public bool GetTrigger(string name) {
            return GetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 获取Trigger类型参数的值
        /// </summary>
        /// <param name="name_hash">参数名称</param>
        /// <returns></returns>
        public bool GetTrigger(int name_hash )
        {
            foreach (var item in Instances)
            {
                if (item.parameters.TryGetValue(name_hash, out var parameter) && parameter.parameterType == ParameterType.Trigger && (item.GetTriggerCount(name_hash) > 0 || item.GetParamter(name_hash) == 1))
                    return true;
            }
            return false;
        }


        public bool IsContainParameter(string name, ParameterType type)
        {
            return IsContainParameter(StringToHash(name), type);
        }

        public bool IsContainParameter(int name,ParameterType type)
        {
            foreach (var item in Instances)
            {
                if(item.parameters.ContainsKey(name) && item.parameters[name].parameterType == type)
                    return true;
            }

            return false;
        }


        /// <summary>
        /// 运行时添加并执行状态配置文件
        /// </summary>
        /// <param name="controller"></param>
        public void AddRuntimeFSMController(RuntimeFSMController controller)
        {
            if (!controller || destroying) return;
            string layerName = string.Format("Layer:{0}", controller.GetHashCode());
            AddRuntimeFSMController(controller, layerName);
        }

        /// <summary>
        /// 运行时添加并执行状态配置文件
        /// </summary>
        /// <param name="controller"></param>
        public void AddRuntimeFSMController(RuntimeFSMController controller,string layerName)
        {
            if (controller == null || destroying) return;

            if (Contains(_runtimeFSMControllerLayer, controller))
            {
                Debug.LogWarningFormat("状态机:{0}已经执行,请勿重复添加!", controller.name);
                return; // 说明已经添加了该状态机
            }

            RuntimeFSMControllerLayer layer = new RuntimeFSMControllerLayer();
            layer.name = layerName;
            layer.controller = controller;
            _runtimeFSMControllerLayer.Add(layer);


            // 如果没有初始化完成 不需要处理 在初始化的时候会处理
            if (Initialized || initializing)
            {
                StartLayer(layer);
            }

        }

        public void RemoveRuntimeFSMController(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
                return;

            // 同时取消尚未启动的源定义；先摘除，再运行可能重入并添加同名层的退出回调。
            _runtimeFSMControllerLayer.RemoveAll(layer => layer.name == layerName);
            runtimeFSMControllerLayer.RemoveAll(layer => layer.name == layerName);
            var snapshot = Instances.FindAll(instance => instance.layerName == layerName).ToArray();
            foreach (var instance in snapshot) Instances.Remove(instance);
            CloseInstances(snapshot, true);
        }


        /// <summary>
        /// 运行时移除状态机配置文件
        /// </summary>
        /// <param name="index">配置文件下标</param>
        public void RemoveRuntimeFSMController(int index)
        {
            if (index < 0 || index >= RuntimeFSMControllerLayers.Count) return;
            RemoveRuntimeFSMController(RuntimeFSMControllerLayers[index].name);
        }

        /// <summary>
        /// 获取当前正在执行的状态信息
        /// </summary>
        /// <param name="index">状态配置下标</param>
        /// <param name="subStateName">子状态机名称(默认:空)</param>
        /// <returns></returns>
        public FSMStateNode GetCurrentStateInfo(int index,string subStateName = "")
        {
            if (index < 0 || index >= Instances.Count) return null;
            return Instances[index].GetCurrentState(subStateName);
        }

        public FSMStateNode GetCurrentStateInfo(string layerName, string subStateName = "") {

            if (string.IsNullOrEmpty(layerName))
                return null;

            foreach (var item in Instances)
            {
                if (item.layerName.Equals(layerName))
                    return item.GetCurrentState(subStateName);
            }

            return null;
        }

        /// <summary>
        /// 获取当前的状态切换信息
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        public FSMTransition GetCurrentTransition(int index)
        {
            if (index < 0 || index >= Instances.Count) return null;
            return Instances[index].currentTransition;
        }


        private bool Contains(List<RuntimeFSMControllerLayer> layers,RuntimeFSMController controller)
        {
            if(controller == null) return false;

            foreach (var layer in layers)
            {
                if(layer == null) continue;
                if (layer.controller == controller) return true;
            }
            return false;
        }

        #endregion


        #region 静态方法

        /// <summary>
        /// 启动状态(适用于没有具体游戏物体的状态管理,例如:游戏状态)
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <param name="fsm">状态配置文件</param>
        /// <param name="userData">自定义参数或数据</param>
        /// <returns></returns>
        public static FSMController StartupFSM(string fsmName,RuntimeFSMController fsm,object userData = null)
        {
             return FSMControllerExtension.StartupFSM(fsmName, fsm, userData);
        }

        /// <summary>
        /// 查询通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <returns></returns>
        public static FSMController GetFSM(string fsmName)
        {
            return FSMControllerExtension.GetFSM(fsmName);
        }

        /// <summary>
        /// 移除通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        public static void RemoveFSM(string fsmName)
        {
            FSMControllerExtension.RemoveFSM(fsmName);
        }

        #endregion


        #region StringToHash

        public static int StringToHash(string event_name)
        {
            return FSMControllerExtension.StringToHash(event_name);
        }

        internal static string HashToString(int hash)
        {
            return FSMControllerExtension.HashToString(hash);
        }

        #endregion


    }

}
