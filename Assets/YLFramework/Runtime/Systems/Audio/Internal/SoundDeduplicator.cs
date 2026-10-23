using System.Collections.Generic;

namespace YLFramework.Services.Audio
{
    // 帧窗口为闭区间：请求所在帧仍在有效窗口内时会拒播。
    // SoundFrames 模式按每个 Clip 自己的记录帧计算；GlobalFrames 模式共用一个窗口起点，
    // 由第一个落在窗口外的接纳重新开始计算，窗口外的旧记录不再拦截请求。
    internal sealed class SoundDeduplicator
    {
        internal AudioKit.PlaySoundModes DefaultPlaySoundMode = AudioKit.PlaySoundModes.EveryOne;
        internal int GlobalFrameCountForIgnoreSameSound = 5;
        internal int SoundFrameCountForIgnoreSameSound = 5;
        private readonly Dictionary<int, AudioHandle> _global = new Dictionary<int, AudioHandle>();
        private readonly Dictionary<int, RecordData> _sounds = new Dictionary<int, RecordData>();
        private readonly List<int> _expired = new List<int>();
        private int _globalFrame;
        private int _pruneFrame;
        private readonly struct RecordData
        {
            internal readonly int Frame;
            internal readonly AudioHandle Handle;
            internal RecordData(int frame, AudioHandle handle)
            {
                Frame = frame;
                Handle = handle;
            }
        }
        internal bool CanPlay(int clipId, AudioKit.PlaySoundModes mode, int frame)
        {
            if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames)
            {
                return frame < _globalFrame || frame - _globalFrame > GlobalFrameCountForIgnoreSameSound || !_global.ContainsKey(clipId);
            }

            if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames && _sounds.TryGetValue(clipId, out var record))
            {
                return frame < record.Frame || frame - record.Frame > SoundFrameCountForIgnoreSameSound;
            }

            return true;
        }
        internal void Record(int clipId, AudioKit.PlaySoundModes mode, int frame, AudioHandle handle)
        {
            if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames)
            {
                if (frame < _globalFrame || frame - _globalFrame > GlobalFrameCountForIgnoreSameSound)
                {
                    _globalFrame = frame;
                    _global.Clear();
                }
                _global[clipId] = handle;
            }
            else if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames)
            {
                _sounds[clipId] = new RecordData(frame, handle);
            }
        }
        internal void Finished(int clipId, AudioHandle handle)
        {
            if (_sounds.TryGetValue(clipId, out var record) && ReferenceEquals(record.Handle.Voice, handle.Voice) && record.Handle.Generation == handle.Generation)
            {
                _sounds.Remove(clipId);
            }
        }
        internal void Rollback(int clipId, AudioKit.PlaySoundModes mode, AudioHandle handle)
        {
            if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInGlobalFrames)
            {
                if (_global.TryGetValue(clipId, out var owner) && ReferenceEquals(owner.Voice, handle.Voice) && owner.Generation == handle.Generation)
                {
                    _global.Remove(clipId);
                }
            }
            else if (mode == AudioKit.PlaySoundModes.IgnoreSameSoundInSoundFrames &&
                _sounds.TryGetValue(clipId, out var record) && ReferenceEquals(record.Handle.Voice, handle.Voice) && record.Handle.Generation == handle.Generation)
            {
                _sounds.Remove(clipId);
            }
        }
        internal void Prune(int frame)
        {
            // 距上次 Prune 未超过 SoundFrames 窗口长度时，本帧无需扫描。
            if (frame >= _pruneFrame && frame - _pruneFrame <= SoundFrameCountForIgnoreSameSound)
            {
                return;
            }

            _pruneFrame = frame;
            _expired.Clear();
            foreach (var pair in _sounds)
            {
                if (frame < pair.Value.Frame || frame - pair.Value.Frame > SoundFrameCountForIgnoreSameSound)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (var key in _expired)
            {
                _sounds.Remove(key);
            }

            _expired.Clear();
            if (frame < _globalFrame || frame - _globalFrame > GlobalFrameCountForIgnoreSameSound)
            {
                _global.Clear();
            }
        }
    }
}
