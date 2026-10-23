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
            internal readonly string Id;
            internal readonly string Title;
            internal readonly string Group;
            internal readonly string Summary;
            internal readonly string Behavior;
            internal readonly string Notes;
            internal readonly string ExampleContext;
            internal readonly string Example;
            internal readonly string[] TypeNames;
            internal string[] FeaturedTypeNames;
            internal string RequiredAssembly;
            internal bool IsCompleteExample => Id == "overview";
            internal bool IsAvailable => RequiredAssembly == null || ApiAssemblies().Any(assembly => assembly.GetName().Name == RequiredAssembly);

            internal Topic(string id, string title, string group, string summary, string behavior, string notes,
                string exampleContext, string example, params string[] typeNames)
            {
                Id = id;
                Title = title;
                Group = group;
                Summary = summary;
                Behavior = behavior;
                Notes = notes;
                ExampleContext = exampleContext;
                Example = example;
                TypeNames = typeNames;
                FeaturedTypeNames = typeNames;
            }

            internal bool Matches(string search) => Contains(SearchText, search);
            internal string SearchText => Title + "\n" + Summary + "\n" + Behavior + "\n" + Notes + "\n" + ExampleContext + "\n" +
                Example + "\n" + string.Join(" ", TypeNames);
        }

        internal static Topic[] Topics => ActionKitApiHelpGuides.Topics;

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
                {
                    throw new InvalidOperationException("DG.Tweening.Tween 必须由 wrapper 引用的 DOTween 程序集定义。");
                }

                assemblies.Add(tween.Assembly);
            }
            return assemblies.Select(assembly => assembly.Location).Distinct().ToArray();
        }

        internal static HelpApiType[] BuildIndex() => HelpApiIndex.Build(ActionKitHelpProvider.CreateModule());

        internal static IEnumerable<Topic> RelatedTopics(Type type)
        {
            var root = type;
            while (root.IsNested && root != typeof(ActionKit.ScreenTransition))
            {
                root = root.DeclaringType;
            }

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
