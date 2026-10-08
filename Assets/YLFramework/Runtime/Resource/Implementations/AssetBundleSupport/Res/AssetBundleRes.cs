using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using YLFramework.Core.Pool;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// AssetBundle 文件本身的 IRes 实现。
    /// <para>
    /// Ownership：<see cref="AssetBundle"/> 属性持有 UnityEngine.AssetBundle；
    /// 引用计数归零时 <see cref="OnReleaseRes"/> 调用 <c>AssetBundle.Unload(_unloadFlag)</c> 卸载。
    /// 默认 <c>_unloadFlag = true</c>（Unload(true)，同时销毁已加载对象）；
    /// <see cref="UnloadInstantiate"/> 会按 Loader 传入的 flag 改写它（实例 ownership 属于后续阶段）。
    /// </para>
    /// <para>
    /// 依赖：<see cref="GetDependBundleList"/> 来自资源数据表（Bundle -> 依赖 Bundle）。
    /// 依赖的加载与持有由 <see cref="ResLoader"/> 统一完成（同步按依赖优先顺序、异步以
    /// <see cref="Res.DependResLoadIsFinish"/> 门控），本类自身不额外 Retain 依赖。
    /// </para>
    /// <para>
    /// 失败回滚：同步/异步加载失败（文件缺失、request.assetBundle 为 null）都会回 Waiting 并通知 false，
    /// 不会以 null Asset 进入 Ready；加载异常会记录日志并按失败处理。
    /// </para>
    /// </summary>
    public class AssetBundleRes : Res
    {
        #region 字段

        /// <summary>Unload 参数：true 同时销毁从该 Bundle 加载出的对象；默认 true。</summary>
        private bool _unloadFlag = true;
        private string[] _dependencies;
        private AsyncOperation _assetBundleCreateRequest;

        #endregion

        #region 属性

        /// <summary>已加载的 UnityEngine.AssetBundle；未加载或已卸载时为 null。</summary>
        public AssetBundle AssetBundle
        {
            get => (AssetBundle)Asset;
            set => Asset = value;
        }

        #endregion

        /// <summary>
        /// 从资源数据表读取本 Bundle 的依赖 Bundle 名（无依赖时为 null）。
        /// </summary>
        public void InitAssetBundleInfo()
        {
            _dependencies = AssetBundleSetting.AssetBundleDataFile.GetAllDependenciesByUrl(AssetName);
        }

        /// <summary>
        /// 从对象池分配并初始化依赖信息。
        /// </summary>
        public static AssetBundleRes Allocate(string name)
        {
            var abRes = SafeObjectPool<AssetBundleRes>.Instance.Allocate();
            abRes.AssetName = name;
            abRes.AssetType = typeof(AssetBundle);
            abRes.InitAssetBundleInfo();
            return abRes;
        }

        /// <inheritdoc />
        public override bool LoadSync()
        {
            //检查能否进行
            if (!CheckLoadAble())
            {
                return false;
            }

            State = ResState.Loading;
            //新一轮加载重置 Unload 参数，避免上一轮 UnloadInstantiate(false) 的 flag 污染本次卸载。
            _unloadFlag = true;
            if (!AssetBundleHelper.SimulationMode)
            {
                var url = AssetBundleSetting.ConvertAssetBundleNameToUrl(AssetName);
                AssetBundle assetBundle = null;
                try
                {
                    assetBundle = AssetBundle.LoadFromFile(url);
                }
                catch (Exception e)
                {
                    //异常也按加载失败回滚，避免 State 永久停留在 Loading。
                    Debug.LogException(e);
                }

                if (assetBundle == null)
                {
                    Debug.LogError("#YLFramework: Failed to load AssetBundle:" + AssetName);
                    OnResLoadFail();
                    return false;
                }

                AssetBundle = assetBundle;
            }

            //模拟模式不加载真实文件：Bundle Res 仅作为依赖图节点直接进入 Ready。
            State = ResState.Ready;
            return true;
        }

        /// <inheritdoc />
        public override void LoadAsync()
        {
            if (!CheckLoadAble())
            {
                return;
            }

            //新一轮加载重置 Unload 参数，避免上一轮 UnloadInstantiate(false) 的 flag 污染本次卸载。
            _unloadFlag = true;
            State = ResState.Loading;
            ServiceManager.Instance.AddIEnumeratorTask(this);
        }

        /// <summary>
        /// 实际的异步加载：模拟模式空转一帧；WebGL 走 UnityWebRequest；
        /// 其他平台走 AssetBundle.LoadFromFileAsync。完成时先提交 Asset 再进入 Ready 并回调。
        /// </summary>
        public override IEnumerator DoLoadAsync(Action finishCallback)
        {
            //开始的时候已经结束了
            if (RefCount <= 0)
            {
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            if (AssetBundleHelper.SimulationMode)
            {
                yield return null;
            }
            else
            {
                var url = AssetBundleSetting.ConvertAssetBundleNameToUrl(AssetName);
                if (PlatformCheck.IsWebGL)
                {
                    UnityWebRequest abcR = null;
                    Exception startException = null;
                    try
                    {
                        abcR = UnityWebRequestAssetBundle.GetAssetBundle(url);
                        _assetBundleCreateRequest = abcR.SendWebRequest();
                    }
                    catch (Exception e)
                    {
                        startException = e;
                    }

                    if (startException != null)
                    {
                        Debug.LogException(startException);
                        OnResLoadFail();
                        finishCallback?.Invoke();
                        yield break;
                    }

                    yield return _assetBundleCreateRequest;
                    _assetBundleCreateRequest = null;

                    //请求失败或内容为空都不能进入 Ready；无论成功失败都要释放 request。
                    var error = abcR.error;
                    var ab = string.IsNullOrEmpty(error) ? DownloadHandlerAssetBundle.GetContent(abcR) : null;
                    abcR.Dispose();

                    if (!string.IsNullOrEmpty(error) || ab == null)
                    {
                        Debug.LogError("#YLFramework: Failed to load AssetBundle(WebGL):" + AssetName +
                                       " Error:" + error);
                        OnResLoadFail();
                        finishCallback?.Invoke();
                        yield break;
                    }

                    AssetBundle = ab;
                }
                else
                {
                    AssetBundleCreateRequest abcR = null;
                    Exception startException = null;
                    try
                    {
                        abcR = AssetBundle.LoadFromFileAsync(url);
                    }
                    catch (Exception e)
                    {
                        startException = e;
                    }

                    if (startException != null)
                    {
                        Debug.LogException(startException);
                        OnResLoadFail();
                        finishCallback?.Invoke();
                        yield break;
                    }

                    _assetBundleCreateRequest = abcR;
                    yield return abcR;
                    _assetBundleCreateRequest = null;

                    //request 完成但 assetBundle 为 null（文件缺失/损坏）按失败处理。
                    if (!abcR.isDone || abcR.assetBundle == null)
                    {
                        Debug.LogError("#YLFramework: Failed to load AssetBundle:" + AssetName);
                        OnResLoadFail();
                        finishCallback?.Invoke();
                        yield break;
                    }

                    AssetBundle = abcR.assetBundle;
                }
            }

            State = ResState.Ready;
            finishCallback?.Invoke();
        }

        /// <summary>
        /// 依赖 Bundle 名列表；依赖的实际加载/持有由 ResLoader 负责。
        /// </summary>
        public override List<string> GetDependBundleList()
        {
            return _dependencies?.ToList();
        }

        /// <summary>
        /// 记录 Loader 期望的 Unload 行为：flag 会作为后续 <see cref="AssetBundle.Unload(bool)"/> 的参数。
        /// 仅在 Bundle 已加载时生效；属于"当前一轮加载"的参数，新一轮加载与
        /// <see cref="OnRecycle"/> 都会重置为 true。固定返回 true 表示该 Res 属于"有实例需要处理"的一类。
        /// </summary>
        public override bool UnloadInstantiate(bool flag)
        {
            if (AssetBundle != null)
            {
                _unloadFlag = flag;
            }

            return true;
        }

        public override void RecycleToCache()
        {
            SafeObjectPool<AssetBundleRes>.Instance.Recycle(this);
        }

        public override void OnRecycle()
        {
            base.OnRecycle();
            _unloadFlag = true;
            _dependencies = null;
            _assetBundleCreateRequest = null;
        }

        protected override float CalculateLoadProgress()
        {
            if (_assetBundleCreateRequest == null)
            {
                return 0;
            }
            return _assetBundleCreateRequest.progress;
        }

        /// <summary>
        /// 卸载 UnityEngine.AssetBundle：只有引用计数归零（ReleaseRes）才会走到这里；
        /// Unload 参数取决 <see cref="_unloadFlag"/>（默认 true，历史语义保持不变）。
        /// 重复调用是安全的（AssetBundle 已为 null 时直接跳过）。
        /// </summary>
        protected override void OnReleaseRes()
        {
            if (AssetBundle != null)
            {
                //Unload 会连同从该 Bundle 加载出的对象一起处理（是否销毁对象取决于 _unloadFlag）；
                //模拟模式下本类不加载真实 Bundle 文件，不会走到这里。
                AssetBundle.Unload(_unloadFlag);
            }

            //无论资产是否已失效，都必须清空引用，避免池复用残留
            Asset = null;
        }

        public override string ToString()
        {
            return $"Type:AssetBundle\t {base.ToString()}";
        }
    }
}
