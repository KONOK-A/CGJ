using System;
using System.Collections.Generic;
using System.Threading;
using YLFramework.Core.Pool;
using YLFramework.Core.RoslynAnalyzers;
using YLFramework.Core.Singleton;


 
namespace YLFramework.Core
{
    /// <summary>
    /// 加载 View 的参数，只能在主线程使用 New 创建，并在同一线程完成使用和归还。
    /// 接管前可读写；接管后只有 OnLoaded 回调的借用窗口可读，操作终态立即归还并清引用。
    /// </summary>
    /// <remarks>
    /// 只能通过 New 和加载操作的专用入口管理生命周期，不支持通用池接管。
    /// 不为每次租用分配 token：原始池对象重新租用后无法识别非法旧别名，禁止保存参数引用。
    /// </remarks>
    [DisableNew("请使用静态方法 LoadViewArgs.New() 来创建对象!")]
    [MethodLocalOnlyAttribute("该类会在加载操作终态自动回收,请勿保存参数引用!")]
    public class LoadViewArgs
    {
        #region 字段

        private static readonly ObjectPool<LoadViewArgs> Pool = new(
            () => new LoadViewArgs(), onAllocate: args => args.BeginLease(),
            reset: args => args.ResetLease(), cacheOrder: PoolCacheOrder.Lifo);

        static LoadViewArgs()
        {
            // 只清空闲缓存；已接管参数仍由原操作终态负责释放，不替换其所属池。
            SingletonSessionState.RegisterSessionReset(Pool.Clear);
        }

#pragma warning disable IDE0044 // 添加只读修饰符
        private Dictionary<string, string> strings = new Dictionary<string, string>(); 

        private Dictionary<string, int> ints = new Dictionary<string, int>();

        private Dictionary<string, bool> bools = new Dictionary<string, bool>();
         
        private Dictionary<string, float> floats = new Dictionary<string, float>();

        private Dictionary<string, long> longs = new Dictionary<string, long>();

        private Dictionary<string, double> doubles = new Dictionary<string, double>();

        private Dictionary<string, object> objects = new Dictionary<string, object>();

        private bool leased;
        private bool owned;
        private bool reading;
        private int threadId;

#pragma warning restore IDE0044 // 添加只读修饰符

        #endregion

        #region 查询

        public string GetString(string name)
        {
            EnsureReadable();
            if(strings.ContainsKey(name))
                return strings[name];

            return string.Empty;
        }

        public int GetInt(string name)
        {
            EnsureReadable();
            if (ints.ContainsKey(name))
                return ints[name];

            return 0;
        }

        public bool GetBool(string name)
        {
            EnsureReadable();
            if (bools.ContainsKey(name))
                return bools[name];

            return false;
        }

        public float GetFloat(string name)
        {
            EnsureReadable();
            if (floats.ContainsKey(name))
                return floats[name];

            return 0.0f;
        }

        public long GetLong(string name)
        {
            EnsureReadable();
            if (longs.ContainsKey(name))
                return longs[name];

            return 0;
        }

        public double GetDouble(string name)
        {
            EnsureReadable();
            if (doubles.ContainsKey(name))
                return doubles[name];

            return 0.0;
        }

        public object GetObject(string name)
        {
            EnsureReadable();
            if (objects.ContainsKey(name))
                return objects[name];
            return null;
        }

        #endregion

        #region 修改

        public void SetString(string name, string value)
        {
            EnsureWritable();
            strings[name] = value;
        }

        public void SetInt(string name, int value)
        {
            EnsureWritable();
            ints[name] = value;
        }

        public void SetBool(string name, bool value)
        {
            EnsureWritable();
            bools[name] = value;
        }

        public void SetFloat(string name, float value)
        {
            EnsureWritable();
            floats[name] = value;
        }

        public void SetLong(string name, long value)
        {
            EnsureWritable();
            longs[name] = value;
        }

        public void SetDouble(string name, double value)
        {
            EnsureWritable();
            doubles[name] = value; 
        }

        public void SetObject(string name, object value)
        {
            EnsureWritable();
            objects[name] = value;
        }

        #endregion

        #region 生命周期

        private void BeginLease()
        {
            if (leased)
                throw new InvalidOperationException("The pool returned leased LoadViewArgs.");
            ClearValues();
            threadId = Thread.CurrentThread.ManagedThreadId;
            owned = false;
            reading = false;
            leased = true;
        }

        private void ResetLease()
        {
            ClearValues();
            reading = false;
            owned = false;
            leased = false;
        }

        /// <summary>接管本次租用。失败不改变既有所有权或数据。</summary>
        internal void Claim()
        {
            EnsureWritable();
            owned = true;
        }

        /// <summary>归还已接管参数一次；已归还时无操作。</summary>
        internal void Release()
        {
            EnsureThread();
            if (!leased) return;
            if (!owned)
                throw new InvalidOperationException("LoadViewArgs has not been claimed.");
            ReturnToPool();
        }

        /// <summary>仅在调用业务 OnLoaded 前开放临时只读借用，必须配对 EndRead。</summary>
        internal void BeginRead()
        {
            EnsureLeased();
            if (!owned || reading)
                throw new InvalidOperationException("LoadViewArgs must be owned without an active read window.");
            reading = true;
        }

        /// <summary>结束业务回调的读取权，不归还参数。</summary>
        internal void EndRead()
        {
            EnsureLeased();
            if (!owned || !reading)
                throw new InvalidOperationException("LoadViewArgs has no active read window.");
            reading = false;
        }

        private void EnsureThread()
        {
            if (threadId != 0 && threadId != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("LoadViewArgs must stay on the thread that called New.");
        }

        private void EnsureLeased()
        {
            if (!leased)
                throw new ObjectDisposedException(nameof(LoadViewArgs));
            EnsureThread();
        }

        private void EnsureReadable()
        {
            EnsureLeased();
            if (owned && !reading)
                throw new InvalidOperationException("LoadViewArgs is only readable during OnLoaded after being claimed.");
        }

        private void EnsureWritable()
        {
            EnsureLeased();
            if (owned)
                throw new InvalidOperationException("Claimed LoadViewArgs cannot be modified or recycled by the caller.");
        }

        private void ClearValues()
        {
            strings.Clear();
            ints.Clear();
            bools.Clear();
            floats.Clear();
            longs.Clear();
            doubles.Clear();
            objects.Clear();
        }

        private void ReturnToPool()
        {
            Pool.Recycle(this);
        }

        #endregion

        #region 构造方法

        /// <summary>
        /// 在主线程开始本地租用。接管前由调用方读写，交给加载操作后不得保存引用。
        /// </summary>
        /// <returns></returns>
        public static LoadViewArgs New()
        {
            return Pool.Allocate();
        }

        /// <summary>归还未接管参数；已归还时无操作，已接管时拒绝。</summary>
        internal static void Recycle(LoadViewArgs args)
        {
            if (args == null) return;
            args.EnsureThread();
            if (!args.leased) return;
            args.EnsureWritable();
            args.ReturnToPool();
        }

        #endregion 

    } 

}
