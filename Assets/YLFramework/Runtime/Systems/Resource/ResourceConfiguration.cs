using System;
using System.Collections.Generic;

namespace YLFramework.Services.Resource
{
    /// <summary>显式只读配置。8.1 默认仅装配 Resources；不读取旧设置，不扫描工程，不定义磁盘格式。</summary>
    public sealed class ResourceConfiguration
    {
        public IReadOnlyList<ResourceCatalog> Catalogs { get; }
        internal IReadOnlyList<IResourceSource> Sources { get; }

        public ResourceConfiguration(IEnumerable<ResourceCatalog> catalogs)
            : this(catalogs, new IResourceSource[] { new UnityResourcesSource() }) { }

        internal ResourceConfiguration(IEnumerable<ResourceCatalog> catalogs, IEnumerable<IResourceSource> sources)
        {
            Catalogs = ResourceCatalog.Copy(catalogs ?? throw new ArgumentNullException(nameof(catalogs)));
            Sources = ResourceCatalog.Copy(sources ?? throw new ArgumentNullException(nameof(sources)));
        }
    }
}
