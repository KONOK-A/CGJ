using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// <see cref="IResLoader"/> 的便捷扩展。
    /// <para>
    /// 本类内部创建的 <see cref="ResKey"/> 由本类负责回收（Allocate → 调用 → RecycleToCache）；
    /// 传入的 key 不消费。加载失败时统一返回 null，不抛异常。
    /// </para>
    /// </summary>
    public static class ResLoaderExtensions
    {
        private static readonly Type ComponentType = typeof(Component);
        private static readonly Type GameObjectType = typeof(GameObject);

        /// <summary>
        /// 按资源名同步加载。T 为 Component 时按 GameObject 预制查询并取组件；加载失败返回 null。
        /// </summary>
        public static T LoadSync<T>(this IResLoader self, string assetName) where T : Object
        {
            var type = typeof(T);
            //继承自Component则需要寻找预制体
            if (ComponentType.IsAssignableFrom(type))
            {
                var resKey = ResKey.Allocate(assetName, null, GameObjectType);
                var resAsset = (self.LoadAssetSync(resKey) as GameObject)?.GetComponent<T>();
                resKey.RecycleToCache();
                return resAsset;
            }
            else
            {
                var resKey = ResKey.Allocate(assetName, null, type);
                var resAsset = self.LoadAssetSync(resKey) as T;
                resKey.RecycleToCache();
                return resAsset;
            }
        }
        
        
        /// <summary>
        /// 按 Bundle + 资源名同步加载。T 为 Component 时按 GameObject 预制查询并取组件；加载失败返回 null。
        /// </summary>
        public static T LoadSync<T>(this IResLoader self, string ownerBundle, string assetName) where T : Object
        {
            var type = typeof(T);
            if (ComponentType.IsAssignableFrom(type))
            {
                var resKey = ResKey.Allocate(assetName, ownerBundle, GameObjectType);
                var resAsset = (self.LoadAssetSync(resKey) as GameObject)?.GetComponent<T>();
                resKey.RecycleToCache();
                return resAsset;
            }
            else
            {
                var resKey = ResKey.Allocate(assetName, ownerBundle, type);
                var resAsset = self.LoadAssetSync(resKey) as T;
                resKey.RecycleToCache();
                return resAsset;
            }
        }
        /// <summary>
        /// 按资源名把加载请求加入预加载队列（不限定资源类型）；
        /// 调用 <see cref="IResLoader.LoadAsync"/> 时统一执行。
        /// </summary>
        /// <param name="assetName">资源名（不指定 Bundle）。</param>
        /// <param name="listener">单个资源的加载完成回调，可为 null。</param>
        /// <param name="lastOrder">true 追加到等待队列尾部，false 插入头部（更早被调度）。</param>
        public static void AddToPreLoad(this IResLoader self, string assetName, Action<bool, IRes> listener = null,
            bool lastOrder = true)
        {
            
            var resKey = ResKey.Allocate(assetName);
            self.AddToPreLoad(resKey, listener, lastOrder);
            resKey.RecycleToCache();
        }
        /// <summary>
        /// 按资源名把加载请求加入预加载队列并按 T 限定类型：
        /// T 为 Component 时按 GameObject 预制查询，否则按 T 类型查询。
        /// </summary>
        /// <param name="assetName">资源名（不指定 Bundle）。</param>
        /// <param name="listener">单个资源的加载完成回调，可为 null。</param>
        /// <param name="lastOrder">true 追加到等待队列尾部，false 插入头部（更早被调度）。</param>
        public static void AddToPreLoad<T>(this IResLoader self, string assetName, Action<bool, IRes> listener = null,
            bool lastOrder = true)
        {
            var type = typeof(T);
            //加载Component
            if (ComponentType.IsAssignableFrom(type))
            {
                var resKey = ResKey.Allocate(assetName, null, GameObjectType);
                self.AddToPreLoad(resKey, listener, lastOrder);
                resKey.RecycleToCache();
            }
            //加载普通类
            else
            {
                var resKey = ResKey.Allocate(assetName, null, type);
                self.AddToPreLoad(resKey, listener, lastOrder);
                resKey.RecycleToCache();
            }
        }

        /// <summary>
        /// 按 Bundle + 资源名把加载请求加入预加载队列（不限定资源类型）；
        /// 调用 <see cref="IResLoader.LoadAsync"/> 时统一执行。
        /// </summary>
        /// <param name="ownerBundle">所属 AssetBundle 名。</param>
        /// <param name="assetName">资源名。</param>
        /// <param name="listener">单个资源的加载完成回调，可为 null。</param>
        /// <param name="lastOrder">true 追加到等待队列尾部，false 插入头部（更早被调度）。</param>
        public static void AddToPreLoad(this IResLoader self, string ownerBundle, string assetName,
            Action<bool, IRes> listener = null, bool lastOrder = true)
        {
            var resKey = ResKey.Allocate(assetName, ownerBundle);
            self.AddToPreLoad(resKey, listener, lastOrder);
            resKey.RecycleToCache();
        }

        /// <summary>
        /// 按 Bundle + 资源名把加载请求加入预加载队列并按 T 限定类型：
        /// T 为 Component 时按 GameObject 预制查询，否则按 T 类型查询。
        /// </summary>
        /// <param name="ownerBundle">所属 AssetBundle 名。</param>
        /// <param name="assetName">资源名。</param>
        /// <param name="listener">单个资源的加载完成回调，可为 null。</param>
        /// <param name="lastOrder">true 追加到等待队列尾部，false 插入头部（更早被调度）。</param>
        public static void AddToPreLoad<T>(this IResLoader self, string ownerBundle, string assetName,
            Action<bool, IRes> listener = null, bool lastOrder = true)
        {
            var type = typeof(T);
            //加载Component
            if (ComponentType.IsAssignableFrom(type))
            {
                var resKey = ResKey.Allocate(assetName, ownerBundle, GameObjectType);
                self.AddToPreLoad(resKey, listener, lastOrder);
                resKey.RecycleToCache();
            }
            //加载普通类
            else
            {
                var searchRule = ResKey.Allocate(assetName, ownerBundle, type);
                self.AddToPreLoad(searchRule, listener, lastOrder);
                searchRule.RecycleToCache();
            }
        }
        
        
        /// <summary>
        /// 按场景名同步加载场景（默认 Single）。内部先加载场景所属 Bundle，再交给 SceneManager。
        /// </summary>
        /// <param name="assetName">场景资源名（不指定 Bundle）。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="physicsMode">2D 物理模式。</param>
        public static void LoadSceneSync(this IResLoader self, string assetName,
            LoadSceneMode mode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            var resKey = ResKey.Allocate(assetName);
            self.LoadSceneSync(resKey, mode, physicsMode);
            resKey.RecycleToCache();
        }

        /// <summary>
        /// 按 Bundle + 场景名同步加载场景（默认 Single）。内部先加载场景所属 Bundle，再交给 SceneManager。
        /// </summary>
        /// <param name="ownerBundle">场景所属 AssetBundle 名。</param>
        /// <param name="assetName">场景资源名。</param>
        /// <param name="mode">场景加载模式。</param>
        /// <param name="physicsMode">2D 物理模式。</param>
        public static void LoadSceneSync(this IResLoader self, string ownerBundle, string assetName,
            LoadSceneMode mode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            var resKey = ResKey.Allocate(assetName, ownerBundle);
            self.LoadSceneSync(resKey, mode, physicsMode);
            resKey.RecycleToCache();
        }

        /// <summary>
        /// 同步加载场景：Bundle 缺失/未就绪时记录错误并直接返回，不再继续调用 SceneManager。
        /// <para>
        /// Scene ownership：本方法只负责把场景交给 SceneManager 加载；
        /// 释放场景 Res / 回收 Loader 不会卸载已加载的 Scene（Scene 卸载由调用方显式操作）。
        /// </para>
        /// </summary>
        public static void LoadSceneSync(this IResLoader self, ResKey resKey,
            LoadSceneMode mode = LoadSceneMode.Single,
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None)
        {
            if (!ResFactory.AssetBundleSceneResCreator.Match(resKey))
            {
                Debug.LogError("资源名称错误！请检查资源名称是否正确或是否被标记！AssetName:" + resKey.AssetName);
                return;
            }
#if UNITY_EDITOR
            if (AssetBundleHelper.SimulationMode)
            {
                //模拟模式只需要 Bundle 名来定位 Scene 资产；用于探测的 Res 立即归还对象池。
                var sceneRes = (AssetBundleSceneRes)ResFactory.AssetBundleSceneResCreator.Create(resKey);
                var bundleName = sceneRes?.AssetBundleName;
                sceneRes?.RecycleToCache();

                var paths = string.IsNullOrEmpty(bundleName)
                    ? null
                    : UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
                {
                    Debug.LogError("#YLFramework: Load Scene Failed, Not Find Scene Asset:" + resKey.AssetName);
                    return;
                }

                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(paths[0],
                    new LoadSceneParameters(mode, physicsMode));
                return;
            }
#endif
            //运行时：先通过 Loader 加载场景 Bundle（SceneRes 门控），成功后才交给 SceneManager。
            var sceneIRes = self.LoadResSync(resKey);
            if (sceneIRes == null || sceneIRes.State != ResState.Ready)
            {
                Debug.LogError("#YLFramework: Load Scene Failed, Res Not Ready:" + resKey.AssetName);
                return;
            }

            SceneManager.LoadScene(resKey.OriginalAssetName, new LoadSceneParameters(mode, physicsMode));
        }

        /// <summary>
        /// 按场景名异步加载场景（不指定所属 Bundle）；内部先异步等待场景 Bundle 就绪，再交给 SceneManager。
        /// </summary>
        /// <param name="sceneName">场景资源名。</param>
        /// <param name="loadSceneMode">场景加载模式，默认 Single。</param>
        /// <param name="physicsMode">2D 物理模式，默认 None。</param>
        /// <param name="onStartLoading">场景开始加载时的回调，返回异步加载场景的 AsyncOperation；加载失败不会调用。</param>
        public static void LoadSceneAsync(this IResLoader self, string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, 
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            Action<AsyncOperation> onStartLoading = null)
        {

            var resSearchKey = ResKey.Allocate(sceneName);
            self.LoadSceneAsync(resSearchKey,loadSceneMode,physicsMode,onStartLoading);
            resSearchKey.RecycleToCache();
        }

        /// <summary>
        /// 按 Bundle + 场景名异步加载场景；内部先异步等待场景 Bundle 就绪，再交给 SceneManager。
        /// </summary>
        /// <param name="bundleName">场景所属 AssetBundle 名。</param>
        /// <param name="sceneName">场景资源名。</param>
        /// <param name="loadSceneMode">场景加载模式，默认 Single。</param>
        /// <param name="physicsMode">2D 物理模式，默认 None。</param>
        /// <param name="onStartLoading">场景开始加载时的回调，返回异步加载场景的 AsyncOperation；加载失败不会调用。</param>
        public static void LoadSceneAsync(this IResLoader self, string bundleName,string sceneName,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, 
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            Action<AsyncOperation> onStartLoading = null)
        {

            var resSearchKey = ResKey.Allocate(sceneName,bundleName);
            self.LoadSceneAsync(resSearchKey,loadSceneMode,physicsMode,onStartLoading);
            resSearchKey.RecycleToCache();
        }
        
        /// <summary>
        /// 资源加载器进行异步加载场景的操作。
        /// <para>
        /// Scene ownership：先异步等待场景 Bundle 就绪，再调用 SceneManager.LoadSceneAsync，
        /// 并通过 onStartLoading 返回 AsyncOperation；Bundle 失败时记录错误、不调用 SceneManager。
        /// 释放场景 Res / 回收 Loader 不会卸载已加载的 Scene。
        /// </para>
        /// </summary>
        /// <param name="onStartLoading">场景开始加载时的回调，返回异步加载场景的 AsyncOperation；加载失败不会调用。</param>
        public static void LoadSceneAsync(this IResLoader self,ResKey resKey,
            LoadSceneMode loadSceneMode = LoadSceneMode.Single, 
            LocalPhysicsMode physicsMode = LocalPhysicsMode.None,
            Action<AsyncOperation> onStartLoading = null)
        {

            if (!ResFactory.AssetBundleSceneResCreator.Match(resKey))
            {
                Debug.LogError("场景名称错误！请检查名称是否正确或资源是否被标记！AssetName:" + resKey.AssetName);
                return;
            }
#if UNITY_EDITOR
            if (AssetBundleHelper.SimulationMode)
            {
                //模拟模式只需要 Bundle 名来定位 Scene 资产；用于探测的 Res 立即归还对象池。
                var sceneRes = (AssetBundleSceneRes)ResFactory.AssetBundleSceneResCreator.Create(resKey);
                var bundleName = sceneRes?.AssetBundleName;
                sceneRes?.RecycleToCache();

                var paths = string.IsNullOrEmpty(bundleName)
                    ? null
                    : UnityEditor.AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
                if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0]))
                {
                    Debug.LogError("#YLFramework: Load Scene Failed, Not Find Scene Asset:" + resKey.AssetName);
                    return;
                }

                var sceneParameters = new LoadSceneParameters
                {
                    loadSceneMode = loadSceneMode,
                    localPhysicsMode = physicsMode
                };
                var asyncOperation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    paths[0], sceneParameters);
                onStartLoading?.Invoke(asyncOperation);
                return;
            }
#endif
            //resKey 由调用方持有并在本方法返回后回收，这里先取出回调需要的数据。
            var sceneName = resKey.OriginalAssetName;
            var sceneAssetName = resKey.AssetName;
            self.AddToPreLoad(resKey,(success, _) =>
            {
                if (!success)
                {
                    //Bundle 失败：不再调用 SceneManager，避免无效/异常的场景加载。
                    Debug.LogError("#YLFramework: Load Scene Failed, Res Not Ready:" + sceneAssetName);
                    return;
                }

                var asyncOperation = SceneManager.LoadSceneAsync(sceneName, new LoadSceneParameters
                {
                    loadSceneMode = loadSceneMode,
                    localPhysicsMode = physicsMode
                });
                onStartLoading?.Invoke(asyncOperation);
            });
            //调用异步加载方法
            self.LoadAsync();
        }
        
    }
}