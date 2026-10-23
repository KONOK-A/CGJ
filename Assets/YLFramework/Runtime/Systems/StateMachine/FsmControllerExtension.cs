using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.StateMachine
{

    public class FsmControllerExtension
    {
        #region 单例

        private static FsmControllerExtension _instance;
        private static bool _isQuitting;

        internal static FsmControllerExtension Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new FsmControllerExtension();
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _instance = null;
            _stringHashes.Clear();
            _hashStrings.Clear();
            _isQuitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting() => _isQuitting = true;

        private FsmControllerExtension()
        {
        }

        #endregion


        #region 静态字段

        private Dictionary<string, FsmController> _fsms = new Dictionary<string, FsmController>();

        private Transform _fsmParent;

        #endregion

        #region 静态属性


        private static Transform FsmParent
        {
            get
            {
                if (!Instance._fsmParent && YLFramework.Foundation.ApplicationTool.IsPlaying)
                {
                    GameObject obj = new GameObject("YL Asset FSM Controllers");
                    GameObject.DontDestroyOnLoad(obj);
                    Instance._fsmParent = obj.transform;
                }

                return Instance._fsmParent;
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
        public static FsmController StartupFsm(string fsmName, RuntimeFsmController fsm, object userData = null)
        {
            if (_isQuitting || !Application.isPlaying || !YLFramework.Foundation.ApplicationTool.IsPlaying)
            {
                return null;
            }

            if (!fsm || string.IsNullOrWhiteSpace(fsmName))
            {
                return null;
            }

            if (GetFsm(fsmName))
            {
                return null;
            }

            GameObject obj = new GameObject(fsmName);
            obj.transform.SetParent(FsmParent, false);
            FsmController controller = obj.AddComponent<FsmController>();
            controller.UserDataValue = userData;
            controller.AddRuntimeFsmController(fsm);
            Instance._fsms.Add(fsmName, controller);
            return controller;
        }

        /// <summary>
        /// 查询通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        /// <returns></returns>
        public static FsmController GetFsm(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName))
            {
                return null;
            }

            if (Instance._fsms.TryGetValue(fsmName, out var controller))
            {
                if (controller)
                {
                    return controller;
                }

                Instance._fsms.Remove(fsmName);
            }
            return null;
        }

        /// <summary>
        /// 移除通过FSMController.StartupFSM启动的状态机
        /// </summary>
        /// <param name="fsmName">状态机名称</param>
        public static void RemoveFsm(string fsmName)
        {
            if (string.IsNullOrEmpty(fsmName))
            {
                return;
            }

            if (!Instance._fsms.ContainsKey(fsmName))
            {
                return;
            }

            FsmController controller = Instance._fsms[fsmName];
            Instance._fsms.Remove(fsmName);
            if (controller)
            {
                GameObject.Destroy(controller.gameObject);
            }
        }

        internal static void Unregister(FsmController controller)
        {
            if (_instance == null)
            {
                return;
            }

            string key = null;
            foreach (var pair in _instance._fsms)
            {
                if (ReferenceEquals(pair.Value, controller))
                {
                    key = pair.Key;
                    break;
                }
            }

            if (key != null)
            {
                _instance._fsms.Remove(key);
            }
        }

        #endregion


        #region StringToHash

        private static Dictionary<string, int> _stringHashes = new Dictionary<string, int>();
        private static Dictionary<int, string> _hashStrings = new Dictionary<int, string>();


        public static int StringToHash(string eventName)
        {
            if (_stringHashes.ContainsKey(eventName))
            {
                return _stringHashes[eventName];
            }

            int hash = Animator.StringToHash(eventName);

            _stringHashes.Add(eventName, hash);
            if (!_hashStrings.ContainsKey(hash))
            {
                _hashStrings.Add(hash, eventName);
            }

            return _stringHashes[eventName];
        }

        internal static string HashToString(int hash)
        {
            if (_hashStrings.ContainsKey(hash))
            {
                return _hashStrings[hash];
            }

            return string.Empty;
        }

        #endregion

    }
}
