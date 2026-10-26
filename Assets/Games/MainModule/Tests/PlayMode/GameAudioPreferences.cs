using System;
using System.Linq;
using UnityEngine;

internal sealed class GameAudioPreferences : IDisposable
{
    private static readonly string[] Floats = { "KEY_AUDIO_MANAGER_MUSIC_VOLUME", "KEY_AUDIO_MANAGER_SOUND_VOLUME", "AudioConfigVolume:Music", "AudioConfigVolume:Sound" };
    private static readonly string[] Ints = { "KEY_AUDIO_MANAGER_MUSIC_ON", "KEY_AUDIO_MANAGER_SOUND_ON", "AudioConfigMute:Music", "AudioConfigMute:Sound", "MainModule.Audio.XFPreferencesMigrated.v1" };
    private readonly bool[] hadFloats = Floats.Select(PlayerPrefs.HasKey).ToArray();
    private readonly float[] floats = Floats.Select(key => PlayerPrefs.GetFloat(key)).ToArray();
    private readonly bool[] hadInts = Ints.Select(PlayerPrefs.HasKey).ToArray();
    private readonly int[] ints = Ints.Select(key => PlayerPrefs.GetInt(key)).ToArray();

    public void UseAudibleDefaults()
    {
        PlayerPrefs.SetFloat("AudioConfigVolume:Music", 1);
        PlayerPrefs.SetFloat("AudioConfigVolume:Sound", 1);
        PlayerPrefs.SetInt("AudioConfigMute:Music", 0);
        PlayerPrefs.SetInt("AudioConfigMute:Sound", 0);
        PlayerPrefs.SetInt("MainModule.Audio.XFPreferencesMigrated.v1", 1);
    }

    public void Dispose()
    {
        for (var i = 0; i < Floats.Length; i++)
            if (hadFloats[i]) PlayerPrefs.SetFloat(Floats[i], floats[i]); else PlayerPrefs.DeleteKey(Floats[i]);
        for (var i = 0; i < Ints.Length; i++)
            if (hadInts[i]) PlayerPrefs.SetInt(Ints[i], ints[i]); else PlayerPrefs.DeleteKey(Ints[i]);
        PlayerPrefs.Save();
    }
}
