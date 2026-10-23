using System;
using UnityEngine;
using YLFramework.Core.Pool;
using YLFramework.Core.Singleton;
using YLFramework.Events;

namespace YLFramework.Binding
{
    /// <summary>
    /// 只读可绑定属性契约：读取当前值并订阅值变化。
    /// 泛型协变（out），只读引用可赋给更宽泛的 T 使用；同时继承 <see cref="IEasyEvent"/> 以便与事件系统互操作。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    public interface IReadOnlyBindableProperty<out T> : IEasyEvent
    {
        /// <summary>
        /// 当前值；读取本身不会触发任何回调。
        /// </summary>
        T Value
        {
            get;
        }

        /// <summary>
        /// 注册值变化监听并立即用当前值同步回调一次，随后保持注册。
        /// </summary>
        /// <param name="onValueChanged">值变化回调，为 null 时不产生实际监听。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        IUnregister RegisterWithInitValue(Action<T> onValueChanged);

        /// <summary>
        /// 注册值变化监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="onValueChanged">值变化回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        IUnregister Register(Action<T> onValueChanged);

        /// <summary>
        /// 注销一个已注册的值变化回调；未注册时为空操作。
        /// </summary>
        /// <param name="onValueChanged">要注销的回调。</param>
        void Unregister(Action<T> onValueChanged);
    }

    /// <summary>
    /// 可读写可绑定属性契约：在只读契约之上增加写入与强制通知。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    public interface IBindableProperty<T> : IReadOnlyBindableProperty<T>
    {
        /// <summary>
        /// 读写当前值；赋值等价于 <see cref="TrySetValue"/>：仅在判定为变化时才通知。
        /// </summary>
        new T Value
        {
            get; set;
        }

        /// <summary>
        /// 直接写入存储值，不触发任何回调，也不做相等比较；写入后的值作为后续比较的当前值。
        /// </summary>
        void SetValueWithoutCallback(T value);

        /// <summary>
        /// 值发生变化时写入并触发回调；返回是否实际发生变化（未变化时返回 false 且不通知）。
        /// </summary>
        bool TrySetValue(T value);

        /// <summary>
        /// 不改变存储值，强制按当前值触发一次变化回调（适用于值对象被原地修改后）。
        /// </summary>
        void ForceNotify();
    }

    /// <summary>
    /// 可绑定属性：把“值相等时静默、变化时通知”的常见需求封装为可直接替换字段的属性。
    /// 相等判定默认使用静态 <see cref="Comparer"/>（常见 Unity 值类型已在进入 Play 时自动注册合适的比较器），
    /// 也可通过 <see cref="WithComparer"/> 为单个实例覆盖；null 与 null 视为相同，null 与非 null 视为不同。
    /// 可读写属性（<see cref="IBindableProperty{T}.Value"/>）不会重复通知未变化的值；如需绕过比较请用
    /// <see cref="ForceNotify"/> 或 <see cref="SetValueWithoutCallback"/>。
    /// 该类型可通过 <see cref="Allocate"/> / <see cref="Recycle"/> 从池中复用，复用时会清空监听、比较器与值。
    /// 非线程安全。
    /// </summary>
    /// <typeparam name="T">属性值类型。</typeparam>
    public class BindableProperty<T> : IBindableProperty<T>
    {
        /// <summary>
        /// 创建值为 default 的属性（无参构造优先于带默认值的构造）。
        /// </summary>
        public BindableProperty()
        {
            StoredValue = default;
        }

        /// <summary>
        /// 创建带初始值的属性。
        /// </summary>
        /// <param name="defaultValue">初始值。</param>
        public BindableProperty(T defaultValue = default)
        {
            StoredValue = defaultValue;
        }

        /// <summary>
        /// 静态默认比较器：实例比较器未设置时使用，判断新旧值是否相等以决定是否触发通知。
        /// 默认使用 <c>a.Equals(b)</c>，常见值类型会在每次进入 Play 时被自动替换为更合适的比较器（见 ComparerAutoRegister）。
        /// 运行时可直接覆盖；请勿设为 null，否则比较非 null 值时会在调用处抛出 NullReferenceException。
        /// </summary>
        public static Func<T, T, bool> Comparer { get; set; } = (a, b) => a.Equals(b);

        /// <summary>
        /// 为当前实例设置专用比较器，覆盖静态 <see cref="Comparer"/>；仅影响本实例。
        /// </summary>
        /// <param name="comparer">相等判定委托。</param>
        /// <returns>当前实例，便于链式配置。</returns>
        /// <exception cref="ArgumentNullException">comparer 为 null。</exception>
        public BindableProperty<T> WithComparer(Func<T, T, bool> comparer)
        {
            _instanceComparer = comparer ?? throw new ArgumentNullException(nameof(comparer));
            return this;
        }

        #region 值的设置和读取

        /// <summary>
        /// 当前存储值；派生类请通过 <see cref="GetValue"/> / <see cref="SetValue"/> 或 Value 读写，直接访问不触发通知。
        /// </summary>
        protected T StoredValue;
        private Func<T, T, bool> _instanceComparer;

        private Func<T, T, bool> CurrentComparer => _instanceComparer ?? Comparer;

        /// <summary>
        /// 写入存储值（默认直接覆盖，不触发通知）；派生类可扩展写入行为。
        /// </summary>
        protected virtual void SetValue(T value) => StoredValue = value;

        /// <summary>
        /// 读取存储值（默认直接返回内部字段）；派生类可扩展取值来源。
        /// </summary>
        protected virtual T GetValue() => StoredValue;

        /// <summary>
        /// 读写当前值：get 取存储值；set 等价于 <see cref="TrySetValue"/>（未变化不通知）。
        /// </summary>
        public virtual T Value
        {
            get => GetValue();
            set => TrySetValue(value);
        }

        /// <summary>
        /// 设置值并在真的发生变化时触发回调，返回是否发生变化
        /// </summary>
        public virtual bool TrySetValue(T value)
        {
            if (IsSameValue(value))
            {
                return false;
            }

            SetValue(value);
            OnValueChange();
            return true;
        }

        /// <summary>
        /// 不改变值，强制按当前值触发一次回调（适用于值对象被原地修改后）
        /// </summary>
        public virtual void ForceNotify() => OnValueChange();

        /// <summary>
        /// 比较候选值与当前存储值；派生属性可适配首次赋值或外部值的通知策略。
        /// value 为 null 时直接按引用相等判断（null 与 null 相同，null 与非 null 不同）。
        /// </summary>
        protected virtual bool IsSameValue(T value)
        {
            if (value == null)
            {
                return StoredValue == null;
            }

            return CurrentComparer(value, StoredValue);
        }

        /// <summary>
        /// 直接设置值，不触发回调，也不做相等比较。
        /// </summary>
        public virtual void SetValueWithoutCallback(T value) => SetValue(value);

        #endregion

        /// <summary>
        /// 返回当前值的字符串表示；值为 null 时返回空字符串。
        /// </summary>
        public override string ToString() => Value?.ToString() ?? string.Empty;


        private readonly EasyEvent<T> _onValueChanged = EasyEvent<T>.Allocate();

        /// <summary>
        /// 触发一次值变化通知（回调参数为当前值）；派生类可扩展通知渠道，扩展时通常应先调用 base。
        /// </summary>
        protected virtual void OnValueChange()
        {
            _onValueChanged?.Trigger(Value);
        }

        /// <inheritdoc />
        IUnregister IEventSource.Register(Action eventAction)
        {
            return eventAction == null ? EmptyUnregister.Instance : Register(Action);
            void Action(T _) => eventAction();
        }

        /// <summary>
        /// 注销所有值变化监听。
        /// </summary>
        public void UnregisterAll()
        {
            _onValueChanged?.UnregisterAll();
        }

        /// <summary>
        /// 注册值变化监听并立即同步回调当前值一次，随后保持注册。
        /// </summary>
        /// <param name="onValueChanged">值变化回调，为 null 时不产生实际监听。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        /// <remarks>
        /// 初始回调直接使用内部存储值，不经过 <see cref="GetValue"/> 派生逻辑；
        /// 重写了 GetValue 的派生类（如 CustomProperty）建议先读取一次 Value 让缓存刷新，再调用本方法。
        /// </remarks>
        public IUnregister RegisterWithInitValue(Action<T> onValueChanged)
        {
            //先传一下初始值
            onValueChanged?.Invoke(StoredValue);
            return Register(onValueChanged);
        }

        /// <summary>
        /// 注册值变化监听，返回用于注销本次注册的句柄。
        /// </summary>
        /// <param name="onValueChanged">值变化回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public virtual IUnregister Register(Action<T> onValueChanged)
        {
            return _onValueChanged.Register(onValueChanged);
        }

        /// <summary>
        /// 只监听一次值变化；用户回调执行前消费注册，嵌套通知不会重复执行。
        /// </summary>
        public IUnregister RegisterOnce(Action<T> onValueChanged)
        {
            return _onValueChanged.RegisterOnce(onValueChanged);
        }

        /// <summary>
        /// 注销一个已注册的值变化回调；未注册时为空操作。
        /// </summary>
        public virtual void Unregister(Action<T> onValueChanged)
        {
            _onValueChanged.Unregister(onValueChanged);
        }

        #region 对象池相关

        /// <summary>
        /// 从对象池分配一个实例；池首次使用时自动初始化（容量 50、预热 1）。
        /// 分配出的实例带 default 值且没有监听。
        /// </summary>
        public static BindableProperty<T> Allocate()
        {
            EnsurePoolInitialized();
            return SafeObjectPool<BindableProperty<T>>.Instance.Allocate();
        }

        /// <summary>
        /// 把本池取得的实例归还当前类型池，清空监听、比较器并重置值。
        /// 外部构造、派生实例及旧池借用不被新池接纳；持有旧池者可归还原池。
        /// </summary>
        public static void Recycle(BindableProperty<T> property)
        {
            EnsurePoolInitialized();
            SafeObjectPool<BindableProperty<T>>.Instance.Recycle(property);
        }

        // 池实例不存在（首次使用 / Session 重置 / 手动 Dispose 后）时重新初始化配置，
        // 避免静态初始化标志跨会话残留导致池按默认容量运行
        private static void EnsurePoolInitialized()
        {
            if (!SingletonProperty<SafeObjectPool<BindableProperty<T>>>.HasInstance)
            {
                SafeObjectPool<BindableProperty<T>>.Instance.Init(50, 1);
            }
        }


        /// <summary>
        /// 池回收时调用：终结旧通知代次、清空监听与实例比较器并重置值；保留自有事件，不将其归池。
        /// 直接调用不会入池，入池应使用 <see cref="Recycle"/>。
        /// </summary>
        public virtual void OnRecycle()
        {
            _onValueChanged.ResetOwner();
            _instanceComparer = null;
            StoredValue = default;
        }

        /// <summary>
        /// 领域回收标记，由池维护；真实池身份不由此标记决定。
        /// </summary>
        public bool IsRecycled
        {
            get; set;
        }

        /// <summary>
        /// 实现 <see cref="IOfPool"/> 的回收入口，等价于 <see cref="Recycle"/>。
        /// </summary>
        public void RecycleToCache()
        {
            Recycle(this);
        }

        #endregion
    }

    /// <summary>
    /// 在进入 Play 时为常见值类型注册默认比较器，使 BindableProperty&lt;T&gt; 的相等判定不依赖各类型 Equals 的默认实现
    /// （例如 float 采用 Mathf.Approximately 近似比较，Color32/RangeInt 按字段比较）。
    /// Domain Reload disabled 时每次进入 Play 都会重新执行，会覆盖运行时被替换的静态比较器。
    /// </summary>
    internal static class ComparerAutoRegister
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void AutoRegister()
        {
            BindableProperty<int>.Comparer = (a, b) => a == b;
            BindableProperty<float>.Comparer = Mathf.Approximately;
            BindableProperty<double>.Comparer = (a, b) => Math.Abs(a - b) < double.Epsilon;
            BindableProperty<string>.Comparer = (a, b) => a == b;
            BindableProperty<long>.Comparer = (a, b) => a == b;
            BindableProperty<Vector2>.Comparer = (a, b) => a == b;
            BindableProperty<Vector3>.Comparer = (a, b) => a == b;
            BindableProperty<Vector4>.Comparer = (a, b) => a == b;
            BindableProperty<Color>.Comparer = (a, b) => a == b;
            BindableProperty<Color32>.Comparer =
                (a, b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            BindableProperty<Bounds>.Comparer = (a, b) => a == b;
            BindableProperty<Rect>.Comparer = (a, b) => a == b;
            BindableProperty<Quaternion>.Comparer = (a, b) => a == b;
            BindableProperty<Vector2Int>.Comparer = (a, b) => a == b;
            BindableProperty<Vector3Int>.Comparer = (a, b) => a == b;
            BindableProperty<BoundsInt>.Comparer = (a, b) => a == b;
            BindableProperty<RangeInt>.Comparer = (a, b) => a.start == b.start && a.length == b.length;
            BindableProperty<RectInt>.Comparer = (a, b) => a.Equals(b);
        }
    }
}
