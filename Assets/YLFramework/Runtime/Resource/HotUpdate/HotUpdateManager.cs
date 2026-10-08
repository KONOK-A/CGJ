using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YLFramework.Core.Singleton;


namespace YLFramework.Services.Resource
{
    /// <summary>热更状态。</summary>
    public enum HotUpdateStates
    {
        /// <summary>从未进行过热更。</summary>
        NeverUpdate,
        /// <summary>本地（persistentDataPath）资源版本较新。</summary>
        Updated,
        /// <summary>本地更新被包内版本覆盖（包内版本不低于本地版本）。</summary>
        Override
    }

    /// <summary>
    /// 热更流程入口：检查热更状态、比较版本并触发资源下载。
    /// <para>
    /// 当前实现状态（与源码一致，接入版本文件前调用方需注意）：
    /// 版本文件路径的来源工具已被注释，实际使用空路径，
    /// 因此 <see cref="CheckState"/> 总是把状态置为 NeverUpdate；
    /// <see cref="GetLocalResVersion"/> 在 NeverUpdate / Override 状态下直接返回、不调用回调，
    /// 依赖该回调的组合流程（如 <see cref="CheckHasNewVersionRes"/>）不会得到结果。
    /// </para>
    /// </summary>
    public class HotUpdateManager : Singleton<HotUpdateManager>
    {
        /// <summary>当前热更状态；初始为 <see cref="HotUpdateStates.NeverUpdate"/>。</summary>
        public HotUpdateStates HotUpdateState { get; private set; }

        public override void OnSingletonInit()
        {
            base.OnSingletonInit();
            HotUpdateState = HotUpdateStates.NeverUpdate;
        }

        /// <summary>检查热更状态；当前版本文件路径为空，总是置为 NeverUpdate 并调用 checkDone（见类注释）。</summary>
        public void CheckState(Action checkDone)
        {
            var persistentResVersionPath = string.Empty;//ResUtility.GetPersistentAssetResVersionPath();
            if (!File.Exists(persistentResVersionPath))
            {
                HotUpdateState = HotUpdateStates.NeverUpdate;
                checkDone?.Invoke();
            }
            else
            {
                var persistentResVersionJson = File.ReadAllText(persistentResVersionPath);
                var persistentResVersion = JsonConvert.DeserializeObject<ResVersion>(persistentResVersionJson);

                
                // ActionManager.Coroutine(()=> GetStreamingAssetResVersion(streamingResVersion =>
                // {
                //     Debug.Log(persistentResVersion.version + "." + streamingResVersion);
                //     HotUpdateState = persistentResVersion.version > streamingResVersion
                //         ? HotUpdateStates.Updated
                //         : HotUpdateStates.Override;
                //     checkDone?.Invoke();
                // })).StartGlobal();
            }
        }

        private static IEnumerator GetStreamingAssetResVersion(Action<int> getResVersion)
        {
            var streamingResVersionFilePath = default(string);//ResUtility.GetStreamingAssetResVersionPath();
            var www = new WWW(streamingResVersionFilePath);
            yield return www;
            var streamingResVersionJson = www.text;
            var streamingResVersion = JsonConvert.DeserializeObject<ResVersion>(streamingResVersionJson);
            getResVersion?.Invoke(streamingResVersion.version);
        }
        
        /// <summary>
        /// 获取本地资源版本：NeverUpdate / Override 状态下直接返回、不会调用回调（见类注释）；
        /// 其余状态读取本地版本文件并回调版本号。
        /// </summary>
        public void GetLocalResVersion(Action<int> getLocalResVersion)
        {
            if (HotUpdateState is HotUpdateStates.NeverUpdate or HotUpdateStates.Override)
            {
                // ActionManager.Coroutine(() => GetStreamingAssetResVersion(getLocalResVersion)).StartGlobal();
                return;
            }
            
            var persistentVersionPath = default(string);//ResUtility.GetPersistentAssetResVersionPath();
            var json = File.ReadAllText(persistentVersionPath);
            var persistentResVersion = JsonConvert.DeserializeObject<ResVersion>(json);
            getLocalResVersion?.Invoke(persistentResVersion.version);
        }

        /// <summary>比较远程与本地资源版本，回调是否存在新版本；依赖 <see cref="GetLocalResVersion"/> 的回调（见类注释）。</summary>
        public void CheckHasNewVersionRes(Action<bool> onResult)
        {
            FakeResServer.Instance.GetRemoteResVersion(remoteResVersion =>
            {
                GetLocalResVersion(localResVersion =>
                {
                    onResult(remoteResVersion > localResVersion);
                });
            });
        }

        /// <summary>从服务器（FakeResServer）下载资源，完成后回调。</summary>
        public void UpdateRes(Action onUpdateDone)
        {
            //从服务器端下载资源
            Debug.Log("开始更新");
            FakeResServer.Instance.DownloadRes(() =>
            {
                
                Debug.Log("结束更新");
                onUpdateDone?.Invoke();
            });
        }
        
        private static void ReplaceRes()
        {
            Debug.Log("替换本地资源");
            var tempAssetBundleFolders = FakeResServer.TempAssetBundlesPath;
            var assetBundleFolders = default(string);//ResUtility.PersistentAssetBundlesFolder;
            //先删除当前的资源目录
            if (Directory.Exists(assetBundleFolders))
            {
                Directory.Delete(assetBundleFolders, true);
            }
            //将临时资源移动到真正资源目录下
            Directory.Move(tempAssetBundleFolders, assetBundleFolders);
            //结束时删除临时的资源目录
            if (Directory.Exists(tempAssetBundleFolders))
            {
                Directory.Delete(tempAssetBundleFolders, true);
            }
        }
    }
}