using System;
using System.Collections;
using UnityEngine;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{

    /// <summary>NetImage Res 的名字工具。</summary>
    public static class NetImageResUtil
    {
        /// <summary>把网络 URL 包装为 NetImage Res 名（AssetName 会被 ResKey 统一转小写）。</summary>
        public static string ToNetImageResName(this string selfHttpUrl)
        {
            return $"NetImage:{selfHttpUrl}";
        }
    }

    /// <summary>
    /// 网络图片的 IRes 实现（WWW）。
    /// <para>
    /// Ownership：<see cref="Asset"/> 是 WWW 在运行时解码出的 Texture2D（运行时创建对象），
    /// 引用计数归零时由 <see cref="OnReleaseRes"/> 通过 <c>Object.Destroy</c> 销毁。
    /// URL 取自 <see cref="ResKey.OriginalAssetName"/>，保留原始大小写与大小写敏感的路径段。
    /// </para>
    /// <para>
    /// 失败语义与核心契约一致：URL 无效 / 网络错误 / 解码失败 / 零引用都回 Waiting 并通知一次 false，
    /// 不会以 null Asset 进入 Ready，也不会因静默 return 让 Loader 永久挂起。
    /// </para>
    /// </summary>
    public class NetImageRes : Res
    {
        /// <summary>Res 名称前缀，URL 从 OriginalAssetName 中去掉该前缀得到。</summary>
        private const string NamePrefix = "NetImage:";

        private string _url;
        private string _hashCode;
        private object _rawAsset;
#pragma warning disable CS0618
        private WWW _www;
#pragma warning restore CS0618

        /// <summary>
        /// 从对象池分配：<paramref name="lowerName"/> 作为归一化 AssetName，
        /// <paramref name="originalName"/> 用于提取保留大小写的真实 URL。
        /// </summary>
        public static NetImageRes Allocate(string lowerName,string originalName)
        {
            var res = SafeObjectPool<NetImageRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = lowerName;
                res.SetUrl(GetUrl(originalName));
            }
            return res;
        }

        private static string GetUrl(string originalName)
        {
            if (string.IsNullOrEmpty(originalName) || originalName.Length < NamePrefix.Length)
            {
                return originalName;
            }
            return originalName.Substring(NamePrefix.Length);
        }

        /// <summary>由 URL 哈希拼出的本地缓存路径（位于 PersistentDataPathForPhoto 下）；本类不会写入该文件。</summary>
        public string LocalResPath => $"{AssetBundleHelper.PersistentDataPathForPhoto}{_hashCode}";

        /// <summary>预留的原始数据引用；当前实现不会赋值，恒为 null。</summary>
        public virtual object RawAsset => _rawAsset;
        /// <summary>当前是否有引用（RefCount &gt; 0）。</summary>
        public bool NeedDownload => RefCount > 0;
        /// <summary>保留原始大小写的 URL。</summary>
        public string Url => _url;
        /// <summary>固定返回 1，未反映实际文件大小。</summary>
        public int FileSize => 1;

        private void SetUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            _url = url;
            _hashCode = $"Photo_{_url.GetHashCode()}";
        }

        /// <inheritdoc />
        public override bool UnloadInstantiate(bool flag)
        {
            return false;
        }

        /// <summary>
        /// 网络图片不支持同步加载：恒返回 false 且不改变 State；请使用 <see cref="LoadAsync"/>。
        /// </summary>
        public override bool LoadSync()
        {
            //网络图片只支持异步加载
            return false;
        }

        /// <inheritdoc />
        public override void LoadAsync()
        {
            if (!CheckLoadAble())
            {
                return;
            }

            if (string.IsNullOrEmpty(AssetName))
            {
                //没有资源名：直接失败，避免 Loader 永久等待回调。
                Debug.LogError("#YLFramework: NetImageRes AssetName Is Empty");
                OnResLoadFail();
                return;
            }

            DoLoadWork();
        }

        private void DoLoadWork()
        {
            State = ResState.Loading;

            OnDownLoadResult(true);
            
        }

        /// <summary>
        /// 释放运行时创建的 Texture2D；WWW 在各完成路径已 Dispose。
        /// 即使 Texture 已被外部销毁（fake-null），字段也必须清空，避免池复用残留。
        /// </summary>
        protected override void OnReleaseRes()
        {
            if (Asset != null)
            {
                Object.Destroy(Asset);
            }
            Asset = null;
            _rawAsset = null;
        }

        public override void RecycleToCache()
        {
            SafeObjectPool<NetImageRes>.Instance.Recycle(this);
        }

        /// <summary>
        /// 回池清理：调用基类清空 AssetName / 加载事件，并重置 URL 与请求字段，避免复用残留。
        /// </summary>
        public override void OnRecycle()
        {
            base.OnRecycle();
            _url = null;
            _hashCode = null;
            _rawAsset = null;
            _www = null;
        }


        private void OnDownLoadResult(bool result)
        {
            if (!result)
            {
                OnResLoadFail();
                return;
            }

            //调度前引用已归零：不再发起请求，按失败通知。
            if (RefCount <= 0)
            {
                Debug.LogError("#YLFramework: NetImageRes RefCount Is Zero, Url:" + _url);
                OnResLoadFail();
                return;
            }
            ServiceManager.Instance.AddIEnumeratorTask(this);
        }

        /// <summary>
        /// 通过 WWW 异步加载并解码 Texture2D；所有失败路径都会回到 Waiting 并调用完成回调，
        /// 只有解码出非 null 的 Texture 才进入 Ready。
        /// </summary>
        /// <param name="finishCallback">加载结束（无论成败）时调用一次。</param>
        //完全的WWW方式,Unity 帮助管理纹理缓存，效率貌似更高
        public override IEnumerator DoLoadAsync(Action finishCallback)
        {
            if (RefCount <= 0)
            {
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

#pragma warning disable CS0618
            var www = new WWW(_url);
#pragma warning restore CS0618

            _www = www;
            yield return www;
            _www = null;

            if (www.error != null)
            {
                Debug.LogError($"#YLFramework: Res:{_url}, WWW Errors:{www.error}");
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            if (!www.isDone)
            {
                Debug.LogError("#YLFramework: NetImageRes WWW Not Done! Url:" + _url);
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            if (RefCount <= 0)
            {
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            //这里是同步的操作；解码失败时 www.texture 为 null，不能进入 Ready
            var texture = www.texture;
            www.Dispose();
            if (texture == null)
            {
                Debug.LogError("#YLFramework: NetImageRes Decode Failed! Url:" + _url);
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            Asset = texture;
            State = ResState.Ready;
            finishCallback?.Invoke();
        }

        protected override float CalculateLoadProgress()
        {
            if (_www == null)
            {
                return 0;
            }
            return _www.progress;
        }
    }

    /// <summary>NetImage Res 创建器：匹配 <c>netimage:</c> 前缀。</summary>
    public class NetImageResCreator : IResCreator
    {
        /// <inheritdoc />
        public bool Match(ResKey resKey)
        {
            return resKey.AssetName.StartsWith("netimage:", StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public IRes Create(ResKey resKey)
        {
            //AssetName 用小写名查找，URL 用 OriginalAssetName 保留大小写
            return NetImageRes.Allocate(resKey.AssetName,resKey.OriginalAssetName);
        }
    }
}
