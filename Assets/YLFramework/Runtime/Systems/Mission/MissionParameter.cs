using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
using YLFramework.Events;
using YLFramework.Foundation;

namespace YLFramework.MissionSystem
{
    /// <summary>
    /// 任务参数类型
    /// </summary>
    public enum MissionParamType
    {
        Float,
        Int,
        Bool
    }

    /// <summary>
    /// 参数
    /// </summary>
    [Serializable]
    public class MissionParameter
    {

        #region 字段

        /// <summary>
        /// 参数名称
        /// </summary>
        [Tooltip("参数名称")]
        [Header("参数名称")]
        [YLFramework.Excel.ExcelScriptableObjectId]
        public string name;

        /// <summary>
        /// 参数类型
        /// </summary>
        [Tooltip("参数类型")]
        [Header("参数类型")]
        public MissionParamType type;

        [Tooltip("当前参数的值")]
        private float _value = 0;

        /// <summary>
        /// 只能在任务执行中设置参数,如果参数对应的任务不在执行中,则无法设置!(默认:true)
        /// </summary>
        [Tooltip("只能在任务执行中设置参数,如果参数对应的任务不在执行中,则无法设置!(默认:true)")]
        [Header("只能在任务执行中设置参数,如果参数对应的任务不在执行中,则无法设置!(默认:true)")]
        [FormerlySerializedAs("only_can_set_in_progress"), JsonProperty("only_can_set_in_progress")] public bool onlySetInProgress = true;

        /// <summary>
        /// 设置参数时,设置的目标值比当前值大时生效,否则无效(默认:false)
        /// </summary>
        [Tooltip("设置参数时,设置的目标值比当前值大时生效,否则无效(默认:false)")]
        [Header("设置参数时,设置的目标值比当前值大时生效,否则无效(默认:false)")]
        [FormerlySerializedAs("only_can_set_larger"), JsonProperty("only_can_set_larger")] public bool onlySetLarger = false;

        /// <summary>
        /// 参数描述
        /// </summary>
        [Tooltip("参数描述")]
        [Header("参数描述")]
        [TextArea]
        [FormerlySerializedAs("des"), JsonProperty("des")] public string description;

        /// <summary>
        /// 自定义数据
        /// </summary>
        [SerializeField]
        [Tooltip("自定义数据")]
        [Header("自定义数据")]
        [JsonProperty("custom_data"), FormerlySerializedAs("custom_data")]
        private List<string> customData = new List<string>();
        #endregion

        #region 事件

        /// <summary>
        /// 内部使用的事件
        /// </summary>
        [NonSerialized] private EventChannel<MissionParameter> _changes;
        private int _version;
        internal int Version => _version;
        internal IUnregister RegisterChanged(Action<MissionParameter> callback) => (_changes ??= new EventChannel<MissionParameter>()).Register(callback);

        /// <summary>
        /// 值改变的事件
        /// </summary>
        [Tooltip("值改变的事件")]
        [JsonIgnore]
        public Action<MissionParameter> ValueChanged;
        #endregion


        #region 属性

        /// <summary>
        /// 当前参数的值
        /// </summary>
        public float Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (this._value.Equals(value))
                {
                    return;
                }

                var version = ++_version;
                this._value = value;
                _changes?.TriggerAll(this);
                if (version == _version)
                {
                    ValueChanged?.Invoke(this);
                }
            }
        }


        #endregion


        #region 方法

        internal void Reset()
        {
            _version++;
            ValueChanged = null;
            _changes?.Dispose();
            _changes = null;
            _value = 0;
        }

        /// <summary>独立复制配置与自定义数据，不复制运行值、监听或通知状态。</summary>
        public MissionParameter CloneConfiguration()
        {
            var clone = (MissionParameter)MemberwiseClone();
            clone._value = 0;
            clone.ValueChanged = null;
            clone._changes = null;
            clone._version = 0;
            clone.customData = customData == null ? new List<string>() : new List<string>(customData);
            return clone;
        }

        /// <summary>
        /// 是否包含自定义数据
        /// </summary>
        /// <param name="customData">自定义数据</param>
        /// <returns></returns>
        public bool ContainsCustomData(string customData)
        {
            if (string.IsNullOrEmpty(customData))
            {
                return false;
            }

            if (this.customData == null || this.customData.Count == 0)
            {
                return false;
            }

            return this.customData.Contains(customData);
        }



        public void SetValueWithoutNotify(float v)
        {
            this._value = v;
        }

        #endregion

    }

}
