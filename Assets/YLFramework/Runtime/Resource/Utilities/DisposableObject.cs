using System;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 手动释放模式的对象基类：显式 <see cref="Dispose"/> 只执行一次释放流程（幂等能力由本类保证），
    /// 并抑制终结器；子类通过重写 <see cref="DisposeGC"/> / <see cref="DisposeNGC"/> 扩展释放逻辑。
    /// <para>
    /// 未被显式 Dispose 的对象在终结器路径下只走 <see cref="DisposeNGC"/>，不会执行托管侧清理。
    /// </para>
    /// </summary>
    public class DisposableObject
    {
        private bool _isDisposed;

        ~DisposableObject()
        {
            Dispose(false);
        }

        /// <summary>
        /// 释放对象：首次调用触发 DisposeGC + DisposeNGC 并抑制终结器；重复调用无效果。
        /// </summary>
        public virtual void Dispose()
        {
            Dispose(true);
            //请求垃圾回收时不要调用该对象的终结器
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放托管资源的重写点；仅由显式 <see cref="Dispose()"/> 调用，终结器路径不会调用。
        /// </summary>
        protected virtual void DisposeGC()
        {
            
        }

        /// <summary>
        /// 释放非托管资源的重写点；显式 Dispose 与终结器路径都会调用。
        /// </summary>
        protected virtual void DisposeNGC()
        {
            
        }
        /// <param name="disposing">true 表示由 <see cref="Dispose()"/> 主动释放；false 表示来自终结器。</param>
        private void Dispose(bool disposing)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                DisposeGC();
            }
            DisposeNGC();
            _isDisposed = true;
        }
        
        
    }
}