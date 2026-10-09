using YLFramework.Core.Extensions;
using System; 
using System.Collections.Generic;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using YLFramework.Core.RoslynAnalyzers;

namespace YLFramework.Core
{

    // 事件委托(无参)
    public delegate void XFEvent();
    // 事件委托(1参)
    public delegate void XFEvent<T1>(T1 p1);
    // 事件委托(2参)
    public delegate void XFEvent<T1, T2>(T1 p1, T2 p2);
    // 事件委托(3参)
    public delegate void XFEvent<T1, T2, T3>(T1 p1, T2 p2, T3 p3);
    // 事件委托(4参)
    public delegate void XFEvent<T1, T2, T3, T4>(T1 p1, T2 p2, T3 p3, T4 p4);
    // 事件委托(5参)
    public delegate void XFEvent<T1, T2, T3, T4, T5>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5);
    // 事件委托(6参)
    public delegate void XFEvent<T1, T2, T3, T4, T5, T6>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6);
    // 事件委托(7参)
    public delegate void XFEvent<T1, T2, T3, T4, T5, T6, T7>(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7);


    public class EventManager
    {

        #region 单例

        private static EventManager _instance;

        internal static EventManager Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new EventManager();
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif


        private EventManager()
        {
        }

        #endregion

        #region 字段

        private Dictionary<int, Action<object[]>> events = new Dictionary<int, Action<object[]>>();

        private Dictionary<string, int> string_hashs = new Dictionary<string, int>();
        private Dictionary<int, string> hash_strings = new Dictionary<int, string>();

        private Dictionary<int, Delegate> _events = new Dictionary<int, Delegate>();

        #endregion

        #region 属性

        private static Dictionary<int, Action<object[]>> Events => Instance.events;

        private static Dictionary<string, int> StringHashs => Instance.string_hashs;

        private static Dictionary<int, string> HashStrings => Instance.hash_strings;

        private static Dictionary<int, Delegate> _Event => Instance._events;

        #endregion



        /// <summary>
        /// 触发事件
        /// </summary>
        /// <param name="event_name_hash">事件名称</param>
        /// <param name="args">参数</param>
        [Obsolete("已过时,请使用同名的泛型方法代替!",true)]
        public static void TriggerEvent(int event_name_hash, params object[] args)
        {
            if (!Events.ContainsKey(event_name_hash)) return;
            Events[event_name_hash]?.Invoke(args);
        }
        [Obsolete("已过时,请使用同名的泛型方法代替!",true)]
        public static void TriggerEvent(string event_name, params object[] args)
        { 
        }

        /// <summary>
        /// 注册事件
        /// </summary>
        /// <param name="event_name_hash">事件名称</param>
        /// <param name="callback">回调</param>
        [Obsolete("已过时,请使用同名的泛型方法代替!",true)]
        public static void AddEvent(int event_name_hash, Action<object[]> callback)
        {

            if (Events.ContainsKey(event_name_hash))
            {
                Events[event_name_hash] += callback;
            }
            else
            {
                Action<object[]> action = null;
                action += callback;
                Events.Add(event_name_hash, action);
            }

        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="event_name_hash">事件名称</param>
        /// <param name="callback">回调</param>
        [Obsolete("已过时,请使用同名的泛型方法代替!",true)]
        public static void RemoveEvent(int event_name_hash, Action<object[]> callback)
        {

            if (!Events.ContainsKey(event_name_hash))
                return;

            Events[event_name_hash] -= callback;

            if (Events[event_name_hash] == null)
                Events.Remove(event_name_hash);

        }
        [Obsolete("已过时,请使用同名的泛型方法代替!", true)]
        public static void AddEvent(string event_name, Action<object[]> callback) {
            AddEvent(StringToHash(event_name), callback);
        }
        [Obsolete("已过时,请使用同名的泛型方法代替!",true)]
        public static void RemoveEvent(string event_name, Action<object[]> callback) {
            RemoveEvent(StringToHash(event_name), callback);
        }
 
        public static int StringToHash(string event_name)
        {
            if (StringHashs.ContainsKey(event_name))
                return StringHashs[event_name];

            int hash = Animator.StringToHash(event_name);

            StringHashs.Add(event_name, hash);
            if (!HashStrings.ContainsKey(hash))
                HashStrings.Add(hash, event_name);

            return StringHashs[event_name];
        }

        internal static string HashToString(int hash)
        {
            if (HashStrings.ContainsKey(hash))
                return HashStrings[hash];

            return string.Empty;
        }



        #region 事件系统

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent(int event_name_hash, XFEvent callback) 
        { 
            AddEvent(event_name_hash, callback as Delegate); 
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent(int event_name_hash, XFEvent callback)
        { 
            RemoveEvent(event_name_hash,callback as Delegate); 
        }

        public static void TriggerEvent(int event_name_hash )
        {
            if (!_Event.ContainsKey(event_name_hash)) 
                return;
             
            XFEvent d = _Event[event_name_hash] as XFEvent;
            d?.Invoke();

            if (d == null && _Event[event_name_hash] != null) 
            {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1>(int event_name_hash, XFEvent<T1> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1>(int event_name_hash, XFEvent<T1> callback)
        { 
            RemoveEvent(event_name_hash, callback as Delegate);
        }

        public static void TriggerEvent<T1>(int event_name_hash,T1 p)
        { 
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1> d = _Event[event_name_hash] as XFEvent<T1>;
            d?.Invoke(p);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
               
        }


        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1,T2>(int event_name_hash, XFEvent<T1, T2> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1,T2>(int event_name_hash, XFEvent<T1, T2> callback)
        { 
            RemoveEvent(event_name_hash, callback as Delegate); 
        }

        public static void TriggerEvent<T1,T2>(int event_name_hash, T1 p,T2 p2)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2> d = _Event[event_name_hash] as XFEvent<T1,T2>;
            d?.Invoke(p,p2);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2,T3>(int event_name_hash, XFEvent<T1, T2, T3> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3>(int event_name_hash, XFEvent<T1, T2, T3> callback)
        {
            RemoveEvent(event_name_hash, callback as Delegate); 
        }

        public static void TriggerEvent<T1, T2, T3>(int event_name_hash, T1 p, T2 p2,T3 p3)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2, T3> d = _Event[event_name_hash] as XFEvent<T1, T2, T3>;
            d?.Invoke(p, p2,p3);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2, T3>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3,T4>(int event_name_hash, XFEvent<T1, T2, T3, T4> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4>(int event_name_hash, XFEvent<T1, T2, T3, T4> callback)
        {
            RemoveEvent(event_name_hash, callback as Delegate);
        }

        public static void TriggerEvent<T1, T2, T3, T4>(int event_name_hash, T1 p, T2 p2, T3 p3,T4 p4)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2, T3, T4> d = _Event[event_name_hash] as XFEvent<T1, T2, T3, T4>;
            d?.Invoke(p, p2, p3,p4);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2, T3, T4>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }


        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4,T5>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5> callback)
        {
            RemoveEvent(event_name_hash, callback as Delegate); 
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4,T5 p5)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2, T3, T4, T5> d = _Event[event_name_hash] as XFEvent<T1, T2, T3, T4, T5>;
            d?.Invoke(p, p2, p3, p4,p5);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2, T3, T4, T5>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4, T5,T6>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5, T6> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5, T6> callback)
        {
            RemoveEvent(event_name_hash, callback as Delegate); 
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4, T5 p5,T6 p6)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2, T3, T4, T5, T6> d = _Event[event_name_hash] as XFEvent<T1, T2, T3, T4, T5, T6>;
            d?.Invoke(p, p2, p3, p4, p5,p6);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2, T3, T4, T5, T6>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4, T5, T6,T7>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            AddEvent(event_name_hash, callback as Delegate);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, XFEvent<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            RemoveEvent(event_name_hash, callback as Delegate); 
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6, T7>(int event_name_hash, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6,T7 p7)
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            XFEvent<T1, T2, T3, T4, T5, T6, T7> d = _Event[event_name_hash] as XFEvent<T1, T2, T3, T4, T5, T6, T7>;
            d?.Invoke(p, p2, p3, p4, p5, p6,p7);

            if (d == null && _Event[event_name_hash] != null) {
                Exception e = new Exception(string.Format("触发事件:{0}失败! 触发的委托类型和注册的类型不一致! \n\t触发类型:{1}\n\t注册类型:{2}\n触发类型和注册类型必须保持一致!", HashToString(event_name_hash), typeof(XFEvent<T1, T2, T3, T4, T5, T6, T7>).GetDelegateDisplayName(), _Event[event_name_hash].GetType().GetDelegateDisplayName()));
                Debug.LogException(e);
            }  
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent(string event_name, XFEvent callback)
        {
            AddEvent(StringToHash(event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent(string event_name, XFEvent callback)
        {
            RemoveEvent( StringToHash(event_name), callback);
        }

        public static void TriggerEvent(string event_name)
        {
            TriggerEvent(StringToHash(event_name));
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1>(string event_name, XFEvent<T1> callback)
        {
            AddEvent(StringToHash(event_name),callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1>(string event_name, XFEvent<T1> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1>(string event_name, T1 p)
        {
            TriggerEvent(StringToHash(event_name), p);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2>(string event_name, XFEvent<T1, T2> callback)
        {
            AddEvent(StringToHash(event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2>(string event_name, XFEvent<T1, T2> callback)
        { 
            RemoveEvent(StringToHash(event_name), callback); 
        }

        public static void TriggerEvent<T1, T2>(string event_name, T1 p, T2 p2)
        {
            TriggerEvent(StringToHash(event_name), p, p2);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3>(string event_name, XFEvent<T1, T2, T3> callback)
        {
            AddEvent(StringToHash( event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3>(string event_name, XFEvent<T1, T2, T3> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1, T2, T3>(string event_name, T1 p, T2 p2, T3 p3)
        {
            TriggerEvent(StringToHash( event_name ), p, p2, p3);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4>(string event_name, XFEvent<T1, T2, T3, T4> callback)
        {
            AddEvent(StringToHash(event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4>(string event_name, XFEvent<T1, T2, T3, T4> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1, T2, T3, T4>(string event_name, T1 p, T2 p2, T3 p3, T4 p4)
        {
            TriggerEvent(StringToHash(event_name),   p,   p2,   p3,   p4);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")] 
        public static void AddEvent<T1, T2, T3, T4, T5>(string event_name, XFEvent<T1, T2, T3, T4, T5> callback)
        {
            AddEvent(StringToHash(event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5>(string event_name, XFEvent<T1, T2, T3, T4, T5> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5)
        {
            TriggerEvent(StringToHash(event_name), p, p2, p3, p4, p5);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4, T5, T6>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6> callback)
        {
            AddEvent(StringToHash(event_name), callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6)
        {
            TriggerEvent(StringToHash(event_name),p,p2,p3, p4, p5, p6);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 方法代替!")]
        public static void AddEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            AddEvent(StringToHash(event_name),  callback);
        }

        [WarnInClasses(typeof(View), "推荐使用 View 中的 RegisterEvent 来注册事件,通过该方法注册事件会自动移除!")]
        public static void RemoveEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, XFEvent<T1, T2, T3, T4, T5, T6, T7> callback)
        {
            RemoveEvent(StringToHash(event_name), callback);
        }

        public static void TriggerEvent<T1, T2, T3, T4, T5, T6, T7>(string event_name, T1 p, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7)
        {
            TriggerEvent(StringToHash(event_name),   p,   p2,   p3,   p4,   p5,   p6,   p7);
        }


        /// <summary>
        /// 移除事件系统的所有监听
        /// </summary>
        public static void RemoveAllEvent() 
        { 
            List<int> keys = ClassObjectPoolManager.Load<List<int>>();
            keys.Clear();
             
            foreach (var item in _Event.Keys)
            {
                keys.Add(item);
            }

            foreach (var item in keys)
            {
                RemoveAllEvent(item);
            }
             
            ClassObjectPoolManager.Unload(keys);
        }

        /// <summary>
        /// 移除某一个事件的所有监听
        /// </summary>
        /// <param name="event_name"></param>
        public static void RemoveAllEvent(string event_name)
        {
            RemoveAllEvent(StringToHash(event_name));
        }

        public static void RemoveAllEvent(int event_name_hash) 
        {
            if (!_Event.ContainsKey(event_name_hash))
                return;

            _Event[event_name_hash] = Delegate.RemoveAll(_Event[event_name_hash], _Event[event_name_hash]);  
        }
 
        internal static void AddEvent(int event_name_hash, Delegate callback) 
        {
            if (callback == null) return;

            if (!_Event.ContainsKey(event_name_hash))
                _Event.Add(event_name_hash, null);

            if (_Event[event_name_hash] != null)
            {
                Type origin_type = _Event[event_name_hash].GetType();
                Type current_type = callback.GetType();

                if (origin_type == current_type)
                {
                    _Event[event_name_hash] = Delegate.Combine(_Event[event_name_hash], callback);
                }
                else
                { 
                    Exception e = new Exception(string.Format("注册事件:{0}失败! 委托类型不一致! \n\t注册类型:{1}\n\t当前类型:{2}\n同一个事件的回调方法类型必须保持一致!", HashToString(event_name_hash), current_type.GetDelegateDisplayName(), origin_type.GetDelegateDisplayName()));
                    Debug.LogException(e);
                }
            }
            else 
            {
                _Event[event_name_hash] = Delegate.Combine(_Event[event_name_hash], callback);
            }
             
        }
        
        internal static void RemoveEvent(int event_name_hash, Delegate callback)
        {

            if (callback == null) return;

            if (!_Event.ContainsKey(event_name_hash))
                return;

            if (_Event[event_name_hash] == null)
                return;

            Type origin_type = _Event[event_name_hash].GetType();
            Type current_type = callback.GetType();

            if (origin_type == current_type)
            {
                _Event[event_name_hash] = Delegate.Remove(_Event[event_name_hash], callback);
            }
            else 
            {
                Exception e = new Exception(string.Format("移除事件:{0}失败! 委托类型不一致! \n\t移除类型:{1}\n\t当前类型:{2}\n同一个事件的回调方法类型必须保持一致!", HashToString(event_name_hash), current_type.GetDelegateDisplayName(), origin_type.GetDelegateDisplayName()));
                //Exception e = new Exception(string.Format("移除事件:{0} 类型:{1} {2}不匹配!", HashToString(event_name_hash), callback.GetType().FullName, _Event[event_name_hash].GetType().FullName));
                Debug.LogException(e);
            } 
        }
         
        #endregion 

    }

}

