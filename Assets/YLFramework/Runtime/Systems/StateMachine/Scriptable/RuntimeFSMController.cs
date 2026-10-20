using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.StateMachine
{
    public class RuntimeFSMController : ScriptableObject
    {

#if UNITY_EDITOR
        /// <summary>
        /// 背景线的中心点坐标
        /// </summary>
        [HideInInspector]
        public Vector3 viewPosition = new Vector3(100,100,0);
        [HideInInspector]
        public Vector3 viewScale = Vector3.one * 0.8f;

        /// <summary>
        /// 当前用户选择的状态机层级
        /// </summary>
        [HideInInspector]
        public List<string> Layers = new List<string>();

        /// <summary>
        /// 实例化该对象的源文件的GUID
        /// </summary>
        [HideInInspector]
        public string originGUID;

        public void AddLayer(string layer)
        {
            if(Layers.Contains(layer))
                return;
            Layers.Add(layer);
            Save();
        }

        public void RemoveLayer(int index, int count)
        {
            Layers.RemoveRange(index, count);
            Save();
        }

        /// <summary>
        /// 清空当前用户选择的状态层级
        /// </summary>
        public void ClearLayer()
        {
            Layers.Clear();
            Save();
        }
#endif

        /// <summary>
        /// 所有的状态
        /// </summary>
        [HideInInspector]
        public List<FSMStateNodeData> states = new List<FSMStateNodeData>();

        /// <summary>
        /// 所有的参数
        /// </summary>
        [HideInInspector]
        public List<FSMParameterData> parameters = new List<FSMParameterData>();

        /// <summary>
        /// 保存所有的过渡
        /// </summary>
        [HideInInspector]
        public List<FSMTransitionData> transitions = new List<FSMTransitionData>();

        /// <summary>在克隆和状态脚本实例化前定位配置中的无效字段。</summary>
        public bool ValidateConfiguration(out string error)
        {
            error = FSMConfigurationValidator.Validate(states, parameters, transitions);
            if (error != null) return false;
            foreach (var state in states)
                foreach (var script in state.StateScripts)
                {
                    var type = script == null ? null : YLFramework.Foundation.AssemblyTool.GetType(script.className);
                    if (type != null && !type.IsAbstract && !type.ContainsGenericParameters && typeof(FSMState).IsAssignableFrom(type) && type.GetConstructor(System.Type.EmptyTypes) != null) continue;
                    error = $"state '{state.name}'.StateScripts className '{script?.className}' must resolve to a concrete FSMState with a public parameterless constructor.";
                    return false;
                }
            return true;
        }


        private Dictionary<string,FSMStateNodeData> states_dic = new Dictionary<string, FSMStateNodeData> ();
        private Dictionary<string,FSMParameterData> parameters_dic = new Dictionary<string, FSMParameterData>();
        private Dictionary<string, FSMStateNodeData> default_states_dic = new Dictionary<string, FSMStateNodeData>();
        private Dictionary<string, FSMTransitionData> transitions_dic = new Dictionary<string, FSMTransitionData>();


        public FSMStateNodeData GetStateNodeData(string name) {

            if (name == null)
                return null;

            if (states_dic.Count == 0) {
                foreach (FSMStateNodeData state in states)
                {
                    if (states_dic.ContainsKey(state.name))
                        throw new System.Exception(string.Format("状态名称重复:{0}", state.name));
                    states_dic.Add (state.name, state);
                }
            }

            if(states_dic.ContainsKey(name))
                return states_dic[name];

            return null;
        }

        public FSMParameterData GetParameterData(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if(name == null)
                return null;

            if (parameters_dic.Count == 0) {
                foreach (var item in parameters)
                {
                    if (parameters_dic.ContainsKey(item.name))
                        throw new System.Exception(string.Format("参数名称重复:{0}", item.name));
                    parameters_dic.Add (item.name, item);
                }
            }

            if(parameters_dic.ContainsKey(name))
                return parameters_dic[name];

            return null;
        }

        /// <summary>
        /// 获取某一个层级的默认状态
        /// </summary>
        /// <param name="parent"></param>
        /// <returns></returns>
        public FSMStateNodeData GetDefaultStateNodeData(string parent)
        {
            if (parent == null)
                return null;

            if (default_states_dic.Count == 0)
            {
                foreach (var item in states)
                {
                    if (!item.defaultState) continue;
                    if (default_states_dic.ContainsKey(item.Parent))
                        throw new System.Exception(string.Format("检测到层级:{0}有多个默认状态!",item.Parent));
                    default_states_dic.Add(item.Parent, item);
                }
            }

            if(default_states_dic.ContainsKey(parent))
                return default_states_dic[parent];

            return null;
        }

        public FSMTransitionData GetTransitionData(string from, string to) {

            if (transitions_dic.Count == 0) {
                foreach (var item in transitions)
                {
                    transitions_dic.Add(item.Key, item);
                }
            }

            string key = string.Format("{0}:{1}",from,to);

            if(transitions_dic.ContainsKey(key))
                return transitions_dic[key];

            return null;
        }

#if UNITY_EDITOR

        private Dictionary<string, List<FSMStateNodeData>> current_show_states = new Dictionary<string, List<FSMStateNodeData>>();
        private Dictionary<string, List<FSMTransitionData>> current_show_transitions = new Dictionary<string, List<FSMTransitionData>>();



        public void AddState(FSMStateNodeData state) {
            ClearCache();
            states.Add(state);
            Save();
        }

        public void RemoveState(FSMStateNodeData state)
        {
            ClearCache();
            states.Remove(state);
            Save();
        }

        public void AddParameters(FSMParameterData data) {
            ClearCache();
            parameters.Add(data);
            Save();
        }

        public void RemoveParameters(FSMParameterData data) {
            ClearCache();
            parameters.Remove(data);
            Save();
        }

        public void AddTransition(FSMTransitionData data) {
            ClearCache();
            transitions.Add(data);
            Save();
        }

        public void RemoveTransition(FSMTransitionData data) {
            ClearCache();
            transitions.Remove(data);
            Save();
        }


        public void ClearCache()
        {
            states_dic.Clear();
            parameters_dic.Clear();
            default_states_dic.Clear();
            current_show_states.Clear();
            current_show_transitions.Clear();
            transitions_dic.Clear();
        }


        public List<FSMStateNodeData> GetCurrentShowStateNodeData(string parent)
        {

            if (parent == null)
                return null;

            if (current_show_states.Count == 0)
            {
                foreach (var item in states)
                {
                    if (item == null) continue;
                    if (current_show_states.ContainsKey(item.Parent))
                    {
                        current_show_states[item.Parent].Add(item);
                    }
                    else {
                        current_show_states.Add(item.Parent,new List<FSMStateNodeData> { item });
                    }
                }
            }

            if (current_show_states.ContainsKey(parent))
            {
                List<FSMStateNodeData> current_show_state = current_show_states[parent];
                if (current_show_state.Count == 0)
                    ClearLayer();
                return current_show_state;
            }

            ClearLayer();

            return null;
        }

        public List<FSMTransitionData> GetCurrentShowTransitionData(string parent)
        {

            if (parent == null)
                return null;

            if (current_show_transitions.Count == 0) {
                foreach (var item in transitions)
                {
                    if (item == null) continue;
                    FSMStateNodeData from = states.Find(state => state != null && state.name == item.fromStateName);
                    FSMStateNodeData to = states.Find(state => state != null && state.name == item.toStateName);

                    if (from == null || to == null || !from.CompareParent(to)) continue;

                    if (current_show_transitions.ContainsKey(from.Parent))
                        current_show_transitions[from.Parent].Add(item);
                    else
                        current_show_transitions.Add(from.Parent, new List<FSMTransitionData>() { item});
                }
            }

            if(current_show_transitions.ContainsKey(parent))
                return current_show_transitions[parent];


            return null;
        }


#endif


        public void Save() {
#if UNITY_EDITOR
            FSMEditorHooks.Save?.Invoke(this);
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
