using YLFramework.Foundation;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.SkillSystem
{

    public enum ReleaseSkillStatus
    {
        [Tooltip("成功")]
        Success,
        [Tooltip("该技能正在释放中")]
        Releasing,
        [Tooltip("有其他的不能同时释放的技能正在释放")]
        OtherReleasing,
        [Tooltip("当前技能释放条件不满足")]
        DonotMeetTheCondition,
        [Tooltip("该技能正在冷却中")]
        InCooling,
        [Tooltip("游戏角色当前的状态不能释放技能")]
        ReleaserCannotRelease
    }

    /// <summary>
    /// 技能基类
    /// </summary>
    public abstract class SkillBase
    {

        #region 字段
        // 能够同时释放的技能
        private List<int> canbe_released_at_the_same_time = new List<int>();
        // 是否能够主动取消
        private bool active_cancellation = false;


        private float fixedTimer = 0;


        private bool isCooldown = true;

        private float _duration;

        private float _cooling_time;
        private int _version;
        private bool _removing;
        internal int Version => _version;
        internal bool IsAttached => !_removing && Releaser != null;

        #endregion

        #region 属性

        /// <summary>
        /// 技能释放者(装备当前技能的游戏角色对象)
        /// </summary>
        public IEquippableSkill Releaser { get; set; }

        /// <summary>
        /// 技能配置数据
        /// </summary>
        public SkillConfigData SkillData { get;internal set; }

        /// <summary>
        /// 技能是否冷却完成(如果想要重置技能cd,可以直接把该属性设置为true)
        /// </summary>
        public bool IsCooldown
        {
            get {
                return isCooldown;
            }
            set
            {
                if (isCooldown == value) return;
                isCooldown = value;
                CooldownTimer = value ? Math.Max(0, CoolingTime) : 0;

                if (isCooldown)
                {
                    var version = _version;
                    onCooldownCompleteCallback?.Invoke(this);
                    if (version == _version) OnCooldownComplete();
                }

            }
        }

        /// <summary>
        /// 已冷却的时间
        /// </summary>
        public float CooldownTimer { get; set; }

        /// <summary>
        /// 冷却时间
        /// </summary>
        public virtual float CoolingTime
        {
            get
            {
                return _cooling_time;
            }
            set
            {
                _cooling_time = value;
            }
        }

        /// <summary>
        /// 是否正在释放中
        /// </summary>
        public bool IsReleasing { get; protected set; }

        /// <summary>
        /// 已释放的时间
        /// </summary>
        public float ReleasedTimer { get; protected set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public virtual float Duration
        {
            get
            {
                return _duration;
            }
            set {
                _duration = value;
            }
        }

        /// <summary>
        /// 冷却进度
        /// </summary>
        public float CooldownProgress
        {
            get
            {
                if (IsCooldown || CoolingTime <= 0) return 1;

                return Mathf.Clamp01(CooldownTimer / CoolingTime);
            }
        }

        /// <summary>
        /// 释放进度(0:开始释放 1:释放结束)
        /// </summary>
        public float ReleasingProgress
        {
            get
            {

                if (Duration == 0)
                    return 0;

                return Mathf.Clamp01(ReleasedTimer / Duration);
            }
        }

        /// <summary>
        /// 能够同时释放的技能的id集合
        /// </summary>
        public List<int> CanbeReleasedAtTheSameTime
        {
            get
            {
                return canbe_released_at_the_same_time;
            }
            set
            {
                this.canbe_released_at_the_same_time = value;
            }
        }

        /// <summary>
        /// 是否能够主动取消
        /// </summary>
        public bool ActiveCancellation
        {
            get
            {
                return active_cancellation;
            }
            set
            {
                active_cancellation = value;
            }
        }

        /// <summary>
        /// 获取当前技能释放状态, Success表示该技能可以释放
        /// </summary>
        public ReleaseSkillStatus ReleaseSkillStatus
        {
            get
            {
                if (!IsAttached || !Releaser.IsCanRelease())
                    return ReleaseSkillStatus.ReleaserCannotRelease;

                if (IsReleasing)
                    return ReleaseSkillStatus.Releasing;

                if (!IsCooldown)
                    return ReleaseSkillStatus.InCooling;

                if (!IsCanRelease())
                    return ReleaseSkillStatus.DonotMeetTheCondition;

                if (!IsCanbeReleaseAtSameTime())
                    return ReleaseSkillStatus.OtherReleasing;

                return ReleaseSkillStatus.Success;
            }
        }

        #endregion

        #region 事件

        [Tooltip("开始冷却的事件回调")]
        public Action<SkillBase> onStartCooldownCallback;

        [Tooltip("冷却中的事件回调")]
        public Action<SkillBase,float> onCoolingCallback;

        [Tooltip("冷却完成的事件回调")]
        public Action<SkillBase> onCooldownCompleteCallback;

        [Tooltip("开始释放的事件回调")]
        public Action<SkillBase> onStartReleaseCallback;

        [Tooltip("释放中的事件回调")]
        public Action<SkillBase,float> onReleasingCallback;

        [Tooltip("释放完成的事件回调")]
        public Action<SkillBase> onReleaseCompleteCallback;



        [Tooltip("中断的事件回调")]
        public Action<SkillBase> onInterruptCallback;

        #endregion

        #region 方法

        internal void Update() => Advance(Time.deltaTime);

        internal void Advance(float deltaTime)
        {
            if (!IsAttached) return;
            var version = _version;
            if (IsReleasing)
            {
                // 释放
                ReleasedTimer += deltaTime;

                onReleasingCallback?.Invoke(this, ReleasingProgress);
                if (version != _version || !IsAttached) return;
                OnUpdate();
                if (version != _version || !IsAttached) return;
                if (ReleasedTimer >= Duration && IsComplete())
                {
                    ReleasedTimer = 0;
                    IsReleasing = false;
                    onReleaseCompleteCallback?.Invoke(this);
                    if (version != _version || !IsAttached) return;
                    // 释放结束
                    OnReleaseComplete();
                }

            }
            else
            {
                // 判断是否正在冷却

                if (!IsCooldown)
                {
                    if (CooldownTimer == 0)
                    {
                        onStartCooldownCallback?.Invoke(this);
                        if (version != _version || !IsAttached) return;
                    }

                    CooldownTimer += deltaTime;
                    onCoolingCallback?.Invoke(this, CooldownProgress);
                    if (version != _version || !IsAttached) return;
                    // 冷却中
                    OnCooling();
                    if (version != _version || !IsAttached) return;
                    if (CooldownTimer >= CoolingTime)
                    {
                        IsCooldown = true;

                    }
                }

            }
        }


        internal void FixedUpdate()
        {
            // 这句代码是为了保证 FixedUpdate 和 Update 同步执行
            // 假如在释放技能的过程中游戏出现卡顿 导致技能执行的时间变长 FixedUpdate的调用就会变多
            // 导致表现的效果 和 不卡的时候有一定的差距
            if (fixedTimer - ReleasedTimer > Time.fixedDeltaTime * 2)
                return;

            fixedTimer += Time.fixedDeltaTime;
            OnFixedUpdate();
        }

        /// <summary>
        /// 释放技能
        /// </summary>
        /// <returns></returns>
        public ReleaseSkillStatus Release()
        {
            if (ReleaseSkillStatus != ReleaseSkillStatus.Success)
                return ReleaseSkillStatus;

            CooldownTimer = 0;
            IsCooldown = false;
            IsReleasing = true;
            ReleasedTimer = 0;
            fixedTimer = 0;
            var version = _version;
            OnRelease();
            if (version != _version || !IsAttached) return ReleaseSkillStatus.Success;
            // 触发事件
            onStartReleaseCallback?.Invoke(this);

            return ReleaseSkillStatus.Success;
        }

        /// <summary>
        /// 取消释放(前提是该技能可以主动取消)
        /// </summary>
        public void Cancel()
        {
            if (ActiveCancellation && IsReleasing)
                Interrupt();
        }

        /// <summary>
        /// 中断技能
        /// </summary>
        public void Interrupt()
        {
            if (!IsReleasing) return;
            IsReleasing = false;
            ReleasedTimer = 0;
            var version = _version;

            onInterruptCallback?.Invoke(this);
            if (version != _version) return;
            OnInterrupt();
            if (version != _version) return;
            OnReleaseComplete();
        }

        /// <summary>
        /// 判断当前技能是否能够释放(默认:true,如果该技能在某种特殊情况下不能释放,可以重写此方法,返回false即可!)
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsCanRelease()
        {

            return true;
        }


        /// <summary>
        /// 判断技能是否释放完成(适用于不是根据持续时间来决定是否结束的技能,返回true代表结束!)
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsComplete()
        {
            return true;
        }


        /// <summary>
        /// 是否能够同时释放技能
        /// </summary>
        /// <param name="equipper"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool IsCanbeReleaseAtSameTime()
        {
            foreach (var item in Releaser.Skills.Values)
            {
                // 如果技能没在释放中 直接判断下一个
                if (!item.IsReleasing) continue;
                // 如果正在释放中的技能是自己 说明当前这个技能正在释放中 直接return false;
                if (item.SkillData.id == this.SkillData.id) return false;

                bool release = CanbeReleasedAtTheSameTime.Contains(item.SkillData.id);
                release |= item.CanbeReleasedAtTheSameTime.Contains(SkillData.id);

                // 如果不能同时释放 直接return false;
                if (!release)
                    return false;
            }

            return true;
        }


        /// <summary>
        /// 重置能够同时释放的技能id集合
        /// </summary>
        public void ResetCanbeReleasedAtTheSameTime()
        {
            if (canbe_released_at_the_same_time == null)
                canbe_released_at_the_same_time = new List<int>();

            canbe_released_at_the_same_time.Clear();
            canbe_released_at_the_same_time.AddRange(SkillData.canbe_released_at_the_same_time);
        }

        /// <summary>
        /// 重置是否能够主动取消
        /// </summary>
        public void ResetActiveCancellation()
        {
            ActiveCancellation = SkillData.active_cancellation;
        }

        internal void ResetData()
        {
            _version++;
            _removing = false;
            _duration = SkillData.duration;
            _cooling_time = SkillData.cooling_time;
            ResetCanbeReleasedAtTheSameTime();
            ResetActiveCancellation();
            isCooldown = true;
            CooldownTimer = 0;
            IsReleasing = false;
            ReleasedTimer = 0;
            fixedTimer = 0;

            onStartCooldownCallback = null;
            onCoolingCallback = null;
            onCooldownCompleteCallback = null;

            onStartReleaseCallback = null;
            onReleasingCallback = null;
            onReleaseCompleteCallback = null;

            onInterruptCallback = null;
        }

        internal void BeginRemove() { _removing = true; _version++; }

        internal void ClearRuntime()
        {
            _version++;
            _removing = true;
            Releaser = null;
            SkillData = null;
            IsReleasing = false;
            isCooldown = true;
            CooldownTimer = ReleasedTimer = fixedTimer = 0;
            canbe_released_at_the_same_time.Clear();
            onStartCooldownCallback = null;
            onCoolingCallback = null;
            onCooldownCompleteCallback = null;
            onStartReleaseCallback = null;
            onReleasingCallback = null;
            onReleaseCompleteCallback = null;
            onInterruptCallback = null;
        }

        #endregion



        #region 生命周期

        /// <summary>
        /// 添加技能时调用
        /// </summary>
        internal protected virtual void OnAdd() { }

        /// <summary>
        /// 移除技能时调用
        /// </summary>
        internal protected virtual void OnRemove() { }

        /// <summary>
        /// 当技能释放的时候调用
        /// </summary>
        protected virtual void OnRelease() { }

        /// <summary>
        /// 当技能释放结束时调用(中断技能时也会触发该回调)
        /// </summary>
        protected virtual void OnReleaseComplete() { }

        /// <summary>
        /// 当技能正在冷却中调用
        /// </summary>
        protected virtual void OnCooling() { }

        /// <summary>
        /// 当技能冷却完成时调用
        /// </summary>
        protected virtual void OnCooldownComplete() { }

        /// <summary>
        /// 当技能中断时调用
        /// </summary>
        protected virtual void OnInterrupt() { }

        /// <summary>
        /// Update回调(仅在技能正在释放中时才会触发)
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// FixedUpdate回调(仅在技能正在释放中时才会触发)
        /// </summary>
        internal protected virtual void OnFixedUpdate() { }
        /// <summary>
        /// LateUpdate回调(仅在技能正在释放中时才会触发)
        /// </summary>
        internal protected virtual void OnLateUpdate() { }


        #endregion

    }
}
