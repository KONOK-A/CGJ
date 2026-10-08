using System.Collections.Generic;
using UnityEngine;
using YLFramework.Utilities.Extensions;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 挂载对象销毁时，自动回收其关联的 ResLoader。
    /// </summary>
    public class ResLoaderOnDestroyRecycler : MonoBehaviour
    {
        private HashSet<ResLoader> _resLoaders = new HashSet<ResLoader>();

        /// <summary>登记一个需要随本对象销毁而回收的 Loader；重复登记同一 Loader 只保留一份。</summary>
        public void AddResLoader(ResLoader loader)
        {
            _resLoaders.Add(loader);
        }

        private void OnDestroy()
        {
            foreach (var resLoader in _resLoaders)
            {
                resLoader.RecycleToCache();
            }

            _resLoaders.Clear();
            _resLoaders = null;
        }
    }

    public static class ResLoaderOnDestroyRecyclerExtensions
    {
        /// <summary>
        /// 当指定组件所在对象销毁时，自动回收该 ResLoader
        /// </summary>
        public static ResLoader RecycleWhenGameObjectDestroyed(this ResLoader self, Component component)
        {
            var recycler = component.GetOrAddComponent<ResLoaderOnDestroyRecycler>();
            recycler.AddResLoader(self);
            return self;
        }

        /// <summary>
        /// 当指定对象销毁时，自动回收该 ResLoader
        /// </summary>
        public static ResLoader RecycleWhenGameObjectDestroyed(this ResLoader self, GameObject gameObj)
        {
            var recycler = gameObj.GetOrAddComponent<ResLoaderOnDestroyRecycler>();
            recycler.AddResLoader(self);
            return self;
        }
    }
}
