namespace YLFramework.Audio
{
    /// <summary>Allocation-free session snapshot of Sound requests, idle cache and owned sources. Music and Voice are excluded.</summary>
    /// <remarks>Release counts mean ownership was relinquished and destruction requested, not that Unity destroyed native objects synchronously.</remarks>
    public readonly struct AudioSoundStats
    {
        /// <summary>Current effective requests, including paused, muted and looping sounds.</summary>
        public int ActiveCount
        {
            get;
        }
        /// <summary>Largest effective request count since the last reset.</summary>
        public int PeakActiveCount
        {
            get;
        }
        /// <summary>Current idle cached voices, excluding active requests.</summary>
        public int CachedCount
        {
            get;
        }
        /// <summary>Current Sound sources owned by this session, including idle sources.</summary>
        public int OwnedSourceCount
        {
            get;
        }
        /// <summary>Committed admissions, including those that subsequently fail to start.</summary>
        public long AdmittedCount
        {
            get;
        }
        /// <summary>Exceptions while configuring or starting an admitted request.</summary>
        public long StartFailureCount
        {
            get;
        }
        /// <summary>Requests rejected by frame-window deduplication.</summary>
        public long DedupRejectedCount
        {
            get;
        }
        /// <summary>Requests rejected by global capacity when Clip capacity did not block them.</summary>
        public long GlobalRejectedCount
        {
            get;
        }
        /// <summary>Requests rejected by Clip capacity, including when both capacities are full.</summary>
        public long ClipRejectedCount
        {
            get;
        }
        /// <summary>Requests rejected because their priority was lower than the eligible victim.</summary>
        public long PriorityRejectedCount
        {
            get;
        }
        /// <summary>Existing requests stopped to admit a replacement.</summary>
        public long StolenCount
        {
            get;
        }
        /// <summary>Requests stopped to satisfy changed runtime limits.</summary>
        public long TrimmedCount
        {
            get;
        }
        /// <summary>Playback allocations served by the idle cache; prewarming is excluded.</summary>
        public long CacheHitCount
        {
            get;
        }
        /// <summary>Playback allocations requiring a new voice; prewarming is excluded.</summary>
        public long CacheMissCount
        {
            get;
        }
        /// <summary>Real Sound AudioSource creations, including prewarming.</summary>
        public long SourceCreatedCount
        {
            get;
        }
        /// <summary>Sound source ownership releases and destruction requests.</summary>
        public long SourceReleasedCount
        {
            get;
        }

        internal AudioSoundStats(AudioSoundCounters counters, int active, int cached)
        {
            ActiveCount = active;
            CachedCount = cached;
            PeakActiveCount = counters.PeakActiveCount;
            OwnedSourceCount = counters.OwnedSourceCount;
            AdmittedCount = counters.AdmittedCount;
            StartFailureCount = counters.StartFailureCount;
            DedupRejectedCount = counters.DedupRejectedCount;
            GlobalRejectedCount = counters.GlobalRejectedCount;
            ClipRejectedCount = counters.ClipRejectedCount;
            PriorityRejectedCount = counters.PriorityRejectedCount;
            StolenCount = counters.StolenCount;
            TrimmedCount = counters.TrimmedCount;
            CacheHitCount = counters.CacheHitCount;
            CacheMissCount = counters.CacheMissCount;
            SourceCreatedCount = counters.SourceCreatedCount;
            SourceReleasedCount = counters.SourceReleasedCount;
        }
    }

    internal struct AudioSoundCounters
    {
        internal int PeakActiveCount;
        internal int OwnedSourceCount;
        internal long AdmittedCount;
        internal long StartFailureCount;
        internal long DedupRejectedCount;
        internal long GlobalRejectedCount;
        internal long ClipRejectedCount;
        internal long PriorityRejectedCount;
        internal long StolenCount;
        internal long TrimmedCount;
        internal long CacheHitCount;
        internal long CacheMissCount;
        internal long SourceCreatedCount;
        internal long SourceReleasedCount;
    }
}
