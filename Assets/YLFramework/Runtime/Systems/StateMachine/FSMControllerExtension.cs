using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.StateMachine
{

    public class FSMControllerExtension
    {
        #region 单例

        private static FSMControllerExtension _instance;
        private static bool quitting;

        internal static FSMControllerExtension Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new FSMControllerExtension();
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _instance = null;
            string_hashs.Clear();
            hash_strings.Clear();
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => quitting = true;

        private FSMControllerExtension()
        {
        }

        #endregion


        #region 静态字段

        private Dictionary<string, FSMController> fsms = new Dictionary<string, FSMController>();

        private Transform fsmParent;

        #endregion

        #region 静态属性


        private static Transform FSMParent
        {
            get
            {
                if (!Instance.fsmParent && YLFramework.Foundation.ApplicationTool.isPlaying)
                {
                    GameObject obj = new GameObject("YL Asset FSM Controllers");
                    GameObject.DontDestroyOnLoad(obj);
                    Instance.fsmParent = obj.transform;
                }

                return Instance.fsmParent;
            }
        }

        #endregion

        #region 静态方法

        /// <summary>
        /// 启动状态(适用于没有具体游戏物体的状态管理,例如:游戏状态)
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <param name="fsm">状态配置文件</param>
        /// <param name="userData">自定义参数或数据</param>
        /// <returns></returns>
        public static FSMController StartupFSM(string fsmName, RuntimeFSMController fsm, object userData = null)
        {
            if (quitting || !Application.isPlaying || !YLFramework.Foundation.ApplicationTool.isPlaying) return null;
            if (!fsm || string.IsNullOrWhiteSpace(fsmName)) return null;

            if (GetFSM(fsmName)) return null;

            GameObject obj = new GameObject(fsmName);
            obj.transform.SetParent(FSMParent, false);
            FSMController controller = obj.AddComponent<FSMController>();
            controller.userData = userData;
            controller.AddRuntimeFSMController(fsm);
            Instance.fsms.Add(fsmName, controller);
            return controller;
        }

        /// <summary>
        /// 查询通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <returns></returns>
        public static FSMController GetFSM(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName)) return null;
            if (Instance.fsms.TryGetValue(fsmName, out var controller))
            {
                if (controller) return controller;
                Instance.fsms.Remove(fsmName);
            }
            return null;
        }

        /// <summary>
        /// 移除通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        public static void RemoveFSM(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName)) return;
            if (!Instance.fsms.ContainsKey(fsmName)) return;
            FSMController controller = Instance.fsms[fsmName];
            Instance.fsms.Remove(fsmName);
            if (controller) GameObject.Destroy(controller.gameObject);
        }

        internal static void Unregister(FSMController controller)
        {
            if (_instance == null) return;
            string key = null;
            foreach (var pair in _instance.fsms)
                if (ReferenceEquals(pair.Value, controller)) { key = pair.Key; break; }
            if (key != null) _instance.fsms.Remove(key);
        }

        #endregion


        #region StringToHash

        private static Dictionary<string, int> string_hashs = new Dictionary<string, int>();
        private static Dictionary<int, string> hash_strings = new Dictionary<int, string>();


        public static int StringToHash(string event_name)
        {
            if (string_hashs.ContainsKey(event_name))
                return string_hashs[event_name];

            int hash = Animator.StringToHash(event_name);

            string_hashs.Add(event_name, hash);
            if (!hash_strings.ContainsKey(hash))
                hash_strings.Add(hash, event_name);

            return string_hashs[event_name];
        }

        internal static string HashToString(int hash)
        {
            if (hash_strings.ContainsKey(hash))
                return hash_strings[hash];

            return string.Empty;
        }

        #endregion

    }
}
