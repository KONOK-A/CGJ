using System; 
using System.Collections.Generic; 
using UnityEngine; 
using YLFramework.Foundation;

namespace YLFramework.Core
{
    public abstract partial class Module
    {
        #region 移除View

        /// <summary>
        /// 清空View
        /// </summary>
        public void ClearView()
        {
            ViewManager.ClearView(this);
        }

        /// <summary>
        /// 清空本模块指定类型及派生类型的View。
        /// </summary>
        public void ClearView<T>() where T : View
        {
            ViewManager.ClearView<T>(this);
        }

        /// <summary>
        /// 清空指定名称的View
        /// </summary>
        public void ClearView(string asset_name)
        {
            ViewManager.ClearView(this, asset_name);
        }

        #endregion

        #region 加载View

        /// <summary>纳管已有View，不实例化、不激活、不取得GameObject所有权。合法重复挂载不重复初始化；参数由本次调用接管。</summary>
        public T AttachView<T>(T view, LoadViewArgs loadViewArgs = null) where T : View
        {
            ViewManager.AttachView(this, view, loadViewArgs);
            return view;
        }

        /// <summary>纳管已有层级的所有View（包含inactive）。失败只撤销本次新增会话，不改变已有会话和对象激活状态。</summary>
        public List<View> AttachViews(GameObject root)
        {
            return ViewManager.AttachViews(this, root);
        }

        /// <summary>明确纳管指定已加载Scene中的View（包含inactive），不自动卸载场景；使用Scene身份而非名称。</summary>
        public List<View> AttachViews(UnityEngine.SceneManagement.Scene scene)
        {
            return ViewManager.AttachViews(this, scene);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">View预制体名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public View LoadView(string asset_name, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadView(this,asset_name, parent, param);
        }

        /// <summary>
        /// 加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(string asset_name, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewLegacy<T>(this, asset_name, parent, param);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView(GameObject prefab, Transform parent = null,LoadViewArgs loadViewArgs)代替!")]
        public View LoadView(GameObject prefab, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadView(this, prefab, parent, param);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(GameObject prefab, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(GameObject prefab, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewLegacy<T>(this, prefab, parent, param);
        }

        /// <summary>
        /// 加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadView<T>(Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadView<T>(Transform parent = null, params object[] param) where T : View
        {
            return LoadView<T>(typeof(T).Name, parent, param);
        }


        /// <summary>
        /// 异步加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<View> LoadViewAsync(string asset_name, Transform parent = null, params object[] param)
        {
            return ViewManager.LoadViewAsync<View>(this, asset_name, parent,true, param);
        }

        /// <summary>
        /// 异步加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="param"></param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync<T>(string asset_name, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<T> LoadViewAsync<T>(string asset_name, Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this ,asset_name, parent,true , param);
        }

        /// <summary>
        /// 异步加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadViewAsync<T>(Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadViewRequest<T> LoadViewAsync<T>(Transform parent = null, params object[] param) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this,typeof(T).Name, parent,true, param);
        }


        #endregion

        #region 加载View(With LoadViewArgs)

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">View预制体名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public View LoadView(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadView<View>(this, asset_name, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadView<T>(this,asset_name, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public View LoadView(GameObject prefab, Transform parent = null,LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadView<View>(this, prefab, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="prefab">View预制体</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(GameObject prefab, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadView<T>(this,prefab, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadView<T>(Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return LoadView<T>(typeof(T).Name, parent, loadViewArgs);
        }


        /// <summary>
        /// 异步加载View,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<View> LoadViewAsync(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return ViewManager.LoadViewAsync<View>(this, asset_name, parent, true, loadViewArgs);
        }

        /// <summary>
        /// 异步加载View(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<T> LoadViewAsync<T>(string asset_name, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this, asset_name, parent, true, loadViewArgs);
        }

        /// <summary>
        /// 异步加载View(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadViewRequest<T> LoadViewAsync<T>(Transform parent = null, LoadViewArgs loadViewArgs = null) where T : View
        {
            return ViewManager.LoadViewAsync<T>(this, typeof(T).Name, parent, true, loadViewArgs);
        }

        #endregion
        #region 查询View

        /// <summary>
        /// 仅查询本模块View或Panel；跨模块访问须显式调用其owner。
        /// </summary>
        /// <param name="assetName">资源名</param>
        /// <param name="id">View ID</param>
        /// <returns></returns>
        public View GetView( string assetName, int id)
        {
            return ViewManager.GetView(this,assetName,id);
        }

        /// <summary>
        /// 仅查询本模块View或Panel；结果类型不匹配时抛出异常。
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="assetName">资源名称</param>
        /// <param name="id">View ID</param>
        /// <returns></returns>
        public T GetView<T>(string assetName, int id) where T : View
        {
            return ViewManager.GetView<T>(this, assetName, id);
        }

        /// <summary>
        /// 按实例ID查询本模块View或Panel，不要求脚本名与资源名一致。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetView<T>(int id) where T : View
        {
            return ViewManager.GetView<T>(this, id);
        }

        /// <summary>
        /// 按资源名查询本模块唯一View或Panel；多个实例时须指定ID。
        /// </summary>
        /// <param name="assetName">资源名</param> 
        /// <returns></returns>
        public View GetView(string assetName )
        {
            return ViewManager.GetView(this, assetName );
        }

        /// <summary>
        /// 按资源名查询本模块唯一View或Panel；结果类型不匹配时抛出异常。
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="assetName">资源名称</param> 
        /// <returns></returns>
        public T GetView<T>(string assetName ) where T : View
        {
            return ViewManager.GetView<T>(this, assetName );
        }

        /// <summary>
        /// 按实际类型查询本模块唯一View或Panel；多个实例时须指定ID。
        /// </summary>
        /// <typeparam name="T"></typeparam> 
        /// <returns></returns>
        public T GetView<T>() where T : View
        {
            return ViewManager.GetView<T>(this);
        }

        /// <summary>
        /// 查询本模块某个资源名的所有View或Panel，返回独立结果集合。
        /// </summary>
        /// <param name="assetName">预制体资源名称</param>
        /// <returns></returns>
        public List<View> GetViews(string assetName)
        {
            return ViewManager.GetViews(this, assetName);
        }

        /// <summary>
        /// 查询本模块某个资源名的所有可赋给T的View或Panel。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public List<T> GetViews<T>( string assetName) where T : View
        {
            return ViewManager.GetViews<T>(this,assetName);
        }

        /// <summary>
        /// 查询本模块指定类型及派生类型的所有View或Panel。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetViews<T>() where T : View
        {
            return ViewManager.GetViews<T>(this);
        }


        public void GetViewsNonAlloc<T>(List<T> results) where T : View
        {
            ViewManager.GetViewsNonAlloc(this,results);
        }

        #endregion
    }
}
