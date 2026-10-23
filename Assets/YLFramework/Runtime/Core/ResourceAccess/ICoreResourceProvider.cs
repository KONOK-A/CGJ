using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Singleton;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YLFramework.Core
{
    public enum CoreResourceStatus
    {
        Pending,
        Succeeded,
        Failed,
        Canceled,
        Invalidated
    }
    public enum CoreResourceError
    {
        Missing,
        InProgress,
        LoadFailed,
        InvalidInput,
        Unavailable
    }

    /// <summary>Core 消费失败分类；InnerException 保留实际后端 cause，不导入具体 Systems 类型。</summary>
    public sealed class CoreResourceException : Exception
    {
        public CoreResourceError Kind
        {
            get;
        }
        public CoreResourceException(CoreResourceError kind, string message, Exception cause = null) : base(message, cause) => Kind = kind;
    }

    /// <summary>消费方窄请求合同。yield 只观察；TakeResult 单次交割，Dispose 不释放已交割结果。</summary>
    /// <remarks>完成通知逐个隔离异常；终态稳定，Dispose 后领取抛 ObjectDisposedException。</remarks>
    public abstract class CoreResourceRequest<T> : CustomYieldInstruction, IDisposable
    {
        public abstract CoreResourceStatus Status
        {
            get;
        }
        public abstract float Progress
        {
            get;
        }
        public abstract CoreResourceException Error
        {
            get;
        }
        public abstract bool IsDisposed
        {
            get;
        }
        public override bool keepWaiting => Status == CoreResourceStatus.Pending;
        public abstract event Action<CoreResourceRequest<T>> Completed;
        public abstract T TakeResult();
        public abstract void Cancel();
        public abstract void Dispose();
    }

    /// <summary>Core 借读 prefab 并持有自己的责任；Instantiate 不消费 prefab lease，实例另持独立责任。</summary>
    public abstract class CorePrefabLease : IDisposable
    {
        public abstract GameObject Prefab
        {
            get;
        }
        public abstract bool IsValid
        {
            get;
        }
        public abstract CoreInstanceLease Instantiate(Transform parent, Scene scene);
        public abstract void Dispose();
    }

    /// <summary>Core 实例 checkout；正常 Return、损坏 Discard。Dispose 等价 Return，不归还其他 checkout。</summary>
    public abstract class CoreInstanceLease : IDisposable
    {
        public abstract GameObject Instance
        {
            get;
        }
        public abstract bool IsValid
        {
            get;
        }
        public abstract void Return();
        public abstract void Discard();
        public void Dispose() => Return();
    }

    /// <summary>本次真实 Scene 与副作用观察值；不拥有 Scene 卸载权，不随 Module.Close 隐式卸载。</summary>
    public readonly struct CoreSceneResult
    {
        public Scene Scene
        {
            get;
        }
        public bool HasSceneSideEffects
        {
            get;
        }
        public CoreSceneResult(Scene scene, bool hasSceneSideEffects)
        {
            Scene = scene;
            HasSceneSideEffects = hasSceneSideEffects;
        }
    }

    /// <summary>仅覆盖 Core 文本、prefab/实例和场景消费。命名操作必须显式绑定后端。</summary>
    public interface ICoreResourceProvider
    {
        CoreResourceRequest<string> ReadTextAsync(string projectId, string address);
        CorePrefabLease AcquirePrefab(string projectId, string address);
        CoreResourceRequest<CorePrefabLease> AcquirePrefabAsync(string projectId, string address);
        CoreResourceRequest<CoreSceneResult> LoadSceneAsync(string projectId, string address, LoadSceneMode mode);
    }

    /// <summary>显式主线程绑定槽，无默认后端、无 XFAB 回退、无加载表。</summary>
    public static class CoreResourceAccess
    {
        private static ICoreResourceProvider _provider;
        public static ICoreResourceProvider Provider => SingletonSessionState.CanRunExecution ? _provider : null;

        internal static ICoreResourceProvider RequireProvider(ICoreResourceProvider value) => value ??
            throw new CoreResourceException(CoreResourceError.Unavailable, "Named Core resources require an explicitly bound resource provider.");

        internal static bool CanFallback(Exception error) => error is CoreResourceException resource &&
            (resource.Kind == CoreResourceError.Missing || resource.Kind == CoreResourceError.LoadFailed);

        static CoreResourceAccess()
        {
            SingletonSessionState.RegisterSessionReset(Reset);
            Application.quitting += Reset;
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
#endif
        }

        public static void Bind(ICoreResourceProvider value)
        {
            if (!SingletonSessionState.CanRunExecution)
            {
                throw new InvalidOperationException("Core resource binding requires an active Play/Player session.");
            }

            _provider = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>旧 owner 解绑不覆盖后来显式绑定的 provider。</summary>
        public static bool Unbind(ICoreResourceProvider value)
        {
            if (value == null || !ReferenceEquals(_provider, value))
            {
                return false;
            }

            _provider = null;
            return true;
        }

        private static void Reset() => _provider = null;
#if UNITY_EDITOR
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                Reset();
            }
        }
#endif
    }
}
