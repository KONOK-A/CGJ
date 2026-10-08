// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/UpdateManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.

using System; 
using System.Collections.Generic;
using UnityEngine;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;



namespace YLFramework.Core
{

    // Fix 编码

    public class UpdateManager : MonoBehaviour
    {

        #region 静态字段

        // 继承自MonoBehaviour的静态对象 在编辑器关掉的时候会自动设置为空
        // 即使禁用 Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 也没关系
        private static UpdateManager _instance;

        #endregion

        #region 静态属性

        private static UpdateManager Instance 
        {

            get 
            {
                if (!_instance && ApplicationTool.isPlaying) 
                { 
                    GameObject obj = new GameObject("UpdateManager");
                    DontDestroyOnLoad(obj);
                    _instance = obj.AddComponent<UpdateManager>();
                }
                 
                return _instance;
            }

        }

        #endregion

        #region 事件

        private Action _update;
        private Action _fixedUpdte;
        private Action _lateUpdate;


        [Obsolete("已过时,请使用方法UpdateManager.AddUpdateEvent来注册Update事件!", true)]
        public static Action onUpdate;
 
        [Obsolete("已过时,请使用方法UpdateManager.AddFixedUpdateEvent来注册Update事件!", true)]
        public static Action onFixedUpdate;
 
        [Obsolete("已过时,请使用方法UpdateManager.AddLateUpdateEvent来注册Update事件!", true)]
        public static Action onLateUpdate;  

        #endregion
 

        private void FixedUpdate()
        {
            _fixedUpdte?.Invoke();
        }

        private void Update()
        {
            _update?.Invoke();
        }

        private void LateUpdate()
        {
           _lateUpdate?.Invoke();
        }

        /// <summary>
        /// 注册Update回调
        /// </summary>
        /// <param name="action"></param>
        public static void AddUpdateEvent(Action action) 
        {
            if (Instance) 
            {
                Instance._update += action;
            }
        }

        /// <summary>
        /// 注册FixedUpdate回调
        /// </summary>
        /// <param name="action"></param>
        public static void AddFixedUpdateEvent(Action action) 
        {
            if (Instance)
            {
                Instance._fixedUpdte += action;
            }
        }

        /// <summary>
        /// 注册LateUpdate回调
        /// </summary>
        /// <param name="action"></param>
        public static void AddLateUpdateEvent(Action action) 
        {
            if (Instance)
            {
                Instance._lateUpdate += action;
            }
        }

        /// <summary>
        /// 移除Update回调
        /// </summary>
        /// <param name="action"></param>
        public static void RemoveUpdateEvent(Action action) 
        {
            if (Instance)
            {
                Instance._update -= action;
            }
        }

        /// <summary>
        /// 移除FixedUpdate回调
        /// </summary>
        /// <param name="action"></param>
        public static void RemoveFixedUpdateEvent(Action action)
        {
            if (Instance)
            {
                Instance._fixedUpdte -= action;
            }
        }

        /// <summary>
        /// 移除LateUpdate回调
        /// </summary>
        /// <param name="action"></param>
        public static void RemoveLateUpdateEvent(Action action)
        {
            if (Instance)
            {
                Instance._lateUpdate -= action;
            }
        }
         
    }
}

