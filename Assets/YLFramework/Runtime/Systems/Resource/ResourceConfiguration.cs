using System;
using System.Collections.Generic;

namespace YLFramework.Services.Resource
{
    /// <summary>显式只读配置。默认装配 Resources、本地/URL Texture；已验证快照由唯一 ResourceSystem 装配 Bundle 来源，不读取旧设置。</summary>
    public sealed class ResourceConfiguration
    {
        public IReadOnlyList<ResourceCatalog> Catalogs { get; }
        internal IReadOnlyList<IResourceSource> Sources { get; }

        public ResourceConfiguration(IEnumerable<ResourceCatalog> catalogs)
            : this(catalogs, new IResourceSource[] { new UnityResourcesSource(), new TextureResourceSource(true), new TextureResourceSource(false) }) { }

        internal ResourceConfiguration(IEnumerable<ResourceCatalog> catalogs, IEnumerable<IResourceSource> sources)
        {
            Catalogs = ResourceCatalog.Copy(catalogs ?? throw new ArgumentNullException(nameof(catalogs)));
            Sources = ResourceCatalog.Copy(sources ?? throw new ArgumentNullException(nameof(sources)));
        }
    }
}
