// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/AsyncOperation/StartUpModuleRequest.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections; 
using Newtonsoft.Json;
using UnityEngine;
using AssetBundleManager = XFABManager.AssetBundleManager;
using LoadAssetRequest = XFABManager.LoadAssetRequest;
using LoadSceneRequest = XFABManager.LoadSceneRequest;

namespace YLFramework.Core
{

    public enum StartingModuleSteps
    { 
        /// <summary>
        /// 加载配表
        /// </summary>
        PreloadConfigTables,

        /// <summary>
        /// 加载场景
        /// </summary>
        LoadScene
    }


    public class StartUpModuleRequest : YLFramework.Foundation.CustomAsyncOperation<StartUpModuleRequest>
    {
         
        public StartingModuleSteps Step { get;private set; }
        
        public Module Module { get; protected set; }


        internal IEnumerator StartUp(Module module) 
        {
            if (module == null)
            {
                Completed("无法启动空模块！");
                yield break;
            }

            this.Module = module; // 保存模块对象

            ModuleManager.AddModule(module);

            progress = 0;

            Step = StartingModuleSteps.PreloadConfigTables;

            // 加载配表
#pragma warning disable CS0618 // 类型或成员已过时
            if (module.PreloadConfigTables != null && module.PreloadConfigTables.Count != 0)
            {
                float allCount = module.PreloadConfigTables.Count;
                float currentCount = 0;
                foreach (string asset_name in module.PreloadConfigTables.Keys)
                {
                    LoadAssetRequest request = AssetBundleManager.LoadAssetAsync<TextAsset>(module.ProjectName, asset_name);
                    yield return request;

                    if (request == null || !string.IsNullOrEmpty(request.error))
                    {
                        Debug.LogErrorFormat("BaseModule配表:{0}加载失败:{1}", asset_name, request != null ? request.error : string.Empty);
                        continue;
                    }

                    TextAsset text = request.asset as TextAsset;

                    if (text == null || string.IsNullOrEmpty(text.text))
                    {
                        Debug.LogWarningFormat("配表:{0} 数据为空!", asset_name);
                        continue;
                    }

                    Model[] models = null;

                    try
                    {
                        // 把 json 转成对应的对象 
                        models = JsonConvert.DeserializeObject(text.text, module.PreloadConfigTables[asset_name]) as Model[];
                        if (models == null || models.Length == 0) continue;
                    }
                    catch (Exception e)
                    {
                        // 卸载资源
                        AssetBundleManager.UnloadAsset(text);
                        Completed(string.Format("json解析异常:content:{0} error:{1}", text.text,e.ToString()));
                        yield break;
                    }

                    // 卸载资源
                    AssetBundleManager.UnloadAsset(text);

                    foreach (var model in models)
                    {
                        module.AddModel(model);
                    }

                    currentCount++;
                    progress = currentCount / allCount * 0.5f; // 更新进度
                }
            }
#pragma warning restore CS0618 // 类型或成员已过时


            Step = StartingModuleSteps.LoadScene;

            // 加载默认场景
            if (!string.IsNullOrEmpty(module.DefaultStartUpScene))
            {
                //startMessage = "正在加载场景...";
                LoadSceneRequest operation = AssetBundleManager.LoadSceneAsynchrony(module.ProjectName, module.DefaultStartUpScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
                while (operation != null && !operation.isDone)
                {
                    yield return null;
                    progress = 0.5f + operation.progress * 0.5f;
                }

            }

            try
            {
                module.ProjectName = module.GetType().Name; // 默认名称跟类名相同
                module.OnStart();
                Completed();
            }
            catch (Exception e)
            {
                Completed(e.ToString());
            } 
        }

      

    }

}