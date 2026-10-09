using UnityEngine;
using YLFramework.Services.Audio;

namespace YLFramework.Tests.Audio
{
    public sealed class AudioBudgetStopOnDisable : MonoBehaviour
    {
        public AudioHandle Other;
        public int Calls;
        public bool DisableCache;

        private void OnDisable()
        {
            Calls++;
            if (DisableCache && Calls == 1) AudioKit.SoundCacheCapacity = 0;
            Other.Stop();
        }
    }
}
