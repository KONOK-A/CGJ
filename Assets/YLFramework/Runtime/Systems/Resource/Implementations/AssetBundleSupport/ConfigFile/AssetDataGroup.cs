using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资产数据组：一个"组键"（Bundle 名首段，见 ResData.GetKeyWithABName）对应一组数据，
    /// 统一记录组内全部 ABUnit（Bundle 及其依赖）与 AssetData。
    /// <para>
    /// 索引规则：AssetData 按小写资源名建索引；指定 OwnerBundleName 查询时改走
    /// "Bundle 名 + 资源名" 的组合索引，用于区分不同 Bundle 下的同名资产。
    /// </para>
    /// </summary>
    public class AssetDataGroup
    {
        #region 内部类
        
        /// <summary>
        /// AB包单元 用来记录AB包的依赖关系
        /// </summary>
        [Serializable]
        public class ABUnit
        {
            /// <summary>Bundle 名。</summary>
            public string abName;
            /// <summary>依赖的 Bundle 名数组；无依赖时为 null。</summary>
            public string[] abDependencies;
            private static StringBuilder _stringBuilder = new();

            /// <summary>创建单元：依赖为 null 或空数组时保持 <see cref="abDependencies"/> 为 null。</summary>
            public ABUnit(string abName, string[] abDependencies)
            {
                this.abName = abName;
                if (abDependencies == null || abDependencies.Length == 0)
                {
                }
                else
                {
                    this.abDependencies = abDependencies;
                }
            }

            public override string ToString()
            {
                _stringBuilder.Clear();
                _stringBuilder.Append(string.Format("ABName:" + abName));
                if (abDependencies == null || abDependencies.Length == 0)
                {
                    _stringBuilder.Append(" Depends:null");
                    return _stringBuilder.ToString();
                }
                _stringBuilder.Append(" Depends:");
                foreach (var abDependency in abDependencies)
                {
                    _stringBuilder.Append($" #:{abDependency}");
                }
                return _stringBuilder.ToString();
            }
        }
        /// <summary>AssetDataGroup 的二进制序列化数据载体。</summary>
        [Serializable]
        public class SerializeData
        {
            private string _key;
            private ABUnit[] _abUnits;
            private AssetData[] _assetDatas;

            public string Key
            {
                get => _key;
                set => _key = value;
            }

            public ABUnit[] ABUnits
            {
                get => _abUnits;
                set => _abUnits = value;
            }

            public AssetData[] AssetDatas
            {
                get => _assetDatas;
                set => _assetDatas = value;
            }
        }

        #endregion
        
        
        #region 字段

        private List<ABUnit> _abUnits;
        private Dictionary<string, AssetData> _assetDataMap;
        private Dictionary<string, AssetData> _assetDataMapWithUid;

        #endregion

        #region 属性

        /// <summary>组内全部资产记录（内部字典的值集合）。</summary>
        public IEnumerable<AssetData> AssetDatas => _assetDataMap.Values;
        /// <summary>组内全部 AB 单元（内部列表本身）。</summary>
        public IEnumerable<ABUnit> ABUnits => _abUnits;
        /// <summary>组键（由 ResData 按 Bundle 名首段生成）；构造后不变。</summary>
        public string Key { get; private set; }

        #endregion

        #region 构造与回收

        /// <summary>创建空数据组。</summary>
        public AssetDataGroup(string key)
        {
            Key = key;
        }

        /// <summary>从序列化数据重建数据组。</summary>
        public AssetDataGroup(SerializeData sd)
        {
            Key = sd.Key;
            ConvertSerializeData(sd);
        }

        /// <summary>清空 AB 单元与两个索引（清空后数据组仍可继续使用）。</summary>
        public void Reset()
        {
            _abUnits?.Clear();
            _assetDataMap?.Clear();
            _assetDataMapWithUid?.Clear();
        }
        

        #endregion

        #region 方法

        /// <summary>
        /// 添加 Bundle 记录并返回其在组内的 AbIndex：已存在同名记录时直接复用；
        /// 新记录同时会以 <see cref="ResLoadType.AssetBundle"/> 类型登记一条 AssetData。abName 为空返回 -1。
        /// </summary>
        public int AddAssetBundleRecord(string abName, string[] abDependencies)
        {
            if (string.IsNullOrEmpty(abName))
            {
                return -1;
            }
            _abUnits ??= new List<ABUnit>();
            var resKey = ResKey.Allocate(abName);
            var assetData = GetAssetData(resKey);
            resKey.RecycleToCache();
            //已经有了该AB包的记录
            if (assetData != null)
            {
                return assetData.AbIndex;
            }
            //添加一个新的AssetBundle类型的AssetData
            _abUnits.Add(new ABUnit(abName, abDependencies));
            var abIndex = _abUnits.Count - 1;
            AddAssetData(new AssetData(abName,ResLoadType.AssetBundle,abIndex,null));
            return abIndex;
        }

        /// <summary>按 Bundle 名取得对应 AB 单元；未找到返回 null。</summary>
        public ABUnit GetABUnit(string assetName)
        {
            var resKey = ResKey.Allocate(assetName);
            var data = GetAssetData(resKey);
            resKey.RecycleToCache();

            if (data == null)
            {
                return null;
            }

            return _assetDataMap == null ? null : _abUnits[data.AbIndex];
        }

        /// <summary>按 Bundle 名取依赖数组；未找到时返回 false 且 <paramref name="abDependencies"/> 为 null。</summary>
        public bool GetABDependencies(string abName, out string[] abDependencies)
        {
            abDependencies = null;
            var abUnit = GetABUnit(abName);
            if (abUnit == null)
            {
                return false;
            }
            abDependencies = abUnit.abDependencies;
            return true;
        }


        /// <summary>
        /// 按查询键取资产记录：key 指定 OwnerBundleName 时走 Bundle+资源名索引，
        /// 否则走资源名索引（键均为小写）；未命中返回 null。
        /// </summary>
        public AssetData GetAssetData(ResKey resKey)
        {
            AssetData assetData;
            if (resKey.OwnerBundleName != null && _assetDataMapWithUid != null)
            {
                return _assetDataMapWithUid.TryGetValue(resKey.OwnerBundleName + resKey.AssetName, out assetData) ? assetData : null;
            }

            if (resKey.OwnerBundleName == null && _assetDataMap != null)
            {
                return _assetDataMap.TryGetValue(resKey.AssetName, out assetData) ? assetData : null;
            }
            return null;
        }

        /// <summary>
        /// 登记资产记录并在资源名索引与 Bundle+资源名索引中建立映射；
        /// 重复键只输出日志提示、保留先写入的记录；当前实现始终返回 true。
        /// </summary>
        public bool AddAssetData(AssetData assetData)
        {
            _assetDataMap ??= new Dictionary<string, AssetData>();
            _assetDataMapWithUid ??= new Dictionary<string, AssetData>();
            
            //统一使用不变文化的小写做索引键，与 ResKey 的归一规则一致。
            var assetDataKey = assetData.AssetName.ToLowerInvariant();
            //尝试添加该数据
            if (!_assetDataMap.TryAdd(assetDataKey, assetData))
            {
                //已经有了该资产数据，重复添加，给出提示
                var resKey = ResKey.Allocate(assetData.AssetName);
                var old = GetAssetData(resKey);
                resKey.RecycleToCache();
                try
                {
                    Debug.LogFormat("Already Add AssetData :{0} \n OldAB:{1}      NewAB:{2}", assetData.AssetName,
                        _abUnits[old.AbIndex].abName, _abUnits[assetData.AbIndex].abName);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            var assetDataUid = assetData.Uid.ToLowerInvariant();
            if (!_assetDataMapWithUid.TryAdd(assetDataUid, assetData))
            {
                var resKey = ResKey.Allocate(assetData.AssetName,assetData.OwnerBundleName);
                var old = GetAssetData(resKey);
                resKey.RecycleToCache();
                try
                {
                    Debug.LogFormat("Already Add AssetData :{0} \n OldAB:{1}      NewAB:{2}", assetData.Uid,
                        _abUnits[old.AbIndex].abName, _abUnits[assetData.AbIndex].abName);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return true;
        }
        /// <summary>生成当前组的序列化数据（供 ResData.Save 使用）。</summary>
        public SerializeData GetSerializeData()
        {
            var sd = new SerializeData();
            sd.Key = Key;
            sd.ABUnits = _abUnits.ToArray();
            if (_assetDataMap != null)
            {
                var assetDatas = new AssetData[_assetDataMap.Count];
                int index = 0;
                foreach (var item in _assetDataMap)
                {
                    assetDatas[index] = item.Value;
                    index += 1;
                }
                sd.AssetDatas = assetDatas;
            }
            return sd;
        }

        /// <summary>从序列化数据重建组内容（重建 AB 单元列表与索引）；data 为 null 时忽略。</summary>
        public void ConvertSerializeData(SerializeData data)
        {
            if(data == null) return;
            
            _abUnits = new List<ABUnit>(data.ABUnits);
            if (data.AssetDatas == null) return;
            _assetDataMap = new Dictionary<string, AssetData>();
            foreach (var assetData in data.AssetDatas)
            {
                AddAssetData(assetData);
            }
        }
        
        #endregion


    }
}