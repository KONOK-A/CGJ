using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// AssetBundle 运行时设置与路径规则。
    /// <para>
    /// 路径优先级：persistentDataPath 中的 AssetBundles/&lt;Platform&gt;/ 优先（热更后的资源），
    /// 不存在时回退 StreamingAssets 中的同结构目录。
    /// </para>
    /// </summary>
    public class AssetBundleSetting
    {
        private static IResData _assetBundleDataFile = null;
        //默认工厂方法为直接new一个对象出来
        private static Func<IResData> _assetBundleDataFileFactory = () => new ResData();
        /// <summary>子工程的资源数据表集合；运行时由外部注册，当前仅参与 Creator 匹配。</summary>
        public static List<IResData> SubProjectAssetBundleDataFiles = new();

        /// <summary>资源数据表工厂，可在运行时替换（例如接入自定义配置加载）。</summary>
        public static Func<IResData> AssetBundleDataFileFactory
        {
            set => _assetBundleDataFileFactory = value;
        }

        /// <summary>
        /// 当前资源数据表（asset_bundle_config）；首次访问时通过工厂创建。
        /// </summary>
        public static IResData AssetBundleDataFile
        {
            get { return _assetBundleDataFile ??= _assetBundleDataFileFactory.Invoke(); }
            set => _assetBundleDataFile = value;
        }

        /// <summary>
        /// 是否从 StreamingAssets 读取资源数据（false 表示读取 persistentDataPath 下热更后的数据）。
        /// 通过 PlayerPrefs 持久化；未设置时默认为 true。
        /// </summary>
        public static bool LoadAssetResFromStreamingAssetsPath
        {
            get => PlayerPrefs.GetInt("LoadAssetResFromStreamingAssetsPath", 1) == 1;
            set => PlayerPrefs.SetInt("LoadAssetResFromStreamingAssetsPath", value ? 1 : 0);
        }

        /// <summary>
        /// 把 AssetBundle 的绝对/流式路径还原为 Bundle 名（去掉 StreamingAssets / persistentDataPath 前缀）。
        /// </summary>
        public static string ConvertAssetBundleUrlToName(string assetBundleUrl)
        {
            var patten = AssetBundleHelper.StreamingAssetsPath + "AssetBundles/" +
                         AssetBundleHelper.GetPlatformName() + "/";
            var abName = assetBundleUrl.Replace(patten, "");

            patten = AssetBundleHelper.PersistentDataPath + "AssetBundles/" + 
                     AssetBundleHelper.GetPlatformName() + "/";
            abName = abName.Replace(patten, "");
            return abName;
        }

        /// <summary>
        /// 由 Bundle 名解析运行时文件路径：persistentDataPath 优先，其次 StreamingAssets。
        /// </summary>
        public static string ConvertAssetBundleNameToUrl(string abName)
        {
            //先判断PersistentDataPath中有无数据
            var abUrl = AssetBundleHelper.PersistentDataPath + "AssetBundles/" + 
                        AssetBundleHelper.GetPlatformName() + "/" + abName;
            if (File.Exists(abUrl))
            {
                return abUrl;
            }
            return AssetBundleHelper.StreamingAssetsPath + "AssetBundles/" +
                   AssetBundleHelper.GetPlatformName() + "/" + abName;
        }

        /// <summary>
        /// 资源数据表（<see cref="ResData.FileName"/>）的运行时文件路径：
        /// 按来源选择 persistentDataPath 或 StreamingAssets，并附加平台路径前缀与平台目录。
        /// </summary>
        /// <param name="fromStreamingAssetsPath">true 使用 StreamingAssets；false 使用 persistentDataPath。</param>
        public static string GetAssetBundleConfigFilePath(bool fromStreamingAssetsPath)
        {
            var root = fromStreamingAssetsPath
                ? AssetBundleHelper.StreamingAssetsPath
                : AssetBundleHelper.PersistentDataPath;
            return AssetBundleHelper.PathPrefix + root + "AssetBundles/" +
                   AssetBundleHelper.GetPlatformName() + "/" + ResData.FileName;
        }

        /// <summary>
        /// AssetBundles 平台子目录的相对路径样式（/AssetBundles/&lt;Platform&gt;/），
        /// 用于与 StreamingAssets / persistentDataPath 前缀拼接。
        /// </summary>
        public static string RELATIVE_AB_ROOT_FOLDER => "/AssetBundles/" + AssetBundleHelper.GetPlatformName() + "/";
    }
}
