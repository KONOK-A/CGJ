using System;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Actions
{
    /// <summary>当前场景的动作宿主，由 StartCurrentScene 等场景范围入口按需创建，随所在场景卸载。</summary>
    public class ActionKitCurrentScene : MonoSingletonHost<ActionKitCurrentScene>
    {
        /// <summary>当前缓存的场景宿主；保留原公开字段，调用方应通过 SceneComponent 取得实例。</summary>
        public static ActionKitCurrentScene mSceneComponent = null;
        // 自建宿主保持在当前场景中，不设 DontDestroyOnLoad，随场景卸载一起销毁并取消其动作。
        protected override bool PersistentHost => false;

        /// <summary>取得可用场景宿主，不存在时在当前活动场景中创建；关停期间不可获取。</summary>
        /// <exception cref="System.InvalidOperationException">模块正在关停时抛出。</exception>
        public static ActionKitCurrentScene SceneComponent
        {
            get
            {
                if (ActionKit.IsShuttingDown) throw new InvalidOperationException("ActionKit is shutting down.");
                if (!mSceneComponent || mSceneComponent.HostReleased)
                {
                    mSceneComponent = GetInstance(true);
                }
                return mSceneComponent;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            // 宿主是内部实现细节，隐藏于层级与 Inspector，避免被当作普通场景对象误删或误改。
            hideFlags = HideFlags.HideInHierarchy;
        }

        protected override void OnDestroy()
        {
            try { base.OnDestroy(); }
            finally { if (mSceneComponent == this) mSceneComponent = null; }
        }

        // Shutdown 专用：只释放模块自建的宿主（借用场景组件时保留组件本身），再清空单例与静态缓存。
        internal static void ReleaseOwnedInstances()
        {
            foreach (var instance in FindObjectsOfType<ActionKitCurrentScene>(true))
            {
                if (instance.OwnsHostGameObject) instance.ReleaseHost();
            }
            ResetHostReference();
            mSceneComponent = null;
        }
    }
}
