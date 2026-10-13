using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace YLFramework.Editor.Resource
{
    // 项目生成物的窄承载契约，不为未知业务源码创建/改写程序集。
    internal static class ResReferenceOutput
    {
        internal const string DirectoryPath = "Assets/YLFrameworkData";
        internal const string AssemblyName = "YLFramework.Generated.ResourceNames";
        internal const string SourceName = "YLResData.cs";
        private const string Header = "// YL Resource names v1 SHA256: ";
        internal const string AssemblyDefinition = "{\n  \"name\": \"" + AssemblyName + "\",\n  \"references\": [],\n  \"autoReferenced\": true,\n  \"overrideReferences\": true,\n  \"precompiledReferences\": [],\n  \"noEngineReferences\": true\n}\n";

        internal static void Publish(string directory, string source)
        {
            var content = Header + Hash(source) + "\n" + source;
            var path = Path.Combine(directory, SourceName);
            var asmdef = Path.Combine(directory, AssemblyName + ".asmdef");
            ResourceBuildOperation.ValidateAssetPath(directory);
            // 根级 asmdef/asmref 会改变新目录的身份；不能静默改为本工具的子程序集。
            var parent = Path.GetDirectoryName(Path.GetFullPath(directory));
            while (parent != null && parent.Length >= Path.GetFullPath("Assets").Length)
            {
                if (Directory.EnumerateFiles(parent).Any(file => file.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                                                               file.EndsWith(".asmref", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("生成位置已有上级程序集配置，请先确定消费者承载：" + parent);
                parent = Path.GetDirectoryName(parent);
            }
            if (File.Exists(directory)) throw new InvalidOperationException("生成目录被文件占用：" + directory);
            var existed = Directory.Exists(directory);
            if (!existed && File.Exists(directory + ".meta"))
                throw new InvalidOperationException("生成目录存在未知孤立 meta，拒绝接管：" + directory);
            if (existed)
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                    if (entry != path && entry != asmdef && entry != path + ".meta" && entry != asmdef + ".meta")
                        throw new InvalidOperationException("生成目录含未知源码或资产，拒绝改变程序集身份：" + entry);
            var hadSource = File.Exists(path);
            var hadAssembly = File.Exists(asmdef);
            if (hadSource != hadAssembly || (!hadSource && (File.Exists(path + ".meta") || File.Exists(asmdef + ".meta"))))
                throw new InvalidOperationException("生成物/程序集不完整或属于旧承载，请先确认迁移：" + directory);
            if (hadSource)
            {
                var old = File.ReadAllText(path);
                var newline = old.IndexOf('\n');
                if (newline < 0 || old.Substring(0, newline) != Header + Hash(old.Substring(newline + 1)) ||
                    File.ReadAllText(asmdef) != AssemblyDefinition)
                    throw new InvalidOperationException("已有源码被手写/修改或程序集配置不匹配，拒绝覆盖：" + directory);
                if (old == content) return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset"))
            {
                var other = AssetDatabase.GUIDToAssetPath(guid);
                if (string.Equals(Path.GetFullPath(other), Path.GetFullPath(asmdef), StringComparison.OrdinalIgnoreCase)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(other);
                if (!asset) throw new InvalidOperationException("程序集资产不可读取：" + other);
                if (JsonUtilityName(asset.text) == AssemblyName)
                    throw new InvalidOperationException("生成程序集名称已被其他位置占用：" + other);
            }
            var temporary = Path.Combine(directory, ".yl-names-" + Guid.NewGuid().ToString("N"));
            var assemblyTemporary = temporary + ".asmdef-tmp";
            var createdAssembly = false;
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(temporary, content, new UTF8Encoding(false));
                if (!hadAssembly)
                {
                    File.WriteAllText(assemblyTemporary, AssemblyDefinition, new UTF8Encoding(false));
                    File.Move(assemblyTemporary, asmdef);
                    createdAssembly = true;
                }
                // 同目录暂存后替换文件本体，绝不删除/重建已有 .meta。
                if (hadSource) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch
            {
                if (createdAssembly) File.Delete(asmdef);
                throw;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                if (File.Exists(assemblyTemporary)) File.Delete(assemblyTemporary);
                if (!existed && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
        }

        private static string Hash(string text)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        [Serializable]
        private sealed class AssemblyIdentity { public string name; }
        private static string JsonUtilityName(string json) => UnityEngine.JsonUtility.FromJson<AssemblyIdentity>(json)?.name;
    }
}
