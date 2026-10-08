// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/ModelManager.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System; 
using System.Collections.Generic; 


namespace YLFramework.Core
{

    internal class ModelManager
    {  
        // 添加
        public static void AddModel(Module module, Model model)
        { 
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (model == null) throw new ArgumentNullException(nameof(model));
            module.EnsureCanAddObjects();
            Type type = model.GetType();
            if (model.Module != null)
            {
                if (!ReferenceEquals(model.Module, module))
                    throw new InvalidOperationException("Model is already owned by another Module.");
                if (model.IsInitializing || model.IsExiting)
                    throw new InvalidOperationException("Cannot register a Model during its lifecycle callback.");
                if (ReferenceEquals(GetModel(module, type, model.Id), model)) return;
                throw new InvalidOperationException("Model owner and registration do not match.");
            }
            if (module.creatingModels.Contains(type))
                throw new InvalidOperationException("Cannot register a Model while its constructor is running.");

            module.models.TryGetValue(type, out var models);
            long originalId = model.Id;
            long id = originalId;
            if (id == -1)
            {
                do
                {
                    id = module.nextModelId;
                    module.nextModelId = id == long.MaxValue ? 0 : id + 1;
                } while (models != null && models.ContainsKey(id));
            }
            if (models != null && models.ContainsKey(id))
                throw new InvalidOperationException($"Model {type.FullName} with ID {id} is already registered.");

            if (models == null)
            {
                models = new Dictionary<long, Model>();
                module.models.Add(type, models);
            }
            model.Id = id;
            models.Add(id, model);
            model.Module = module;
            model.IsInitializing = true;
            try
            {
                model.OnInit();
                model.IsInitializing = false;
            }
            catch (Exception initializationError)
            {
                model.IsInitializing = false;
                try { ExitModel(module, model); }
                catch (Exception cleanupError) { initializationError.Data["YLFramework.Core.CleanupException"] = cleanupError; }
                model.Id = originalId;
                throw;
            }
        }

        // 移除
        public static void RemoveModel(Module module, Model model)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (model == null || !ReferenceEquals(model.Module, module)) return;
            Type type = model.GetType();
            if (!ReferenceEquals(GetModel(module, type, model.Id), model)) return;
            if (model.IsExiting) return;
            if (model.IsInitializing)
                throw new InvalidOperationException("Cannot remove a Model during initialization.");
            ExitModel(module, model);
        }

        private static void ExitModel(Module module, Model model)
        {
            model.IsExiting = true;
            try { model.OnExit(); }
            finally
            {
                Type type = model.GetType();
                if (module.models.TryGetValue(type, out var models) &&
                    models.TryGetValue(model.Id, out var registered) && ReferenceEquals(registered, model))
                {
                    models.Remove(model.Id);
                    if (models.Count == 0) module.models.Remove(type);
                }
                model.Module = null;
                model.IsExiting = false;
            }
        }

        public static Model GetModel(Module module, Type modelType)
        {
            ValidateModelType(modelType);
            // 从当前模块中查询
            if (module.models.ContainsKey(modelType) && module.models[modelType].Count > 1)
                throw new Exception(string.Format("数据:{0}存在多份，请根据id查询!", modelType.FullName));


            if (module.models.ContainsKey(modelType) && module.models[modelType].Count == 1)
                foreach (var item in module.models[modelType].Values)
                    return item;

            return null;
        }

        // 查询
        public static Model GetModel(Module module, Type modelType, long id)
        {
            ValidateModelType(modelType);
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

            try
            {
                foreach (var item in temp_models) RemoveModel(module, item);
            }
            finally
            {
                temp_models.Clear();
                ClassObjectPoolManager.Unload(temp_models);
            }

        }

        public static void ClearModel(Module module, Type modelType)
        {
            ValidateModelType(modelType);
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

            try
            {
                foreach (var item in temp_models) RemoveModel(module, item);
            }
            finally
            {
                temp_model_keys.Clear();
                temp_models.Clear();
                ClassObjectPoolManager.Unload(temp_model_keys);
                ClassObjectPoolManager.Unload(temp_models);
            }


        }

        public static List<Model> GetModels(Module module, Type modelType)
        {
            ValidateModelType(modelType);
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
            ValidateModelType(modelType);
             
            Model model = GetModel(module, modelType);  
            if (model != null)
            {
                if (model.IsExiting) throw new InvalidOperationException("Cannot load a Model during exit.");
                return model;
            }
            module.EnsureCanAddObjects();
            if (modelType.IsAbstract || modelType.ContainsGenericParameters || modelType.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException("Model type must be concrete and have a public parameterless constructor.", nameof(modelType));
            if (!module.creatingModels.Add(modelType))
                throw new InvalidOperationException("Recursive Model construction is not allowed.");
            try { model = (Model)Activator.CreateInstance(modelType); }
            finally { module.creatingModels.Remove(modelType); }
            AddModel(module, model);
            return model;
        }

        public static T LoadModel<T>(Module module) where T : Model 
        { 
            return LoadModel(module,typeof(T)) as T;
        }

        private static void ValidateModelType(Type modelType)
        {
            if (modelType == null) throw new ArgumentNullException(nameof(modelType));
            if (!typeof(Model).IsAssignableFrom(modelType))
                throw new ArgumentException("Type must inherit from Model.", nameof(modelType));
        }
         
    }

}
