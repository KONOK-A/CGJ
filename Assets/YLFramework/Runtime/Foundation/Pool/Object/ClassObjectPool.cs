using System;
using System.Collections.Generic;
using System.Reflection;
using YLFramework.Core.Singleton;

namespace YLFramework.Core.Pool
{
    /// <summary>
    /// 按运行时类型分桶的普通类对象池（对象需实现 IOfPool）。
    /// 分配时先反射重置实例的字段与可写属性（仅限声明类型自身、非只读、非静态成员），再调用 OnAllocate。
    /// 注意：
    /// 1. 该池与 SafeObjectPool 相互独立，同一类型不要同时交给两个池管理，否则同一实例可能同时存在于两个池中；
    /// 2. 反射重置不覆盖基类私有字段，复杂继承状态建议由 OnRecycle / OnAllocate 自行清理；
    /// 3. IsRecycled 始终通过 IOfPool 接口直接读写，不依赖反射；反射重置中成员 setter 抛出的异常会向上传播。
    /// </summary>
    public class ClassObjectPool : ISingleton
    {
        private static readonly Dictionary<Type, List<IOfPool>> ObjectDict = new();

        #region 单例相关

        /// <summary>
        /// 单例初始化：清空所有类型的缓存。
        /// </summary>
        public void OnSingletonInit()
        {
            ObjectDict.Clear();
        }

        /// <summary>
        /// 对象池单例入口。
        /// </summary>
        public static ClassObjectPool Instance => SingletonProperty<ClassObjectPool>.Instance;

        #endregion

        #region 方法

        /// <summary>
        /// 按类型分配对象：优先复用缓存，缓存为空时反射创建。
        /// </summary>
        /// <param name="type">实现 IOfPool 的类型。</param>
        /// <returns>已清除回收标记并完成 OnAllocate 回调的对象。</returns>
        /// <exception cref="ArgumentNullException">type 为 null。</exception>
        /// <exception cref="ArgumentException">type 未实现 IOfPool。</exception>
        /// <exception cref="MissingMethodException">type 没有可供反射创建的无参构造函数。</exception>
        public static IOfPool Allocate(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!typeof(IOfPool).IsAssignableFrom(type))
            {
                throw new ArgumentException(
                    $"类型 {type.FullName} 未实现 IOfPool 接口，无法由 ClassObjectPool 管理。",
                    nameof(type));
            }

            // 优先复用缓存，从尾部出池保证 O(1)
            if (ObjectDict.TryGetValue(type, out var list) && list.Count > 0)
            {
                var lastIndex = list.Count - 1;
                var obj = list[lastIndex];
                list.RemoveAt(lastIndex);
                if (list.Count == 0)
                {
                    ObjectDict.Remove(type);
                }

                SetDefaultValue(obj);
                obj.IsRecycled = false;
                obj.OnAllocate();
                return obj;
            }

            // 缓存为空则反射创建
            IOfPool newObj;
            try
            {
                newObj = MultiObjectFactory.Create(type) as IOfPool;
            }
            catch (MissingMemberException e)
            {
                throw new MissingMethodException(
                    $"类型 {type.FullName} 需要无参构造函数才能由 ClassObjectPool 创建。", e);
            }

            if (newObj == null) return null;

            SetDefaultValue(newObj);
            newObj.IsRecycled = false;
            newObj.OnAllocate();
            return newObj;
        }

        /// <summary>
        /// 回收对象：空对象或已回收对象会被忽略，成功后进入对应类型的缓存。
        /// </summary>
        /// <param name="obj">待回收对象。</param>
        public static void Recycle(IOfPool obj)
        {
            // 对象为空或者已经被回收了则直接返回，避免同一实例在池中出现多次
            if (obj == null || obj.IsRecycled) return;

            var type = obj.GetType();

            obj.OnRecycle();
            obj.IsRecycled = true;

            if (ObjectDict.TryGetValue(type, out var list))
            {
                list.Add(obj);
            }
            else
            {
                ObjectDict.Add(type, new List<IOfPool> { obj });
            }
        }

        // 将实例的字段与可写属性重置为默认值（引用类型为 null，值类型为默认值）
        private static void SetDefaultValue(object obj)
        {
            if (obj == null) return;
            var type = obj.GetType();
            //设置所有字段的默认值
            foreach (var field in type.GetFields(
                         BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                //忽略只读字段
                if (!field.IsInitOnly)
                {
                    field.SetValue(obj, GetDefaultValue(field.FieldType));
                }
            }

            //设置所有属性的默认值
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                        BindingFlags.Instance))
            {
                if (property.CanWrite) //属性可写才设置
                {
                    property.SetValue(obj, GetDefaultValue(property.PropertyType), null);
                }
            }
        }

        private static object GetDefaultValue(Type type)
        {
            //若为值类型则返回默认实例值，引用类型则返回null
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        #endregion
    }
}
