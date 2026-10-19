using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.StateMachine
{
    /// <summary>
    /// 状态节点
    /// </summary>
    public class FSMStateNode
    {
        #region 字段
        /// <summary>
        /// 状态数据
        /// </summary>
        public FSMStateNodeData data;
        internal RuntimeFSMControllerInstance instance;
        private List<FSMState> states = new List<FSMState>();

        internal string lastState;
        internal string nextState;
        /// <summary>
        /// 是否正在执行中
        /// </summary>
        internal bool isRuning = false;

        private readonly FSMCreationTracker creation;
        private int activationVersion;

        [Tooltip("当前状态名称Hash,可通过方法FSMController.StringToHash()转换!")]
        public int nameHash;

        #endregion

        #region 方法
        internal FSMStateNode(FSMStateNodeData data,RuntimeFSMControllerInstance instance)
        {
            this.data = data;
            this.instance = instance;
            nameHash = FSMController.StringToHash(data.name);

            states.Clear();
            foreach (var item in this.data.StateScripts)
            {
                Type type = YLFramework.Foundation.AssemblyTool.GetType(item.className);
                FSMState state = null;
                if (type != null)
                    state = Activator.CreateInstance(type) as FSMState;

                if (state != null)
                {
                    state.controller = instance.FSMController;
                    state.userData = instance.FSMController.userData;
                    state.currentStateInfo = this;
                    states.Add(state);
                }
            }
            creation = new FSMCreationTracker(states.Count);

        }

        internal void OnEnter()
        {
            isRuning = true;
            int version = ++activationVersion;
            for (int i = 0; i < states.Count; i++)
            {
                var item = states[i];
                if (!isRuning || version != activationVersion || !instance.Started) break;
                item.lastState = lastState;
                item.nextState = nextState;

                if (creation.TryMarkCreated(i))
                    item.OnCreate(); // 调用 OnCreate 的方法
                if (!isRuning || version != activationVersion || !instance.Started) break;
                item.OnEnter();
            }

        }

        internal void OnExit()
        {
            isRuning = false;
            activationVersion++;
            Exception firstError = null;
            for (int i = 0; i < states.Count; i++)
            {
                if (!creation.IsCreated(i)) continue;
                var item = states[i];
                item.lastState = lastState;
                item.nextState = nextState;
                try { item.OnExit(); }
                catch (Exception error) { firstError ??= error; }
            }
            if (firstError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstError).Throw();
        }

        internal void OnUpdate()
        {
            foreach (var item in this.states)
            {
                if (!isRuning || !instance.Started) break;
                item.OnUpdate();
            }
        }

        internal void OnFixedUpdate()
        {
            foreach (var item in this.states)
            {
                if (!isRuning || !instance.Started) break;
                item.OnFixedUpdate();
            }
        }

        internal void OnLateUpdate()
        {
            foreach (var item in this.states)
            {
                if (!isRuning || !instance.Started) break;
                item.OnLateUpdate();
            }
        }

        #endregion
    }

}
