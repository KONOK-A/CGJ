// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ModelManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic; 
using UnityEngine;


namespace YLFramework.Core
{

    internal class ModelManager
    {  
        // 添加
        public static void AddModel(Module module, Model model)
        { 
            if (model == null) return;
              
            //string Name = model.ModelName;
            // 说明没有指定id , 这里使用它的hashcode作为id
            // 如果希望通过id查询，请手动指定id
            if (model.Id == -1)
                model.Id = model.GetHashCode();

            Type type = model.GetType();

            if (!module.models.ContainsKey(type))
            {
                module.models.Add(type, new Dictionary<long, Model>());
            }

            if (!module.models[type].ContainsKey(model.Id))
            {
                model.Module = module;
                module.models[type].Add(model.Id, model);
                model.OnInit();
            }
            else
            {
                Debug.LogWarningFormat("model name:{0} id:{1} 已经存在,请勿重复添加!", type.FullName, model.Id);
            }
        }

        // 移除
        public static void RemoveModel(Module module, Model model)
        {
            if(model == null) return;
            Type type = model.GetType();
            if (module.models.ContainsKey(type) && module.models[type].ContainsKey(model.Id))
            {
                model.OnExit();
                //model.Module = null;
                module.models[type].Remove(model.Id);
                if (module.models[type].Count == 0)
                    module.models.Remove(type);


                ClassObjectPoolManager.Unload(model);
            }
            else
                Debug.LogWarningFormat("移除 model name:{0} id:{1} 失败,未查询到 或 已经移除!", type, model.Id);

        }

        public static Model GetModel(Module module, Type modelType)
        {
            // 从当前模块中查询
            if (module.models.ContainsKey(modelType) && module.models[modelType].Count > 1)
                throw new Exception(string.Format("数据:{0}存在多份，请根据id查询!", modelType.FullName));


            if (module.models.ContainsKey(modelType) && module.models[modelType].Count == 1)
                foreach (var item in module.models[modelType].Values)
                    return item;

            return null;
        }

        // 查询
        public static Model GetModel(Module module, Type modelType, int id)
        {
            if (module.models.ContainsKey(modelType) && module.models[modelType].ContainsKey(id))
                return module.models[modelType][id];

            return null;
        }



        // 清空模块中所有的数据
        public static void ClearModel(Module module)
        {

            List<Model> temp_models = ClassObjectPoolManager.Load<List<Model>>(); 
            temp_models.Clear();

            foreach (var item in module.models.Values)
            {
                foreach (var model in item.Values)
                { 
                    temp_models.Add(model);
                } 
            }

            foreach (var item in temp_models)
            {
                RemoveModel(module, item);
            }

            ClassObjectPoolManager.Unload(temp_models);

        }

        public static void ClearModel(Module module, Type modelType)
        {
            List<Type> temp_model_keys = ClassObjectPoolManager.Load<List<Type>>();
            temp_model_keys.Clear();

            foreach (var key in module.models.Keys)
            {
                if (key == modelType || key.IsSubclassOf(modelType))
                {
                    temp_model_keys.Add(key);
                }
            }

            List<Model> temp_models = ClassObjectPoolManager.Load<List<Model>>(); 
            temp_models.Clear();

            foreach (var key in temp_model_keys)
            {
                if (!module.models.ContainsKey(key))
                    continue;

                Dictionary<long, Model> models = module.models[key];
                foreach (Model item in models.Values)
                {
                    temp_models.Add(item);
                }

            }

            foreach (var item in temp_models)
            {
                RemoveModel(module, item);
            }

            ClassObjectPoolManager.Unload(temp_model_keys);
            ClassObjectPoolManager.Unload(temp_models);


        }

        public static List<Model> GetModels(Module module, Type modelType)
        {
            List<Model> models = new List<Model>();

            foreach (var item in module.models.Keys)
            {
                if (item == modelType || item.IsSubclassOf(modelType))
                {
                    foreach (var model in module.models[item].Values)
                        models.Add(model);
                }
            }


            return models;
        }

        public static Model LoadModel(Module module, Type modelType) 
        {
            if (!modelType.IsSubclassOf(typeof(Model)))
                throw new Exception("类型必须继承自Model!");
             
            Model model = GetModel(module, modelType);  
            if (model == null) 
            { 
                model = ClassObjectPoolManager.Load(modelType) as Model;
                AddModel(module, model);
            } 
            return model;
        }

        public static T LoadModel<T>(Module module) where T : Model 
        { 
            return LoadModel(module,typeof(T)) as T;
        }
         
    }

}
