using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace YLFramework.StateMachine
{
    public class RuntimeFsmController : ScriptableObject
    {

#if UNITY_EDITOR
        /// <summary>
        /// 背景线的中心点坐标
        /// </summary>
        [HideInInspector]
        public Vector3 viewPosition = new Vector3(100, 100, 0);
        [HideInInspector]
        public Vector3 viewScale = Vector3.one * 0.8f;

        /// <summary>
        /// 当前用户选择的状态机层级
        /// </summary>
        [HideInInspector]
        [FormerlySerializedAs("Layers")] public List<string> layers = new List<string>();

        /// <summary>
        /// 实例化该对象的源文件的GUID
        /// </summary>
        [HideInInspector]
        [FormerlySerializedAs("originGUID")] public string originGuid;

        public void AddLayer(string layer)
        {
            if (layers.Contains(layer))
            {
                return;
            }

            layers.Add(layer);
            Save();
        }

        public void RemoveLayer(int index, int count)
        {
            layers.RemoveRange(index, count);
            Save();
        }

        /// <summary>
        /// 清空当前用户选择的状态层级
        /// </summary>
        public void ClearLayer()
        {
            layers.Clear();
            Save();
        }
#endif

        /// <summary>
        /// 所有的状态
        /// </summary>
        [HideInInspector]
        public List<FsmStateNodeData> states = new List<FsmStateNodeData>();

        /// <summary>
        /// 所有的参数
        /// </summary>
        [HideInInspector]
        public List<FsmParameterData> parameters = new List<FsmParameterData>();

        /// <summary>
        /// 保存所有的过渡
        /// </summary>
        [HideInInspector]
        public List<FsmTransitionData> transitions = new List<FsmTransitionData>();

        /// <summary>在克隆和状态脚本实例化前定位配置中的无效字段。</summary>
        public bool ValidateConfiguration(out string error)
        {
            error = FsmConfigurationValidator.Validate(states, parameters, transitions);
            if (error != null)
            {
                return false;
            }

            foreach (var state in states)
            {
                foreach (var script in state.stateScripts)
                {
                    var type = script == null ? null : YLFramework.Foundation.AssemblyTool.GetType(script.className);
                    if (type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(FsmState).IsAssignableFrom(type) && type.GetConstructor(System.Type.EmptyTypes) != null)
                    {
                        continue;
                    }

                    error = $"state '{state.name}'.StateScripts className '{script?.className}' must resolve to a concrete FSMState with a public parameterless constructor.";
                    return false;
                }
            }

            return true;
        }


        private Dictionary<string, FsmStateNodeData> _statesDic = new Dictionary<string, FsmStateNodeData>();
        private Dictionary<string, FsmParameterData> _parametersDic = new Dictionary<string, FsmParameterData>();
        private Dictionary<string, FsmStateNodeData> _defaultStatesDic = new Dictionary<string, FsmStateNodeData>();
        private Dictionary<string, FsmTransitionData> _transitionsDic = new Dictionary<string, FsmTransitionData>();


        public FsmStateNodeData GetStateNodeData(string name)
        {

            if (name == null)
            {
                return null;
            }

            if (_statesDic.Count == 0)
            {
                foreach (FsmStateNodeData state in states)
                {
                    if (_statesDic.ContainsKey(state.name))
                    {
                        throw new System.Exception(string.Format("状态名称重复:{0}", state.name));
                    }

                    _statesDic.Add(state.name, state);
                }
            }

            if (_statesDic.ContainsKey(name))
            {
                return _statesDic[name];
            }

            return null;
        }

        public FsmParameterData GetParameterData(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (name == null)
            {
                return null;
            }

            if (_parametersDic.Count == 0)
            {
                foreach (var item in parameters)
                {
                    if (_parametersDic.ContainsKey(item.name))
                    {
                        throw new System.Exception(string.Format("参数名称重复:{0}", item.name));
                    }

                    _parametersDic.Add(item.name, item);
                }
            }

            if (_parametersDic.ContainsKey(name))
            {
                return _parametersDic[name];
            }

            return null;
        }

        /// <summary>
        /// 获取某一个层级的默认状态
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public FsmStateNodeData GetDefaultStateNodeData(string parent)
        {
            if (parent == null)
            {
                return null;
            }

            if (_defaultStatesDic.Count == 0)
            {
                foreach (var item in states)
                {
                    if (!item.isDefaultState)
                    {
                        continue;
                    }

                    if (_defaultStatesDic.ContainsKey(item.Parent))
                    {
                        throw new System.Exception(string.Format("检测到层级:{0}有多个默认状态!", item.Parent));
                    }

                    _defaultStatesDic.Add(item.Parent, item);
                }
            }

            if (_defaultStatesDic.ContainsKey(parent))
            {
                return _defaultStatesDic[parent];
            }

            return null;
        }

        public FsmTransitionData GetTransitionData(string from, string to)
        {

            if (_transitionsDic.Count == 0)
            {
                foreach (var item in transitions)
                {
                    _transitionsDic.Add(item.Key, item);
                }
            }

            string key = string.Format("{0}:{1}", from, to);

            if (_transitionsDic.ContainsKey(key))
            {
                return _transitionsDic[key];
            }

            return null;
        }

#if UNITY_EDITOR

        private Dictionary<string, List<FsmStateNodeData>> _currentShowStates = new Dictionary<string, List<FsmStateNodeData>>();
        private Dictionary<string, List<FsmTransitionData>> _currentShowTransitions = new Dictionary<string, List<FsmTransitionData>>();



        public void AddState(FsmStateNodeData state)
        {
            ClearCache();
            states.Add(state);
            Save();
        }

        public void RemoveState(FsmStateNodeData state)
        {
            ClearCache();
            states.Remove(state);
            Save();
        }

        public void AddParameters(FsmParameterData data)
        {
            ClearCache();
            parameters.Add(data);
            Save();
        }

        public void RemoveParameters(FsmParameterData data)
        {
            ClearCache();
            parameters.Remove(data);
            Save();
        }

        public void AddTransition(FsmTransitionData data)
        {
            ClearCache();
            transitions.Add(data);
            Save();
        }

        public void RemoveTransition(FsmTransitionData data)
        {
            ClearCache();
            transitions.Remove(data);
            Save();
        }


        public void ClearCache()
        {
            _statesDic.Clear();
            _parametersDic.Clear();
            _defaultStatesDic.Clear();
            _currentShowStates.Clear();
            _currentShowTransitions.Clear();
            _transitionsDic.Clear();
        }


        public List<FsmStateNodeData> GetCurrentShowStateNodeData(string parent)
        {

            if (parent == null)
            {
                return null;
            }

            if (_currentShowStates.Count == 0)
            {
                foreach (var item in states)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (_currentShowStates.ContainsKey(item.Parent))
                    {
                        _currentShowStates[item.Parent].Add(item);
                    }
                    else
                    {
                        _currentShowStates.Add(item.Parent, new List<FsmStateNodeData> { item });
                    }
                }
            }

            if (_currentShowStates.ContainsKey(parent))
            {
                List<FsmStateNodeData> currentShowState = _currentShowStates[parent];
                if (currentShowState.Count == 0)
                {
                    ClearLayer();
                }

                return currentShowState;
            }

            ClearLayer();

            return null;
        }

        public List<FsmTransitionData> GetCurrentShowTransitionData(string parent)
        {

            if (parent == null)
            {
                return null;
            }

            if (_currentShowTransitions.Count == 0)
            {
                foreach (var item in transitions)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    FsmStateNodeData from = states.Find(state => state != null && state.name == item.fromStateName);
                    FsmStateNodeData to = states.Find(state => state != null && state.name == item.toStateName);

                    if (from == null || to == null || !from.CompareParent(to))
                    {
                        continue;
                    }

                    if (_currentShowTransitions.ContainsKey(from.Parent))
                    {
                        _currentShowTransitions[from.Parent].Add(item);
                    }
                    else
                    {
                        _currentShowTransitions.Add(from.Parent, new List<FsmTransitionData>() { item });
                    }
                }
            }

            if (_currentShowTransitions.ContainsKey(parent))
            {
                return _currentShowTransitions[parent];
            }

            return null;
        }


#endif


        public void Save()
        {
#if UNITY_EDITOR
            FsmEditorHooks.Save?.Invoke(this);
#endif
        }

        public void RefreshStateScripts()
        {
#if UNITY_EDITOR
            foreach (var state in states)
            {
                state.RefreshStateScripts(this);
            }
#endif
        }


    }

}
