using System;
using System.Collections;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 可被 <see cref="ServiceManager"/> 协程队列调度的异步任务契约。
    /// <para>
    /// 任务被调度时执行 <see cref="DoLoadAsync"/>；实现必须在加载结束（无论成功或失败）时调用
    /// 传入的完成回调，ServiceManager 借此释放并发额度并调度下一个任务。
    /// </para>
    /// </summary>
    public interface IEnumeratorTask
    {
        /// <summary>
        /// 执行一次异步加载。
        /// </summary>
        /// <param name="finishCallback">加载结束（无论成败）时调用一次，用于通知调度方本任务已完成。</param>
        IEnumerator DoLoadAsync(Action finishCallback);
    }
}