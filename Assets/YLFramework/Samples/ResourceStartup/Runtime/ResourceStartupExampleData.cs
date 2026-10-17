using UnityEngine;

namespace YLFramework.Samples.Resource
{
    /// <summary>独立示例的编译资产类型；Bundle配置必须显式传入typeof(ResourceStartupExampleData)。</summary>
    public sealed class ResourceStartupExampleData : ScriptableObject
    {
        public string Text;
    }
}
