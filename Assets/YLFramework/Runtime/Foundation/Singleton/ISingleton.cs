namespace YLFramework.Core.Singleton
{
    /// <summary>
    /// 单例标记接口：配合 SingletonProperty / MonoSingletonProperty / MonoSingleton 等入口使用，
    /// 实例的创建、缓存与释放由对应的入口类型负责，本接口只提供初始化回调。
    /// </summary>
    public interface ISingleton
    {
        /// <summary>
        /// 单例实例创建完成后调用一次（每次重新创建都会再次调用）。
        /// 适合在这里做初始化；请避免在其中再次访问 Instance——部分入口未做重入保护，可能导致递归创建或重复初始化。
        /// </summary>
        void OnSingletonInit();
    }
}
