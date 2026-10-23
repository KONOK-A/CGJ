using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YLFramework.StateMachine
{
    [Serializable]
    public class RuntimeFsmControllerLayer
    {
        /// <summary>
        /// 状态机层的名称
        /// </summary>
        public string name;
        /// <summary>
        /// 状态配置文件
        /// </summary>
        public RuntimeFsmController controller;
    }

    public class FsmController : MonoBehaviour
    {

        #region 字段

        [SerializeField]
        [FormerlySerializedAs("_runtimeFSMControllerLayer")]
        private List<RuntimeFsmControllerLayer> runtimeFsmControllerLayers = new List<RuntimeFsmControllerLayer>();

        internal object UserDataValue;
        /// <summary>在启动前为直接挂载的控制器设置状态脚本共享数据，不必使用静态宿主。</summary>
        public object UserData
        {
            get => UserDataValue; set => UserDataValue = value;
        }

        private List<RuntimeFsmControllerInstance> _instances = new List<RuntimeFsmControllerInstance>();
        private readonly List<RuntimeFsmControllerInstance> _phaseSnapshot = new List<RuntimeFsmControllerInstance>();
        private readonly Stack<List<RuntimeFsmControllerInstance>> _broadcastPool = new Stack<List<RuntimeFsmControllerInstance>>();
        private bool _isInitializing;
        private bool _isDestroying;

        private List<RuntimeFsmController> _runtimeControllers = new List<RuntimeFsmController>();

        private List<RuntimeFsmControllerLayer> _activeLayers = new List<RuntimeFsmControllerLayer>();

        [SerializeField]
        [Tooltip("是否在OnDisable时重置状态机")]
        [Header("是否在OnDisable时重置状态机")]
        private bool resetOnDisable = true;

        #endregion

        #region 事件
        /// <summary>
        /// 初始化完成的回调
        /// </summary>
        public Action Initialized;
        /// <summary>
        /// 状态改变的回调 参数1:状态机名称 参数2:当前状态
        /// </summary>
        public Action<string, string> StateChanged;

        #endregion

        #region 属性

        /// <summary>
        /// 当前状态机执行的状态配置文件列表
        /// </summary>
        public List<RuntimeFsmController> RuntimeFsmController
        {
            get
            {
                _runtimeControllers.Clear();

                if (Application.isPlaying && (IsInitialized || _isInitializing))
                {
                    foreach (var item in _activeLayers)
                    {
                        if (item.controller == null)
                        {
                            continue;
                        }

                        _runtimeControllers.Add(item.controller);
                    }
                }
                else
                {
                    foreach (var item in runtimeFsmControllerLayers)
                    {
                        if (item.controller == null)
                        {
                            continue;
                        }

                        _runtimeControllers.Add(item.controller);
                    }
                }

                return _runtimeControllers;
            }
        }

        public List<RuntimeFsmControllerLayer> RuntimeFsmControllerLayers
        {
            get
            {
                if (Application.isPlaying && (IsInitialized || _isInitializing))
                {
                    return _activeLayers;
                }

                return runtimeFsmControllerLayers;
            }
        }

        /// <summary>
        /// 状态机是否初始化完成
        /// </summary>
        public bool IsInitialized { get; private set; } = false;

        #endregion

        #region 生命周期

        private void Start()
        {
            // 不推荐在Awake中初始化,如果在Awake中初始化，
            // 状态机的逻辑执行过早，在状态逻辑中,如果需要使用其他的组件,其他的组件如果没有初始化就会报错
            // 暂时觉得没有必要把初始化的时机配置成选项,固定在Start中初始化没有任何问题,
            // Awake 仅仅只是比Start早了一帧,其他没有任何必要性
            // 在Start之前所有数据保持默认,Start之后由状态机控制行为逻辑!
            if (!IsInitialized)
            {
                Init();
            }
        }

        private void Update()
            => Tick(0);

        private void LateUpdate()
            => Tick(1);

        private void FixedUpdate()
            => Tick(2);

        private void Tick(int phase)
        {
            _phaseSnapshot.Clear();
            _phaseSnapshot.AddRange(_instances);
            try
            {
                foreach (var item in _phaseSnapshot)
                {
                    if (!this || !isActiveAndEnabled || _isDestroying)
                    {
                        break;
                    }

                    if (item.IsDisposed || !_instances.Contains(item))
                    {
                        continue;
                    }

                    if (phase == 0 && !item.HasStarted)
                    {
                        StartInstance(item);
                    }

                    if (!this || !isActiveAndEnabled || item.IsDisposed || !_instances.Contains(item))
                    {
                        continue;
                    }

                    if (phase == 0)
                    {
                        item.Update();
                    }
                    else if (phase == 1)
                    {
                        item.LateUpdate();
                    }
                    else
                    {
                        item.FixedUpdate();
                    }
                }
            }
            finally
            {
                _phaseSnapshot.Clear();
            }
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            IsInitialized = false;
            var snapshot = _instances.ToArray();
            _instances.Clear();
            _activeLayers.Clear();
            try
            {
                CloseInstances(snapshot, true);
            }
            finally
            {
                Initialized = null;
                StateChanged = null;
                FsmControllerExtension.Unregister(this);
            }
        }

        private void OnDisable()
        {
            if (resetOnDisable)
            {
                CloseInstances(_instances.ToArray(), false);
            }
        }

        private static void CloseInstances(RuntimeFsmControllerInstance[] snapshot, bool dispose)
        {
            Exception firstError = null;
            foreach (var instance in snapshot)
            {
                try
                {
                    if (dispose)
                    {
                        instance.Dispose();
                    }
                    else
                    {
                        instance.Close();
                    }
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }
            // 用户退出回调的异常不吞掉，但必须先归还其余层的资源。
            if (firstError != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
            }
        }

        #endregion

        #region 方法

        // 初始化
        private void Init()
        {
            _instances.Clear();
            _activeLayers.Clear();

            _isInitializing = true;
            Exception firstError = null;
            try
            {
                foreach (var item in runtimeFsmControllerLayers.ToArray())
                {
                    if (_isDestroying)
                    {
                        break;
                    }

                    if (!runtimeFsmControllerLayers.Contains(item) || !item.controller)
                    {
                        continue;
                    }

                    try
                    {
                        StartLayer(item);
                    }
                    catch (Exception error)
                    {
                        firstError ??= error;
                    }
                }
            }
            finally
            {
                _isInitializing = false;
            }
            if (!_isDestroying)
            {
                IsInitialized = true;
                try
                {
                    Initialized?.Invoke();
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
            }
            if (firstError != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
            }
        }

        private void StartLayer(RuntimeFsmControllerLayer source)
        {
            RuntimeFsmControllerInstance instance;
            try
            {
                instance = new RuntimeFsmControllerInstance(source.controller, this, source.name);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError(exception.Message, this);
                return;
            }
            catch (System.Reflection.TargetInvocationException exception)
            {
                Debug.LogError($"Asset '{source.controller.name}', layer '{source.name}': state constructor failed. {exception.InnerException}", this);
                return;
            }
            // 先登记所有权，再执行用户回调，允许回调移除自身或其他层。
            _activeLayers.Add(new RuntimeFsmControllerLayer { name = source.name, controller = instance.RuntimeFsmController });
            _instances.Add(instance);
            if (isActiveAndEnabled)
            {
                StartInstance(instance);
            }
        }

        private void StartInstance(RuntimeFsmControllerInstance instance)
        {
            try
            {
                instance.StartUp();
            }
            catch (Exception error)
            {
                _instances.Remove(instance);
                _activeLayers.RemoveAll(layer => ReferenceEquals(layer.controller, instance.RuntimeFsmController));
                try
                {
                    instance.Dispose();
                }
                catch (Exception cleanupError)
                {
                    Debug.LogException(cleanupError, this);
                }
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
        /// <param name="nameHash">参数名称</param>
        /// <param name="v">值</param>
        public void SetBool(int nameHash, bool v)
            => BroadcastParameter(nameHash, v ? 1 : 0, ParameterType.Bool);

        public void SetFloat(string name, float v)
        {
            SetFloat(StringToHash(name), v);
        }

        /// <summary>
        /// 设置float类型参数的值
        /// </summary>
        /// <param name="nameHash">参数名称</param>
        /// <param name="v">值</param>
        public void SetFloat(int nameHash, float v)
            => BroadcastParameter(nameHash, v, ParameterType.Float);

        public void SetInt(string name, int v)
        {
            SetInt(StringToHash(name), v);
        }

        /// <summary>
        /// 设置Int类型参数的值
        /// </summary>
        /// <param name="nameHash">参数名称</param>
        /// <param name="v">值</param>
        public void SetInt(int nameHash, int v)
            => BroadcastParameter(nameHash, v, ParameterType.Int);

        public void SetTrigger(string name)
        {
            SetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 触发Trigger
        /// </summary>
        /// <param name="nameHash">Trigger名称</param>
        public void SetTrigger(int nameHash)
            => BroadcastParameter(nameHash, 1, ParameterType.Trigger);

        private void BroadcastParameter(int hash, double value, ParameterType type, bool resetTrigger = false)
        {
            if (_isDestroying)
            {
                return;
            }

            if (!IsInitialized)
            {
                Initialized += () => BroadcastParameter(hash, value, type, resetTrigger);
                return;
            }
            if (!ContainsParameter(hash, type))
            {
                if (!resetTrigger)
                {
                    Debug.LogWarningFormat("未查询到参数:{0} 类型:{1}", HashToString(hash), type);
                }

                return;
            }
            // 参数回调允许递归广播及增删层；借用独立快照，预热后不逐帧分配。
            var snapshot = _broadcastPool.Count > 0 ? _broadcastPool.Pop() : new List<RuntimeFsmControllerInstance>();
            snapshot.AddRange(_instances);
            try
            {
                foreach (var item in snapshot)
                {
                    if (_isDestroying || item.IsDisposed || !_instances.Contains(item))
                    {
                        continue;
                    }

                    if (type == ParameterType.Bool)
                    {
                        item.SetBool(hash, value != 0);
                    }
                    else if (type == ParameterType.Float)
                    {
                        item.SetFloat(hash, (float)value);
                    }
                    else if (type == ParameterType.Int)
                    {
                        item.SetInt(hash, (int)value);
                    }
                    else if (resetTrigger)
                    {
                        item.ResetTrigger(hash);
                    }
                    else
                    {
                        item.SetTrigger(hash);
                    }
                }
            }
            finally
            {
                snapshot.Clear();
                _broadcastPool.Push(snapshot);
            }
        }

        public void ResetTrigger(string name)
        {
            ResetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 还原Trigger
        /// </summary>
        /// <param name="nameHash">Trigger名称</param>
        public void ResetTrigger(int nameHash)
            => BroadcastParameter(nameHash, 0, ParameterType.Trigger, true);

        public bool GetBool(string name)
        {
            return GetBool(StringToHash(name));
        }

        /// <summary>
        /// 获取Bool类型参数的值
        /// </summary>
        /// <param name="nameHash">参数名称</param>
        /// <returns></returns>
        public bool GetBool(int nameHash)
        {
            foreach (var item in _instances)
            {
                if (item.Parameters.TryGetValue(nameHash, out var parameter) && parameter.parameterType == ParameterType.Bool && item.GetParamter(nameHash) == 1)
                {
                    return true;
                }
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
        /// <param name="nameHash">参数名称</param>
        /// <returns></returns>
        public float GetFloat(int nameHash)
        {
            foreach (var item in _instances)
            {
                if (!item.Parameters.TryGetValue(nameHash, out var parameter) || parameter.parameterType != ParameterType.Float)
                {
                    continue;
                }

                float v = item.GetParamter(nameHash);
                if (v != 0)
                {
                    return v;
                }
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
        /// <param name="nameHash">参数名称</param>
        /// <returns></returns>
        public int GetInt(int nameHash)
        {
            foreach (var item in _instances)
            {
                if (!item.Parameters.TryGetValue(nameHash, out var parameter) || parameter.parameterType != ParameterType.Int)
                {
                    continue;
                }

                int v = (int)(item.GetParamter(nameHash));
                if (v != 0)
                {
                    return v;
                }
            }

            return 0;
        }

        public bool GetTrigger(string name)
        {
            return GetTrigger(StringToHash(name));
        }

        /// <summary>
        /// 获取Trigger类型参数的值
        /// </summary>
        /// <param name="nameHash">参数名称</param>
        /// <returns></returns>
        public bool GetTrigger(int nameHash)
        {
            foreach (var item in _instances)
            {
                if (item.Parameters.TryGetValue(nameHash, out var parameter) && parameter.parameterType == ParameterType.Trigger && (item.GetTriggerCount(nameHash) > 0 || item.GetParamter(nameHash) == 1))
                {
                    return true;
                }
            }
            return false;
        }


        public bool ContainsParameter(string name, ParameterType type)
        {
            return ContainsParameter(StringToHash(name), type);
        }

        public bool ContainsParameter(int name, ParameterType type)
        {
            foreach (var item in _instances)
            {
                if (item.Parameters.ContainsKey(name) && item.Parameters[name].parameterType == type)
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// 运行时添加并执行状态配置文件
        /// </summary>
        /// <param name="controller"></param>
        public void AddRuntimeFsmController(RuntimeFsmController controller)
        {
            if (!controller || _isDestroying)
            {
                return;
            }

            string layerName = string.Format("Layer:{0}", controller.GetHashCode());
            AddRuntimeFsmController(controller, layerName);
        }

        /// <summary>
        /// 运行时添加并执行状态配置文件
        /// </summary>
        /// <param name="controller"></param>
        public void AddRuntimeFsmController(RuntimeFsmController controller, string layerName)
        {
            if (controller == null || _isDestroying)
            {
                return;
            }

            if (Contains(runtimeFsmControllerLayers, controller))
            {
                Debug.LogWarningFormat("状态机:{0}已经执行,请勿重复添加!", controller.name);
                return; // 说明已经添加了该状态机
            }

            RuntimeFsmControllerLayer layer = new RuntimeFsmControllerLayer();
            layer.name = layerName;
            layer.controller = controller;
            runtimeFsmControllerLayers.Add(layer);


            // 如果没有初始化完成 不需要处理 在初始化的时候会处理
            if (IsInitialized || _isInitializing)
            {
                StartLayer(layer);
            }

        }

        public void RemoveRuntimeFsmController(string layerName)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                return;
            }

            // 同时取消尚未启动的源定义；先摘除，再运行可能重入并添加同名层的退出回调。
            runtimeFsmControllerLayers.RemoveAll(layer => layer.name == layerName);
            _activeLayers.RemoveAll(layer => layer.name == layerName);
            var snapshot = _instances.FindAll(instance => instance.LayerName == layerName).ToArray();
            foreach (var instance in snapshot)
            {
                _instances.Remove(instance);
            }

            CloseInstances(snapshot, true);
        }


        /// <summary>
        /// 运行时移除状态机配置文件
        /// </summary>
        /// <param name="index">配置文件下标</param>
        public void RemoveRuntimeFsmController(int index)
        {
            if (index < 0 || index >= RuntimeFsmControllerLayers.Count)
            {
                return;
            }

            RemoveRuntimeFsmController(RuntimeFsmControllerLayers[index].name);
        }

        /// <summary>
        /// 获取当前正在执行的状态信息
        /// </summary>
        /// <param name="index">状态配置下标</param>
        /// <param name="subStateName">子状态机名称(默认:空)</param>
        /// <returns></returns>
        public FsmStateNode GetCurrentStateInfo(int index, string subStateName = "")
        {
            if (index < 0 || index >= _instances.Count)
            {
                return null;
            }

            return _instances[index].GetCurrentState(subStateName);
        }

        public FsmStateNode GetCurrentStateInfo(string layerName, string subStateName = "")
        {

            if (string.IsNullOrEmpty(layerName))
            {
                return null;
            }

            foreach (var item in _instances)
            {
                if (item.LayerName.Equals(layerName))
                {
                    return item.GetCurrentState(subStateName);
                }
            }

            return null;
        }

        /// <summary>
        /// 获取当前的状态切换信息
        /// </summary>
        /// <param name="controller"></param>
        /// <returns></returns>
        public FsmTransition GetCurrentTransition(int index)
        {
            if (index < 0 || index >= _instances.Count)
            {
                return null;
            }

            return _instances[index].CurrentTransition;
        }


        private bool Contains(List<RuntimeFsmControllerLayer> layers, RuntimeFsmController controller)
        {
            if (controller == null)
            {
                return false;
            }

            foreach (var layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                if (layer.controller == controller)
                {
                    return true;
                }
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
        public static FsmController StartupFsm(string fsmName, RuntimeFsmController fsm, object userData = null)
        {
            return FsmControllerExtension.StartupFsm(fsmName, fsm, userData);
        }

        /// <summary>
        /// 查询通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <returns></returns>
        public static FsmController GetFsm(string fsmName)
        {
            return FsmControllerExtension.GetFsm(fsmName);
        }

        /// <summary>
        /// 移除通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        public static void RemoveFsm(string fsmName)
        {
            FsmControllerExtension.RemoveFsm(fsmName);
        }

        #endregion


        #region StringToHash

        public static int StringToHash(string eventName)
        {
            return FsmControllerExtension.StringToHash(eventName);
        }

        internal static string HashToString(int hash)
        {
            return FsmControllerExtension.HashToString(hash);
        }

        #endregion


    }

}
