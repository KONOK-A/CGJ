using System;
using System.Collections;
using UnityEngine;
using YLFramework.Core.Pool;
using Object = UnityEngine.Object;

namespace YLFramework.Services.Resource
{

    /// <summary>LocalImage Res 的名字工具。</summary>
    public static class LocalIamgeResUtil
    {
        /// <summary>把本地文件路径包装为 LocalImage Res 名（AssetName 会被 ResKey 统一转小写）。</summary>
        public static string ToLocalImageResName(this string selfFilePath)
        {
            return $"LocalImage:{selfFilePath}";
        }
    }

    /// <summary>
    /// 本地图片（file://）的 IRes 实现。
    /// <para>
    /// Ownership：<see cref="Asset"/> 是 WWW 在运行时解码出的 Texture2D（运行时创建对象，不是 Resources/Bundle 资产），
    /// 引用计数归零时由 <see cref="OnReleaseRes"/> 通过 <c>Object.Destroy</c> 销毁，不能使用 Resources.UnloadAsset。
    /// </para>
    /// <para>
    /// 路径大小写：AssetName 是 ResKey 归一后的小写名（用于查找），实际文件路径来自
    /// <see cref="ResKey.OriginalAssetName"/>，保留原始大小写，避免大小写敏感平台加载失败。
    /// 失败语义与核心契约一致：任何失败（文件缺失/解码失败/零引用）都回 Waiting 并通知一次 false。
    /// </para>
    /// </summary>
    public class LocalImageRes : Res
    {
        /// <summary>Res 名称前缀，路径从 OriginalAssetName 中去掉该前缀得到。</summary>
        private const string NamePrefix = "LocalImage:";

        private string _fullPath;
        private string _hashCode;
        private object _rawAsset;

#pragma warning disable CS0618 // 类型或成员已过时
        private WWW _www;
#pragma warning restore CS0618 // 类型或成员已过时

        /// <summary>兼容旧调用：AssetName 与文件路径都将使用 <paramref name="name"/>（不做大小写处理）。</summary>
        public static LocalImageRes Allocate(string name)
        {
            return Allocate(name, name);
        }

        /// <summary>
        /// 从对象池分配：<paramref name="lowerName"/> 作为归一化 AssetName，
        /// <paramref name="originalName"/> 用于提取保留大小写的真实文件路径。
        /// </summary>
        public static LocalImageRes Allocate(string lowerName, string originalName)
        {
            var res = SafeObjectPool<LocalImageRes>.Instance.Allocate();
            if (res != null)
            {
                res.AssetName = lowerName;
                res.SetUrl(GetPath(originalName));
            }
            return res;
        }

        private static string GetPath(string originalName)
        {
            if (string.IsNullOrEmpty(originalName) || originalName.Length < NamePrefix.Length)
            {
                return originalName;
            }
            return originalName.Substring(NamePrefix.Length);
        }

        /// <summary>由路径哈希拼出的本地缓存路径（位于 PersistentDataPathForPhoto 下）；本类不会写入该文件。</summary>
        public string LocalResPath => $"{AssetBundleHelper.PersistentDataPathForPhoto}{_hashCode}";
        /// <summary>预留的原始数据引用；当前实现不会赋值，恒为 null。</summary>
        public virtual object RawAsset => _rawAsset;
        /// <summary>当前是否有引用（RefCount &gt; 0）。</summary>
        public bool NeedDownload => RefCount > 0;
        /// <summary>保留原始大小写的本地文件路径。</summary>
        public string Url => _fullPath;
        /// <summary>固定返回 1，未反映实际文件大小。</summary>
        public int FileSize => 1;

        /// <summary>设置本地文件路径并刷新缓存哈希；<paramref name="url"/> 为空时忽略（不覆盖已有值）。</summary>
        public void SetUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return;
            }
            _fullPath = url;
            _hashCode = $"Photo_{_fullPath.GetHashCode()}";
        }

        /// <inheritdoc />
        public override bool UnloadInstantiate(bool flag)
        {
            return false;
        }

        /// <summary>
        /// 本地图片不支持同步加载：恒返回 false 且不改变 State；请使用 <see cref="LoadAsync"/>。
        /// </summary>
        public override bool LoadSync()
        {
            //本地图片只支持异步加载
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
                Debug.LogError("#YLFramework: LocalImageRes AssetName Is Empty");
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

        private void OnDownLoadResult(bool result)
        {
            if (!result)
            {
                OnResLoadFail();
                return;
            }

            //调度前引用已归零：不再发起请求，按失败通知（Loading 期间由 SimpleRc 保护，不会提前回收）。
            if (RefCount <= 0)
            {
                Debug.LogError("#YLFramework: LocalImageRes RefCount Is Zero, Url:" + _fullPath);
                OnResLoadFail();
                return;
            }
            ServiceManager.Instance.AddIEnumeratorTask(this);
        }

        /// <summary>
        /// 释放运行时创建的 Texture2D；WWW 在成功路径已 Dispose。
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
            SafeObjectPool<LocalImageRes>.Instance.Recycle(this);
        }

        /// <summary>
        /// 回池清理：调用基类清空 AssetName / 加载事件，并重置路径与请求字段，避免复用残留。
        /// </summary>
        public override void OnRecycle()
        {
            base.OnRecycle();
            _fullPath = null;
            _hashCode = null;
            _rawAsset = null;
            _www = null;
        }
        
        /// <summary>
        /// 通过 WWW 异步读取并解码本地文件为 Texture2D；所有失败路径都会回到 Waiting 并调用完成回调，
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
            var www = new WWW("file://" + _fullPath);
#pragma warning restore CS0618

            _www = www;
            yield return www;
            _www = null;

            if (www.error != null)
            {
                Debug.LogError($"#YLFramework: Res:{_fullPath}, WWW Errors:{www.error}");
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            if (!www.isDone)
            {
                Debug.LogError("#YLFramework LocalImageRes WWW Not Done! Url:" + _fullPath);
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            //加载后引用数为0，说明无需保留资源，直接按失败结束并由 ServiceManager 回收
            if (RefCount <= 0)
            {
                www.Dispose();
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            //解码失败时 www.texture 为 null：不能以 null Asset 进入 Ready
            var texture = www.texture;
            www.Dispose();
            if (texture == null)
            {
                Debug.LogError("#YLFramework: LocalImageRes Decode Failed! Url:" + _fullPath);
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

    /// <summary>LocalImage Res 创建器：匹配 <c>localimage:</c> 前缀。</summary>
    public class LocalImageResCreator : IResCreator
    {
        /// <inheritdoc />
        public bool Match(ResKey resKey)
        {
            return resKey.AssetName.StartsWith("localimage:", StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public IRes Create(ResKey resKey)
        {
            //AssetName 用小写名查找，文件路径用 OriginalAssetName 保留大小写
            return LocalImageRes.Allocate(resKey.AssetName, resKey.OriginalAssetName);
        }
    }
}
