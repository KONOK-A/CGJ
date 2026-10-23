using System.Collections.Generic;
using System.Text;
#if UNITY_EDITOR
#endif
using UnityEngine;

namespace YLFramework.StateMachine
{



    /// <summary>
    /// RuntimeFSMController 运行时的示例
    /// </summary>
    public class RuntimeFsmControllerInstance
    {

        public string Name;

        public string LayerName;

        public RuntimeFsmController RuntimeFsmController;
        public RuntimeFsmController SourceController
        {
            get;
        }
        public bool IsDisposed
        {
            get; private set;
        }
        internal bool IsResettingTriggers
        {
            get; set;
        }
        private bool _isClosing;

        // 所有的参数
        internal Dictionary<int, FsmParameterData> Parameters = new Dictionary<int, FsmParameterData>();

        [Tooltip("参数的默认值")]
        internal Dictionary<int, float> ParameterDefaults = new Dictionary<int, float>();

        // 所有的状态
        internal Dictionary<string, FsmStateNode> States = new Dictionary<string, FsmStateNode>();

        internal List<FsmTransition> Transitions = new List<FsmTransition>();

        // 默认状态
        internal FsmStateNode DefaultState { get; private set; } = null;

        private Dictionary<string, FsmStateNode> _currentStates = new Dictionary<string, FsmStateNode>();

        private List<FsmStateNode> _currentStatesArray = new List<FsmStateNode>();

        // 当前的过渡
        public FsmTransition CurrentTransition { get; private set; } = null;

        public FsmController FsmController
        {
            get; private set;
        }

        /// <summary>
        /// 状态切换计数(为了避免状态切换进入死循环)
        /// </summary>
        private readonly FsmTransitionBudget _transitionBudget = new FsmTransitionBudget();

        /// <summary>
        /// 当前的帧数
        /// </summary>
        private int _loopErrorFrame = -1;

        /// <summary>
        /// 记录Trigger触发的次数
        /// </summary>
        private readonly FsmTriggerQueue _triggerQueue = new FsmTriggerQueue();

        private List<int> _triggerKeys = new List<int>();

        public bool HasStarted { get; private set; } = false;


        public RuntimeFsmControllerInstance(RuntimeFsmController runtimeFsmController, FsmController controller, string layerName)
        {
            FsmController = controller;
            SourceController = runtimeFsmController;
            this.LayerName = layerName;
            // 把数据复制一份
            if (!runtimeFsmController)
            {
                throw new System.ArgumentNullException(nameof(runtimeFsmController));
            }

            if (!controller)
            {
                throw new System.ArgumentNullException(nameof(controller));
            }

            if (!runtimeFsmController.ValidateConfiguration(out string error))
            {
                throw new System.ArgumentException($"Asset '{runtimeFsmController.name}', layer '{layerName}': {error}", nameof(runtimeFsmController));
            }

            Name = runtimeFsmController.name;
            // 默认不执行任何状态
            _currentStates.Clear();

            RuntimeFsmController = GameObject.Instantiate(runtimeFsmController);
#if UNITY_EDITOR
            FsmEditorHooks.CloneCreated?.Invoke(runtimeFsmController, RuntimeFsmController);
#endif
            RuntimeFsmController.name = runtimeFsmController.name;
            try
            {
                // 参数
                Parameters.Clear();
                for (int i = 0; i < RuntimeFsmController.parameters.Count; i++)
                {
                    FsmParameterData data = RuntimeFsmController.parameters[i];

                    int nameHash = FsmController.StringToHash(data.name);

                    if (Parameters.ContainsKey(nameHash))
                    {
                        continue;
                    }

                    data.nameHash = nameHash;

                    data.ValueChanged = null;
                    Parameters.Add(nameHash, data);
                    ParameterDefaults.Add(nameHash, data.Value);
                }

                // 状态
                States.Clear();
                for (int i = 0; i < RuntimeFsmController.states.Count; i++)
                {
                    FsmStateNodeData stateData = RuntimeFsmController.states[i];
                    FsmStateNode state = new FsmStateNode(stateData, this);

                    if (States.ContainsKey(stateData.name))
                    {
                        continue;
                    }

                    if (stateData.isDefaultState && stateData.BaseLayer())
                    {
                        DefaultState = state;
                    }

                    States.Add(stateData.name, state);
                }
                // 过渡
                Transitions.Clear();
                foreach (var item in RuntimeFsmController.transitions)
                {
                    FsmTransition transition = new FsmTransition(this, item);
                    Transitions.Add(transition);
                }

                HasStarted = false;
            }
            catch
            {
                Dispose();
                throw;
            }
        }


        // 启动
        public void StartUp()
        {
            if (IsDisposed || HasStarted || _isClosing)
            {
                return;
            }

            HasStarted = true;
            // 找到默认状态 切换
            SwitchState(DefaultState, null);
        }

        public void Close()
        {
            if (_isClosing)
            {
                return;
            }

            _isClosing = true;
            HasStarted = false;
            try
            {
                ExitState(string.Empty, null);
            }
            finally
            {
                _currentStates.Clear();
                _triggerQueue.Clear();
                _triggerKeys.Clear();
                _transitionBudget.Clear();
                _loopErrorFrame = -1;
                CurrentTransition = null;
                ResetParamter();
                _isClosing = false;
            }
        }

        /// <summary>归还本实例拥有的配置克隆；Close仅重置，不销毁以便重新启用。</summary>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;
            try
            {
                Close();
            }
            finally
            {
                foreach (var parameter in Parameters.Values)
                {
                    parameter.ValueChanged = null;
                }

                Parameters.Clear();
                ParameterDefaults.Clear();
                States.Clear();
                Transitions.Clear();
                if (RuntimeFsmController)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(RuntimeFsmController);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(RuntimeFsmController);
                    }
                }
                RuntimeFsmController = null;
            }
        }
        internal void SwitchState(FsmStateNode state, FsmTransition transition)
        {
            if (!HasStarted || IsDisposed || _isClosing)
            {
                return;
            }

            if (state != null && !_transitionBudget.TryEnter(Time.frameCount, state.Data.name))
            {
                if (_loopErrorFrame != Time.frameCount)
                {
                    _loopErrorFrame = Time.frameCount;
                    Debug.LogError($"Asset '{Name}', layer '{LayerName}', state '{state.Data.name}': transition cycle budget exceeded in frame {Time.frameCount}. Check transition conditions.", FsmController);
                }
                return;
            }

            string parent = state != null ? state.Data.Parent : string.Empty;

            // 退出当前状态
            ExitState(parent, state);
            EnterState(state);

            if (!HasStarted || IsDisposed)
            {
                return;
            }

            CurrentTransition = transition;

            // 检测当前状态是否有已经满足的条件
            CheckConditionAndSwitch();
        }


        private void CheckConditionAndSwitch()
        {
            if (!HasStarted || IsDisposed || _isClosing)
            {
                return;
            }
            // 检测当前的状态是否有已经满足条件的过渡
            foreach (var item in Transitions)
            {
                // 检测条件是否满足
                if (item.CheckConditionAndSwitch())
                {
                    break;
                }
            }
        }

        private void ExitState(string parent, FsmStateNode nextState)
        {
            if (!_currentStates.ContainsKey(parent))
            {
                return;
            }

            if (_currentStates[parent] == null)
            {
                return;
            }

            FsmStateNode currentState = _currentStates[parent];

            if (nextState != null)
            {
                currentState.NextState = nextState.Data.name;
            }

            if (nextState != null && nextState.Data.CompareParent(currentState.Data))
            {
                nextState.LastState = currentState.Data.name;
            }

            if (nextState != null)
            {
                nextState.NextState = string.Empty;  // 把下一个状态的值设置为空
            }
            // 移除状态
            _currentStates.Remove(parent);
            System.Exception firstError = null;
            try
            {
                currentState.OnExit();
            }
            catch (System.Exception error)
            {
                firstError = error;
            }

            // 判断一下当前状态是否子状态机
            if (currentState.Data.isSubStateMachine)
            {
                // 把子状态也退出掉
                try
                {
                    ExitState(currentState.Data.name, nextState);
                }
                catch (System.Exception error)
                {
                    firstError ??= error;
                }
            }
            if (firstError != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
            }
        }

        private void EnterState(FsmStateNode state)
        {
            if (state == null || !HasStarted || IsDisposed || _isClosing)
            {
                return;
            }

            string parent = state.Data.Parent;

            if (_currentStates.ContainsKey(parent))
            {
                _currentStates[parent] = state;
            }
            else
            {
                _currentStates.Add(parent, state);
            }

            state?.OnEnter();
            if (!HasStarted || IsDisposed || !state.IsRunning || GetCurrentState(parent) != state)
            {
                return;
            }
            // 触发事件
            FsmController.StateChanged?.Invoke(RuntimeFsmController.name, state.Data.name);
            if (!HasStarted || IsDisposed || GetCurrentState(parent) != state)
            {
                return;
            }

            // 判断是否有子状态机
            if (state.Data.isSubStateMachine)
            {
                // 进入到子状态机的默认状态
                FsmStateNodeData data = RuntimeFsmController.GetDefaultStateNodeData(state.Data.name);
                if (data != null && States.ContainsKey(data.name))
                {
                    EnterState(States[data.name]);
                }
            }

        }

        public void SetBool(int nameHash, bool v)
        {
            SetParamter(nameHash, v ? 1 : 0, ParameterType.Bool);
        }

        public void SetFloat(int nameHash, float v)
        {
            SetParamter(nameHash, v, ParameterType.Float);
        }


        public void SetInt(int nameHash, int v)
        {
            SetParamter(nameHash, v, ParameterType.Int);
        }


        public void SetTrigger(int name)
        {
            FsmParameterData parameter;
            if (Parameters.TryGetValue(name, out parameter))
            {
                if (parameter.parameterType != ParameterType.Trigger)
                {
                    return;
                }

                if (parameter.value == 1)
                {
                    _triggerQueue.Enqueue(name);
                }
                else
                {
                    SetParamter(name, 1, ParameterType.Trigger);
                }

            }
        }

        /// <summary>
        /// 当Trigger触发后还原trigger
        /// </summary>
        /// <param name="name"></param>
        internal void ClearTrigger(int name)
        {
            SetParamter(name, 0, ParameterType.Trigger);
        }

        //public void ResetTrigger(string name)
        //{
        //    ResetTrigger(FSMController.StringToHash(name));
        //}

        public void ResetTrigger(int name)
        {
            _triggerQueue.Reset(name);
            ClearTrigger(name);
        }

        private void SetParamter(int name, float v, ParameterType type)
        {
            if (IsDisposed || _isClosing)
            {
                return;
            }

            FsmParameterData parameter;
            if (Parameters.TryGetValue(name, out parameter))
            {
                if (parameter.parameterType == type)
                {
                    parameter.Value = v;
                }
            }
        }


        //internal float GetParamter(string name)
        //{
        //    return GetParamter(FSMController.StringToHash(name));
        //}

        internal float GetParamter(int name)
        {
            FsmParameterData parameter;
            if (Parameters.TryGetValue(name, out parameter))
            {
                return parameter.value;
            }

            return 0;
        }

        internal int GetTriggerCount(string name)
        {
            return GetTriggerCount(FsmController.StringToHash(name));
        }

        internal int GetTriggerCount(int name)
        {

            return _triggerQueue.GetCount(name);
        }


        public void Update()
        {
            if (!HasStarted || IsDisposed)
            {
                return;
            }

            _currentStatesArray.Clear();

            foreach (var item in _currentStates.Keys)
            {
                if (_currentStates[item] == null)
                {
                    continue;
                }

                _currentStatesArray.Add(_currentStates[item]);
            }

            foreach (var item in _currentStatesArray)
            {
                if (!HasStarted || IsDisposed)
                {
                    break;
                }

                if (!item.IsRunning)
                {
                    continue;
                }

                item.OnUpdate();
            }

            // 更新Trigger
            UpdateTrigger();
        }

        public void LateUpdate()
        {
            if (!HasStarted || IsDisposed)
            {
                return;
            }

            _currentStatesArray.Clear();

            foreach (var item in _currentStates.Keys)
            {
                if (_currentStates[item] == null)
                {
                    continue;
                }

                _currentStatesArray.Add(_currentStates[item]);
            }

            foreach (var item in _currentStatesArray)
            {
                if (!HasStarted || IsDisposed)
                {
                    break;
                }

                if (!item.IsRunning)
                {
                    continue;
                }

                item.OnLateUpdate();
            }
        }

        public void FixedUpdate()
        {
            if (!HasStarted || IsDisposed)
            {
                return;
            }

            _currentStatesArray.Clear();

            foreach (var item in _currentStates.Keys)
            {
                if (_currentStates[item] == null)
                {
                    continue;
                }

                _currentStatesArray.Add(_currentStates[item]);
            }

            foreach (var item in _currentStatesArray)
            {
                if (!HasStarted || IsDisposed)
                {
                    break;
                }

                if (!item.IsRunning)
                {
                    continue;
                }

                item.OnFixedUpdate();
            }
        }



        private void UpdateTrigger()
        {
            _triggerQueue.CopyKeysTo(_triggerKeys);
            for (int i = 0; i < _triggerKeys.Count && HasStarted && !IsDisposed; i++)
            {
                int item = _triggerKeys[i];
                if (_triggerQueue.TryTake(item, GetParamter(item) == 0))
                {
                    SetParamter(item, 1, ParameterType.Trigger);
                }
            }
        }

        public FsmStateNode GetCurrentState(string parent)
        {

            if (_currentStates.ContainsKey(parent))
            {
                return _currentStates[parent];
            }

            return null;
        }
        internal void ResetParamter()
        {
            foreach (var item in ParameterDefaults.Keys)
            {
                FsmParameterData parameter;
                if (Parameters.TryGetValue(item, out parameter))
                {
                    parameter.value = ParameterDefaults[item];
                }
            }
        }
    }
}
