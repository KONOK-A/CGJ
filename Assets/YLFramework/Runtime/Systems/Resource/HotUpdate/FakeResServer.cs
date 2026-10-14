using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Services.Resource
{
    /// <summary>资源版本信息（版本号 + 该版本的 Bundle 文件名列表）。</summary>
    [Serializable]
    public class ResVersion
    {
        /// <summary>版本号。</summary>
        public int version;
        /// <summary>该版本包含的 AssetBundle 文件名列表。</summary>
        public List<string> assetBundleNames = new();
    }
    
    /// <summary>
    /// 本地模拟资源服务器：从工程目录（Application.dataPath 下 HotUpdate/Remote）读取版本文件与 Bundle 文件，
    /// 模拟热更下载流程，下载结果写入 <see cref="TempAssetBundlesPath"/>。
    /// </summary>
    public class FakeResServer : MonoSingleton<FakeResServer>
    {
        /// <summary>下载临时目录（persistentDataPath/TempAssetBundles/）。</summary>
        public static string TempAssetBundlesPath => Application.persistentDataPath + "/TempAssetBundles/";

        /// <summary>读取远程版本文件，回调版本号。</summary>
        public void GetRemoteResVersion(Action<int> onResVersionGet)
        {
            StartCoroutine(RequestRemoteResVersion(resVersion =>
            {
                onResVersionGet(resVersion.version);
            }));
        }

        
        /// <summary>读取远程版本并下载其中列出的 Bundle（含 .manifest）到临时目录，完成后回调。</summary>
        public void DownloadRes(Action onResDownloaded)
        {
            StartCoroutine(RequestRemoteResVersion(remoteResVersion =>
            {
                StartCoroutine(DoDownloadRes(remoteResVersion, onResDownloaded));
            }));
        }

        // 实际下载流程：先把远程版本写入临时目录，再逐个把 Bundle 及其 .manifest 复制到临时目录。
        private IEnumerator DoDownloadRes(ResVersion remoteResVersion,Action onResDownloadDone)
        {
            //创建临时目录
            if (!Directory.Exists(TempAssetBundlesPath))
            {
                Directory.CreateDirectory(TempAssetBundlesPath);
            }
            var tempResVersionFilePath = TempAssetBundlesPath + "ResVersion.json";
            var tempResVersionJson = JsonConvert.SerializeObject(remoteResVersion, Formatting.Indented);
            File.WriteAllText(tempResVersionFilePath, tempResVersionJson);
            var remoteBasePath = Application.dataPath + "/YLFramework/Runtime/Systems/Resource/HotUpdate/Remote/";
            //remoteResVersion.assetBundleNames.Add(ResUtility.GetPlatformName());
            foreach (var assetBundleName in remoteResVersion.assetBundleNames)
            {
                
                var wwwAssets = new WWW(remoteBasePath + assetBundleName);
                var wwwManifest = new WWW(remoteBasePath + $"{assetBundleName}.manifest");
                yield return wwwAssets;
                yield return wwwManifest;
                var bytes = wwwAssets.bytes;
                var filePath = TempAssetBundlesPath + assetBundleName;
                File.WriteAllBytes(filePath, bytes);
                bytes = wwwManifest.bytes; 
                filePath = TempAssetBundlesPath + $"{assetBundleName}.manifest";
                File.WriteAllBytes(filePath, bytes);
            }
            onResDownloadDone?.Invoke();
        }

        private IEnumerator RequestRemoteResVersion(Action<ResVersion> onResDownloaded)
        {
            var remoteResVersionPath =
                Application.dataPath + "/YLFramework/Runtime/Systems/Resource/HotUpdate/Remote/ResVersion.json";
            var www = new WWW(remoteResVersionPath);
            yield return www;
            
            var json = www.text;
            var resVersion = JsonUtility.FromJson<ResVersion>(json);
            onResDownloaded(resVersion);
        }
    }
}
