using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YLFramework.Actions;
using YLFramework.Editor.Help;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Editor.Actions
{
    [InitializeOnLoad]
    internal static class ActionKitHelpProvider
    {
        internal const string ModuleId = "actionkit";
        internal const string MenuPath = "YLFramework/Action/帮助";

        static ActionKitHelpProvider() => HelpRegistry.Register(ModuleId, "ActionKit", CreateModule);

        [MenuItem(MenuPath)]
        private static void Open() => HelpWindow.Open(ModuleId);

        internal static HelpModule CreateModule()
        {
            var assemblies = ActionKitApiHelpContent.ApiAssemblies().ToArray();
            var types = assemblies.SelectMany(assembly => assembly.GetExportedTypes())
                .Where(type => type.Namespace == "YLFramework.Actions" ||
                    (type.Namespace != null && type.Namespace.StartsWith("YLFramework.Actions.", StringComparison.Ordinal)))
                .Where(type => type.Assembly == typeof(ActionKit).Assembly ||
                    type.FullName == "YLFramework.Actions.DOTweenAction" || type.FullName == "YLFramework.Actions.ActionKitDOTweenExtension")
                .OrderBy(ActionKitApiHelpReference.TypeGroup, StringComparer.Ordinal)
                .ThenBy(ActionKitApiHelpContent.FormatType, StringComparer.Ordinal).ToArray();
            return new HelpModule
            {
                Guides = ActionKitApiHelpContent.GuideGroups.SelectMany(group => ActionKitApiHelpContent.Topics
                    .Where(topic => topic.Group == group).Select(topic => CreateGuide(topic, types))).ToArray(),
                About = CreateAbout(),
                ApiTypes = types.Select(type => new HelpApiTypeDefinition(type, ActionKitApiHelpReference.TypeGroup(type))).ToArray(),
                XmlPaths = assemblies.ToDictionary(assembly => assembly, assembly => Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "Library", assembly.GetName().Name + ".xml"))),
                Documentation = ActionKitApiHelpReference.Describe
            };
        }

        private static HelpPage CreateGuide(ActionKitApiHelpContent.Topic topic, Type[] types)
        {
            var blocks = new List<HelpBlock>();
            if (!topic.IsAvailable)
                blocks.Add(HelpBlock.Notice("可选程序集未加载", "此指南使用可选程序集 " + topic.RequiredAssembly +
                    "，当前未加载。代码仅供查阅，接入前需要安装并引用该程序集。"));
            if (topic.Example.Length != 0)
                blocks.Add(HelpBlock.Code("常用用法", topic.ExampleContext, topic.Example));
            else if (!string.IsNullOrWhiteSpace(topic.ExampleContext))
                blocks.Add(HelpBlock.Paragraph("使用方式", topic.ExampleContext));
            if (!string.IsNullOrWhiteSpace(topic.Behavior))
                blocks.Add(HelpBlock.Paragraph("参数、执行与生命周期", topic.Behavior));
            if (!string.IsNullOrWhiteSpace(topic.Notes))
                blocks.Add(HelpBlock.Notice("注意事项", topic.Notes));
            return new HelpPage(topic.Id, topic.Title, topic.Group, topic.Summary, blocks.ToArray())
            {
                Keywords = topic.TypeNames,
                RelatedTypes = types.Where(type => ActionKitApiHelpContent.RelatedTopics(type).Contains(topic)).ToArray(),
                FeaturedTypes = types.Where(type => (!type.IsNested && topic.FeaturedTypeNames.Contains(type.Name.Split('`')[0])) ||
                    topic.FeaturedTypeNames.Contains(ActionKitApiHelpContent.FormatType(type))).ToArray()
            };
        }

        private static HelpPage[] CreateAbout() => new[]
        {
            new HelpPage("usage", "使用与版本", "关于与许可", "",
                HelpBlock.Paragraph("", "YLFramework.Actions 是 YLFramework 内建的 Unity 动作编排模块，包含 BCL Observable 适配。使用方引用 YLFramework.Runtime 接入；" +
                    "沿用 YLFramework 的依赖契约，动作核心不要求 DOTween；可选包装程序集为 YLFramework.Actions.DOTween。\n\n" +
                    "Unity 版本：" + Application.unityVersion + "\nRuntime 程序集版本：" + typeof(ActionKit).Assembly.GetName().Version +
                    "\n程序集版本来自编译元数据，不代表单独的模块发布版本。\n\n" +
                    "帮助窗口只供查阅和复制，不执行示例、不创建动作宿主。API 声明来自当前编译程序集；说明按模块源码整理。")),
            new HelpPage("source", "来源与版权", "关于与许可", "",
                HelpBlock.Paragraph("", ActionKitApiHelpContent.Attribution + "\n\n" +
                    "DOTween 可选扩展的包装源码保留 liangxiegame 的 MIT 声明；DOTween 库由 Demigiant 提供，适用其独立 Artistic 许可，并非 MIT。" +
                    "本工程核对的版本为 1.2.825。分发标准库时需保留原 copyright、免责声明及 readme.txt；不要把修改后的供应商库按本模块许可分发。\n" +
                    "版权：Copyright (c) 2014-2018 Daniele Giardini - Demigiant\nhttps://dotween.demigiant.com/license.php"),
                HelpBlock.Link("QFramework 源码与作者项目", "https://github.com/liangxiegame/QFramework"),
                HelpBlock.Link("DOTween 官方许可证", ActionKitApiHelpContent.DOTweenLicenseUrl)),
            new HelpPage("license", "MIT 许可证", "关于与许可", "",
                HelpBlock.Paragraph("", "以下 MIT 许可适用于 ActionKit 源码及适配包装，不替代 DOTween 等第三方库的独立许可；见“来源与版权”。" +
                    "分发时保留原始作者声明和许可文本。\n" + ActionKitApiHelpContent.LicenseText))
        };
    }
}
