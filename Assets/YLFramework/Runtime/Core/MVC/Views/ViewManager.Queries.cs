using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using YLFramework.Core.Singleton;
using YLFramework.Events;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;
using UpdateManager = YLFramework.Foundation.UpdateManager;

namespace YLFramework.Core
{

    internal partial class ViewManager
    {
        #region 查询

        internal static View GetView(Module module, string assetName, int id)
        {
            if (assetName == null)
            {
                throw new ArgumentNullException(nameof(assetName));
            }

            PruneDestroyed(module);
            return module.Views.TryGetValue(assetName, out var named) && named.TryGetValue(id, out var view) ? view : null;
        }

        internal static T GetView<T>(Module module, string assetName, int id) where T : View
        {
            return Cast<T>(GetView(module, assetName, id));
        }

        internal static T GetView<T>(Module module, int id) where T : View
        {
            PruneDestroyed(module);
            return module.OwnedViews.TryGetValue(id, out var view) ? Cast<T>(view) : null;
        }

        private static T Cast<T>(View view) where T : View
        {
            if (view == null)
            {
                return null;
            }

            if (view is T typed)
            {
                return typed;
            }

            throw new InvalidCastException($"View {view.GetType().FullName} cannot be queried as {typeof(T).FullName}.");
        }

        private static View Single(IEnumerable<View> views)
        {
            View result = null;
            foreach (var view in views)
            {
                if (result != null)
                {
                    throw new InvalidOperationException("Multiple Views match; specify an ID.");
                }

                result = view;
            }
            return result;
        }


        internal static View GetView(Module module, string assetName)
        {
            if (assetName == null)
            {
                throw new ArgumentNullException(nameof(assetName));
            }

            PruneDestroyed(module);
            return module.Views.TryGetValue(assetName, out var named) ? Single(named.Values) : null;
        }

        internal static T GetView<T>(Module module, string assetName) where T : View
        {
            return Cast<T>(GetView(module, assetName));
        }


        internal static T GetView<T>(Module module) where T : View
        {
            PruneDestroyed(module);
            return module.TypeViews.TryGetValue(typeof(T), out var typed) ? (T)Single(typed) : null;
        }

        internal static List<View> GetViews(Module module, string assetName)
        {
            if (assetName == null)
            {
                throw new ArgumentNullException(nameof(assetName));
            }

            PruneDestroyed(module);
            return module.Views.TryGetValue(assetName, out var named) ? new List<View>(named.Values) : new List<View>();
        }

        internal static List<T> GetViews<T>(Module module, string assetName) where T : View
        {
            var views = new List<T>();
            foreach (var view in GetViews(module, assetName))
            {
                if (view is T typed)
                {
                    views.Add(typed);
                }
            }

            return views;
        }


        internal static List<T> GetViews<T>(Module module) where T : View
        {
            List<T> views = new List<T>();
            GetViewsNonAlloc<T>(module, views);
            return views;
        }


        internal static void GetViewsNonAlloc<T>(Module module, List<T> results) where T : View
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            PruneDestroyed(module);
            results.Clear();
            foreach (var view in module.OwnedViews.Values)
            {
                if (view is T typed)
                {
                    results.Add(typed);
                }
            }
        }

        #endregion

    }
}
