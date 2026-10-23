using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using YLFramework.Services.Resource;

namespace YLFramework.Editor.Resource
{
    /// <summary>资源项目目录的统一解析入口。读取不保存、不创建目录；来源准入不授予写权限。</summary>
    public sealed class ResourceProjectPaths
    {
        public sealed class DirectoryEntry
        {
            public string Guid
            {
                get;
            }
            public string Path
            {
                get;
            }
            public string AddressPrefix
            {
                get;
            }
            internal DirectoryEntry(ResourceSourceDirectory source, string path)
            {
                Guid = source.guid;
                Path = path;
                AddressPrefix = source.addressPrefix ?? "";
            }
        }

        public IReadOnlyList<DirectoryEntry> Directories
        {
            get;
        }
        private ResourceProjectPaths(IEnumerable<DirectoryEntry> directories) => Directories = Array.AsReadOnly(directories.ToArray());

        public static ResourceProjectPaths Resolve(ResourceProject project, bool validateProjects = true)
        {
            if (!project)
            {
                throw new ArgumentNullException(nameof(project));
            }

            if (!string.IsNullOrEmpty(project.automaticAtlasConfigurationGuid) && (!project.automaticAtlases ||
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project.automaticAtlases)) != project.automaticAtlasConfigurationGuid))
            {
                throw new InvalidOperationException("自动图集引用与原登记身份不一致。请恢复已登记配置；原生成目录仍受保护，不会被接管。");
            }

            var entries = ResolveDefinitions(project.sourceDirectories);
            if (entries.Length == 0)
            {
                throw new InvalidOperationException("尚未设置资源目录。请在项目设置中添加源资源所在的文件夹。");
            }

            if (validateProjects)
            {
                ValidateProjects(project);
            }

            return new ResourceProjectPaths(entries);
        }

        internal static DirectoryEntry[] ResolveDefinitions(IEnumerable<ResourceSourceDirectory> definitions)
        {
            if (definitions == null)
            {
                throw new InvalidOperationException("资源目录列表缺失。");
            }

            var result = new List<DirectoryEntry>();
            foreach (var definition in definitions)
            {
                if (result.Count == 256)
                {
                    throw new InvalidOperationException("资源目录不能超过256个。");
                }

                var path = ResolveFolder(definition?.guid);
                var prefix = definition.addressPrefix ?? "";
                if (prefix.Length != 0)
                {
                    try
                    {
                        _ = new ResourceAddress("directory", prefix + "/entry");
                    }
                    catch (ArgumentException error)
                    {
                        throw new InvalidOperationException("目录地址前缀无效：" + prefix + "\n" + path, error);
                    }
                }
                foreach (var existing in result)
                {
                    if (existing.Guid == definition.guid || Overlaps(existing.Path, path))
                    {
                        throw new InvalidOperationException("资源目录重复或重叠 (overlap)：\n" + existing.Path + "\n" + path + "\n请只保留其中一个目录，不会自动合并或修改地址。");
                    }
                }

                result.Add(new DirectoryEntry(definition, path));
            }
            return result.ToArray();
        }

        public bool ContainsSource(string path) => Directories.Any(x => Contains(x.Path, path));

        public void RequireSource(string path)
        {
            if (!ContainsSource(path))
            {
                throw new InvalidOperationException("资源不在当前资源项目的资源目录中 (outside)：" + path +
                "\n请先在‘项目设置’中添加它所在的目录；已有资源条目不会被自动移除。");
            }

            ResourceBuildOperation.ValidateAssetPath(path);
        }

        public string DefaultAddress(string path)
        {
            RequireSource(path);
            return DisplayAddress(path);
        }

        /// <summary>仅供已解析快照展示地址，不扫描文件或授予构建准入；正式入口仍须完整验证路径。</summary>
        internal string DisplayAddress(string path)
        {
            var directory = Directories.SingleOrDefault(x => Contains(x.Path, path)) ??
                throw new InvalidOperationException("资源不在当前目录快照中：" + path);
            var relative = path.Length == directory.Path.Length ? "" : path.Substring(directory.Path.Length + 1);
            return directory.AddressPrefix.Length == 0 ? relative : relative.Length == 0 ? directory.AddressPrefix : directory.AddressPrefix + "/" + relative;
        }

        public static string ResolveFolder(string guid)
        {
            if (guid == null || guid.Length != 32 || guid.Any(x => !Uri.IsHexDigit(x)))
            {
                throw new InvalidOperationException("资源目录需要有效的Unity GUID：" + guid);
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!IsSourceFolder(path))
            {
                throw new InvalidOperationException("资源目录失效或路径不受支持：" + (string.IsNullOrEmpty(path) ? guid : path) +
                "\n请选择Assets下的真实子目录，不能使用StreamingAssets或Packages。");
            }

            ResourceBuildOperation.ValidateAssetPath(path);
            return path;
        }

        public static bool IsSourceFolder(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("Assets/", StringComparison.Ordinal) &&
            AssetDatabase.IsValidFolder(path) && !Contains("Assets/StreamingAssets", path);

        public static bool Contains(string directory, string path) => !string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(path) &&
            (string.Equals(directory, path, StringComparison.OrdinalIgnoreCase) || path.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase));
        public static bool Overlaps(string first, string second) => Contains(first, second) || Contains(second, first);

        /// <summary>即使引用断开，也保留隐藏登记GUID所指生成范围的保护；这里不授予生成或删除权限。</summary>
        public static string GeneratedDirectory(ResourceProject project)
        {
            var registered = project.automaticAtlasConfigurationGuid;
            var visible = project.automaticAtlases ? AssetDatabase.GetAssetPath(project.automaticAtlases) : null;
            var path = string.IsNullOrEmpty(registered) ? visible : AssetDatabase.GUIDToAssetPath(registered);
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        private static IEnumerable<string> ProtectedGeneratedDirectories(ResourceProject project) =>
            new[] { AssetDatabase.GUIDToAssetPath(project.automaticAtlasConfigurationGuid ?? ""),
                project.automaticAtlases ? AssetDatabase.GetAssetPath(project.automaticAtlases) : null }
                .Where(x => !string.IsNullOrEmpty(x)).Select(x => Path.GetDirectoryName(x)?.Replace('\\', '/'))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.OrdinalIgnoreCase);

        public static void ValidateProjects(ResourceProject project, string candidateGenerationDirectory = null)
        {
            var paths = ResolveDefinitions(project.sourceDirectories).Select(x => x.Path).ToArray();
            var generated = candidateGenerationDirectory ?? GeneratedDirectory(project);
            foreach (var guid in AssetDatabase.FindAssets("t:ResourceProject"))
            {
                var other = AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!other || other == project)
                {
                    continue;
                }

                if (string.Equals(other.projectId, project.projectId, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(other.projectId) && ResourceBuildMap.Token(other.projectId) == ResourceBuildMap.Token(project.projectId ?? "")))
                {
                    throw new InvalidOperationException("资源项目身份冲突 (identity/token conflict)：" + other.projectId + "\n" + AssetDatabase.GetAssetPath(other));
                }

                if (other.sourceDirectories == null)
                {
                    throw new InvalidOperationException("资源目录列表缺失：" + AssetDatabase.GetAssetPath(other));
                }

                var otherPaths = other.sourceDirectories.Where(x => x != null).Select(x => AssetDatabase.GUIDToAssetPath(x.guid)).Where(IsSourceFolder).ToArray();
                foreach (var path in paths.Concat(string.IsNullOrEmpty(generated) ? Array.Empty<string>() : new[] { generated }))
                {
                    foreach (var occupied in otherPaths.Concat(ProtectedGeneratedDirectories(other)))
                    {
                        if (Overlaps(path, occupied))
                        {
                            throw new InvalidOperationException("资源项目的目录重叠 (overlap)：\n" + path + "\n" + occupied +
                            "\n另一资源项目：" + other.projectId + "\n来源目录不能取得其他资源项目生成文件的管理权限。");
                        }
                    }
                }
            }
        }

        /// <summary>不含展示顺序的配置身份；供正式冻结/后验使用，不做资源扫描或SHA。</summary>
        public static string SemanticIdentity(ResourceProject project)
        {
            if (!project)
            {
                throw new ArgumentNullException(nameof(project));
            }

            var text = new StringBuilder("resource-directories-v1;");
            void Add(string value)
            {
                value ??= "";
                text.Append(value.Length).Append(':').Append(value).Append(';');
            }
            if (project.sourceDirectories == null)
            {
                throw new InvalidOperationException("资源目录列表缺失。");
            }

            foreach (var source in project.sourceDirectories.OrderBy(x => x?.guid, StringComparer.Ordinal).ThenBy(x => x?.addressPrefix ?? "", StringComparer.Ordinal))
            {
                Add(source?.guid);
                Add(source?.addressPrefix);
            }
            Add(project.automaticAtlasConfigurationGuid);
            Add(project.automaticAtlases ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(project.automaticAtlases)) : "");
            Add(GeneratedDirectory(project));
            return text.ToString();
        }
    }
}
