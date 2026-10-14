using System; 
using System.Collections.Generic; 
using UnityEngine; 
using YLFramework.Foundation;

namespace YLFramework.Core
{
    public abstract partial class Module
    {
        #region 加载Panel

        /// <summary>
        /// 加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel(string asset_name, UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public Panel LoadPanel(string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param)
        {
            return PanelManager.LoadPanel(this,asset_name, type, parent, param);
        }

        /// <summary>
        /// 加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel<T>(string asset_name, UIType type,Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public T LoadPanel<T>(  string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelLegacy<T>(this, asset_name, type, parent, param);
        }



        /// <summary>
        /// 加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanel<T>(UIType type, Transform parent ,LoadViewArgs loadViewArgs)代替!")]
        public T LoadPanel<T>(UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelLegacy<T>(this, typeof(T).Name, type, parent, param);
        }

        /// <summary>
        /// 异步加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="uiType">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync(string asset_name, UIType uiType, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<Panel> LoadPanelAsync(string asset_name, UIType uiType = UIType.UI, Transform parent = null, params object[] param)
        {
            return PanelManager.LoadPanelAsync<Panel>(this, asset_name, uiType, parent, param);
        }

        /// <summary>
        /// 异步加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="param">参数</param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync<T>( string asset_name,UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<T> LoadPanelAsync<T>( string asset_name, UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this,asset_name, type, parent, param);
        }

        /// <summary>
        /// 异步加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        [Obsolete("已过时,请使用重载函数:LoadPanelAsync<T>(UIType type, Transform parent,LoadViewArgs loadViewArgs)代替!")]
        public LoadPanelRequest<T> LoadPanelAsync<T>(UIType type = UIType.UI, Transform parent = null, params object[] param) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, typeof(T).Name, type, parent, param);
        }


        #endregion


        #region 加载Panel(With LoadViewArgs)

        /// <summary>
        /// 加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public Panel LoadPanel(string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null)
        {
            return PanelManager.LoadPanel<Panel>(this, asset_name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadPanel<T>(string asset_name, UIType type = UIType.UI, Transform parent = null,LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanel<T>(this,asset_name, type, parent, loadViewArgs);
        }



        /// <summary>
        /// 加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public T LoadPanel<T>(UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return LoadPanel<T>(typeof(T).Name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="asset_name">资源名称</param>
        /// <param name="uiType">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<Panel> LoadPanelAsync(string asset_name, UIType uiType = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null)
        {
            return PanelManager.LoadPanelAsync<Panel>(this, asset_name, uiType, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T">Panel类型</typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent">父节点</param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<T> LoadPanelAsync<T>(string asset_name, UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, asset_name, type, parent, loadViewArgs);
        }

        /// <summary>
        /// 异步加载UI(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="asset_name">资源名称</param>
        /// <param name="type">UI类型,"UI"普通UI，"DontDestroyUI"切换场景时不会销毁的UI</param>
        /// <param name="parent"></param>
        /// <param name="loadViewArgs">参数(使用LoadViewArgs.New() 来创建参数对象)</param>
        /// <returns></returns>
        public LoadPanelRequest<T> LoadPanelAsync<T>(UIType type = UIType.UI, Transform parent = null, LoadViewArgs loadViewArgs = null) where T : Panel
        {
            return PanelManager.LoadPanelAsync<T>(this, typeof(T).Name, type, parent, loadViewArgs);
        }

        #endregion
    }
}
