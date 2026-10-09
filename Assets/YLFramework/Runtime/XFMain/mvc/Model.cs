// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/Model.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.


using System;
using UnityEngine;
using YLFramework.Core.RoslynAnalyzers;

namespace YLFramework.Core
{

    [ProtectedHierarchy(typeof(MonoBehaviour),typeof(View))]
    public abstract class Model
    {
        private long id = -1;
        internal bool IsInitializing;
        internal bool IsExiting;
        [NonSerialized] internal object Registration;

        #region 属性

        /// <summary>
        /// 业务ID；-1表示注册时自动分配。未注册时可赋值，注册期间不可改变。
        /// </summary>
        public long Id
        {
            get => id;
            set
            {
                if (Module != null && value != id)
                    throw new InvalidOperationException("Cannot change the ID of a registered Model.");
                id = value;
            }
        }

        /// <summary>
        /// 拥有者；初始化及退出回调期间有效，移除或初始化失败后为null。
        /// </summary>
        public Module Module { get; internal set; }
          
        #endregion

        #region 方法


        /// <summary>
        /// 每次有效注册时调用一次；期间可查询自身，但注册尚未提交完成。
        /// </summary>  
        public virtual void OnInit() { }

        /// <summary>
        /// 移除Model时调用
        /// </summary>
        public virtual void OnExit() { }

        #endregion


    }

}
