using System;
using UnityEngine;

namespace YLFramework.Services.Audio
{
    /// <summary>A caller-owned clip and its relative random-selection weight.</summary>
    [Serializable]
    public struct AudioWeightedClip
    {
        /// <summary>The caller-owned clip; null and destroyed clips are excluded from selection.</summary>
        public AudioClip Clip;

        /// <summary>A finite, nonnegative relative weight. Zero excludes this entry from selection.</summary>
        public float Weight;

        /// <summary>Store a clip and weight without validation; the selector validates every entry.</summary>
        /// <param name="clip">Caller-owned clip candidate.</param>
        /// <param name="weight">Relative weight, defaulting to one.</param>
        public AudioWeightedClip(AudioClip clip, float weight = 1f)
        {
            Clip = clip;
            Weight = weight;
        }
    }
}
