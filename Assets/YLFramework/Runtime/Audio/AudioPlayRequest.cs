using System;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>命名请求阶段；Accepted 不保证启播，Started 表示 Source.Play 已提交而非可听见。</summary>
    public enum AudioPlayState { Loading, Ready, Accepted, Started, Ended }
    /// <summary>命名请求的稳定终态；只有 Completed 表示自然完成。</summary>
    public enum AudioPlayEnd { None, Completed, Stopped, Replaced, Unavailable, Failed, Shutdown, Canceled, Rejected }

    /// <summary>一次命名播放的稳定身份。加载中 Handle 无效；Cancel/Dispose 只结束本请求，不物理取消共享加载。</summary>
    /// <remarks>Unity 主线程使用。Error/Handle/HasStarted 在终态仍保留原请求证据；Stop 等可在开始回调中同步终结。</remarks>
    public sealed class AudioPlayRequest : IDisposable
    {
        private readonly AudioManager mOwner;
        internal readonly AudioCategory Category;
        internal readonly AudioChannel Channel;
        private readonly AudioPlaybackOptions mOptions;
        private readonly Action<AudioClip, AudioHandle> mStarted, mFinished;
        private readonly float mCrossfade;
        private readonly bool mWaitForData;
        private readonly ulong mSession;
        private IDisposable mWaiting;
        private AudioClipLease mClip;
        private AudioResourceLease mResource;
        private bool mDataRequested, mSubmitting;
        /// <summary>完整资源定位。</summary>
        public AudioClipAddress Address { get; }
        /// <summary>当前阶段；终结后不会恢复。</summary>
        public AudioPlayState State { get; private set; } = AudioPlayState.Loading;
        /// <summary>终态是否已确定，不等于底层 producer 已停止。</summary>
        public bool IsDone => State == AudioPlayState.Ended;
        /// <summary>稳定终态原因。</summary>
        public AudioPlayEnd EndReason { get; private set; }
        /// <summary>后端或提交错误；取消/拒播没有伪造加载错误。</summary>
        public Exception Error { get; private set; }
        /// <summary>资产加载进度；不声称包含全部解码/Streaming 数据。</summary>
        public float Progress { get; private set; }
        /// <summary>资产曾由后端取得。</summary>
        public bool AssetReady { get; private set; }
        /// <summary>音频数据曾达到 Loaded；不表示 Streaming 全文件常驻内存。</summary>
        public bool DataReady { get; private set; }
        /// <summary>是否曾提交 Source.Play。</summary>
        public bool HasStarted { get; private set; }
        /// <summary>接纳/开始时的原播放句柄；加载中无效，终态保留原身份但不可控制新代次。</summary>
        public AudioHandle Handle { get; private set; }
        internal bool Pending => State == AudioPlayState.Loading || State == AudioPlayState.Ready;

        internal AudioPlayRequest(AudioManager owner, AudioClipAddress address, AudioCategory category, AudioChannel channel,
            AudioPlaybackOptions options, Action<AudioClip, AudioHandle> started, Action<AudioClip, AudioHandle> finished,
            bool waitForData, float crossfade)
        {
            mOwner = owner; Address = address; Category = category; Channel = channel; mOptions = options;
            mStarted = started; mFinished = finished; mWaitForData = waitForData; mCrossfade = crossfade;
            mSession = AudioKit.SoundSession;
        }
        internal void Load(bool asynchronous)
        {
            if (IsDone) return;
            if (!asynchronous)
            {
                try { Receive(mOwner.ClipCache.Load(Address), null); }
                catch (Exception exception) { Fail(exception); throw; }
                return;
            }
            var waiting = mOwner.ClipCache.LoadAsync(Address, Receive, value => { if (!IsDone) Progress = value; });
            if (Pending && !AssetReady) mWaiting = waiting;
            else waiting.Dispose();
        }
        private bool Available => mOwner && mOwner.Available && mSession == AudioKit.SoundSession && (Channel == null || Channel.IsValid);
        private void Receive(AudioClipLease lease, Exception error)
        {
            if (IsDone || !Available) { lease?.ReleaseSafely(); return; }
            mWaiting = null;
            if (error != null) { lease?.ReleaseSafely(); Fail(error); return; }
            mClip = lease; AssetReady = true; Progress = 1; State = AudioPlayState.Ready;
            try { Tick(); }
            catch (Exception exception)
            {
                Fail(exception);
                if (!mWaitForData) throw;
            }
        }
        internal void Tick()
        {
            if (IsDone || !AssetReady || !Pending || mSubmitting) return;
            if (!Available) { End(AudioPlayEnd.Unavailable); return; }
            var clip = mClip.Clip;
            if (!clip) { End(AudioPlayEnd.Unavailable); return; }
            if (clip.loadState == AudioDataLoadState.Failed) { Fail(new InvalidOperationException("Audio data failed to load: " + Address.Key)); return; }
            if (mWaitForData && clip.loadState != AudioDataLoadState.Loaded)
            {
                if (!mDataRequested && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    mDataRequested = true;
                    if (!clip.LoadAudioData()) { Fail(new InvalidOperationException("Audio data load was rejected: " + Address.Key)); return; }
                }
                if (clip.loadState != AudioDataLoadState.Loaded) return;
            }
            DataReady = clip.loadState == AudioDataLoadState.Loaded;
            mOptions.ValidateClip(clip);
            var acquired = mClip; mClip = null;
            mResource = new AudioResourceLease(acquired, reason => End((AudioPlayEnd)reason, stopPlayback: false), handle =>
            {
                Handle = handle; State = AudioPlayState.Accepted; mOwner.RemovePending(this);
            });
            // 替换旧轨/抢占释放仍可重入取消；直到原 Voice 已绑定并交付句柄才撤销 pending。
            mSubmitting = true;
            Action<AudioClip, AudioHandle> started = (actual, handle) =>
            {
                Handle = handle; HasStarted = true;
                if (!IsDone) State = AudioPlayState.Started;
                mStarted?.Invoke(actual, handle);
            };
            AudioHandle result;
            if (Channel != null) result = Channel.PlayWithSnapshot(clip, mOptions, started, mFinished, mResource, namedCommit: true);
            else if (Category == AudioCategory.Sound) result = AudioKit.PlaySoundWithSnapshot(clip, mOptions, mFinished, started, resource: mResource);
            else if (mCrossfade >= 0f) result = AudioKit.CrossFadeMusicWithSnapshot(clip, mCrossfade, mOptions, started, mFinished, resource: mResource, namedCommit: true);
            else result = AudioKit.PlaySlotWithSnapshot(clip, Category == AudioCategory.Music, mOptions, started, mFinished, mResource, namedCommit: true);
            if (!HasStarted) Handle = result;
            if (!mResource.IsBound && !IsDone) End(AudioPlayEnd.Rejected);
        }
        internal void Fail(Exception exception) { Error = exception; End(AudioPlayEnd.Failed); }
        internal void End(AudioPlayEnd reason, bool stopPlayback = true)
        {
            if (IsDone) return;
            State = AudioPlayState.Ended; EndReason = reason;
            mOwner.RemovePending(this);
            var waiting = mWaiting; mWaiting = null;
            var clip = mClip; mClip = null;
            try { waiting?.Dispose(); }
            finally
            {
                clip?.ReleaseSafely();
                if (mResource != null && !mResource.IsBound) mResource.Dispose();
                if (stopPlayback) Handle.Stop();
            }
        }
        /// <summary>逻辑取消等待或停止原播放。共享 producer 继续；迟到结果按原责任归还。</summary>
        public void Cancel()
        {
            if (IsDone) return;
            End(AudioPlayEnd.Canceled);
        }
        /// <summary>等价于 Cancel，不影响其他等待者。</summary>
        public void Dispose() => Cancel();
    }
}
