// Descriptions adapted from the local ActionKit API metadata and guidelines.
// Copyright (c) 2015 - 2025 liangxiegame and the authors named in the runtime sources, MIT License.
// https://github.com/liangxiegame/QFramework
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YLFramework.Actions;
using YLFramework.Editor.Help;

namespace YLFramework.Editor.Actions
{
    internal static class ActionKitApiHelpContent
    {
        internal static readonly string[] GuideGroups = { "入门与生命周期", "基础动作", "组合与执行", "扩展与工具" };

        internal sealed class Topic
        {
            internal readonly string Id, Title, Group, Summary, Behavior, Notes, ExampleContext, Example;
            internal readonly string[] TypeNames;
            internal string[] FeaturedTypeNames;
            internal string RequiredAssembly;
            internal bool IsCompleteExample => Id == "overview";
            internal bool IsAvailable => RequiredAssembly == null || ApiAssemblies().Any(assembly => assembly.GetName().Name == RequiredAssembly);

            internal Topic(string id, string title, string group, string summary, string behavior, string notes,
                string exampleContext, string example, params string[] typeNames)
            {
                Id = id; Title = title; Group = group; Summary = summary; Behavior = behavior; Notes = notes;
                ExampleContext = exampleContext; Example = example; TypeNames = typeNames; FeaturedTypeNames = typeNames;
            }

            internal bool Matches(string search) => Contains(SearchText, search);
            internal string SearchText => Title + "\n" + Summary + "\n" + Behavior + "\n" + Notes + "\n" + ExampleContext + "\n" +
                Example + "\n" + string.Join(" ", TypeNames);
        }

        internal static Topic[] Topics => ActionKitApiHelpGuides.Topics;
        internal const string Attribution = "来源：本地 QFramework ActionKit API 说明及使用指南。\n" +
            "Copyright liangxiegame and contributors, MIT License.\nhttps://github.com/liangxiegame/QFramework\n" +
            "帮助内容依据 YLFramework.Actions 的实现整理；声明来自核心 Runtime 与当前已加载的明确可选程序集。原始作者与年份声明保留在各源码文件中。";
        internal const string DOTweenLicenseUrl = "https://dotween.demigiant.com/license.php";
        internal const string DOTweenLicenseNotice = "ActionKit DOTween wrapper 使用 MIT License；DOTween 本体不是 MIT，" +
            "使用 Demigiant 的 Artistic License（https://dotween.demigiant.com/license.php）。当前安装版本为 1.2.825。" +
            "默认使用 standard 供应商版本并保留其 readme；本模块不打包修改版供应商。";
        internal const string LicenseText =
            "Permission is hereby granted, free of charge, to any person obtaining a copy\n" +
            "of this software and associated documentation files (the Software), to deal\n" +
            "in the Software without restriction, including without limitation the rights\n" +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell\n" +
            "copies of the Software, and to permit persons to whom the Software is\n" +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in\n" +
            "all copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\n" +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\n" +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\n" +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\n" +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\n" +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN\n" +
            "THE SOFTWARE.";

        internal static IEnumerable<Assembly> ApiAssemblies() => new[] { typeof(ActionKit).Assembly }
            .Concat(AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                assembly.GetName().Name == "YLFramework.Actions.DOTween")).Distinct();

        internal static string[] GetReferenceAssemblies()
        {
            var assemblies = ApiAssemblies().ToList();
            var wrapper = assemblies.SingleOrDefault(assembly => assembly.GetName().Name == "YLFramework.Actions.DOTween");
            if (wrapper != null)
            {
                var dependency = wrapper.GetReferencedAssemblies().Single(reference => reference.Name == "DOTween");
                var dotween = AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
                    assembly.GetName().Name == "DOTween" && assembly.GetName().FullName == dependency.FullName);
                var tween = dotween.GetType("DG.Tweening.Tween", true);
                if (tween.Assembly != dotween)
                    throw new InvalidOperationException("DG.Tweening.Tween 必须由 wrapper 引用的 DOTween 程序集定义。");
                assemblies.Add(tween.Assembly);
            }
            return assemblies.Select(assembly => assembly.Location).Distinct().ToArray();
        }

        internal static HelpApiType[] BuildIndex() => HelpApiIndex.Build(ActionKitHelpProvider.CreateModule());

        internal static IEnumerable<Topic> RelatedTopics(Type type)
        {
            var root = type;
            while (root.IsNested && root != typeof(ActionKit.ScreenTransition)) root = root.DeclaringType;
            var name = root.Name.Split('`')[0];
            return Topics.Where(topic => topic.TypeNames.Contains(name));
        }

        internal static IEnumerable<HelpApiType> FeaturedTypes(Topic topic, HelpApiType[] index) => index.Where(entry =>
            (!entry.Type.IsNested && topic.FeaturedTypeNames.Contains(entry.Type.Name.Split('`')[0])) ||
            topic.FeaturedTypeNames.Contains(entry.Name));

        internal static bool Contains(string text, string search) => HelpSearch.Contains(text, search);
        internal static IEnumerable<MemberInfo> GetDeclaredApi(Type type) => HelpApiFormatter.GetDeclaredApi(type);
        internal static string FormatType(Type type) => HelpApiFormatter.FormatType(type, "YLFramework.Actions");
        internal static string FormatDeclaration(Type type) => HelpApiFormatter.FormatDeclaration(type, "YLFramework.Actions");
        internal static string FormatSignature(MemberInfo member) => HelpApiFormatter.FormatSignature(member, "YLFramework.Actions");

        internal static string ExampleCompilationSource() =>
            "using System;\nusing System.Collections;\nusing System.Threading.Tasks;\nusing UnityEngine;\n" +
            "using YLFramework.Actions;\n" +
            "using YLFramework.Events;\n" +
            (ApiAssemblies().Any(assembly => assembly.GetName().Name == "YLFramework.Actions.DOTween") ? "using DG.Tweening;\n" : "") +
            "namespace ActionKitHelpExamples\n{\npublic static class Usage\n{\n" +
            string.Join("\n\n", Topics.Where(topic => topic.IsAvailable && !topic.IsCompleteExample && topic.Example.Length != 0).Select(topic => topic.Example)) +
            "\n}\n}\n";

        internal static string GettingStartedSource => Topics.Single(topic => topic.IsCompleteExample).Example;
    }
}
