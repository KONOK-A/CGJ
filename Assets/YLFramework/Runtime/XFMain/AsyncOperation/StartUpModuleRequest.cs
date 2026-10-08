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


    /// <summary>
    /// ModuleManager创建的非池化启动请求；不支持Abort取消启动协程。
    /// </summary>
    public sealed class StartUpModuleRequest : YLFramework.Foundation.CustomAsyncOperation<StartUpModuleRequest>
    {
         
        public StartingModuleSteps Step { get;private set; }
        
        public Module Module { get; }

        internal Module PreviousCurrentModule { get; set; }

        internal StartUpModuleRequest(Module module, Module previousCurrentModule = null)
        {
            Module = module;
            PreviousCurrentModule = previousCurrentModule;
        }

        /// <summary>
        /// 启动协程不支持安全取消；在任何完成状态修改前拒绝Abort。
        /// </summary>
        public override void Abort()
        {
            throw new NotSupportedException("Module startup does not support Abort.");
        }

        internal void Finish(string error = null)
        {
            if (isDone) return;
            // 先提交生命周期与移除进行中记录，通知异常不能反过来改写启动结果。
            ModuleManager.CompleteStartup(this, error);
            try { Completed(error); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }

        internal IEnumerator StartUp()
        {
            var routine = RunStartup();
            string failure = null;
            try
            {
                while (true)
                {
                    bool hasNext;
                    try { hasNext = ModuleManager.MoveStartup(Module, routine); }
                    catch (Exception error)
                    {
                        failure = error.ToString();
                        break;
                    }
                    if (!hasNext) break;
                    yield return routine.Current;
                }
            }
            finally { (routine as IDisposable)?.Dispose(); }
            Finish(failure);
        }

        private IEnumerator RunStartup()
        {
            var module = Module;
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
                        throw new InvalidOperationException(string.Format("json解析异常:content:{0} error:{1}", text.text,e.ToString()), e);
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

            module.ProjectName = module.GetType().Name; // 默认名称跟类名相同
            module.OnStart();
        }

      

    }

}
