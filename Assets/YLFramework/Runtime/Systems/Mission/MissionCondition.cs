using YLFramework.Foundation;
using System;
using Newtonsoft.Json;
using UnityEngine;
using YLFramework.Events;

namespace YLFramework.MissionSystem
{

    /// <summary>
    /// 参数比较类型
    /// </summary>
    public enum CompareType
    {
        /// <summary>
        /// 大于
        /// </summary>
        [Tooltip("大于")]
        Greater,
        /// <summary>
        /// 小于
        /// </summary>
        [Tooltip("小于")]
        Less,
        /// <summary>
        /// 等于
        /// </summary>
        [Tooltip("等于")]
        Equals,
        /// <summary>
        /// 不等于
        /// </summary>
        [Tooltip("不等于")]
        NotEquals,
        /// <summary>
        /// 大于等于
        /// </summary>
        [Tooltip("大于等于")]
        GreaterEqual,
        /// <summary>
        /// 小于等于
        /// </summary>
        [Tooltip("小于等于")]
        LessEqual
    }

    /// <summary>
    /// 任务条件
    /// </summary>
    [Serializable]
    public class MissionCondition
    {

        #region 字段
        /// <summary>
        /// 参数名称
        /// </summary>
        [Tooltip("参数名称")]
        public string param_name;
        /// <summary>
        /// 比较方式
        /// </summary>
        [Tooltip("比较方式")]
        public CompareType compareType;
        /// <summary>
        /// 目标值
        /// </summary>
        [Tooltip("目标值")]
        public float target;


        private bool isMeet = false;

        private int config_name;

        private int param_name_hash;
        [NonSerialized] private MissionParameter _parameter;
        [NonSerialized] private IUnRegister _subscription;
        private int _version;

        #endregion

        #region 属性

        /// <summary>
        /// 参数对象
        /// </summary>
        [JsonIgnoreAttribute]
        public MissionParameter Param => _parameter;

        /// <summary>
        /// 条件是否已经满足
        /// </summary>
        [JsonIgnoreAttribute]
        public bool IsMeet
        {
            get
            {
                return isMeet;
            }
            set
            {
                if (isMeet == value) return;
                isMeet = value;
                if (isMeet)
                    onMeet?.Invoke();
            }
        }

        /// <summary>
        /// 进度
        /// </summary>
        [JsonIgnoreAttribute]
        public float Progress
        {
            get
            {
                if(Param == null)
                    return 0;

                if (IsMeet) // 条件已经满足 进度为1
                    return 1;

                switch (Param.type)
                {
                    case MissionParamType.Float:
                    case MissionParamType.Int:

                        // 如果比较方式是 等于 或者 不等于 , 这种要么完成 要么未完成 , 进度是0 或1 , 走到这里说明未完成,return 0即可!
                        if (compareType != CompareType.Greater && compareType != CompareType.GreaterEqual)
                            return 0;

                        if (target == 0) return 0;
                        return Mathf.Clamp01(Param.Value / target);
                    case MissionParamType.Bool:
                        int p = (int)Param.Value;
                        int t = (int)target;
                        return p == t ? 1 : 0;
                }

                return 0;
            }
        }

        /// <summary>
        /// 参数名称的hash值
        /// </summary>
        [JsonIgnoreAttribute]
        public int ParamNameHash => param_name_hash;

        #endregion



        #region 事件

        /// <summary>
        /// 当条件满足时触发
        /// </summary>
        [NonSerialized]
        [Tooltip("当条件满足时触发")]
        public Action onMeet;

        /// <summary>
        /// 当进度改变时触发
        /// </summary>
        [NonSerialized]
        [Tooltip("当进度改变时触发")]
        public Action<MissionCondition> onProgressValueChange;

        #endregion


        #region 方法

        internal void Init(int config_name)
        {
            this.config_name = config_name;
            param_name_hash = MissionManager.StringToHash(param_name);
            Bind(MissionManager.GetMissionParam(config_name, param_name_hash));
        }

        internal void Bind(MissionParameter parameter)
        {
            Dispose();
            _parameter = parameter ?? throw new InvalidOperationException($"任务条件参数未登记: {param_name}");
            _subscription = parameter.RegisterChanged(UpdateState);
            UpdateState(parameter);
        }

        internal void Dispose()
        {
            _version++;
            _subscription?.UnRegister();
            _subscription = null;
            _parameter = null;
            isMeet = false;
            onMeet = null;
            onProgressValueChange = null;
        }


        internal void UpdateState(MissionParameter p)
        {
            var version = _version;
            var parameterVersion = p.Version;
            var wasMeet = isMeet;
            switch (p.type)
            {
                case MissionParamType.Float:

                    switch (compareType)
                    {
                        case CompareType.Greater:
                            isMeet = p.Value > target;
                            break;
                        case CompareType.Less:
                            isMeet = p.Value < target;
                            break;
                        case CompareType.GreaterEqual:
                            isMeet = p.Value >= target;
                            break;
                        case CompareType.LessEqual:
                            isMeet = p.Value <= target;
                            break;
                        case CompareType.Equals:
                            isMeet = p.Value == target;
                            break;
                        case CompareType.NotEquals:
                            isMeet = p.Value != target;
                            break;
                    }

                    break;
                case MissionParamType.Int:

                    float v = Mathf.RoundToInt(p.Value);
                    float t = Mathf.RoundToInt(target);

                    switch (compareType)
                    {
                        case CompareType.Greater:
                            isMeet = v > t;
                            break;
                        case CompareType.Less:
                            isMeet = v < t;
                            break;
                        case CompareType.Equals:
                            isMeet = v == t;
                            break;
                        case CompareType.NotEquals:
                            isMeet = v != t;
                            break;

                        case CompareType.GreaterEqual:
                            isMeet = v >= t;
                            break;

                        case CompareType.LessEqual:
                            isMeet = v <= t;
                            break;
                    }

                    break;
                case MissionParamType.Bool:
                    isMeet = Mathf.RoundToInt(p.Value) == Mathf.RoundToInt(target);
                    break;
            }
            // 先计算当前满足状态，回调读取Progress/IsMeet时不再落后一拍。
            onProgressValueChange?.Invoke(this);
            if (version == _version && parameterVersion == p.Version && isMeet && !wasMeet) onMeet?.Invoke();
        }

        internal void Reset()
        {
            IsMeet = false;
        }

        #endregion

    }

}
