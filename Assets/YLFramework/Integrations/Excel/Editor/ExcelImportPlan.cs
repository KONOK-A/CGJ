using System;
using System.Collections.Generic;
using System.Linq;

namespace YLFramework.Excel.Editor
{
    public enum ExcelCommitResult
    {
        Pending,
        Applied,
        Cancelled,
        Failed
    }
    public enum ExcelChangeKind
    {
        Add,
        Update,
        Delete
    }

    public sealed class ExcelChange
    {
        public ExcelChangeKind Kind
        {
            get;
        }
        public string Key
        {
            get;
        }
        public string Before
        {
            get;
        }
        public string After
        {
            get;
        }
        internal ExcelChange(ExcelChangeKind kind, string key, string before, string after)
        {
            Kind = kind;
            Key = key;
            Before = before;
            After = after;
        }
    }

    /// <summary>预检不改目标；一次性计划必须明确确认，取消或关闭后不可再提交。</summary>
    public sealed class ExcelImportPlan : IDisposable
    {
        private readonly List<string> _errors = new List<string>();
        private readonly List<ExcelChange> _changes = new List<ExcelChange>();
        private Action _apply;
        private Action _release;
        private Action _validation;
        public IReadOnlyList<string> Errors => _errors.AsReadOnly();
        public IReadOnlyList<ExcelChange> Changes => _changes.AsReadOnly();
        public ExcelCommitResult Result
        {
            get; private set;
        }
        public bool CanCommit => Result == ExcelCommitResult.Pending && _errors.Count == 0 && _apply != null;

        internal void Error(string error) => _errors.Add(error);
        internal void Prepare(Action commit, Action cleanup = null)
        {
            _apply = commit;
            _release = cleanup;
        }
        /// <summary>只读检查领域外部约束，在预览及真正写入前各执行一次；失败不提交。</summary>
        public void AddValidation(Action check)
        {
            if (check == null)
            {
                throw new ArgumentNullException(nameof(check));
            }

            if (Result != ExcelCommitResult.Pending)
            {
                throw new InvalidOperationException("The import plan is already resolved.");
            }

            check();
            _validation += check;
        }
        internal void Diff(IDictionary<string, string> before, IDictionary<string, string> after)
        {
            foreach (var item in after)
            {
                if (!before.TryGetValue(item.Key, out var previous))
                {
                    _changes.Add(new ExcelChange(ExcelChangeKind.Add, item.Key, null, item.Value));
                }
                else if (previous != item.Value)
                {
                    _changes.Add(new ExcelChange(ExcelChangeKind.Update, item.Key, previous, item.Value));
                }
            }
            foreach (var item in before.Where(item => !after.ContainsKey(item.Key)))
            {
                _changes.Add(new ExcelChange(ExcelChangeKind.Delete, item.Key, item.Value, null));
            }
        }

        public ExcelCommitResult Commit(bool confirmed)
        {
            if (Result != ExcelCommitResult.Pending)
            {
                return Result;
            }

            if (!confirmed)
            {
                Result = ExcelCommitResult.Cancelled;
            }
            else if (!CanCommit)
            {
                Result = ExcelCommitResult.Failed;
            }
            else
            {
                try
                {
                    _validation?.Invoke();
                    _apply();
                    Result = ExcelCommitResult.Applied;
                }
                catch (Exception e)
                {
                    _errors.Add(e.ToString());
                    Result = ExcelCommitResult.Failed;
                }
            }
            _apply = null;
            _validation = null;
            _release?.Invoke();
            _release = null;
            return Result;
        }
        public void Dispose()
        {
            if (Result == ExcelCommitResult.Pending)
            {
                Commit(false);
            }
        }
    }
}
