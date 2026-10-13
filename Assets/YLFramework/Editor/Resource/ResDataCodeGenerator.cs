using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CSharp;
using UnityEditor;

namespace YLFramework.Editor.Resource
{
    /// <summary>根据有效的 AssetBundle 分组生成资源名常量；失败不向 writer 写入部分源码。</summary>
    public static class ResDataCodeGenerator
    {
        /// <summary>
        /// 每个 Bundle 生成一个公开类，BundleName 保留 Bundle 名，成员值保留文件名去一次扩展名后的原始资源键。
        /// 标识符使用 invariant 首字母大写、非法字符替换为下划线及数字前缀；空名称和身份碰撞明确报错。
        /// writer 由调用方拥有，校验及生成完成后一次写入；不承诺外部 writer 的 I/O 原子性。
        /// </summary>
        public static void WriteClass(TextWriter writer, string @namespace)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.Write(Generate(Collect(), @namespace));
        }

        internal static AssetBundleInfo[] Collect()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请在非播放、非编译、非导入状态生成资源常量。");
            var defaults = new SubProjectData();
            var owners = SubProjectData.SearchAllInProject();
            // 与 6.2 使用相同的来源、目录、Bundle 和依赖归属预检，不读取或接管 Bundle 输出。
            SubProjectData.SplitAssetBundles2DefaultAndSubProjectDatas(defaults, owners);
            return FromBuilds(new[] { defaults }.Concat(owners).SelectMany(owner => owner.Builds));
        }

        internal static AssetBundleInfo[] FromBuilds(IEnumerable<AssetBundleBuild> builds) => builds.Select(build =>
            new AssetBundleInfo(build.assetBundleName) { Assets = build.assetNames.Select(Path.GetFileNameWithoutExtension).ToArray() }).ToArray();

        internal static string Generate(IEnumerable<AssetBundleInfo> bundles, string @namespace)
        {
            if (bundles == null) throw new ArgumentNullException(nameof(bundles));
            using var provider = new CSharpCodeProvider();
            if (string.IsNullOrWhiteSpace(@namespace) || @namespace.Split('.').Any(part => !provider.IsValidIdentifier(part)))
                throw new ArgumentException("无效的 C# namespace：" + @namespace, nameof(@namespace));
            var unit = new CodeCompileUnit();
            var codeNamespace = new CodeNamespace(@namespace);
            unit.Namespaces.Add(codeNamespace);
            var classes = new Dictionary<string, string>(StringComparer.Ordinal);
            var inputs = bundles.ToArray();
            if (inputs.Length == 0) throw new InvalidOperationException("没有有效 Bundle 资源清单，旧常量源码未修改。");
            if (inputs.Any(bundle => bundle == null)) throw new InvalidOperationException("Bundle 输入不能为 null。");
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name != ResReferenceOutput.AssemblyName))
            foreach (var existing in assembly.GetTypes())
            {
                if (existing.FullName != null) occupied.Add(existing.FullName);
                var space = existing.Namespace;
                while (space != null && space.StartsWith(@namespace + ".", StringComparison.Ordinal))
                {
                    occupied.Add(space);
                    space = space.Substring(0, space.LastIndexOf('.'));
                }
            }
            foreach (var bundle in inputs.OrderBy(bundle => bundle.Name, StringComparer.Ordinal))
            {
                ResourceBuildOperation.ValidateBundleName(bundle.Name);
                var className = Identifier(bundle.Name, provider);
                if (!classes.TryAdd(className, bundle.Name))
                    throw new InvalidOperationException("Bundle 类名碰撞：" + classes[className] + " / " + bundle.Name + " -> " + className);
                var fullName = @namespace + "." + className;
                if (occupied.Contains(fullName))
                    throw new InvalidOperationException("Bundle 类名与已有类型或 namespace 冲突：" + bundle.Name + " -> " + fullName);
                if (bundle.Assets == null || bundle.Assets.Length == 0)
                    throw new InvalidOperationException("Bundle 资源清单为空：" + bundle.Name);
                var type = new CodeTypeDeclaration(className);
                codeNamespace.Types.Add(type);
                AddConstant(type, "BundleName", bundle.Name);
                var members = new Dictionary<string, string>(StringComparer.Ordinal) { { "BundleName", "保留的 BundleName" }, { className, "所属类名" } };
                var resourceKeys = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var key in bundle.Assets.OrderBy(key => key, StringComparer.Ordinal))
                {
                    var member = Identifier(key, provider);
                    if (!resourceKeys.TryAdd(key.ToLowerInvariant(), key))
                        throw new InvalidOperationException("同 Bundle 的运行时资源键碰撞：" + bundle.Name + "：" + resourceKeys[key.ToLowerInvariant()] + " / " + key);
                    if (!members.TryAdd(member, key))
                        throw new InvalidOperationException("资源成员名碰撞：" + bundle.Name + "：" + members[member] + " / " + key + " -> " + member);
                    AddConstant(type, member, key);
                }
            }
            using var writer = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };
            provider.GenerateCodeFromCompileUnit(unit, writer, new CodeGeneratorOptions { BlankLinesBetweenMembers = false, BracingStyle = "C" });
            return writer.ToString().Replace("\r\n", "\n");
        }

        private static string Identifier(string value, CSharpCodeProvider provider)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.Any(char.IsLetterOrDigit) || value.Any(char.IsControl))
                throw new InvalidOperationException("资源/Bundle 名称为空或不能生成有效标识符：" + value);
            var identifier = new string(value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            identifier = char.ToUpperInvariant(identifier[0]) + identifier.Substring(1);
            if (char.IsDigit(identifier[0])) identifier = "_" + identifier;
            if (!provider.IsValidIdentifier(identifier)) throw new InvalidOperationException("不能生成有效 C# 标识符：" + value);
            return identifier;
        }

        private static void AddConstant(CodeTypeDeclaration type, string name, string value) => type.Members.Add(new CodeMemberField(typeof(string), name)
        {
            Attributes = MemberAttributes.Public | MemberAttributes.Const,
            InitExpression = new CodePrimitiveExpression(value)
        });
    }
}
