using System;

namespace YLFramework.Services.Audio
{
    /// <summary>Choose how a new sound request behaves when the active sound limit is full.</summary>
    public enum SoundOverflowPolicy
    {
        /// <summary>Reject the new request without stopping an existing sound or recording deduplication.</summary>
        RejectNew = 0,
        /// <summary>Stop the earliest admitted active sound before admitting the new request.</summary>
        StopOldest = 1,
        /// <summary>Replace the lowest-priority eligible request, oldest among ties. A lower-priority incoming request is rejected.</summary>
        StopLowestPriority = 2
    }

    /// <summary>Runtime-only limit for active sound requests, including paused, muted and looping sounds, but not music or voice.</summary>
    /// <remarks>The default value is unlimited with RejectNew. Lowering a limit keeps older requests with RejectNew and newer requests with StopOldest.
    /// StopLowestPriority retains higher priorities and newer requests among ties. Clip and global limits share one policy.</remarks>
    [Serializable]
    public struct AudioSoundConcurrency
    {
        /// <summary>Maximum active sound requests. Zero is unlimited; negative values are invalid.</summary>
        public int Limit;
        /// <summary>Policy for admitting sounds at capacity and retaining sounds when the limit is lowered.</summary>
        public SoundOverflowPolicy OverflowPolicy;
        /// <summary>Default maximum active requests per AudioClip instance. Zero is unlimited; explicit Clip overrides take precedence, but never bypass the global limit.</summary>
        public int DefaultClipLimit;

        internal void Validate()
        {
            if (Limit < 0) throw new ArgumentOutOfRangeException(nameof(Limit));
            if (DefaultClipLimit < 0) throw new ArgumentOutOfRangeException(nameof(DefaultClipLimit));
            if ((uint)OverflowPolicy > (uint)SoundOverflowPolicy.StopLowestPriority)
                throw new ArgumentOutOfRangeException(nameof(OverflowPolicy));
        }
    }
}
