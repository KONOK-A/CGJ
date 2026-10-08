// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Module/Module.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic; 
using UnityEngine; 

namespace YLFramework.Core
{
      
    // 模块
    public abstract class Module
    {

        #region 字段
        /// <summary>
        /// 存放 所有的数据
        /// </summary>
        internal Dictionary<Type, Dictionary<long, Model>> models = new Dictionary<Type, Dictionary<long, Model>>();
        /// <summary>
        /// 存放所有的视图(key:string value:Dictionary<int, View>) 使用名称 和 id 查询比较方便
        /// </summary>
        internal Dictionary<string, Dictionary<int, View>> views = new Dictionary<string, Dictionary<int, View>>();

        /// <summary>
        /// 存放所有的视图(key:type value:List<View>) 使用类型查询比较方便
        /// </summary>
        internal Dictionary<Type,List<View>> type_views = new Dictionary<Type, List<View>>();
        
        /// <summary>
        /// 存放 所有的业务逻辑
        /// </summary>
        internal Dictionary<Type, Controller> controllers = new Dictionary<Type, Controller>();


        private int ui_start_sort_order = 0; // UI 初始的SortingOrder

        #endregion

        #region 属性
        /// <summary>
        /// 模块名
        /// </summary>
        public string ModuleName => GetType().FullName;

        /// <summary>
        /// 当前模块使用的资源模块名
        /// </summary>
        public virtual string ProjectName { get; set; }

       
        /// <summary>
        /// 是否是基础模块
        /// 基础模块启动之后不能关闭 默认false
        /// </summary>
        public virtual bool BaseModule => false;

        /// <summary>
        /// 需要预加载的配表
        /// </summary>

        [Obsolete("已过时!")]
        public virtual Dictionary<string, Type> PreloadConfigTables => null;

        /// <summary>
        /// 默认启动的场景(在启动模块时自动加载的场景名称)
        /// </summary>
        public virtual string DefaultStartUpScene => string.Empty;

        /// <summary>
        /// 当前模块中的UI的初始层级, UI的实际层级 = 初始层级 + UI本身层级 
        /// </summary>
        public int UIStartSortOrder 
        {
            get 
            {
                return ui_start_sort_order;
            }

            set 
            {
                ui_start_sort_order = value;

                foreach (var view in views.Values)
                {
                    foreach (var item in view.Values)
                    {
                        if (item is Panel) 
                        {
                            Panel panel = item as Panel;
                            panel.RefreshSortingOrder(); // 刷新层级
                        }
                    }
                }

            }

        }

        #endregion
         
        #region Model
        /// <summary>
        /// 添加数据
        /// </summary>
        /// <param name="model">数据对象</param>
        public void AddModel(Model model)
        {
            ModelManager.AddModel(this, model);
        }


        /// <summary>
        /// 移除数据
        /// </summary>
        /// <param name="model"></param>
        public void RemoveModel(Model model)
        {
            ModelManager.RemoveModel(this, model);
        }

        /// <summary>
        /// 根据数据类型来查询数据，该重载方法不需要指定Id,但是该类型的数据必须只有一条,如果有多条数据会查询失败,
        /// 从当前模块和基础模块中查询
        /// </summary>
        /// <param name="modelType">数据名</param>
        /// <returns></returns>
        public Model GetModel(Type modelType)
        {
            return ModelManager.GetModel(this, modelType);
        }

        /// <summary>
        /// 根据数据名和id来查询数据,从当前模块和基础模块中查询
        /// </summary>
        /// <param name="modelType"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public Model GetModel(Type modelType, int id)
        {
            return ModelManager.GetModel(this, modelType, id);
        }

        /// <summary>
        /// 查询某一类型的所有数据,从当前模块和基础模块中查询
        /// </summary>
        /// <param name="modelType">数据类型</param>
        /// <returns></returns>
        public List<Model> GetModels(Type modelType)
        {
            return ModelManager.GetModels(this, modelType);
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void ClearModel()
        {
            ModelManager.ClearModel(this);
        }

        /// <summary>
        /// 清空某个类型数据
        /// </summary>
        /// <param name="modelType">数据名称</param>
        public void ClearModel(Type modelType)
        {
            ModelManager.ClearModel(this, modelType);
        }

        /// <summary>
        /// 清空某一个类型数据(泛型方式)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void ClearModel<T>() where T : Model
        {
            ClearModel(typeof(T));
        }

        /// <summary>
        /// 根据泛型和id来查询数据,从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetModel<T>(int id) where T : Model
        {
            return GetModel(typeof(T), id) as T;
        }

        /// <summary>
        /// 根据泛型来查询数据,该重载方法不需要指定Id,但是该类型的数据必须只有一条,如果有多条数据会查询失败,
        /// 从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModel<T>() where T : Model
        {
            return GetModel(typeof(T)) as T;
        }

        /// <summary>
        /// 根据泛型来查询所有数据,从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public List<T> GetModels<T>() where T : Model
        {

            List<Model> models = GetModels(typeof(T));

            List<T> list = new List<T>(models.Count);

            foreach (var item in models)
            {
                list.Add(item as T);
            }
            return list;
        }

        /// <summary>
        /// 加载数据(适用于只有一个数据实例的情况)
        /// 先查询数据，如果没有查询到则创建并添加数据，如果查询到了直接返回!
        /// *注:此种方式是通过对象池创建，可放心清空或移除，不会产生多余对象 
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public Model LoadModel(Type type) 
        {
            return ModelManager.LoadModel(this, type);
        }

        /// <summary>
        /// 加载数据(适用于只有一个数据实例的情况)
        /// 先查询数据，如果没有查询到则创建并添加数据，如果查询到了直接返回!
        /// *注:此种方式是通过对象池创建，可放心清空或移除，不会产生多余对象 
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public T LoadModel<T>() where T : Model {
            return ModelManager.LoadModel<T>(this);
        }

        #endregion

        #region 移除View

        /// <summary>
        /// 清空View
        /// </summary>
        public void ClearView()
        {
            ViewManager.ClearView(this);
        }

        /// <summary>
        /// 清空指定类型的View(类型名需要与资源名称一致)
        /// </summary>
        /// <param name="clear_base_module">是否清空基础模块中的View</param>
        public void ClearView<T>(bool clear_base_module = false) where T : View
        {
            ViewManager.ClearView<T>(this,clear_base_module);
        }

        /// <summary>
        /// 清空指定名称的View
        /// </summary>
        /// <param name="clear_base_module">是否清空基础模块中的View</param>
        public void ClearView(string asset_name, bool clear_base_module = false)
        {
            ViewManager.ClearView(this, asset_name, clear_base_module);
        }

        #endregion

        #region 加载View

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
            return LoadView(asset_name, parent, param) as T;
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
            return LoadView(prefab, parent, param) as T;
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
            return LoadPanel( asset_name, type, parent, param) as T;
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
            return LoadPanel(  typeof(T).Name, type, parent, param) as T;
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


        #region 查询View

        /// <summary>
        /// 查询View或Panel,从当前模块和基础模块中查询
        /// </summary>
        /// <param name="assetName">资源名</param>
        /// <param name="id">View ID</param>
        /// <returns></returns>
        public View GetView( string assetName, int id)
        {
            return ViewManager.GetView(this,assetName,id);
        }

        /// <summary>
        /// 查询View或Panel(泛型方式),从当前模块和基础模块中查询
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
        /// 查询View或Panel(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetView<T>(int id) where T : View
        {
            return ViewManager.GetView<T>(this, typeof(T).Name, id);
        }

        /// <summary>
        /// 查询View或Panel,从当前模块和基础模块中查询
        /// </summary>
        /// <param name="assetName">资源名</param> 
        /// <returns></returns>
        public View GetView(string assetName )
        {
            return ViewManager.GetView(this, assetName );
        }

        /// <summary>
        /// 查询View或Panel(泛型方式),从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T">View类型</typeparam>
        /// <param name="assetName">资源名称</param> 
        /// <returns></returns>
        public T GetView<T>(string assetName ) where T : View
        {
            return ViewManager.GetView<T>(this, assetName );
        }

        /// <summary>
        /// 查询View或Panel(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam> 
        /// <returns></returns>
        public T GetView<T>() where T : View
        {
            return ViewManager.GetView<T>(this);
        }

        /// <summary>
        /// 查询某个名称的所有View或Panel,从当前模块和基础模块中查询 
        /// </summary>
        /// <param name="assetName">预制体资源名称</param>
        /// <returns></returns>
        public List<View> GetViews(string assetName)
        {
            return ViewManager.GetViews(this, assetName);
        }

        /// <summary>
        /// 查询某个名称的所有View或Panel(泛型方式),从当前模块和基础模块中查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="assetName"></param>
        /// <returns></returns>
        public List<T> GetViews<T>( string assetName) where T : View
        {
            return ViewManager.GetViews<T>(this,assetName);
        }

        /// <summary>
        /// 查询某个名称的所有View或Panel(泛型方式且无参数asset_name,但是需要脚本名称与预制体名称一致)
        /// 从当前模块和基础模块中查询
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
          
        #region Controller

        /// <summary>
        /// 加载业务逻辑(反射方式),从当前模块和基础模块中加载
        /// </summary>
        /// <param name="controllerTypeFullName">业务逻辑脚本名称(包含命名空间)</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Controller LoadController(string controllerTypeFullName) 
        {
            if (string.IsNullOrEmpty(controllerTypeFullName)) return null;
            Type type = AssemblyTools.GetType(controllerTypeFullName);
            if (type == null) throw new Exception(string.Format("未查询到类:{0}!", controllerTypeFullName));
            return LoadController(type);
        }

        /// <summary>
        /// 移除业务逻辑
        /// </summary>
        /// <param name="controllerType">业务逻辑类型</param>
        public void RemoveController(Type controllerType)
        {
            ControllerManager.RemoveController(this, controllerType);
        }

        /// <summary>
        /// 加载业务逻辑,从当前模块和基础模块中加载
        /// </summary>
        /// <param name="controllerType">业务逻辑类型</param>
        /// <param name="load_from_base_modules">是否从基础模块加载，为true时,如果基础模块中已有该控制器，直接返回基础模块中的实例</param>
        /// <returns></returns>
        public Controller LoadController(Type controllerType, bool load_from_base_modules = true)
        {
            return ControllerManager.LoadController(this, controllerType, load_from_base_modules);
        }

        /// <summary>
        /// 清空当前模块的所有Controller
        /// </summary>
        public void ClearController()
        {
            ControllerManager.ClearController(this);
        }

        /// <summary>
        /// 加载业务逻辑(泛型方式),从当前模块和基础模块中加载
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="load_from_base_modules">是否从基础模块加载，为true时,如果基础模块中已有该控制器，直接返回基础模块中的实例</param>
        /// <returns></returns>
        public T LoadController<T>(bool load_from_base_modules = true) where T : Controller
        {
            return ControllerManager.LoadController<T>(this, load_from_base_modules);
        }

        /// <summary>
        /// 移除业务逻辑(泛型方式)
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        public void RemoveController<T>() where T : Controller
        {
            RemoveController(typeof(T));
        }


        public T CreateController<T>()  where T: Controller
        {
            return ControllerManager.CreateController<T>(this);
        }

        public Controller CreateController(Type type) 
        {
            return ControllerManager.CreateController(this, type);
        }

        public Controller CreateController(string controllerTypeFullName) {
            if (string.IsNullOrEmpty(controllerTypeFullName)) return null;
            Type type = AssemblyTools.GetType(controllerTypeFullName);
            if (type == null) throw new Exception(string.Format("未查询到类:{0}!", controllerTypeFullName));
            return CreateController(type);
        }

        #endregion

        #region 方法

        /// <summary>
        /// 模块启动完成时触发
        /// </summary>
        public abstract void OnStart();

        /// <summary>
        /// 关闭模块时触发
        /// </summary>
        [Obsolete("已过时,请使用OnClose代替!")]
        public virtual void OnExit()
        {
            if (BaseModule) return;

            // 清空对象
            this.ClearModel();
            this.ClearView();
            this.ClearController();
        }


        internal void Exit()
        {

            if (BaseModule) throw new Exception("基础模块不能关闭!");
             
            // 清空对象
            this.ClearModel();
            this.ClearView();
            this.ClearController();


            OnClose();
        }

        /// <summary>
        /// 关闭模块时触发
        /// </summary>
        protected virtual void OnClose() { }

        /// <summary>
        /// 关闭模块
        /// </summary>
        public void Close() 
        {
            ModuleManager.CloseModule(GetType().FullName);
        }

#endregion

    }
}