using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>Passive IMGUI diagnostics with explicit statistics reset. Does not initialize playback or create a listener.</summary>
    public static class AudioKitDiagnostics
    {
        private static readonly List<AudioHandle> _sounds = new List<AudioHandle>();
        /// <summary>在当前 GUILayout 区域绘制被动诊断界面；唯一的写操作是显式的 Sound 统计重置。</summary>
        public static void Draw(ref Vector2 scrollPosition)
        {
            var settings = AudioKit.CurrentSettings;
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            DrawSoundStats();
            var capacity = AudioKit.CurrentSoundConcurrency;
            GUILayout.Label("Sound limit: " + FormatLimit(capacity.Limit) + " / " + capacity.OverflowPolicy +
                " / Default Clip limit: " + FormatLimit(capacity.DefaultClipLimit));
            if (!Application.isPlaying || settings == null)
            {
                GUILayout.Label("AudioKit is not initialized for playback.");
                GUILayout.EndScrollView();
                return;
            }
            DrawPlayer("Music", AudioKit.Music);
            DrawPlayer("Voice", AudioKit.Voice);
            if (AudioManager.TryGetInstance(out var host))
            {
                DrawPlayer("Music outgoing", host.MusicPlayback.Outgoing);
                GUILayout.Label("Music transition curve: " + host.MusicPlayback.CurrentCurve);
            }
            GUILayout.Label("Music " + settings.IsMusicOn.Value + " / " + settings.MusicVolume.Value +
                " / Muted: " + settings.IsMusicMuted.Value +
                "  Voice " + settings.IsVoiceOn.Value + " / " + settings.VoiceVolume.Value +
                " / Muted: " + settings.IsVoiceMuted.Value +
                "  Sound " + settings.IsSoundOn.Value + " / " + settings.SoundVolume.Value +
                " / Muted: " + settings.IsSoundMuted.Value);
            GUILayout.Label("Sound mode: " + AudioKit.PlaySoundMode);
            DrawGroup("Music default", settings.MusicOutputGroup.Value);
            DrawGroup("Voice default", settings.VoiceOutputGroup.Value);
            DrawGroup("Sound default", settings.SoundOutputGroup.Value);
            AudioKit.CopyPlayingSounds(_sounds);
            GUILayout.Label("Sound requests: " + _sounds.Count);
            foreach (var handle in _sounds)
            {
                DrawPlayer("Sound", handle);
            }

            _sounds.Clear();
            GUILayout.EndScrollView();
        }
        private static string FormatLimit(int limit) => limit == 0 ? "unlimited" : limit.ToString();
        private static void DrawSoundStats()
        {
            if (GUILayout.Button("Reset Sound statistics"))
            {
                AudioKit.ResetSoundStats();
            }

            var stats = AudioKit.SoundStats;
            GUILayout.Label("Sound active: " + stats.ActiveCount + " / Peak: " + stats.PeakActiveCount +
                " / Admitted: " + stats.AdmittedCount + " / Start failures: " + stats.StartFailureCount);
            GUILayout.Label("Rejected - Dedup: " + stats.DedupRejectedCount + " / Global: " + stats.GlobalRejectedCount +
                " / Clip: " + stats.ClipRejectedCount + " / Priority: " + stats.PriorityRejectedCount);
            GUILayout.Label("Stolen: " + stats.StolenCount + " / Trimmed: " + stats.TrimmedCount);
            GUILayout.Label("Sound cache: " + stats.CachedCount + " / Capacity: " + AudioKit.SoundCacheCapacity +
                " / Hits: " + stats.CacheHitCount + " / Misses: " + stats.CacheMissCount);
            GUILayout.Label("Sound sources owned: " + stats.OwnedSourceCount + " / Created: " + stats.SourceCreatedCount +
                " / Release requests: " + stats.SourceReleasedCount);
        }
        private static void DrawPlayer(string title, AudioHandle handle)
        {
            if (!handle.IsValid)
            {
                GUILayout.Label(title + ": (none)");
                return;
            }
            var source = handle.Voice.Source;
            GUILayout.Label(title + ": " + (source && source.clip ? source.clip.name : "(none)") +
                "  Playing: " + handle.IsPlaying + "  Paused: " + handle.IsPaused);
            if (handle.Voice.IsSound)
            {
                var clip = source ? source.clip : null;
                GUILayout.Label("Clip ID: " + handle.Voice.ClipId + " / Active: " + AudioKit.GetSoundCount(clip) +
                    " / Effective limit: " + (clip ? FormatLimit(AudioKit.GetEffectiveClipSoundLimit(clip)) : "(none)") +
                    " / Priority: " + handle.Voice.Priority);
            }
            GUILayout.Label("Time: " + handle.Time.ToString("F2") + " s  Duration: " + handle.Duration.ToString("F2") +
                " s  Progress: " + handle.Progress.ToString("F3"));
            if (source)
            {
                GUILayout.Label("Volume: " + source.volume + "  Pitch: " + source.pitch + "  Spatial: " + source.spatialBlend + "  Transition: " + handle.Voice.TransitionGain);
                DrawGroup("Output (" + handle.Voice.Options.OutputRoute.Mode + ")", source.outputAudioMixerGroup);
                var requestedGroup = handle.Voice.Options.OutputRoute.Group;
                if (AudioOutputRoute.HasReference(requestedGroup) && !requestedGroup)
                {
                    GUILayout.Label("Requested mixer group is no longer alive.");
                }
            }
        }
        private static void DrawGroup(string title, UnityEngine.Audio.AudioMixerGroup group)
        {
            GUILayout.Label(title + ": " + (group ? group.name + " / " + (group.audioMixer ? group.audioMixer.name : "(missing mixer)") :
                AudioOutputRoute.HasReference(group) ? "(missing group)" : "(direct)"));
        }
    }
}
