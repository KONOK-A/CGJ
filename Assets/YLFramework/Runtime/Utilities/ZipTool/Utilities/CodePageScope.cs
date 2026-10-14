using System;
using YLFramework.Utilities.ZipTool.Zip;

namespace YLFramework.Utilities.ZipTool
{
    /// <summary>
    /// nameCodePage 的 try/finally 临时切换作用域。ZipConstants.DefaultCodePage 为进程级静态，非线程安全。
    /// 嵌套使用时按 LIFO 恢复；重复 Dispose 安全。
    /// </summary>
    internal struct CodePageScope : IDisposable
    {
        private readonly bool _changed;
        private readonly int _originalCodePage;
        private bool _disposed;

        public CodePageScope(int? nameCodePage)
        {
            _changed = nameCodePage.HasValue;
            _originalCodePage = _changed ? ZipConstants.DefaultCodePage : 0;
            _disposed = false;
            if (_changed)
            {
                ZipConstants.DefaultCodePage = nameCodePage.Value;
            }
        }

        public void Dispose()
        {
            if (_changed && !_disposed)
            {
                _disposed = true;
                ZipConstants.DefaultCodePage = _originalCodePage;
            }
        }
    }
}
