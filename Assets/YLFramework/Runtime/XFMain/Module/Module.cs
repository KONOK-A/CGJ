// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Module/Module.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic; 
using UnityEngine; 

namespace YLFramework.Core
{
    /// <summary>
    /// 模块生命周期；启动失败复用关闭管线结束逻辑所有权后才报告。
    /// </summary>
    public enum ModuleState
    {
        Created,
        /// <summary>已发布启动请求和注册；允许查询，拒绝Close与Abort取消。</summary>
        Starting,
        /// <summary>OnStart成功结束；重复启动不再次执行生命周期。</summary>
        Running,
        Closing,
        /// <summary>已解除注册且核心对象索引无残留，允许显式重启同一实例。</summary>
        Closed,
        /// <summary>启动失败的内部过渡状态；回滚完成后进入Closed。</summary>
        StartFailed,
        /// <summary>保留的旧失败状态；唯一关闭管线在报告错误前已进入Closed。</summary>
        CloseFailed
    }
      
    /// <summary>业务对象的唯一owner。</summary>
    /// <remarks>
    /// 同步LoadView只返回已完成初始化的实例，参数、资源或初始化错误直接抛出。
    /// LoadViewArgs接管成功即由本次操作负责归还，入口拒绝也消耗参数；回调只借读，不得保存容器。
    /// 异步View请求独立创建；Abort为逻辑取消，owner关闭后失效，终态与原始Exception只提交一次。
    /// </remarks>
    public abstract class Module
    {

        #region 字段
        /// <summary>
        /// 存放 所有的数据
        /// </summary>
        internal Dictionary<Type, Dictionary<long, Model>> models = new Dictionary<Type, Dictionary<long, Model>>();
        /// <summary>
        /// 存放所有的视图(key:string value:Dictionary<int, View>) 使用名称 和 id 查询比较方便
        /// </summary>
        internal Dictionary<string, Dictionary<int, View>> views = new Dictionary<string, Dictionary<int, View>>();

        /// <summary>
        /// 存放所有的视图(key:type value:List<View>) 使用类型查询比较方便
        /// </summary>
        internal Dictionary<Type,List<View>> type_views = new Dictionary<Type, List<View>>();
        internal Dictionary<int, View> ownedViews = new Dictionary<int, View>();
        internal int viewTransactions;
        internal object ViewLifecycle;
        internal readonly HashSet<IPendingViewRequest> pendingViews = new HashSet<IPendingViewRequest>();
        
        /// <summary>
        /// 存放 所有的业务逻辑
        /// </summary>
        internal Dictionary<Type, Controller> controllers = new Dictionary<Type, Controller>();

        internal long nextModelId = 1;
        internal HashSet<Type> creatingModels = new HashSet<Type>();
        internal HashSet<Type> creatingControllers = new HashSet<Type>();


        private int ui_start_sort_order = 0; // UI 初始的SortingOrder
        private string projectName;

        #endregion

        #region 属性
        /// <summary>
        /// 模块名
        /// </summary>
        public string ModuleName => GetType().FullName;

        /// <summary>
        /// 由ModuleManager维护的生命周期状态；注册可见不代表已经Running。
        /// </summary>
        public ModuleState State { get; internal set; } = ModuleState.Created;

        /// <summary>
        /// XFAB资源项目名，与Module类型身份分离；默认类型短名。
        /// 可在启动前赋值或重写，启动请求在首次加载前捕获该值，不依赖OnStart赋值。
        /// </summary>
        public virtual string ProjectName { get => projectName ?? GetType().Name; set => projectName = value; }

       
        /// <summary>
        /// 是否是基础模块
        /// 基础模块不能普通关闭，由ModuleManager.Shutdown或启动失败回滚结束；默认false
        /// </summary>
        public virtual bool BaseModule => false;

        /// <summary>
        /// 必需的启动配表（资产名到Model数组类型）。空数组合法，缺失或无效文本导致启动失败。
        /// 配表及其Bundle须启动过程专用，取文本期间不得由其他调用者并发加载或持有；
        /// XFAB没有资源租约，框架复制文本后归还资产，再执行JSON构造与Model初始化。
        /// </summary>

        [Obsolete("已过时!")]
        public virtual Dictionary<string, Type> PreloadConfigTables => null;

        /// <summary>
        /// 默认启动的场景(在启动模块时自动加载的场景名称)
        /// </summary>
        public virtual string DefaultStartUpScene => string.Empty;

        /// <summary>
        /// 当前模块中的UI的初始层级, UI的实际层级 = 初始层级 + UI本身层级 
        /// </summary>
        public int UIStartSortOrder 
        {
            get 
            {
                return ui_start_sort_order;
            }

            set 
            {
                ui_start_sort_order = value;

                var errors = new List<Exception>();
                foreach (var entry in ViewManager.Snapshot(this))
                {
                    if (entry.Key is Panel panel && panel && ReferenceEquals(panel.Session, entry.Value))
                    {
                        try { panel.RefreshSortingOrder(); } // 包含显式挂载及嵌套Panel，不只命名资源。
                        catch (Exception error) { errors.Add(error); }
                    }
                }
                ViewManager.ThrowCleanupErrors(errors);

            }

        }

        #endregion
         
        #region Model
        /// <summary>
        /// 注册本地Model；已完成注册的同实例重复调用幂等，键冲突或跨模块注册抛出异常。
        /// </summary>
        /// <param name="model">数据对象</param>
        public void AddModel(Model model)
        {
            ModelManager.AddModel(this, model);
        }


        /// <summary>
        /// 仅移除本模块拥有且与索引完全一致的实例；退出异常也会清除索引及owner。
        /// </summary>
        /// <param name="model"></param>
        public void RemoveModel(Model model)
        {
            ModelManager.RemoveModel(this, model);
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型数据，不创建；缺失返回null，多实例抛出异常。
        /// 跨模块共享须在明确的拥有者Module上调用查询，不改变归属或生命周期责任。
        /// </summary>
        /// <param name="modelType">数据名</param>
        /// <returns></returns>
        public Model GetModel(Type modelType)
        {
            return ModelManager.GetModel(this, modelType);
        }

        /// <summary>
        /// 只按精确类型和id查询本模块已注册的数据，不创建；缺失返回null。
        /// </summary>
        /// <param name="modelType"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public Model GetModel(Type modelType, long id)
        {
            return ModelManager.GetModel(this, modelType, id);
        }

        /// <summary>
        /// 查询本模块该类型及其派生类型的所有数据，不创建；缺失返回空集合。
        /// </summary>
        /// <param name="modelType">数据类型</param>
        /// <returns></returns>
        public List<Model> GetModels(Type modelType)
        {
            return ModelManager.GetModels(this, modelType);
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void ClearModel()
        {
            ModelManager.ClearModel(this);
        }

        /// <summary>
        /// 清空某个类型数据
        /// </summary>
        /// <param name="modelType">数据名称</param>
        public void ClearModel(Type modelType)
        {
            ModelManager.ClearModel(this, modelType);
        }

        /// <summary>
        /// 清空某一个类型数据(泛型方式)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void ClearModel<T>() where T : Model
        {
            ClearModel(typeof(T));
        }

        /// <summary>
        /// 只按精确类型和id查询本模块已注册的数据，不创建；缺失返回null。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetModel<T>(long id) where T : Model
        {
            return GetModel(typeof(T), id) as T;
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型数据，不创建；缺失返回null，多实例抛出异常。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModel<T>() where T : Model
        {
            return GetModel(typeof(T)) as T;
        }

        /// <summary>
        /// 查询本模块该类型及其派生类型的所有数据，不创建；缺失返回空集合。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetModels<T>() where T : Model
        {

            List<Model> models = GetModels(typeof(T));

            List<T> list = new List<T>(models.Count);

            foreach (var item in models)
            {
                list.Add(item as T);
            }
            return list;
        }

        /// <summary>
        /// 加载数据(适用于只有一个数据实例的情况)
        /// 只查询本模块数据，缺失时才创建并注册到本模块；多实例抛出异常。
        /// 不使用对象池；初始化期间重入Load返回临时注册的自身，退出期间拒绝Load。
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public Model LoadModel(Type type) 
        {
            return ModelManager.LoadModel(this, type);
        }

        /// <summary>
        /// 加载数据(适用于只有一个数据实例的情况)
        /// 只查询本模块数据，缺失时才创建并注册到本模块；多实例抛出异常。
        /// 不使用对象池；初始化期间重入Load返回临时注册的自身，退出期间拒绝Load。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public T LoadModel<T>() where T : Model {
            return ModelManager.LoadModel<T>(this);
        }

        #endregion

        #region 移除View

        /// <summary>
        /// 清空View
        /// </summary>
        public void ClearView()
        {
            ViewManager.ClearView(this);
        }

        /// <summary>
        /// 清空本模块指定类型及派生类型的View。
        /// </summary>
        public void ClearView<T>() where T : View
        {
            ViewManager.ClearView<T>(this);
        }

        /// <summary>
        /// 清空指定名称的View
        /// </summary>
        public void ClearView(string asset_name)
        {
            ViewManager.ClearView(this, asset_name);
        }

        #endregion

        #region 加载View

        /// <summary>纳管已有View，不实例化、不激活、不取得GameObject所有权。合法重复挂载不重复初始化；参数由本次调用接管。</summary>
        public T AttachView<T>(T view, LoadViewArgs loadViewArgs = null) where T : View
        {
            ViewManager.AttachView(this, view, loadViewArgs);
            return view;
        }

        /// <summary>纳管已有层级的所有View（包含inactive）。失败只撤销本次新增会话，不改变已有会话和对象激活状态。</summary>
        public List<View> AttachViews(GameObject root)
        {
            return ViewManager.AttachViews(this, root);
        }

        /// <summary>明确纳管指定已加载Scene中的View（包含inactive），不自动卸载场景；使用Scene身份而非名称。</summary>
        public List<View> AttachViews(UnityEngine.SceneManagement.Scene scene)
        {
            return ViewManager.AttachViews(this, scene);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">View预制体名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public View LoadView(string asset_name, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadView(this,asset_name, parent, param);
        }

        /// <summary>
        /// 加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(string asset_name, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewLegacy<T>(this, asset_name, parent, param);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView(GameObject prefab, Transform parent = null,LoadViewArgs loadViewArgs)代替!")]
        public View LoadView(GameObject prefab, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadView(this, prefab, parent, param);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(GameObject prefab, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(GameObject prefab, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewLegacy<T>(this, prefab, parent, param);
        }

        /// <summary>
        /// 加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(Transform parent = null, params object[] param) where T : View
        {
            return LoadView<T>(typeof(T).Name, parent, param);
        }


        /// <summary>
        /// 异步加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<View> LoadViewAsync(string asset_name, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadViewAsync<View>(this, asset_name, parent,true, param);
        }

        /// <summary>
        /// 异步加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param"></param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync<T>(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<T> LoadViewAsync<T>(string asset_name, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this ,asset_name, parent,true , param);
        }

        /// <summary>
        /// 异步加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync<T>(Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<T> LoadViewAsync<T>(Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this,typeof(T).Name, parent,true, param);
        }


        #endregion

        #region 加载View(With LoadViewArgs)

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">View预制体名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public View LoadView(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadView<View>(this, asset_name, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadView<T>(this,asset_name, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public View LoadView(GameObject prefab, Transform parent = null,LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadView<View>(this, prefab, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(GameObject prefab, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadView<T>(this,prefab, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return LoadView<T>(typeof(T).Name, parent, loadViewArgs);
        }


        /// <summary>
        /// 异步加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<View> LoadViewAsync(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadViewAsync<View>(this, asset_name, parent, true, loadViewArgs);
        }

        /// <summary>
        /// 异步加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<T> LoadViewAsync<T>(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this, asset_name, parent, true, loadViewArgs);
        }

        /// <summary>
        /// 异步加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<T> LoadViewAsync<T>(Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this, typeof(T).Name, parent, true, loadViewArgs);
        }

        #endregion

        #region 加载Panel

        /// <summary>
        /// 加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel(string asset_name, UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public Panel LoadPanel(string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param)
        {
            return PanelManager.LoadPanel(this,asset_name, type, parent, param);
        }

        /// <summary>
        /// 加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel<T>(string asset_name, UIType type,Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadPanel<T>(  string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelLegacy<T>(this, asset_name, type, parent, param);
        }



        /// <summary>
        /// 加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel<T>(UIType type, Transform parent ,LoadViewArgs loadViewArgs)代替!")]
        public T LoadPanel<T>(UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelLegacy<T>(this, typeof(T).Name, type, parent, param);
        }

        /// <summary>
        /// 异步加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="uiType">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync(string asset_name, UIType uiType, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<Panel> LoadPanelAsync(string asset_name, UIType uiType = UIType.UI, Transform parent = null, params object[] param)
        {
            return PanelManager.LoadPanelAsync<Panel>(this, asset_name, uiType, parent, param);
        }

        /// <summary>
        /// 异步加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync<T>( string asset_name,UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<T> LoadPanelAsync<T>( string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this,asset_name, type, parent, param);
        }

        /// <summary>
        /// 异步加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync<T>(UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<T> LoadPanelAsync<T>(UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, typeof(T).Name, type, parent, param);
        }


        #endregion


        #region 加载Panel(With LoadViewArgs)

        /// <summary>
        /// 加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public Panel LoadPanel(string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null)
        {
            return PanelManager.LoadPanel<Panel>(this, asset_name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadPanel<T>(string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanel<T>(this,asset_name, type, parent, loadViewArgs);
        }



        /// <summary>
        /// 加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadPanel<T>(UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return LoadPanel<T>(typeof(T).Name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="uiType">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<Panel> LoadPanelAsync(string asset_name, UIType uiType = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return PanelManager.LoadPanelAsync<Panel>(this, asset_name, uiType, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<T> LoadPanelAsync<T>(string asset_name, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, asset_name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent"></param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<T> LoadPanelAsync<T>(UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, typeof(T).Name, type, parent, loadViewArgs);
        }

        #endregion


        #region 查询View

        /// <summary>
        /// 仅查询本模块View或Panel；跨模块访问须显式调用其owner。
        /// </summary>
        /// <param name="assetName">资源名</param>
        /// <param name="id">View ID</param>
        /// <returns></returns>
        public View GetView( string assetName, int id)
        {
            return ViewManager.GetView(this,assetName,id);
        }

        /// <summary>
        /// 仅查询本模块View或Panel；结果类型不匹配时抛出异常。
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="id">View ID</param>
        /// <returns></returns>
        public T GetView<T>(string assetName, int id) where T : View
        {
            return ViewManager.GetView<T>(this, assetName, id);
        }

        /// <summary>
        /// 按实例ID查询本模块View或Panel，不要求脚本名与资源名一致。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetView<T>(int id) where T : View
        {
            return ViewManager.GetView<T>(this, id);
        }

        /// <summary>
        /// 按资源名查询本模块唯一View或Panel；多个实例时须指定ID。
        /// </summary>
        /// <param name="assetName">资源名</param> 
        /// <returns></returns>
        public View GetView(string assetName )
        {
            return ViewManager.GetView(this, assetName );
        }

        /// <summary>
        /// 按资源名查询本模块唯一View或Panel；结果类型不匹配时抛出异常。
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="assetName">资源名称</param> 
        /// <returns></returns>
        public T GetView<T>(string assetName ) where T : View
        {
            return ViewManager.GetView<T>(this, assetName );
        }

        /// <summary>
        /// 按实际类型查询本模块唯一View或Panel；多个实例时须指定ID。
        /// </summary>
        /// <typeparam name="T"></typeparam> 
        /// <returns></returns>
        public T GetView<T>() where T : View
        {
            return ViewManager.GetView<T>(this);
        }

        /// <summary>
        /// 查询本模块某个资源名的所有View或Panel，返回独立结果集合。
        /// </summary>
        /// <param name="assetName">预制体资源名称</param>
        /// <returns></returns>
        public List<View> GetViews(string assetName)
        {
            return ViewManager.GetViews(this, assetName);
        }

        /// <summary>
        /// 查询本模块某个资源名的所有可赋给T的View或Panel。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public List<T> GetViews<T>( string assetName) where T : View
        {
            return ViewManager.GetViews<T>(this,assetName);
        }

        /// <summary>
        /// 查询本模块指定类型及派生类型的所有View或Panel。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetViews<T>() where T : View
        {
            return ViewManager.GetViews<T>(this);
        }


        public void GetViewsNonAlloc<T>(List<T> results) where T : View
        {
            ViewManager.GetViewsNonAlloc(this,results);
        }

        #endregion
          
        #region Controller

        /// <summary>
        /// 注册外部构造的本地Controller；已完成注册的同实例重复调用幂等，冲突抛出异常。
        /// </summary>
        public void AddController(Controller controller)
        {
            ControllerManager.AddController(this, controller);
        }

        /// <summary>
        /// 仅移除本模块拥有且与索引完全一致的实例；退出异常也会清除索引及owner。
        /// </summary>
        public void RemoveController(Controller controller)
        {
            ControllerManager.RemoveController(this, controller);
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型Controller，不创建；缺失返回null。
        /// 跨模块共享须在明确的拥有者Module上调用查询，不改变归属或生命周期责任。
        /// </summary>
        public Controller GetController(Type controllerType)
        {
            return ControllerManager.GetController(this, controllerType);
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型Controller，不创建；缺失返回null。
        /// </summary>
        public T GetController<T>() where T : Controller
        {
            return GetController(typeof(T)) as T;
        }

        /// <summary>
        /// 按完整类型名只查询本模块Controller，不创建；类型或实例不存在时返回null。
        /// </summary>
        public Controller GetController(string controllerTypeFullName)
        {
            if (string.IsNullOrEmpty(controllerTypeFullName)) return null;
            Type type = AssemblyTools.GetType(controllerTypeFullName);
            if (type == null) return null;
            return GetController(type);
        }

        /// <summary>
        /// 按完整类型名加载本模块Controller，缺失时才创建并注册到本模块。
        /// </summary>
        /// <param name="controllerTypeFullName">业务逻辑脚本名称(包含命名空间)</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Controller LoadController(string controllerTypeFullName) 
        {
            if (string.IsNullOrEmpty(controllerTypeFullName)) throw new ArgumentException("Controller type name is required.", nameof(controllerTypeFullName));
            Type type = AssemblyTools.GetType(controllerTypeFullName);
            if (type == null) throw new Exception(string.Format("未查询到类:{0}!", controllerTypeFullName));
            return LoadController(type);
        }

        /// <summary>
        /// 移除业务逻辑
        /// </summary>
        /// <param name="controllerType">业务逻辑类型</param>
        public void RemoveController(Type controllerType)
        {
            ControllerManager.RemoveController(this, controllerType);
        }

        /// <summary>
        /// 加载本模块Controller，缺失时才创建；初始化期间返回临时注册对象，退出期间拒绝Load。
        /// </summary>
        /// <param name="controllerType">业务逻辑类型</param>
        /// <returns></returns>
        public Controller LoadController(Type controllerType)
        {
            return ControllerManager.LoadController(this, controllerType);
        }

        /// <summary>
        /// 清空当前模块的所有Controller
        /// </summary>
        public void ClearController()
        {
            ControllerManager.ClearController(this);
        }

        /// <summary>
        /// 加载本模块Controller，缺失时才创建；初始化期间返回临时注册对象，退出期间拒绝Load。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T LoadController<T>() where T : Controller
        {
            return ControllerManager.LoadController<T>(this);
        }

        /// <summary>
        /// 移除业务逻辑(泛型方式)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public void RemoveController<T>() where T : Controller
        {
            RemoveController(typeof(T));
        }


        /// <summary>
        /// 新建并注册本地Controller；类型已注册时在构造前拒绝，不借用其他模块实例。
        /// </summary>
        public T CreateController<T>()  where T: Controller
        {
            return ControllerManager.CreateController<T>(this);
        }

        /// <summary>
        /// 新建并注册本地Controller；类型已注册时在构造前拒绝，不借用其他模块实例。
        /// </summary>
        public Controller CreateController(Type type) 
        {
            return ControllerManager.CreateController(this, type);
        }

        /// <summary>
        /// 按完整类型名新建本地Controller，不查询或借用其他模块的实例。
        /// </summary>
        public Controller CreateController(string controllerTypeFullName) {
            if (string.IsNullOrEmpty(controllerTypeFullName)) throw new ArgumentException("Controller type name is required.", nameof(controllerTypeFullName));
            Type type = AssemblyTools.GetType(controllerTypeFullName);
            if (type == null) throw new Exception(string.Format("未查询到类:{0}!", controllerTypeFullName));
            return CreateController(type);
        }

        #endregion

        #region 方法

        /// <summary>
        /// 模块启动完成时触发
        /// </summary>
        public abstract void OnStart();

        /// <summary>
        /// 进入Closing后、业务对象清理前触发；无需调用base即可完成框架收尾。
        /// </summary>
        protected virtual void OnClose() { }

        internal void Exit() => OnClose();

        internal void EnsureCanAddObjects()
        {
            if (State == ModuleState.Closing || State == ModuleState.Closed || State == ModuleState.CloseFailed)
                throw new InvalidOperationException($"Cannot add objects to Module in state {State}: {ModuleName}");
        }

        /// <summary>
        /// 关闭当前注册的自身实例；Starting期间拒绝关闭，不作为启动取消。
        /// </summary>
        public void Close() 
        {
            ModuleManager.CloseModule(this);
        }

#endregion

    }
}
