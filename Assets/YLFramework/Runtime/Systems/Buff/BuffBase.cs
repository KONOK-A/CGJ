// Adapted from XF Buff. Original copyright and MIT License: Runtime/Systems/Buff/LICENSE.txt.
using YLFramework.Foundation;

using System;
using UnityEngine;

namespace YLFramework.BuffSystem
{
    /// <summary>
    /// Buff基类
    /// </summary>
    public class BuffBase
    {
        #region 字段

        [Tooltip("用户自定义数据")]
        public object userData;

        private float fixedUpdateTimer;

        private float _duration;
        private int _version;
        internal int Version => _version;

        #endregion

        #region 属性

        /// <summary>
        /// 拥有当前Buff的游戏角色对象
        /// </summary>
        public IAddableBuff Owner { get; internal set; }

        /// <summary>
        /// Buff配置信息
        /// </summary>
        public BuffConfigData BuffData { get; internal set; }

        /// <summary>
        /// Buff已存在的时间
        /// </summary>
        public float ExistedTime { get; private set; }

        /// <summary>
        /// 持续时间
        /// </summary>
        public virtual float Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                _duration = value;
            }
        }

        /// <summary>
        /// 进度
        /// </summary>
        public float Progress
        {
            get
            {
                if (Duration <= 0)
                    return -1;

                return ExistedTime / Duration;
            }
        }

        #endregion

        #region 方法

        /// <summary>
        /// 当Buff被添加时调用
        /// </summary>
        public virtual void OnAdd() { }

        /// <summary>
        /// 当Buff被移除时调用
        /// </summary>
        public virtual void OnRemove() { }

        internal void Update() => Advance(UnityEngine.Time.deltaTime);
        internal void Advance(float deltaTime)
        {
            ExistedTime += deltaTime;
            OnUpdate();
        }

        internal void FixedUpdate()
        {
            if (fixedUpdateTimer - ExistedTime > UnityEngine.Time.fixedDeltaTime * 2)
                return;

            fixedUpdateTimer += UnityEngine.Time.fixedDeltaTime;
            OnFixedUpdate();
        }

        internal void ResetData()
        {
            _version++;
            _duration = BuffData.duration;
            ExistedTime = 0;
            Owner = null;
            fixedUpdateTimer = 0;
        }

        /// <summary>
        /// Update
        /// </summary>
        protected virtual void OnUpdate() { }

        /// <summary>
        /// FixedUpdate
        /// </summary>
        public virtual void OnFixedUpdate() { }

        /// <summary>
        /// LateUpdate
        /// </summary>
        public virtual void OnLateUpdate() { }

        public void OnLoaded()
        {

        }

        public void OnUnloaded()
        {
            ResetData();
        }

        internal void Detach() { _version++; }
        internal void ClearRuntime()
        {
            _version++;
            Owner = null;
            BuffData = null;
            userData = null;
            ExistedTime = fixedUpdateTimer = _duration = 0;
        }

        /// <summary>
        /// 移除当前buff
        /// </summary>
        public void Remove()
        {
            BuffManager.RemoveBuff(Owner, this);
        }

        /// <summary>
        /// 判断当前buff是否能够添加
        /// </summary>
        /// <returns></returns>
        public virtual bool IsCanAdd()
        {
            return true;
        }

        #endregion

    }
}
