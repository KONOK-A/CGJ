using UnityEngine;
using YLFramework.Services.Audio;

namespace YLFramework.Tests.Audio
{
    public sealed class AudioBudgetStopOnDisable : MonoBehaviour
    {
        public AudioHandle Other;
        public int Calls;

        private void OnDisable()
        {
            Calls++;
            Other.Stop();
        }
    }
}
