using UnityEngine;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreSmokeSceneEntry : MonoBehaviour
    {
        public int StartCount { get; private set; }
        public YLCoreSmokeModule ModuleAtStart { get; private set; }

        private void Start()
        {
            StartCount++;
            ModuleAtStart = YLFramework.Core.ModuleManager.GetModule<YLCoreSmokeModule>();
        }
    }
}
