using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Foundation;

namespace YLFramework.Mission
{

    /// <summary>
    /// 任务状态
    /// </summary>
    public enum MissionState
    {
        /// <summary>
        /// 未开始
        /// </summary>
        [Tooltip("未开始")]
        NotStarted,
        /// <summary>
        /// 执行中
        /// </summary>
        [Tooltip("执行中")]
        InProgress,
        /// <summary>
        /// 已完成
        /// </summary>
        [Tooltip("已完成")]
        Completed,
    }

    /// <summary>
    /// 任务基类
    /// </summary>
    public class MissionBase
    {

        #region 字段

        private MissionState _state = MissionState.NotStarted;
        private int _version;

        /// <summary>
        /// 自定义数据
        /// </summary>
        [Tooltip("自定义数据")]
        public object UserData;

        #endregion

        #region 属性

        /// <summary>
        /// 任务配置数据
        /// </summary>
        public MissionConfigData MissionConfig
        {
            get; internal set;
        }

        /// <summary>
        /// 任务状态
        /// </summary>
        public MissionState State
        {
            get
            {
                return _state;
            }
            set
            {
                if (_state == value)
                {
                    return;
                }

                ChangeState(value);
            }
        }

        internal int ConfigNameHash;

        /// <summary>
        /// 当前任务进度(0-1)
        /// </summary>
        public float Progress
        {
            get
            {
                if (State == MissionState.Completed)
                {
                    return 1;
                }

                if (!MissionConfig || MissionConfig.conditions.Count == 0)
                {
                    return 0;
                }

                if (MissionConfig.conditions.Count == 1)
                {
                    return MissionConfig.conditions[0].Progress;
                }

                float p = MissionConfig.conditions[0].Progress;

                for (int i = 1; i < MissionConfig.conditions.Count; i++)
                {
                    p *= MissionConfig.conditions[i].Progress;
                }

                return p;
            }
        }

        #endregion


        #region 事件

        /// <summary>
        /// 状态改变时触发
        /// </summary>
        [Tooltip("状态改变时触发")]
        public Action<MissionBase> StatusChanged;

        /// <summary>
        /// 进度改变时触发
        /// </summary>
        [Tooltip("进度改变时触发")]
        public Action<MissionBase, MissionCondition> ProgressValueChanged;

        #endregion


        #region 方法

        /// <summary>
        /// 完成任务的条件满足时触发
        /// </summary>
        private void OnCompleteConditionMeet()
        {
            // 如果该任务不是在执行中的状态 则不需要处理
            if (State != MissionState.InProgress)
            {
                return;
            }

            // 如果没有条件 任务则不能完成
            if (MissionConfig.conditions.Count == 0)
            {
                return;
            }

            // 判断完成的条件是否全都满足
            foreach (var item in MissionConfig.conditions)
            {
                if (!item.IsMet)
                {
                    return;
                }
            }

            // 任务已经完成
            var version = _version;
            State = MissionState.Completed;
            if (version == _version && State == MissionState.Completed)
            {
                OnMissionCompleted();
            }
        }

        private void OnConditionProgressChange(MissionCondition condition)
        {
            // 如果状态不是进行中 直接return
            if (State != MissionState.InProgress)
            {
                return;
            }

            var version = _version;
            OnProgressChange();
            if (version != _version || State != MissionState.InProgress)
            {
                return;
            }

            ProgressValueChanged?.Invoke(this, condition);
        }

        internal void Init()
        {
            _version++;
            // 初始化条件
            foreach (var item in MissionConfig.conditions)
            {
                item.Init(ConfigNameHash);
            }
        }

        internal void Reset()
        {
            _version++;
            if (MissionConfig)
            {
                RemoveListener();
                foreach (var condition in MissionConfig.conditions)
                {
                    condition.Dispose();
                }
            }
            // 清理事件
            StatusChanged = null;
            ProgressValueChanged = null;

            // 重置状态
            _state = MissionState.NotStarted;
            UserData = null;
        }

        internal void AddListener()
        {

            foreach (var item in MissionConfig.conditions)
            {
                item.Met -= OnCompleteConditionMeet;
                item.Met += OnCompleteConditionMeet;

                item.ProgressValueChanged -= OnConditionProgressChange;
                item.ProgressValueChanged += OnConditionProgressChange;
            }

        }

        internal void RemoveListener()
        {

            foreach (var item in MissionConfig.conditions)
            {
                item.Met -= OnCompleteConditionMeet;
                item.ProgressValueChanged -= OnConditionProgressChange;
            }
        }

        /// <summary>
        /// 任务状态改变时调用
        /// </summary>
        /// <param name="state">当前任务状态</param>
        protected virtual void OnStateChange(MissionState state)
        {

        }

        /// <summary>
        /// 任务进度改变时调用
        /// </summary>
        protected virtual void OnProgressChange()
        {

        }


        private void OnMissionCompleted()
        {
            var version = _version;
            if (!MissionConfig.resetParamOnCompleted)
            {
                return;
            }

            // 遍历所有条件 判断是否有别的任务引用该参数

            foreach (var condition in MissionConfig.conditions)
            {
                if (!ConditionIsCanReset(condition))
                {
                    continue;
                }
                // 对值进行重置
                if (condition.Param != null)
                {
                    condition.Param.Value = 0;
                }

                if (version != _version)
                {
                    return;
                }
            }

        }

        private bool ConditionIsCanReset(MissionCondition condition)
        {
            List<MissionBase> missions = MissionManager.GetParamRefMission(ConfigNameHash, condition.ParamNameHash);

            foreach (var mission in missions)
            {
                if (mission == this)
                {
                    continue;
                }

                if (mission.State == MissionState.InProgress)
                {
                    return false;
                }
            }

            return true;
        }


        /// <summary>
        /// 修改任务状态并且不会触发状态改变的事件
        /// </summary>
        /// <param name="state">任务状态</param>
        public void SetStateWithoutNotify(MissionState state)
        {
            if (this._state == state)
            {
                return;
            }

            ChangeState(state, false);
        }

        private void ChangeState(MissionState state, bool triggerEvent = true)
        {
            var version = _version;
            if (!MissionConfig)
            {
                throw new InvalidOperationException("任务已释放或尚未绑定配表。");
            }

            this._state = state;


            if (state == MissionState.InProgress)
            {
                // 监听条件改变的事件
                AddListener();
            }
            else
            {
                // 移除事件
                RemoveListener();
            }

            if (triggerEvent)
            {
                OnStateChange(state);
                if (_version != version || this._state != state)
                {
                    return;
                }

                StatusChanged?.Invoke(this);
            }

            // 如果任务状态被切换为执行中 这里判断一下是否已经完成
            if (version == _version && this._state == state && state == MissionState.InProgress)
            {
                OnCompleteConditionMeet();
            }
        }

        #endregion

    }
}
