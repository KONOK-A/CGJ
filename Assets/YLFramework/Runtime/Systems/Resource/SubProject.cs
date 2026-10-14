using UnityEngine;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 子工程描述资产：构建时把工程内的 AssetBundle 按资产所在目录归入默认工程或某个子工程。
    /// <para>
    /// 与构建流程的衔接：Editor 侧 SubProjectData 按本类型检索工程内的资产，
    /// 资产所在目录中标记的 AssetBundle 会归入该子工程的构建与数据表。
    /// </para>
    /// </summary>
    [CreateAssetMenu]
    public class SubProject : ScriptableObject
    {
        /// <summary>子工程的命名空间（序列化字段，当前代码未读取）。</summary>
        public string @namespace;
    }
}