using System;

namespace YLFramework.Binding
{
    /// <summary>
    /// 一次性批量作用域：Dispose 时结束批量，重复 Dispose 安全
    /// </summary>
    internal sealed class BatchScope : IDisposable
    {
        private Action _end;

        public BatchScope(Action end)
        {
            _end = end;
        }

        public void Dispose()
        {
            var end = _end;
            _end = null;
            end?.Invoke();
        }
    }
}
