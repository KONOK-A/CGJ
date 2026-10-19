using System; 
using System.Collections.Generic; 
using UnityEngine; 
using YLFramework.Foundation;

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
    public abstract partial class Module
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
        /// YL Resource ProjectId，与Module类型身份分离；默认类型短名，必须对应显式挂载的新YL项目。
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
        /// Provider复制文本并归还自己的资产租约后，再执行JSON构造与Model初始化。
        /// </summary>

        [Obsolete("已过时!")]
        public virtual Dictionary<string, Type> PreloadConfigTables => null;

        /// <summary>
        /// 默认启动的场景(在启动模块时自动加载的场景名称)
        /// </summary>
        public virtual string DefaultStartUpScene => string.Empty;

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
