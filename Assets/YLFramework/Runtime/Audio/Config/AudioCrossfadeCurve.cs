// Copyright (c) 2015 - 2025 liangxiegame, MIT License.
// https://github.com/liangxiegame/QFramework
namespace YLFramework.Services.Audio
{
    /// <summary>Transition gain mapping for music CrossFadeMusic.</summary>
    public enum AudioCrossfadeCurve
    {
        /// <summary>Gain changes linearly. Zero value retained for serialized compatibility; use explicitly for legacy behavior.</summary>
        Linear = 0,
        /// <summary>Default for new music transitions. Sine/cosine gains keep uncorrelated full-gain material closer to constant power, not equal loudness.</summary>
        EqualPower = 1
    }
}
