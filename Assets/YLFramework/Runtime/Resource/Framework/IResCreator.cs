namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 资源创建器契约：决定一类 ResKey 由哪种 <see cref="IRes"/> 实现承载。
    /// <para>
    /// 由 <see cref="ResFactory"/> 按注册顺序依次询问：<see cref="Match"/> 命中者尝试
    /// <see cref="Create"/>；Create 返回 null 时继续询问后续匹配者。
    /// </para>
    /// </summary>
    public interface IResCreator
    {
        /// <summary>判断该查询键是否由本创建器处理；命中不代表一定能创建成功。</summary>
        /// <param name="resKey">资源查询键。</param>
        bool Match(ResKey resKey);

        /// <summary>创建资源对象；返回 null 表示本创建器无法创建该资源，由工厂继续尝试其他创建器。</summary>
        /// <param name="resKey">资源查询键。</param>
        /// <returns>创建出的资源；无法创建时返回 null。</returns>
        IRes Create(ResKey resKey);
    }
}