using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Bundle 内场景资产的 IRes 实现，同时充当"场景 Bundle 加载门控"。
    /// <para>
    /// Ownership：本 Res 不保存 Scene 引用，也从不调用 SceneManager。
    /// 它只表示"场景所属 Bundle 已经就绪"：Ready 后由 <see cref="ResLoaderExtensions.LoadSceneSync"/> /
    /// LoadSceneAsync 交给 SceneManager 加载场景本体。
    /// 因此 <see cref="Res.ReleaseRes"/> / Loader 回收只释放 Bundle 资源，**不会卸载已加载的 Scene**；
    /// 卸载 Scene 必须由调用方显式调用 SceneManager.UnloadSceneAsync。
    /// </para>
    /// <para>
    /// 失败语义与核心契约一致：Bundle 未就绪 / 配置缺失都回 Waiting 并通知一次 false，
    /// 不会因为静默 return 让 Loader 永久挂起。
    /// </para>
    /// </summary>
    public class AssetBundleSceneRes : AssetRes
    {
        public AssetBundleSceneRes(string assetName) : base(assetName) { }
        public AssetBundleSceneRes() { }
        
        /// <summary>
        /// 从对象池分配并初始化所属 Bundle 信息。
        /// </summary>
        public static AssetBundleSceneRes Allocate(string assetName)
        {
            AssetBundleSceneRes res = SafeObjectPool<AssetBundleSceneRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = assetName;
                res.InitAssetBundleInfo();
            }
            return res;
        }

        /// <summary>
        /// 校验所属 AssetBundle 是否已就绪；成功只表示场景可以交给 SceneManager 加载。
        /// 失败会记录错误、回 Waiting 并通知 false（含配置缺失与 Bundle 未就绪）。
        /// </summary>
        public override bool LoadSync()
        {
            if (!CheckLoadAble())
            {
                return false;
            }

            if (string.IsNullOrEmpty(AssetBundleName))
            {
                UnityEngine.Debug.LogError("#YLFramework: Not Find AssetBundle For Scene:" + AssetName);
                OnResLoadFail();
                return false;
            }

            var resKey = ResKey.Allocate(AssetBundleName, null, typeof(AssetBundle));
            var abRes = ServiceManager.Instance.GetRes<AssetBundleRes>(resKey);
            resKey.RecycleToCache();

            //模拟模式下 Bundle Res 不加载真实文件（AssetBundle 为 null），Ready 即视为就绪。
            if (abRes == null || (!AssetBundleHelper.SimulationMode && abRes.AssetBundle == null))
            {
                UnityEngine.Debug.LogError("#YLFramework: Load Scene Failed, AssetBundle Not Ready:" + AssetBundleName);
                OnResLoadFail();
                return false;
            }

            State = ResState.Ready;
            return true;
        }

        /// <summary>
        /// 异步加载：只等待所属 Bundle；Bundle 就绪立即 Ready 并通知，
        /// 失败由 <see cref="LoadSync"/> 统一走失败路径（回 Waiting + 通知 false）。
        /// </summary>
        public override void LoadAsync()
        {
            if (!CheckLoadAble())
            {
                return;
            }

            LoadSync();
        }

        public override void RecycleToCache()
        {
            SafeObjectPool<AssetBundleSceneRes>.Instance.Recycle(this);
        }
    }
}
