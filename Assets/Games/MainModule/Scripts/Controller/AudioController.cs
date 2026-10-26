using System;
using System.Collections.Generic;
using UnityEngine;
using XFABManager;
using XFGameFramework;
using NativeController = XFGameFramework.AudioController;

public class AudioController : Controller
{
    public const string MusicName = "Music";
    public const string SoundName = "Sound";
    private const string MigrationKey = "MainModule.Audio.XFPreferencesMigrated.v1";
    private const int VoiceCount = 48;
    private const double Cooldown = .080;

    public enum SoundPriority { Normal, Important }
    public enum SoundGroup { Explosion, PlayerWeapon, EnemyWeapon, Feedback }

    private sealed class Voice
    {
        public NativeController Controller;
        public SoundGroup Group;
        public string Sound;
        public SoundPriority Priority;
        public long Order;
    }

    private struct Admission
    {
        public double Time;
        public SoundPriority Priority;
    }

    private readonly Voice[] _voices = new Voice[VoiceCount];
    private readonly List<NativeController> _owned = new List<NativeController>(VoiceCount + 2);
    private readonly Dictionary<(SoundGroup Group, string Sound, int Emitter), Admission> _lastPlayed =
        new Dictionary<(SoundGroup, string, int), Admission>();
    private readonly List<(SoundGroup Group, string Sound, int Emitter)> _expiredAdmissions =
        new List<(SoundGroup, string, int)>();
    private NativeController _music;
    private NativeController _sound;
    private long _order;
    private double _nextAdmissionCleanup;
    private bool _running;

    public override void OnInit()
    {
        base.OnInit();
        RequireAvailable(MusicName);
        RequireAvailable(SoundName);
        for (var i = 0; i < VoiceCount; i++) RequireAvailable($"MainModule.Combat.{i:00}");
        MigratePreferences();
        try
        {
            _music = Register(MusicName, true);
            _sound = Register(SoundName, false);
            _music.AudioSource.priority = 24;
            _sound.AudioSource.priority = 32;
            for (var i = 0; i < VoiceCount; i++)
            {
                var group = i < 16 ? SoundGroup.Explosion : i < 32 ? SoundGroup.PlayerWeapon :
                    i < 44 ? SoundGroup.EnemyWeapon : SoundGroup.Feedback;
                _voices[i] = new Voice { Controller = Register($"MainModule.Combat.{i:00}", false), Group = group };
                _voices[i].Controller.AudioSource.priority = SourcePriority(group, SoundPriority.Normal);
            }
            _sound.onVolumeValueChange += OnSoundVolumeChanged;
            _sound.onMuteValueChange += OnSoundMuteChanged;
            OnSoundVolumeChanged(_sound.Volume);
            OnSoundMuteChanged(_sound.Mute);
            _running = true;
        }
        catch
        {
            OnExit();
            throw;
        }
    }

    public float MusicVolume
    {
        get => _music.Volume;
        set => _music.Volume = value;
    }

    public float SoundVolume
    {
        get => _sound.Volume;
        set => _sound.Volume = value;
    }

    public void PlaySound(string assetName, SoundPriority priority = SoundPriority.Normal,
        SoundGroup group = SoundGroup.Explosion, int emitterId = 0)
    {
        TryPlayCombatSound(assetName, priority, Time.unscaledTimeAsDouble, group, emitterId);
    }

    private bool TryPlayCombatSound(string assetName, SoundPriority priority, double now, SoundGroup group, int emitterId)
    {
        if (!_running) return false;
        if (string.IsNullOrEmpty(assetName)) throw new ArgumentException("A combat sound needs an asset name.", nameof(assetName));
        if (priority != SoundPriority.Normal && priority != SoundPriority.Important) throw new ArgumentOutOfRangeException(nameof(priority));
        if (group < SoundGroup.Explosion || group > SoundGroup.Feedback) throw new ArgumentOutOfRangeException(nameof(group));
        // 去重仅需保留 80ms 历史；每秒清理一次，避免随武器销毁累积或每发全表扫描。
        if (now >= _nextAdmissionCleanup)
        {
            foreach (var entry in _lastPlayed)
                if (now - entry.Value.Time >= Cooldown) _expiredAdmissions.Add(entry.Key);
            foreach (var expired in _expiredAdmissions) _lastPlayed.Remove(expired);
            _expiredAdmissions.Clear();
            _nextAdmissionCleanup = now + 1;
        }
        // 玩家按武器实例去重；敌人同类密集射击聚合，但不能吞掉玩家同名枪声。
        var key = (group, assetName, group == SoundGroup.PlayerWeapon ? emitterId : 0);
        if (_lastPlayed.TryGetValue(key, out var last) && now - last.Time < Cooldown && priority <= last.Priority)
            return false;

        Voice free = null, oldest = null, oldestSame = null;
        var sameCount = 0;
        foreach (var voice in _voices)
        {
            if (voice.Group != group) continue;
            if (!voice.Controller.AudioSource.isPlaying)
            {
                if (free == null) free = voice;
                continue;
            }
            if (EarlierVictim(voice, oldest)) oldest = voice;
            if (voice.Sound != assetName) continue;
            sameCount++;
            if (EarlierVictim(voice, oldestSame)) oldestSame = voice;
        }

        // 单音效满额时必须在同音效内抢占，否则会突破单音效上限。
        var sameLimit = group == SoundGroup.EnemyWeapon ? 6 : group == SoundGroup.Feedback ? 4 : 8;
        var target = sameCount >= sameLimit ? null : free;
        var shooting = group == SoundGroup.PlayerWeapon || group == SoundGroup.EnemyWeapon;
        if (target == null && (priority == SoundPriority.Important || shooting))
        {
            var victim = sameCount >= sameLimit ? oldestSame : oldest;
            // 连射优先保新攻击瞬态，但普通枪声不能抢占本组更重要的反馈。
            if (victim != null && victim.Priority <= priority) target = victim;
        }
        if (target == null) return false;

        var clip = AssetBundleManager.LoadAsset<AudioClip>(Module.ProjectName, assetName);
        if (!clip) return false;
        // XF 的自然结束会延迟回收 AudioData；复用前显式结束，避免引用累积。
        AudioPlayer.Stop(target.Controller.Name, StopType.All);
        target.Sound = null;
        target.Controller.AudioSource.volume = _sound.Volume;
        target.Controller.AudioSource.mute = _sound.Mute;
        target.Controller.AudioSource.priority = SourcePriority(group, priority);
        AudioPlayer.Play(target.Controller.Name, clip, false);
        target.Sound = assetName;
        target.Priority = priority;
        target.Order = ++_order;
        _lastPlayed[key] = new Admission { Time = now, Priority = priority };
        return true;
    }

    private static int SourcePriority(SoundGroup group, SoundPriority priority)
    {
        if (group == SoundGroup.Feedback) return 16;
        if (group == SoundGroup.PlayerWeapon) return 64;
        if (group == SoundGroup.EnemyWeapon) return priority == SoundPriority.Important ? 144 : 192;
        return priority == SoundPriority.Important ? 80 : 128;
    }

    private static bool EarlierVictim(Voice candidate, Voice current)
    {
        return current == null || candidate.Priority < current.Priority ||
            (candidate.Priority == current.Priority && candidate.Order < current.Order);
    }

    private static void RequireAvailable(string name)
    {
        if (AudioPlayer.GetAuidoController(name) != null)
            throw new InvalidOperationException("The game cannot take ownership of an existing XF audio controller: " + name);
    }

    private NativeController Register(string name, bool loop)
    {
        AudioPlayer.RegisterAudioController(name, loop, 1f);
        var controller = AudioPlayer.GetAuidoController(name);
        if (controller == null) throw new InvalidOperationException("XF audio registration requires a running game: " + name);
        _owned.Add(controller);
        controller.AudioSource.playOnAwake = false;
        AudioPlayer.Stop(name, StopType.All);
        return controller;
    }

    private void OnSoundVolumeChanged(float value)
    {
        foreach (var voice in _voices)
            if (voice != null && voice.Controller.AudioSource) voice.Controller.AudioSource.volume = value;
    }

    private void OnSoundMuteChanged(bool value)
    {
        foreach (var voice in _voices)
            if (voice != null && voice.Controller.AudioSource) voice.Controller.AudioSource.mute = value;
    }

    public override void OnExit()
    {
        _running = false;
        if (_sound != null)
        {
            _sound.onVolumeValueChange -= OnSoundVolumeChanged;
            _sound.onMuteValueChange -= OnSoundMuteChanged;
        }
        for (var i = _owned.Count - 1; i >= 0; i--)
        {
            var controller = _owned[i];
            if (ReferenceEquals(AudioPlayer.GetAuidoController(controller.Name), controller))
                AudioPlayer.RemoveAudioController(controller.Name);
        }
        _owned.Clear();
        Array.Clear(_voices, 0, _voices.Length);
        _lastPlayed.Clear();
        _expiredAdmissions.Clear();
        _nextAdmissionCleanup = 0;
        _music = _sound = null;
        _order = 0;
        base.OnExit();
    }

    private static void MigratePreferences()
    {
        if (PlayerPrefs.HasKey(MigrationKey)) return;
        foreach (var channel in new[] { MusicName, SoundName })
        {
            var oldVolume = "KEY_AUDIO_MANAGER_" + channel.ToUpperInvariant() + "_VOLUME";
            var volume = "AudioConfigVolume:" + channel;
            if (!PlayerPrefs.HasKey(volume) && PlayerPrefs.HasKey(oldVolume))
            {
                var value = PlayerPrefs.GetFloat(oldVolume);
                if (!float.IsNaN(value) && !float.IsInfinity(value)) PlayerPrefs.SetFloat(volume, Mathf.Clamp01(value));
            }
            var oldOn = "KEY_AUDIO_MANAGER_" + channel.ToUpperInvariant() + "_ON";
            var mute = "AudioConfigMute:" + channel;
            if (!PlayerPrefs.HasKey(mute) && PlayerPrefs.HasKey(oldOn))
            {
                var value = PlayerPrefs.GetInt(oldOn);
                if (value == 0 || value == 1) PlayerPrefs.SetInt(mute, 1 - value);
            }
        }
        PlayerPrefs.SetInt(MigrationKey, 1);
        PlayerPrefs.Save();
    }
}
