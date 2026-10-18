using System;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Resource
{
    public enum ResourceBuildPhase
    {
        PreflightStarted, AtlasPreparationStarted, MapStarted, FingerprintStarted, NativeBuildStarted,
        ReadbackStarted, ContentEncodingStarted, WorkspaceVerified, PublicationStarted, ReferenceGenerationStarted, AttemptFinished
    }

    /// <summary>已实际到达的同步边界，不提供控制权、百分比、取消或测试故障注入。</summary>
    public sealed class ResourceBuildStage
    {
        public ResourceBuildPhase Phase { get; }
        public string ProjectGuid { get; }
        public string ProjectId { get; }
        public string Platform { get; }
        public DateTime AtUtc { get; } = DateTime.UtcNow;
        internal ResourceBuildStage(ResourceBuildPhase phase, string guid, string project, string platform)
        { Phase = phase; ProjectGuid = guid; ProjectId = project; Platform = platform; }
    }

    /// <summary>主线程只读观察。订阅者自行解绑；异常隔离，观察回调内执行重入明确拒绝。</summary>
    public static class ResourceBuildProgress
    {
        public static event Action<ResourceBuildStage> StageReached;
        [ThreadStatic] private static bool observing;
        internal static string PhaseName(ResourceBuildPhase value) => value switch
        {
            ResourceBuildPhase.PreflightStarted => "检查资源和输出",
            ResourceBuildPhase.AtlasPreparationStarted => "准备图集",
            ResourceBuildPhase.MapStarted => "计算分包",
            ResourceBuildPhase.FingerprintStarted => "计算输入指纹",
            ResourceBuildPhase.NativeBuildStarted => "调用 Unity 构建",
            ResourceBuildPhase.ReadbackStarted => "读回并验证构建结果",
            ResourceBuildPhase.ContentEncodingStarted => "处理交付编码",
            ResourceBuildPhase.WorkspaceVerified => "构建工作目录验证完成",
            ResourceBuildPhase.PublicationStarted => "本地发布",
            ResourceBuildPhase.ReferenceGenerationStarted => "生成资源地址常量",
            ResourceBuildPhase.AttemptFinished => "本次执行已结束",
            _ => "未知步骤（" + value + "）"
        };
        internal static void RejectReentry()
        {
            if (observing) throw new InvalidOperationException("A Resource stage observer cannot reenter execution.");
        }
        internal static void Notify(ResourceBuildPhase phase, ResourceProject project, BuildTarget target)
        {
            if (StageReached == null || observing) return;
            Notify(phase, project ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project)) : null,
                project ? project.ProjectId : null, target.ToString());
        }
        internal static void Notify(ResourceBuildPhase phase, string guid, string project, string platform)
        {
            var handlers = StageReached;
            if (handlers == null || observing) return;
            var stage = new ResourceBuildStage(phase, guid, project, platform);
            observing = true;
            try
            {
                foreach (Action<ResourceBuildStage> handler in handlers.GetInvocationList())
                    try { handler(stage); }
                    catch (Exception) { Debug.LogWarning("资源构建进度回调发生异常；构建流程将继续，并照常验证输入和输出。"); }
            }
            finally { observing = false; }
        }
    }
}
