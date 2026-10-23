using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Foundation;

namespace YLFramework.Core
{
    public abstract partial class Module
    {
        #region Model
        /// <summary>
        /// 注册本地Model；已完成注册的同实例重复调用幂等，键冲突或跨模块注册抛出异常。
        /// </summary>
        /// <param name="model">数据对象</param>
        public void AddModel(Model model)
        {
            ModelManager.AddModel(this, model);
        }


        /// <summary>
        /// 仅移除本模块拥有且与索引完全一致的实例；退出异常也会清除索引及owner。
        /// </summary>
        /// <param name="model"></param>
        public void RemoveModel(Model model)
        {
            ModelManager.RemoveModel(this, model);
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型数据，不创建；缺失返回null，多实例抛出异常。
        /// 跨模块共享须在明确的拥有者Module上调用查询，不改变归属或生命周期责任。
        /// </summary>
        /// <param name="modelType">数据名</param>
        /// <returns></returns>
        public Model GetModel(Type modelType)
        {
            return ModelManager.GetModel(this, modelType);
        }

        /// <summary>
        /// 只按精确类型和id查询本模块已注册的数据，不创建；缺失返回null。
        /// </summary>
        /// <param name="modelType"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public Model GetModel(Type modelType, long id)
        {
            return ModelManager.GetModel(this, modelType, id);
        }

        /// <summary>
        /// 查询本模块该类型及其派生类型的所有数据，不创建；缺失返回空集合。
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
        /// 只按精确类型和id查询本模块已注册的数据，不创建；缺失返回null。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetModel<T>(long id) where T : Model
        {
            return GetModel(typeof(T), id) as T;
        }

        /// <summary>
        /// 只查询本模块已注册的精确类型数据，不创建；缺失返回null，多实例抛出异常。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModel<T>() where T : Model
        {
            return GetModel(typeof(T)) as T;
        }

        /// <summary>
        /// 查询本模块该类型及其派生类型的所有数据，不创建；缺失返回空集合。
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
        /// 只查询本模块数据，缺失时才创建并注册到本模块；多实例抛出异常。
        /// 不使用对象池；初始化期间重入Load返回临时注册的自身，退出期间拒绝Load。
        /// </summary>
        /// <param name="type">数据类型</param>
        /// <returns></returns>
        public Model LoadModel(Type type)
        {
            return ModelManager.LoadModel(this, type);
        }

        /// <summary>
        /// 加载数据(适用于只有一个数据实例的情况)
        /// 只查询本模块数据，缺失时才创建并注册到本模块；多实例抛出异常。
        /// 不使用对象池；初始化期间重入Load返回临时注册的自身，退出期间拒绝Load。
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns></returns>
        public T LoadModel<T>() where T : Model
        {
            return ModelManager.LoadModel<T>(this);
        }

        #endregion
    }
}
