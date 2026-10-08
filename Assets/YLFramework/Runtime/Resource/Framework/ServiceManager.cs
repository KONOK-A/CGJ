using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// Resource 服务内部管理器：资源表、异步加载协程队列与零引用资源回收。
    /// 由 <see cref="ResManager.Init"/> / <see cref="ResManager.InitAsync"/> 初始化。
    /// </summary>
    [MonoSingletonPath("YLFramework/ResService/ServiceManager")]
    internal class ServiceManager : MonoBehaviour,ISingleton
    {
        public static ServiceManager Instance => MonoSingletonProperty<ServiceManager>.Instance;

        #region ResService初始化相关

        /// <summary>资源服务是否已完成初始化；仅在初始化成功后提交。</summary>
        public static bool ResMgrInit { get; private set; }

        /// <summary>
        /// 初始化资源服务内部管理类：预热内部对象池并加载资源数据表。
        /// 重复调用幂等；初始化失败（抛异常）时不提交完成标志，可安全重试。
        /// 仅运行时可用（ServiceManager 是 Mono 单例，编辑器非播放模式无法创建实例）。
        /// </summary>
        public static void Init()
        {
            if (ResMgrInit)
            {
                return;
            }

            InitInnerPools();

            //实际初始化管理器；成功后才提交完成标志，避免"初始化一半失败但被认为已完成"。
            Instance.InitResMgr();
            ResMgrInit = true;
        }

        /// <summary>
        /// 异步初始化资源服务（WebGL 平台只支持异步初始化）。
        /// 语义与 <see cref="Init"/> 一致：幂等、成功后提交完成标志、失败可重试；
        /// 不承诺与 Init 并发调用的安全性。
        /// </summary>
        public static IEnumerator InitAsync()
        {
            if (ResMgrInit)
            {
                yield break;
            }

            InitInnerPools();

            //实际初始化管理器；成功后才提交完成标志。
            yield return Instance.InitResMgrAsync();
            ResMgrInit = true;
        }

        /// <summary>
        /// 初始化 ResService 内部使用的所有对象池（同步 / 异步初始化共用）。
        /// </summary>
        private static void InitInnerPools()
        {
            SafeObjectPool<AssetBundleRes>.Instance.Init(40, 20);
            SafeObjectPool<AssetRes>.Instance.Init(40, 20);
            SafeObjectPool<ResourcesRes>.Instance.Init(40, 20);
            SafeObjectPool<NetImageRes>.Instance.Init(40, 20);
            SafeObjectPool<ResKey>.Instance.Init(40, 20);
            SafeObjectPool<ResLoader>.Instance.Init(40, 20);
        }

        private void InitResMgr()
        {
            if (AssetBundleHelper.SimulationMode)
            {
                AssetBundleSetting.AssetBundleDataFile = ConfigFileUtility.BuildEditorDataTable();
            }
            else
            {
                AssetBundleSetting.AssetBundleDataFile.Reset();
                var result = new List<string>();
                
                // 未进行过热更
                if (AssetBundleSetting.LoadAssetResFromStreamingAssetsPath)
                {
                    ZipFileHelper.Instance.GetFileInInner(ResData.FileName,result);
                }
                // 进行过热更
                else
                {
                    AssetBundleHelper.GetFileInFolder(AssetBundleHelper.PersistentDataPath, ResData.FileName,
                        result);
                }

                foreach (var outRes in result)
                {
                    AssetBundleSetting.AssetBundleDataFile.LoadFromFile(outRes);
                }
            }
        }

        /// <summary>
        /// 异步初始化资源数据：模拟模式直接构建编辑器数据表；
        /// 否则按来源（StreamingAssets / persistentDataPath）生成配置路径并异步加载。
        /// </summary>
        private IEnumerator InitResMgrAsync()
        {
            if (AssetBundleHelper.SimulationMode)
            {
                AssetBundleSetting.AssetBundleDataFile = ConfigFileUtility.BuildEditorDataTable();
            }
            else
            {
                AssetBundleSetting.AssetBundleDataFile.Reset();
                //从未进行过热更使用 StreamingAssets；进行过热更使用 persistentDataPath。
                var configPath = AssetBundleSetting.GetAssetBundleConfigFilePath(
                    AssetBundleSetting.LoadAssetResFromStreamingAssetsPath);
                yield return AssetBundleSetting.AssetBundleDataFile.LoadFromFileAsync(configPath);
            }
            yield return null;
        }

        #endregion
        #region 字段

        private readonly ResTable _resTable = new();
        [SerializeField]
        private int currentCoroutineCount;
        //允许资源服务内同时进行的协程数
        private int _maxCoroutineCount = 8;
        private LinkedList<IEnumeratorTask> _enumeratorTasks = new();
        //Res 在ResServiceMgr中 删除的问题，ResServiceMgr定时收集列表中的Res然后删除
        private bool _isResMapDirty;

        #endregion
        #region 属性

        /// <summary>当前资源表中登记的 IRes 数量。</summary>
        public int ResCount => _resTable.Count();
        /// <summary>应用是否正在退出；退出后不再触发资源回收。</summary>
        public static bool IsApplicationQuit { get;private set; }

        #endregion

        /// <summary>
        /// 查询资源：命中资源表直接返回；<paramref name="createNew"/> 为 true 时经 <see cref="ResFactory"/>
        /// 创建并登记新资源；创建失败返回 null。
        /// </summary>
        public IRes GetRes(ResKey resKey, bool createNew = false)
        {
            var res = _resTable.GetResByResKey(resKey);
            if (res != null)
            {
                return res;
            }
            if (!createNew)
            {
                return null;
            }
            res = ResFactory.Create(resKey);
            if (res != null)
            {
                _resTable.Add(res);
            }
            return res;
        }

        /// <summary>查询资源并转换为指定类型；类型不匹配时返回 null。</summary>
        public T GetRes<T>(ResKey resKey, bool createNew = false) where T : class, IRes
        {
            return GetRes(resKey, createNew) as T;
        }

        /// <summary>
        /// 标记资源表需要在下一次 Update 时回收零引用资源。
        /// </summary>
        public void ClearOnUpdate()
        {
            _isResMapDirty = true;
        }

        /// <summary>
        /// 追加一个异步加载任务；超过并发上限的任务排队等待。
        /// </summary>
        public void AddIEnumeratorTask(IEnumeratorTask task)
        {
            if (task == null)
            {
                return;
            }
            _enumeratorTasks.AddLast(task);
            TryStartNextIEnumeratorTask();
        }
        private void OnIEnumeratorFinish()
        {
            currentCoroutineCount--;
            TryStartNextIEnumeratorTask();
        }
        private void TryStartNextIEnumeratorTask()
        {
            if (_enumeratorTasks.Count == 0)
            {
                return;
            }

            if (currentCoroutineCount >= _maxCoroutineCount)
            {
                return;
            }
            
            var task = _enumeratorTasks.First.Value;
            _enumeratorTasks.RemoveFirst();
            currentCoroutineCount++;
            StartCoroutine(task.DoLoadAsync(OnIEnumeratorFinish));
        }

        /// <summary>
        /// 回收零引用资源：卸载已加载完毕的资源并从表移除；仍在 Loading 的零引用资源保持表刷新，
        /// 等加载完成后的下一帧再卸载回收（避免加载完成后无人回收导致资产常驻）。
        /// </summary>
        private void RemoveUnusedRes()
        {
            if (!_isResMapDirty)
            {
                return;
            }
            _isResMapDirty = false;
            foreach (var res in _resTable.ToArray())
            {
                if (res.RefCount > 0)
                {
                    continue;
                }

                if (res.State == ResState.Loading)
                {
                    //加载中的零引用资源完成后再回收，保持 dirty 以便下一帧继续检查。
                    _isResMapDirty = true;
                    continue;
                }

                if (res.ReleaseRes())
                {
                    _resTable.Remove(res);
                    res.RecycleToCache();
                }
            }
        }

        #region 生命周期

        private void Update()
        {
            if (_isResMapDirty)
            {
                RemoveUnusedRes();
            }
        }

        private void OnApplicationQuit()
        {
            IsApplicationQuit = true;
        }

        public void OnSingletonInit()
        {
            
        }

        #endregion
        
    }
}
