using System;
using System.Collections;
using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Services.Resource
{

    /// <summary>
    /// 资源加载名字的前缀类型
    /// </summary>
    internal enum ResNamePrefixType
    {
        Url,// resources://
        Folder,// Resources/
    }
    
    /// <summary>
    /// Resources 目录资源的 IRes 实现。
    /// <para>
    /// 路径前缀：<c>resources://</c> 或 <c>resources/</c>；同步 / 异步均通过 Unity Resources API 加载。
    /// 失败（不存在 / 异步取回 null）回到 Waiting 并通知 false，不会以 null 资产进入 Ready。
    /// 卸载走基类 <see cref="Res.OnReleaseRes"/>：GameObject 不调用 Resources.UnloadAsset（Unity 不允许），
    /// 仅清空引用；Texture / AudioClip / ScriptableObject / TextAsset 等走 Resources.UnloadAsset。
    /// </para>
    /// </summary>
    internal class ResourcesRes : Res
    {
        private ResourceRequest _resourceRequest;
        private string _path;
        

        /// <summary>
        /// 按资源名与前缀类型从对象池分配。
        /// </summary>
        public static ResourcesRes Allocate(string name, ResNamePrefixType prefixType)
        {
            var path = prefixType == ResNamePrefixType.Url
                ? name["resources://".Length..]
                : name["Resources/".Length..];
            var res = SafeObjectPool<ResourcesRes>.Instance.Allocate();
            if (res == null) return null;
            res.AssetName = name;
            res._path = path;
            return res;
        }

        /// <inheritdoc />
        public override bool LoadSync()
        {
            //检测结果为不能加载
            if (!CheckLoadAble())
            {
                return false;
            }
            if (string.IsNullOrEmpty(AssetName))
            {
                return false;
            }
            State = ResState.Loading;
            //实际加载
            Asset = AssetType == null ? Resources.Load(_path) : Resources.Load(_path, AssetType);
            if (Asset == null)
            {
                Debug.LogError("Fail to Load Asset From Resources: " + _path);
                OnResLoadFail();
                return false;
            }
            State = ResState.Ready;
            return true;
        }

        /// <inheritdoc />
        public override void LoadAsync()
        {
            if (!CheckLoadAble() || string.IsNullOrEmpty(AssetName))
            {
                return;
            }

            State = ResState.Loading;
            ServiceManager.Instance.AddIEnumeratorTask(this);
        }

        /// <inheritdoc />
        public override IEnumerator DoLoadAsync(Action finishCallback)
        {
            //当前该资源的引用数为0,应该无需加载才对
            if (RefCount <= 0)
            {
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }

            var resourceRequest =
                AssetType == null ? Resources.LoadAsync(_path) : Resources.LoadAsync(_path, AssetType);
            _resourceRequest = resourceRequest;
            yield return resourceRequest;
            _resourceRequest = null;

            var asset = resourceRequest.asset;
            if (!resourceRequest.isDone || asset == null)
            {
                //资源不存在时 Resources.LoadAsync 不会抛异常，只会取回 null 资产，这里按加载失败处理
                Debug.LogError("#YLFramework: Fail To Load Res From Resources: " + _path);
                OnResLoadFail();
                finishCallback?.Invoke();
                yield break;
            }
            //资源真正加载完成
            Asset = asset;
            State = ResState.Ready;
            finishCallback?.Invoke();
        }

        /// <inheritdoc />
        public override void RecycleToCache()
        {
            SafeObjectPool<ResourcesRes>.Instance.Recycle(this);
        }

        protected override float CalculateLoadProgress()
        {
            if (_resourceRequest == null)
            {
                return 0;
            }
            return _resourceRequest.progress;
        }

        public override string ToString()
        {
            return $"Type:Resources {base.ToString()}";
        }
    }
    
}
