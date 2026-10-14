using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameObjectLoader = XFABManager.GameObjectLoader;
using System.Runtime.ExceptionServices;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;
using UpdateManager = YLFramework.Foundation.UpdateManager;
using YLFramework.Events;
using YLFramework.Core.Singleton;

namespace YLFramework.Core
{

    internal partial class ViewManager
    {
        private static readonly HashSet<Module> sceneOwners = new HashSet<Module>();
        private static readonly List<Module> pruneOwners = new List<Module>();
        private static IUnRegister pruneSubscription;
        private static object pruneRound;
        // 同步初始化调用栈的当前事务身份，仅用于区分 UI 自身准备失败与其他窗口观察异常。
        internal static KeyValuePair<View, object> InitializingView;

        static ViewManager() => SingletonSessionState.RegisterSessionReset(ResetSession);

        private static void ResetSession()
        {
            EndPruneRound();
            sceneOwners.Clear();
            pruneOwners.Clear();
            InitializingView = default;
        }

        private static void TrackScene(Module module, View view)
        {
            view.SceneHandle = view.gameObject.scene.handle;
            if (sceneOwners.Count == 0)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                var round = pruneRound = new object();
                pruneSubscription = UpdateManager.AddUpdateEvent(() =>
                {
                    if (ReferenceEquals(pruneRound, round)) PruneDestroyedSessions(round);
                });
            }
            sceneOwners.Add(module);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            var errors = new List<Exception>();
            foreach (var module in new List<Module>(sceneOwners))
                foreach (var entry in Snapshot(module))
                {
                    var view = entry.Key;
                    // 迁移及DDOL不改变owner；死对象只能使用保存的会话及实例身份。
                    if (view) view.SceneHandle = view.gameObject.scene.handle;
                    else
                    {
                        try { RemoveView(module, view, entry.Value, true); }
                        catch (Exception error) { errors.Add(error); }
                    }
                }
            if (errors.Count != 0)
                Debug.LogException(new AggregateException($"View cleanup after scene unload ({scene.handle}) failed.", errors).Flatten());
        }


    }
}
