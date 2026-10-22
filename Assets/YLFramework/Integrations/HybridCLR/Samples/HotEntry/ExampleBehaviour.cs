using UnityEngine;
using UnityEngine.Scripting;

namespace YLFramework.Samples.HybridCLR.HotUpdate
{
    [Preserve]
    public sealed class ExampleBehaviour : MonoBehaviour
    {
        public string Marker => VersionInfo.Version + "-prefab";
    }
}
