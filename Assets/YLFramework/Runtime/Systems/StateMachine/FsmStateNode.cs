using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.StateMachine
{
    /// <summary>
    /// 状态节点
    /// </summary>
    public class FsmStateNode
    {
        #region 字段
        /// <summary>
        /// 状态数据
        /// </summary>
        public FsmStateNodeData Data;
        internal RuntimeFsmControllerInstance Instance;
        private List<FsmState> _states = new List<FsmState>();

        internal string LastState;
        internal string NextState;
        /// <summary>
        /// 是否正在执行中
        /// </summary>
        internal bool IsRunning = false;

        private readonly FsmCreationTracker _creation;
        private int _activationVersion;

        [Tooltip("当前状态名称Hash,可通过方法FSMController.StringToHash()转换!")]
        public int NameHash;

        #endregion

        #region 方法
        internal FsmStateNode(FsmStateNodeData data, RuntimeFsmControllerInstance instance)
        {
            this.Data = data;
            this.Instance = instance;
            NameHash = FsmController.StringToHash(data.name);

            _states.Clear();
            foreach (var item in this.Data.stateScripts)
            {
                Type type = YLFramework.Foundation.AssemblyTool.GetType(item.className);
                FsmState state = null;
                if (type != null)
                {
                    state = Activator.CreateInstance(type) as FsmState;
                }

                if (state != null)
                {
                    state.Controller = instance.FsmController;
                    state.UserData = instance.FsmController.UserDataValue;
                    state.CurrentStateInfo = this;
                    _states.Add(state);
                }
            }
            _creation = new FsmCreationTracker(_states.Count);

        }

        internal void OnEnter()
        {
            IsRunning = true;
            int version = ++_activationVersion;
            for (int i = 0; i < _states.Count; i++)
            {
                var item = _states[i];
                if (!IsRunning || version != _activationVersion || !Instance.HasStarted)
                {
                    break;
                }

                item.LastState = LastState;
                item.NextState = NextState;

                if (_creation.TryMarkCreated(i))
                {
                    item.OnCreate(); // 调用 OnCreate 的方法
                }

                if (!IsRunning || version != _activationVersion || !Instance.HasStarted)
                {
                    break;
                }

                item.OnEnter();
            }

        }

        internal void OnExit()
        {
            IsRunning = false;
            _activationVersion++;
            Exception firstError = null;
            for (int i = 0; i < _states.Count; i++)
            {
                if (!_creation.IsCreated(i))
                {
                    continue;
                }

                var item = _states[i];
                item.LastState = LastState;
                item.NextState = NextState;
                try
                {
                    item.OnExit();
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

        internal void OnUpdate()
        {
            foreach (var item in this._states)
            {
                if (!IsRunning || !Instance.HasStarted)
                {
                    break;
                }

                item.OnUpdate();
            }
        }

        internal void OnFixedUpdate()
        {
            foreach (var item in this._states)
            {
                if (!IsRunning || !Instance.HasStarted)
                {
                    break;
                }

                item.OnFixedUpdate();
            }
        }

        internal void OnLateUpdate()
        {
            foreach (var item in this._states)
            {
                if (!IsRunning || !Instance.HasStarted)
                {
                    break;
                }

                item.OnLateUpdate();
            }
        }

        #endregion
    }

}
