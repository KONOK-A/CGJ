using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YLFramework.Core;
using YLFramework.Foundation;
using Module = YLFramework.Core.Module;

namespace YLFramework.Integrations.HybridCLR
{
    public sealed class HybridCLRLoadResult
    {
        public string ProjectId { get; internal set; }
        public string CodePackageId { get; internal set; }
        public bool Simulation { get; internal set; }
        public IReadOnlyDictionary<string, Assembly> Assemblies { get; internal set; }
    }

    public sealed class HybridCLRLoadRequest : CustomAsyncOperation<HybridCLRLoadRequest>
    {
        private readonly List<Action<HybridCLRLoadRequest>> continuations = new List<Action<HybridCLRLoadRequest>>();
        public HybridCLRLoadResult Result { get; private set; }
        public Exception Exception { get; private set; }
        public bool RequiresRestart { get; private set; }
        public string ProjectId { get; internal set; }
        public string Stage { get; internal set; } = "Manifest";
        public string AssemblyName { get; internal set; }
        public int? NativeErrorCode { get; internal set; }
        internal void Progress(float value) => progress = value;
        internal void Finish(HybridCLRLoadResult result, Exception exception = null, bool restart = false)
        {
            if (isDone) return;
            Result = result; Exception = exception; RequiresRestart = restart;
            try { Completed(exception?.ToString()); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
            finally
            {
                var pending = continuations.ToArray(); continuations.Clear();
                foreach (var continuation in pending)
                    try { continuation(this); } catch (Exception continuationError) { Debug.LogException(continuationError); }
            }
        }
        internal void ContinueWith(Action<HybridCLRLoadRequest> continuation)
        {
            if (isDone) continuation(this); else continuations.Add(continuation);
        }
        internal void RemoveContinuation(Action<HybridCLRLoadRequest> continuation) => continuations.Remove(continuation);
        public override void Abort() => throw new NotSupportedException("HybridCLR code loading does not support Abort; native loads cannot be undone.");
    }

    public sealed class HybridCLRModuleRequest : CustomAsyncOperation<HybridCLRModuleRequest>
    {
        internal Action Detach;
        internal Action<Exception> Invalidate;
        public Module Module { get; internal set; }
        public HybridCLRLoadResult Code { get; internal set; }
        public Exception Exception { get; private set; }
        public bool RequiresRestart { get; private set; }
        public string ProjectId { get; internal set; }
        public string Stage { get; internal set; } = "LoadCode";
        public string AssemblyName { get; internal set; }
        public int? NativeErrorCode { get; internal set; }
        internal void Finish(Exception exception = null, bool restart = false)
        {
            if (isDone) return;
            Exception = exception; RequiresRestart = restart;
            try { Completed(exception?.ToString()); }
            catch (Exception notificationError) { Debug.LogException(notificationError); }
        }
        public override void Abort() => throw new NotSupportedException("HybridCLR Module startup does not support Abort.");
    }
}
