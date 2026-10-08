using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YLFramework.Services.Resource
{

    /// <summary>
    /// 资源数据表契约：维护 AssetBundle 依赖与资产记录，支持从二进制配置文件读写。
    /// </summary>
    public interface IResData
    {
        /// <summary>按 Bundle URL（或路径）查询其依赖 Bundle 名数组；未命中返回 null。</summary>
        string[] GetAllDependenciesByUrl(string url);
        /// <summary>从文件路径同步加载并合并数据。</summary>
        void LoadFromFile(string filePath);
        /// <summary>从路径异步加载并合并数据；读取或反序列化失败时输出错误日志。</summary>
        IEnumerator LoadFromFileAsync(string filePath);
        /// <summary>按查询键取得资产记录；未命中返回 null。</summary>
        AssetData GetAssetData(ResKey resKey);
        /// <summary>
        /// 添加 Bundle 记录（已存在则复用），返回该 Bundle 在所属数据组中的 AbIndex；
        /// abName 为空时返回 -1。
        /// </summary>
        int AddAssetBundleRecord(string abName,string[] depends,out AssetDataGroup group);
        /// <summary>清空全部数据组与索引。</summary>
        void Reset();
    }
    /// <summary>
    /// <see cref="IResData"/> 的默认实现：持有全部 <see cref="AssetDataGroup"/>，
    /// 在首次查询或加载数据时把各组的 AssetData 汇总为按资源名索引的 <see cref="AssetDataTable"/>。
    /// <para>
    /// 序列化根对象为 <see cref="SerializeData"/>，默认文件名见 <see cref="FileName"/>；
    /// 运行时由 ServiceManager 在初始化时读取该文件（见 AssetBundleSetting）。
    /// </para>
    /// </summary>
    public class ResData : IResData
    {
        
        /// <summary>二进制序列化的根对象，持有全部数据组的序列化形式。</summary>
        [Serializable]
        public class SerializeData
        {
            private AssetDataGroup.SerializeData[] _assetDataGroup;

            public AssetDataGroup.SerializeData[] AssetDataGroup
            {
                get => _assetDataGroup;
                set => _assetDataGroup = value;
            }
        }

        #region 字段

        /// <summary>预留的加密 Key 字段；当前序列化与读取流程未使用该值。</summary>
        public string AesKey = string.Empty;
        /// <summary>资源数据表的默认文件名。</summary>
        public const string FileName = "asset_bundle_config.bin";
        private readonly List<AssetDataGroup> _allAssetDataGroup = new();
        private AssetDataTable _assetDataTable;

        #endregion

        #region 属性

        /// <summary>全部数据组；返回内部列表本身，直接增删会改变 ResData 的数据状态。</summary>
        public IList<AssetDataGroup> AllAssetDataGroup => _allAssetDataGroup;

        #endregion

        #region 方法

        /// <inheritdoc />
        public void Reset()
        {
            //所有的资源数据组都要执行重置方法
            for (var i = _allAssetDataGroup.Count - 1; i >= 0; i--)
            {
                _allAssetDataGroup[i].Reset();
            }
            _allAssetDataGroup.Clear();
            _assetDataTable?.Dispose();
            _assetDataTable = null;
        }

        /// <inheritdoc />
        public int AddAssetBundleRecord(string abName, string[] depends, out AssetDataGroup group)
        {
            group = null;
            if (string.IsNullOrEmpty(abName))
            {
                return -1;
            }
            var key = GetKeyWithABName(abName);
            if(key == null) return -1;
            
            group = GetAssetDataGroup(key);
            if (group == null)
            {
                group = new AssetDataGroup(key);
                Debug.Log("#YLFramework: Create new AssetDataGroup:" + key);
                _allAssetDataGroup.Add(group);
            }
            return group.AddAssetBundleRecord(abName,depends);
        }

        /// <inheritdoc />
        public string[] GetAllDependenciesByUrl(string url)
        {
            var abName = AssetBundleSetting.ConvertAssetBundleUrlToName(url);
            for (var i = _allAssetDataGroup.Count - 1; i >= 0; i--)
            {
                if (!_allAssetDataGroup[i].GetABDependencies(abName, out var depends))
                {
                    continue;
                }
                return depends;
            }
            return null;
        }

        /// <inheritdoc />
        public AssetData GetAssetData(ResKey resKey)
        {
            if (_assetDataTable == null)
            {
                _assetDataTable = new AssetDataTable();
                for (var i = _allAssetDataGroup.Count - 1; i >= 0; i--)
                {
                    foreach (var assetData in _allAssetDataGroup[i].AssetDatas)
                    {
                        _assetDataTable.Add(assetData);
                    }
                }
            }
            return _assetDataTable.GetAssetDataByResKey(resKey);
        }

        /// <inheritdoc />
        public void LoadFromFile(string filePath)
        {
            var data = BinarySerializer.Instance.DeserializeBinary
                (ZipFileHelper.Instance.OpenReadStream(filePath));

            if (data is not SerializeData sd)
            {
                return;
            }
            SetSerializeData(sd);
        }

        /// <inheritdoc />
        /// <remarks>
        /// <paramref name="filePath"/> 为 WWW 可读取的路径（运行时通常由
        /// AssetBundleSetting.GetAssetBundleConfigFilePath 生成，编辑器 / iOS 下带 <c>file://</c> 前缀）。
        /// </remarks>
        public IEnumerator LoadFromFileAsync(string filePath)
        {
#pragma warning disable CS0618 // 类型或成员已过时
            using var www = new WWW(filePath);
            //等待文件数据加载完成
            yield return www;
            if (www.error != null)
            {
                Debug.LogError("#YLFramework:Failed Deserialize AssetDataTable:" + filePath + " Error:" +
                               www.error);
                yield break;
            }

            var stream = new MemoryStream(www.bytes);
            var data = BinarySerializer.Instance.DeserializeBinary(stream);
            if (data == null)
            {
                Debug.LogError("#YLFramework:Failed Deserialize AssetDataTable:" + filePath);
                yield break;
            }

            if (data is not SerializeData sd)
            {
                Debug.LogError("#YLFramework:Failed Load AssetDataTable:" + filePath);
                yield break;
            }
            SetSerializeData(sd);
        }

        /// <summary>把当前全部数据组序列化为二进制配置文件；序列化失败时输出错误日志。</summary>
        public virtual void Save(string savePath)
        {
            var sd = new SerializeData()
            {
                AssetDataGroup = new AssetDataGroup.SerializeData[_allAssetDataGroup.Count],
            };
            for (int i = 0; i < _allAssetDataGroup.Count; i++)
            {
                sd.AssetDataGroup[i] = _allAssetDataGroup[i].GetSerializeData();
            }
            //进行二进制的序列化
            if (BinarySerializer.Instance.SerializeBinary(savePath, sd))
            {
                Debug.Log("Success Save AssetDataTable:" + savePath);
            }
            else
            {
                Debug.LogError("Failed Save AssetDataTable:" + savePath);
            }
        }

        private void SetSerializeData(SerializeData data)
        {
            if (data?.AssetDataGroup == null)
            {
                return;
            }

            for (var i = data.AssetDataGroup.Length - 1; i >= 0; i--)
            {
                _allAssetDataGroup.Add(BuildAssetDataGroup(data.AssetDataGroup[i]));
            }

            if (_assetDataTable != null) return;
            _assetDataTable = new AssetDataTable();
            foreach (var serializeData in data.AssetDataGroup)
            {
                foreach (var assetData in serializeData.AssetDatas)
                {
                    _assetDataTable.Add(assetData);
                }
            }
        }

        private static AssetDataGroup BuildAssetDataGroup(AssetDataGroup.SerializeData data)
        {
            return new AssetDataGroup(data);
        }

        private AssetDataGroup GetAssetDataGroup(string key)
        {
            for (int i = _allAssetDataGroup.Count - 1; i >= 0; i--)
            {
                if (_allAssetDataGroup[i].Key.Equals(key))
                {
                    return _allAssetDataGroup[i];
                }
            }
            return null;
        }

        // 组键 = Bundle 名中第一个 '/' 之前的部分；无 '/' 时即整个名字。
        private static string GetKeyWithABName(string abName)
        {
            var pIndex = abName.IndexOf('/');
            if (pIndex < 0)
            {
                return abName;
            }
            var key = abName[..pIndex];
            return key;
        }

        #endregion

        
    }
    
}