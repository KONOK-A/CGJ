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

        /// <summary>
        /// 首次导致启动失败的原始异常；额外清理错误附在Data["YLFramework.Core.CleanupException"]。
        /// 成功结果为null，失败完成通知前已结束本次Core逻辑所有权。
        /// </summary>
        public Exception Exception { get; private set; }

        internal Module PreviousCurrentModule { get; set; }
        internal ModuleState InitialState { get; }

        internal StartUpModuleRequest(Module module, Module previousCurrentModule = null)
        {
            Module = module;
            InitialState = module.State;
            PreviousCurrentModule = previousCurrentModule;
        }

        /// <summary>
        /// 启动协程不支持安全取消；在任何完成状态修改前拒绝Abort。
        /// </summary>
        public override void Abort()
        {
            throw new NotSupportedException("Module startup does not support Abort.");
        }

        internal void Finish(Exception error = null)
        {
            if (isDone) return;
            // 先提交生命周期与移除进行中记录，通知异常不能反过来改写启动结果。
            Exception = error;
            ModuleManager.CompleteStartup(this, error);
            string message = error?.ToString();
            if (error?.Data["YLFramework.Core.CleanupException"] is Exception cleanupError)
                message += "\nStartup cleanup errors: " + cleanupError;
            try { Completed(message); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }

        internal IEnumerator StartUp()
        {
            var routine = RunStartup();
            Exception failure = null;
            try
            {
                while (true)
                {
                    bool hasNext;
                    try { hasNext = ModuleManager.MoveStartup(Module, routine); }
                    catch (Exception error)
                    {
                        failure = error;
                        break;
                    }
                    if (!hasNext) break;
                    yield return routine.Current;
                }
            }
            finally
            {
                try { (routine as IDisposable)?.Dispose(); }
                catch (Exception cleanupError)
                {
                    if (failure == null) failure = cleanupError;
                    else AttachCleanupError(failure, cleanupError);
                }
            }
            Finish(failure);
        }

        internal static void AttachCleanupError(Exception error, Exception cleanupError)
        {
            const string key = "YLFramework.Core.CleanupException";
            var previous = error.Data[key] as Exception;
            error.Data[key] = previous == null ? cleanupError :
                new AggregateException("Startup cleanup failed.", previous, cleanupError);
        }

        private IEnumerator RunStartup()
        {
            var module = Module;
            progress = 0;
            string projectName = module.ProjectName;
            if (string.IsNullOrWhiteSpace(projectName))
                throw new InvalidOperationException($"Resource project name is required: {module.ModuleName}");

            Step = StartingModuleSteps.PreloadConfigTables;

            // 加载配表
#pragma warning disable CS0618 // 类型或成员已过时
            var tables = module.PreloadConfigTables;
            if (tables != null && tables.Count != 0)
            {
                float allCount = tables.Count;
                float currentCount = 0;
                foreach (var table in tables)
                {
                    string asset_name = table.Key;
                    if (string.IsNullOrWhiteSpace(asset_name))
                        throw new InvalidOperationException("Config table asset name is required.");
                    if (table.Value == null || !table.Value.IsArray || table.Value.GetArrayRank() != 1 ||
                        !typeof(Model).IsAssignableFrom(table.Value.GetElementType()))
                        throw new InvalidOperationException($"Config table must specify a Model array type: {asset_name}");
                    LoadAssetRequest request = AssetBundleManager.LoadAssetAsync<TextAsset>(projectName, asset_name);
                    if (request == null)
                        throw new InvalidOperationException($"Config table request is null: {projectName}/{asset_name}");
                    yield return request;
                    // 启动表及其Bundle须专用且无外部并发持有者；XFAB没有按调用计数的租约。
                    // 复制文本后立即归还，反序列化和业务初始化不再持有TextAsset。
                    var asset = request.asset;
                    string json;
                    Exception tableError = null;
                    try
                    {
                        if (!string.IsNullOrEmpty(request.error))
                            throw new InvalidOperationException($"Config table load failed: {projectName}/{asset_name}: {request.error}");
                        var text = asset as TextAsset;
                        if (!text || string.IsNullOrWhiteSpace(text.text))
                            throw new InvalidOperationException($"Config table asset or JSON text is empty: {projectName}/{asset_name}");
                        json = text.text;
                    }

                    catch (Exception error)
                    {
                        tableError = error;
                        throw;
                    }
                    finally
                    {
                        if (asset)
                        {
                            try { AssetBundleManager.UnloadAsset(asset); }
                            catch (Exception cleanupError)
                            {
                                if (tableError == null) throw;
                                AttachCleanupError(tableError, cleanupError);
                            }
                        }
                    }

                    var models = JsonConvert.DeserializeObject(json, table.Value,
                        new JsonSerializerSettings { CheckAdditionalContent = true }) as Model[];
                    if (models == null)
                        throw new InvalidOperationException($"Config table JSON must contain a Model array: {projectName}/{asset_name}");
                    foreach (var model in models) module.AddModel(model);

                    currentCount++;
                    progress = currentCount / allCount * 0.5f; // 更新进度
                }
            }
#pragma warning restore CS0618 // 类型或成员已过时


            Step = StartingModuleSteps.LoadScene;

            // 加载默认场景
            string sceneName = module.DefaultStartUpScene;
            if (!string.IsNullOrEmpty(sceneName))
            {
                //startMessage = "正在加载场景...";
                LoadSceneRequest operation = AssetBundleManager.LoadSceneAsynchrony(projectName, sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
                if (operation == null)
                    throw new InvalidOperationException($"Default scene request is null: {projectName}/{sceneName}");
                while (!operation.isDone)
                {
                    yield return null;
                    progress = 0.5f + operation.progress * 0.5f;
                }
                if (!string.IsNullOrEmpty(operation.error))
                    throw new InvalidOperationException($"Default scene load failed: {projectName}/{sceneName}: {operation.error}");
                if (!operation.Scene.IsValid() || !operation.Scene.isLoaded)
                    throw new InvalidOperationException($"Default scene result is not loaded: {projectName}/{sceneName}");
            }

            module.OnStart();
        }

      

    }

}
