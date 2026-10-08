using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// AssetBundle 中具体资产（Prefab / Texture / AudioClip 等）的 IRes 实现。
    /// <para>
    /// 定位：<see cref="ResKey"/> 的 AssetName + OwnerBundleName + AssetType 经资源数据表查询后
    /// 得到所属 Bundle（<see cref="AssetBundleName"/>）。Bundle 依赖不在此层维护：
    /// 由 <see cref="ResLoader"/> 递归加载并持有整条依赖链，本类仅在自身加载期间通过
    /// <see cref="Res.HoldDependRes"/> 暂时保住所属 Bundle（<see cref="GetDependBundleList"/> 返回的就是它），
    /// 加载结束后成对释放。
    /// </para>
    /// <para>
    /// 模拟模式（Editor Simulation）通过 AssetDatabase 按路径加载；运行时通过
    /// <see cref="UnityEngine.AssetBundle.LoadAsset(string)"/> / LoadAssetAsync 加载。
    /// 两种模式对外暴露相同的引用计数与失败语义：失败回 Waiting 并通知 false，
    /// 只有真正拿到非 null 资产才会进入 Ready。
    /// </para>
    /// </summary>
    public class AssetRes : Res
    {
        private string[] _assetBundles;
        private AssetBundleRequest _assetBundleRequest;
        private string _ownerBundleName;

        /// <inheritdoc />
        public override string OwnerBundleName
        {
            get => _ownerBundleName;
            internal set => _ownerBundleName = value;
        }

        /// <summary>所属 AssetBundle 名；配置缺失时为 null。</summary>
        public string AssetBundleName => _assetBundles?[0];

        public AssetRes(string assetName) : base(assetName) { }
        public AssetRes() {}

        /// <summary>
        /// 从对象池分配并按资源数据表初始化所属 Bundle 信息。
        /// </summary>
        public static AssetRes Allocate(string assetName, string ownerBundleName, Type assetType)
        {
            var res = SafeObjectPool<AssetRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = assetName;
                res.OwnerBundleName = ownerBundleName;
                res.AssetType = assetType;
                res.InitAssetBundleInfo();
            }
            return res;
        }

        /// <inheritdoc />
        public override bool LoadSync()
        {
            if (!CheckLoadAble())
            {
                return false;
            }

            if (string.IsNullOrEmpty(AssetBundleName))
            {
                Debug.LogError("#YLFramework: Not Find AssetBundle For Asset:" + AssetName);
                return false;
            }

            Object obj;
            //模拟环境下加载
            if (AssetBundleHelper.SimulationMode && !IsAssetBundleManifest())
            {
                var resKey = ResKey.Allocate(AssetBundleName, null, typeof(AssetBundle));
                var abRes = ServiceManager.Instance.GetRes<AssetBundleRes>(resKey);
                resKey.RecycleToCache();

                if (abRes == null)
                {
                    Debug.LogError("#YLFramework: Failed to Load Asset,Not Find AssetBundleRes:" + AssetBundleName);
                    OnResLoadFail();
                    return false;
                }

                var assetPaths = AssetBundleHelper.GetAssetPathsFromAssetBundleAndAssetName(abRes.AssetName, AssetName);
                if (assetPaths == null || assetPaths.Length == 0)
                {
                    Debug.LogError("#YLFramework: Failed to Load Asset:" + AssetName);
                    OnResLoadFail();
                    return false;
                }

                //确保加载过程中依赖资源不被释放；异常时 finally 仍成对释放。
                HoldDependRes();
                State = ResState.Loading;
                try
                {
                    obj = AssetType != null
                        ? AssetBundleHelper.LoadAssetAtPath(assetPaths[0], AssetType)
                        : AssetBundleHelper.LoadAssetAtPath<Object>(assetPaths[0]);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    obj = null;
                }
                finally
                {
                    UnHoldDependRes();
                }
            }
            else
            {
                var resKey = ResKey.Allocate(AssetBundleName, null, typeof(AssetBundle));
                var abRes = ServiceManager.Instance.GetRes<AssetBundleRes>(resKey);
                resKey.RecycleToCache();

                if (abRes == null || !abRes.AssetBundle)
                {
                    //所属 Bundle 正在被其他持有者异步加载：同步路径无法完成本次加载，
                    //也不属于失败；保持 Waiting，由进行中的异步流程完成后置 Ready。
                    if (abRes != null && abRes.State == ResState.Loading)
                    {
                        return false;
                    }

                    Debug.LogError("#YLFramework: Failed to Load Asset,Not Find AssetBundle:" + AssetBundleName);
                    OnResLoadFail();
                    return false;
                }

                //确保加载过程中依赖资源不被释放；异常时 finally 仍成对释放。
                HoldDependRes();
                State = ResState.Loading;
                try
                {
                    obj = AssetType != null
                        ? abRes.AssetBundle.LoadAsset(AssetName, AssetType)
                        : abRes.AssetBundle.LoadAsset(AssetName);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    obj = null;
                }
                finally
                {
                    UnHoldDependRes();
                }
            }

            if (obj == null)
            {
                Debug.LogError("#YLFramework: Failed to Load Asset:" + AssetName + ":" + AssetType + ":" +
                               AssetBundleName);
                OnResLoadFail();
                return false;
            }

            Asset = obj;
            State = ResState.Ready;
            return true;
        }

        /// <inheritdoc />
        public override void LoadAsync()
        {
            //当前不能加载
            if (!CheckLoadAble())
            {
                return;
            }

            if (string.IsNullOrEmpty(AssetBundleName))
            {
                //没有所属 Bundle（配置缺失）：直接失败，避免 Loader 永久等待回调。
                Debug.LogError("#YLFramework: Not Find AssetBundle For Asset:" + AssetName);
                OnResLoadFail();
                return;
            }

            State = ResState.Loading;
            ServiceManager.Instance.AddIEnumeratorTask(this);
        }

        /// <inheritdoc />
        public override IEnumerator DoLoadAsync(Action finishCallback)
        {
            //当前没有引用了，无需执行加载
            if (RefCount <= 0)
            {
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            var resKey = ResKey.Allocate(AssetBundleName, null, typeof(AssetBundle));
            var abRes = ServiceManager.Instance.GetRes<AssetBundleRes>(resKey);
            resKey.RecycleToCache();

            //模拟环境加载
            if (AssetBundleHelper.SimulationMode && !IsAssetBundleManifest())
            {
                if (abRes == null)
                {
                    Debug.LogError("#YLFramework: Failed to Load Asset,Not Find AssetBundleRes:" + AssetBundleName);
                    OnResLoadFail();
                    finishCallback?.Invoke();
                    yield break;
                }

                var assetPaths = AssetBundleHelper.GetAssetPathsFromAssetBundleAndAssetName(abRes.AssetName, AssetName);
                if (assetPaths == null || assetPaths.Length == 0)
                {
                    Debug.LogError("#YLFramework: Failed Load Asset:" + AssetName);
                    OnResLoadFail();
                    finishCallback?.Invoke();
                    yield break;
                }

                //确保加载过程中依赖资源不被释放；即使协程被销毁，finally 仍成对释放。
                HoldDependRes();
                State = ResState.Loading;
                try
                {
                    //模拟登一帧
                    yield return new WaitForEndOfFrame();
                }
                finally
                {
                    UnHoldDependRes();
                }

                Exception loadException = null;
                try
                {
                    Asset = AssetType != null
                        ? AssetBundleHelper.LoadAssetAtPath(assetPaths[0], AssetType)
                        : AssetBundleHelper.LoadAssetAtPath<Object>(assetPaths[0]);
                }
                catch (Exception e)
                {
                    loadException = e;
                }

                if (loadException != null)
                {
                    Debug.LogException(loadException);
                    Asset = null;
                }
            }
            else
            {
                if (abRes == null || abRes.AssetBundle == null)
                {
                    Debug.LogError("#YLFramework: Failed to Load Asset,Not Find AssetBundle:" + AssetBundleName);
                    OnResLoadFail();
                    finishCallback?.Invoke();
                    yield break;
                }

                //确保加载过程中依赖资源不被释放；即使协程被销毁，finally 仍成对释放。
                HoldDependRes();
                State = ResState.Loading;

                AssetBundleRequest abRequest = null;
                Exception startException = null;
                try
                {
                    abRequest = AssetType != null
                        ? abRes.AssetBundle.LoadAssetAsync(AssetName, AssetType)
                        : abRes.AssetBundle.LoadAssetAsync(AssetName);
                }
                catch (Exception e)
                {
                    startException = e;
                }

                if (startException != null)
                {
                    Debug.LogException(startException);
                    UnHoldDependRes();
                    OnResLoadFail();
                    finishCallback?.Invoke();
                    yield break;
                }

                _assetBundleRequest = abRequest;
                try
                {
                    yield return abRequest;
                }
                finally
                {
                    _assetBundleRequest = null;
                    UnHoldDependRes();
                }

                //请求完成但资产为空（资源名不存在）同样按失败处理，不能以 null 资产进入 Ready。
                if (!abRequest.isDone || abRequest.asset == null)
                {
                    Debug.LogError("#YLFramework: Failed to Load Asset:" + AssetName);
                    OnResLoadFail();
                    finishCallback?.Invoke();
                    yield break;
                }

                Asset = abRequest.asset;
            }

            if (Asset == null)
            {
                Debug.LogError("#YLFramework: Failed to Load Asset:" + AssetName);
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            State = ResState.Ready;
            finishCallback?.Invoke();
        }

        /// <summary>
        /// 返回本资产加载期间需要保持引用的 Bundle 名列表（当前仅有所属 Bundle）。
        /// 配置缺失时返回 null；加载完成后由 <see cref="Res.UnHoldDependRes"/> 成对释放。
        /// </summary>
        public override List<string> GetDependBundleList()
        {
            return _assetBundles?.ToList();
        }

        public override void OnRecycle()
        {
            base.OnRecycle();
            _assetBundles = null;
            _assetBundleRequest = null;
        }

        public override void RecycleToCache()
        {
            SafeObjectPool<AssetRes>.Instance.Recycle(this);
        }

        protected override float CalculateLoadProgress()
        {
            if (_assetBundleRequest == null)
            {
                return 0;
            }

            return _assetBundleRequest.progress;
        }

        /// <summary>
        /// 资源名是否为 AssetBundleManifest（该资产不走模拟模式的 AssetDatabase 路径）。
        /// </summary>
        private bool IsAssetBundleManifest()
        {
            return string.Equals(AssetName, "assetbundlemanifest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 通过资源数据表定位所属 Bundle；失败时保持 <see cref="AssetBundleName"/> 为 null。
        /// </summary>
        protected void InitAssetBundleInfo()
        {
            _assetBundles = null;

            var resKey = ResKey.Allocate(AssetName,_ownerBundleName,AssetType);

            var config =  AssetBundleSetting.AssetBundleDataFile.GetAssetData(resKey);

            resKey.RecycleToCache();

            if (config == null)
            {
                Debug.LogError("#YLFramework: Not Find AssetData For Asset:" + AssetName);
                return;
            }

            var assetBundleName = config.OwnerBundleName;

            if (string.IsNullOrEmpty(assetBundleName))
            {
                Debug.LogError("#YLFramework: Not Find AssetBundle In Config:" + config.AbIndex + _ownerBundleName);
                return;
            }

            _assetBundles = new string[1];
            _assetBundles[0] = assetBundleName;
        }

        public override string ToString()
        {
            return $"Type:Asset\t {base.ToString()}\t FromAssetBundle:{AssetBundleName}";
        }
        
    }
}
