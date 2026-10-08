// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/mvc/Model.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.


using UnityEngine;
using YLFramework.Core.RoslynAnalyzers;

namespace YLFramework.Core
{

    [ProtectedHierarchy(typeof(MonoBehaviour),typeof(View))]
    public abstract class Model
    {

        #region 属性

        /// <summary>
        /// Id
        /// </summary>
        public long Id { get; set; } = -1; // 默认等于 -1

        /// <summary>
        /// 当前数据所在的模块
        /// </summary>
        public Module Module { get; internal set; }
          
        #endregion

        #region 方法


        /// <summary>
        /// 添加Model时调用
        /// </summary>  
        public virtual void OnInit() { }

        /// <summary>
        /// 移除Model时调用
        /// </summary>
        public virtual void OnExit() { }

        #endregion


    }

}