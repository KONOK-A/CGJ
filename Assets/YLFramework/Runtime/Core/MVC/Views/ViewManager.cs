using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Singletons;
using YLFramework.Events;
using CoroutineStarter = YLFramework.Async.CoroutineStarter;
using UpdateManager = YLFramework.Update.UpdateManager;

namespace YLFramework.Core
{

    internal partial class ViewManager
    {
        private static readonly HashSet<Module> _sceneOwners = new HashSet<Module>();
        private static readonly List<Module> _pruneOwners = new List<Module>();
        private static IUnregister _pruneSubscription;
        private static object _pruneRound;
        // 同步初始化调用栈的当前事务身份，仅用于区分 UI 自身准备失败与其他窗口观察异常。
        internal static KeyValuePair<View, object> InitializingView;

        static ViewManager() => SingletonSessionState.RegisterSessionReset(ResetSession);

        private static void ResetSession()
        {
            EndPruneRound();
            _sceneOwners.Clear();
            _pruneOwners.Clear();
            InitializingView = default;
        }

        private static void TrackScene(Module module, View view)
        {
            view.SceneHandle = view.gameObject.scene.handle;
            if (_sceneOwners.Count == 0)
            {
                SceneManager.sceneUnloaded += OnSceneUnloaded;
                var round = _pruneRound = new object();
                _pruneSubscription = UpdateManager.AddUpdateEvent(() =>
                {
                    if (ReferenceEquals(_pruneRound, round))
                    {
                        PruneDestroyedSessions(round);
                    }
                });
            }
            _sceneOwners.Add(module);
        }

        private static void OnSceneUnloaded(Scene scene)
        {
            var errors = new List<Exception>();
            foreach (var module in new List<Module>(_sceneOwners))
            {
                foreach (var entry in Snapshot(module))
                {
                    var view = entry.Key;
                    // 迁移及DDOL不改变owner；死对象只能使用保存的会话及实例身份。
                    if (view)
                    {
                        view.SceneHandle = view.gameObject.scene.handle;
                    }
                    else
                    {
                        try
                        {
                            RemoveView(module, view, entry.Value, true);
                        }
                        catch (Exception error)
                        {
                            errors.Add(error);
                        }
                    }
                }
            }

            if (errors.Count != 0)
            {
                Debug.LogException(new AggregateException($"View cleanup after scene unload ({scene.handle}) failed.", errors).Flatten());
            }
        }


    }
}
