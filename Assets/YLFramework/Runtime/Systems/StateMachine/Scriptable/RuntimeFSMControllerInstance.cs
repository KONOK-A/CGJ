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
    public class RuntimeFSMControllerInstance
    {

        public string name;

        public string layerName;

        public RuntimeFSMController RuntimeFSMController;
        public RuntimeFSMController SourceController { get; }
        public bool Disposed { get; private set; }
        internal bool ResettingTriggers { get; set; }
        private bool closing;

        // 所有的参数
        internal Dictionary<int, FSMParameterData> parameters = new Dictionary<int, FSMParameterData>();

        [Tooltip("参数的默认值")]
        internal Dictionary<int, float> parameter_default = new Dictionary<int, float>();

        // 所有的状态
        internal Dictionary<string, FSMStateNode> states = new Dictionary<string, FSMStateNode>();

        internal List<FSMTransition> transitions = new List<FSMTransition>();

        // 默认状态
        internal FSMStateNode defaultState { get; private set; } = null;

        private Dictionary<string, FSMStateNode> current_states = new Dictionary<string, FSMStateNode>();

        private List<FSMStateNode> current_states_array = new List<FSMStateNode>();

        // 当前的过渡
        public FSMTransition currentTransition { get; private set; } = null;

        public FSMController FSMController { get; private set; }

        /// <summary>
        /// 状态切换计数(为了避免状态切换进入死循环)
        /// </summary>
        private readonly FSMTransitionBudget transitionBudget = new FSMTransitionBudget();

        /// <summary>
        /// 当前的帧数
        /// </summary>
        private int loopErrorFrame = -1;

        /// <summary>
        /// 记录Trigger触发的次数
        /// </summary>
        private readonly FSMTriggerQueue triggerQueue = new FSMTriggerQueue();

        private List<int> trigerKeys = new List<int>();

        public bool Started { get; private set; } = false;


        public RuntimeFSMControllerInstance(RuntimeFSMController runtimeFSMController, FSMController controller, string layerName)
        {
            FSMController = controller;
            SourceController = runtimeFSMController;
            this.layerName = layerName;
            // 把数据复制一份
            if (!runtimeFSMController) throw new System.ArgumentNullException(nameof(runtimeFSMController));
            if (!controller) throw new System.ArgumentNullException(nameof(controller));
            if (!runtimeFSMController.ValidateConfiguration(out string error)) throw new System.ArgumentException($"Asset '{runtimeFSMController.name}', layer '{layerName}': {error}", nameof(runtimeFSMController));
            name = runtimeFSMController.name;
            // 默认不执行任何状态
            current_states.Clear();

            RuntimeFSMController = GameObject.Instantiate(runtimeFSMController);
#if UNITY_EDITOR
            FSMEditorHooks.CloneCreated?.Invoke(runtimeFSMController, RuntimeFSMController);
#endif
            RuntimeFSMController.name = runtimeFSMController.name;
            try
            {
            // 参数
            parameters.Clear();
            for (int i = 0; i < RuntimeFSMController.parameters.Count; i++)
            {
                FSMParameterData data = RuntimeFSMController.parameters[i];

                int name_hash = FSMController.StringToHash(data.name);

                if (parameters.ContainsKey(name_hash))
                    continue;
                data.nameHash = name_hash;

                data.onValueChange = null;
                parameters.Add(name_hash, data);
                parameter_default.Add(name_hash, data.Value);
            }

            // 状态
            states.Clear();
            for (int i = 0; i < RuntimeFSMController.states.Count; i++)
            {
                FSMStateNodeData stateData = RuntimeFSMController.states[i];
                FSMStateNode state = new FSMStateNode(stateData, this);

                if (states.ContainsKey(stateData.name)) continue;

                if (stateData.defaultState && stateData.BaseLayer())
                {
                    defaultState = state;
                }

                states.Add(stateData.name, state);
            }
            // 过渡
            transitions.Clear();
            foreach (var item in RuntimeFSMController.transitions)
            {
                FSMTransition transition = new FSMTransition(this, item);
                transitions.Add(transition);
            }

            Started = false;
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
            if (Disposed || Started || closing) return;
            Started = true;
            // 找到默认状态 切换
            SwitchState(defaultState, null);
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            Started = false;
            try { ExitState(string.Empty, null); }
            finally
            {
                current_states.Clear();
                triggerQueue.Clear();
                trigerKeys.Clear();
                transitionBudget.Clear();
                loopErrorFrame = -1;
                currentTransition = null;
                ResetParamter();
                closing = false;
            }
        }

        /// <summary>归还本实例拥有的配置克隆；Close仅重置，不销毁以便重新启用。</summary>
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            try { Close(); }
            finally
            {
                foreach (var parameter in parameters.Values) parameter.onValueChange = null;
                parameters.Clear();
                parameter_default.Clear();
                states.Clear();
                transitions.Clear();
                if (RuntimeFSMController)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(RuntimeFSMController);
                    else UnityEngine.Object.DestroyImmediate(RuntimeFSMController);
                }
                RuntimeFSMController = null;
            }
        }
        internal void SwitchState(FSMStateNode state, FSMTransition transition)
        {
            if (!Started || Disposed || closing) return;
            if (state != null && !transitionBudget.TryEnter(Time.frameCount, state.data.name))
            {
                if (loopErrorFrame != Time.frameCount)
                {
                    loopErrorFrame = Time.frameCount;
                    Debug.LogError($"Asset '{name}', layer '{layerName}', state '{state.data.name}': transition cycle budget exceeded in frame {Time.frameCount}. Check transition conditions.", FSMController);
                }
                return;
            }

            string parent = state != null ? state.data.Parent : string.Empty;

            // 退出当前状态
            ExitState(parent, state);
            EnterState(state);

            if (!Started || Disposed) return;
            currentTransition = transition;

            // 检测当前状态是否有已经满足的条件
            CheckConditionAndSwitch();
        }


        private void CheckConditionAndSwitch()
        {
            if (!Started || Disposed || closing) return;
            // 检测当前的状态是否有已经满足条件的过渡
            foreach (var item in transitions)
            {
                // 检测条件是否满足
                if (item.CheckConditionAndSwitch())
                    break;
            }
        }

        private void ExitState(string parent, FSMStateNode nextState)
        {
            if (!current_states.ContainsKey(parent)) return;
            if (current_states[parent] == null) return;

            FSMStateNode currentState = current_states[parent];

            if (nextState != null)
                currentState.nextState = nextState.data.name;

            if (nextState != null && nextState.data.CompareParent(currentState.data))
                nextState.lastState = currentState.data.name;

            if (nextState != null)
                nextState.nextState = string.Empty;  // 把下一个状态的值设置为空
            // 移除状态
            current_states.Remove(parent);
            System.Exception firstError = null;
            try { currentState.OnExit(); }
            catch (System.Exception error) { firstError = error; }

            // 判断一下当前状态是否子状态机
            if (currentState.data.isSubStateMachine)
            {
                // 把子状态也退出掉
                try { ExitState(currentState.data.name, nextState); }
                catch (System.Exception error) { firstError ??= error; }
            }
            if (firstError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        private void EnterState(FSMStateNode state)
        {
            if (state == null || !Started || Disposed || closing) return;

            string parent = state.data.Parent;

            if (current_states.ContainsKey(parent))
                current_states[parent] = state;
            else
                current_states.Add(parent, state);

            state?.OnEnter();
            if (!Started || Disposed || !state.isRuning || GetCurrentState(parent) != state) return;
            // 触发事件
            FSMController.onStateChange?.Invoke(RuntimeFSMController.name, state.data.name);
            if (!Started || Disposed || GetCurrentState(parent) != state) return;

            // 判断是否有子状态机
            if (state.data.isSubStateMachine)
            {
                // 进入到子状态机的默认状态
                FSMStateNodeData data = RuntimeFSMController.GetDefaultStateNodeData(state.data.name);
                if (data != null && states.ContainsKey(data.name))
                    EnterState(states[data.name]);
            }

        }

        public void SetBool(int name_hash, bool v)
        {
            SetParamter(name_hash, v ? 1 : 0, ParameterType.Bool);
        }

        public void SetFloat(int name_hash, float v)
        {
            SetParamter(name_hash, v, ParameterType.Float);
        }


        public void SetInt(int name_hash, int v)
        {
            SetParamter(name_hash, v, ParameterType.Int);
        }


        public void SetTrigger(int name)
        {
            FSMParameterData parameter;
            if (parameters.TryGetValue(name, out parameter))
            {
                if (parameter.parameterType != ParameterType.Trigger)
                    return;

                if (parameter.value == 1)
                {
                    triggerQueue.Enqueue(name);
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
            triggerQueue.Reset(name);
            ClearTrigger(name);
        }

        private void SetParamter(int name, float v, ParameterType type)
        {
            if (Disposed || closing) return;
            FSMParameterData parameter;
            if (parameters.TryGetValue(name, out parameter))
            {
                if (parameter.parameterType == type)
                    parameter.Value = v;
            }
        }


        //internal float GetParamter(string name)
        //{
        //    return GetParamter(FSMController.StringToHash(name));
        //}

        internal float GetParamter(int name)
        {
            FSMParameterData parameter;
            if (parameters.TryGetValue(name, out parameter))
                return parameter.value;
            return 0;
        }

        internal int GetTriggerCount(string name)
        {
            return GetTriggerCount(FSMController.StringToHash(name));
        }

        internal int GetTriggerCount(int name)
        {

            return triggerQueue.GetCount(name);
        }


        public void Update()
        {
            if (!Started || Disposed) return;

            current_states_array.Clear();

            foreach (var item in current_states.Keys)
            {
                if (current_states[item] == null) continue;
                current_states_array.Add(current_states[item]);
            }

            foreach (var item in current_states_array)
            {
                if (!Started || Disposed) break;
                if (!item.isRuning) continue;
                item.OnUpdate();
            }

            // 更新Trigger
            UpdateTrigger();
        }

        public void LateUpdate()
        {
            if (!Started || Disposed) return;
            current_states_array.Clear();

            foreach (var item in current_states.Keys)
            {
                if (current_states[item] == null) continue;
                current_states_array.Add(current_states[item]);
            }

            foreach (var item in current_states_array)
            {
                if (!Started || Disposed) break;
                if (!item.isRuning) continue;
                item.OnLateUpdate();
            }
        }

        public void FixedUpdate()
        {
            if (!Started || Disposed) return;
            current_states_array.Clear();

            foreach (var item in current_states.Keys)
            {
                if (current_states[item] == null) continue;
                current_states_array.Add(current_states[item]);
            }

            foreach (var item in current_states_array)
            {
                if (!Started || Disposed) break;
                if (!item.isRuning) continue;
                item.OnFixedUpdate();
            }
        }



        private void UpdateTrigger()
        {
            triggerQueue.CopyKeysTo(trigerKeys);
            for (int i = 0; i < trigerKeys.Count && Started && !Disposed; i++)
            {
                int item = trigerKeys[i];
                if (triggerQueue.TryTake(item, GetParamter(item) == 0))
                    SetParamter(item, 1, ParameterType.Trigger);
            }
        }

        public FSMStateNode GetCurrentState(string parent)
        {

            if (current_states.ContainsKey(parent))
                return current_states[parent];
            return null;
        }
        internal void ResetParamter()
        {
            foreach (var item in parameter_default.Keys)
            {
                FSMParameterData parameter;
                if (parameters.TryGetValue(item, out parameter))
                {
                    parameter.value = parameter_default[item];
                }
            }
        }
    }
}
