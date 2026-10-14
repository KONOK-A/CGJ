using System;
using System.IO;
using System.Linq;
using UnityEditor;
using YLFramework.Editor.Help;
using YLFramework.Core.RoslynAnalyzers;

namespace YLFramework.Core.Editor
{
    [InitializeOnLoad]
    internal static class CoreHelpProvider
    {
        private const string ModuleId = "core";

        static CoreHelpProvider() => HelpRegistry.Register(ModuleId, "Core", CreateModule);

        [MenuItem("YLFramework/Core/帮助")]
        private static void Open() => HelpWindow.Open(ModuleId);

        private static HelpModule CreateModule()
        {
            var guides = new[]
            {
                Guide("module-mvc", "Module 与 MVC", "Module 是业务所有者，Model 保存状态，Controller 协调逻辑，View 负责表现。",
                    new[] { typeof(Module), typeof(SingletonModule<>), typeof(ModuleManager), typeof(Model), typeof(Controller) },
                    HelpBlock.Paragraph("显式启动与结束", "派生 Module 或 SingletonModule<T>，实现 OnStart，并通过 ModuleManager.StartUpModule<T>() 启动。" +
                        "启动请求完成后再使用模块；SingletonModule<T>.Instance 只查询已注册实例，不会创建。Module.Close 结束普通模块，基础模块由 ModuleManager.Shutdown 统一结束。"),
                    HelpBlock.Paragraph("MVC 所有权", "通过 Module.AddModel / GetModel、AddController / GetController 管理注册，不从 View 改写 Model 的受保护状态。" +
                        "Model/Controller 的 OnInit 与 OnExit 对应每次注册；Module 负责退出，业务监听与借用资源仍须按自身所有权收尾。"),
                    HelpBlock.Notice("接入边界", "业务 asmdef 显式引用 YLFramework.Runtime；Core、Foundation 和 ZipTool 共用该主体程序集，仍按职责组织源码。" +
                        "Core 保留 XFABManager、UGUI 依赖，ProjectName 是 XFAB 项目名，不是 YL Resource 的 BundleName；本次不融合资源后端。")),
                Guide("view-ui", "View 与 UI", "复用 Module/View 的唯一会话；WindowCatalog 是纯代码定义目录，不是另一套窗口宿主。",
                    new[] { typeof(View), typeof(Panel), typeof(LoadViewArgs), typeof(LoadViewRequest<>), typeof(WindowCatalog), typeof(WindowDefinition), typeof(UIRootContext) },
                    HelpBlock.Paragraph("加载与参数", "场景或动态对象使用 owner.AttachView(view) 显式纳管；Prefab/具名资产使用 Module.LoadView / LoadPanel 或异步入口。" +
                        "异步请求检查 Status/error，必要时 Abort。LoadViewArgs.New() 的参数交给正式加载入口接管，只在 OnLoaded / OnWindowUpdated 借读，不存进字段、属性或容器。"),
                    HelpBlock.Paragraph("生命周期", "在 Awake 的直接方法体使用 RegisterEvent 声明订阅，框架按有效启用轮次管理。覆写 Start 时直接调用 base.Start()。" +
                        "使用 OnViewEnabled、OnViewDisabled、OnClosed，不声明 OnEnable/OnDisable/OnDestroy 遮蔽框架消息。关闭走 View.Close，不能靠保存旧 lease 复用会话。"),
                    HelpBlock.Paragraph("窗口与根", "WindowCatalog.Register(WindowDefinition) 保存定义；Open/OpenAsync 接受有效 owner、key 与实际 Canvas 根。" +
                        "Unique 范围为 owner/key/根，目录不持有活跃窗口。UIRootContext 只配置 Canvas、Camera、层与模态遮罩，不拥有 Module；可复用 Prefabs/YLFramework.prefab。" +
                        "Camera 由调用方管理；输入桥借用已有有效 EventSystem，缺失时按实际输入后端创建，不由 Help 创建。"),
                    Document("UI 接入、Tips 与 ColorSelector", "Modules/UI.md")),
                Guide("authoring", "创建与生成入口", "创建操作留在所属工具，帮助只提供导航，不执行生成器。",
                    new[] { typeof(Module), typeof(WindowCatalog) },
                    HelpBlock.Paragraph("Module 模板", "Assets/Create/YLFramework/Core/Module 输出 Assets/YLFramework/Examples/Core/Modules 下的独立示例和 asmdef，拒绝覆盖已有目录。" +
                        "namespace 为 YLFramework.Core.Examples；不会生成完整 MVC/View/Panel 代码或改写 Assets/Games。"),
                    HelpBlock.Paragraph("默认资源与资源常量", "YLFramework/Core/Create Missing Default Resources 只补缺失的 Core Canvas/EventSystem，不替换已有身份。" +
                        "YLFramework/Service/ResService 保留资源标记、构建与常量入口。新常量承载为 Assets/YLFrameworkData/YLResData.cs 和 YLFramework.Generated.ResourceNames；" +
                        "业务 asmdef 需显式引用生成程序集，未知/手写承载拒绝覆盖。这些用户生成物不在默认导出范围。"),
                    Document("资源工具与生成承载", "Modules/Resource.md")),
                Guide("analyzers", "自有分析器 YL0001～YL0007", "七项诊断默认启用且均为 Warning；提示使用约束，不是运行时安全证明。",
                    new[] { typeof(MustCallBaseAttribute), typeof(RestrictedCallAttribute), typeof(WarnInClassesAttribute),
                        typeof(DisableNewAttribute), typeof(MethodLocalOnlyAttribute), typeof(ProtectedHierarchyAttribute), typeof(View) },
                    HelpBlock.Paragraph("规则与改正方向", "YL0001 MustCallBase：override 的直接方法体须存在正确 base 槽调用，不证明全路径。\n" +
                        "YL0002 RestrictedCall：只允许指定的直接调用者；Lambda/local 不借用外层 Awake。\n" +
                        "YL0003 WarnInClasses：指定类型/基类/接口内的调用提示；View 事件优先 RegisterEvent。\n" +
                        "YL0004 DisableNew：外部 new 受管类型时提示，改用 LoadViewArgs.New() 等工厂。\n" +
                        "YL0005 MethodLocalOnly：字段/属性、数组和实际容器持有受管类型时提示；方法借用允许。\n" +
                        "YL0006 ProtectedHierarchy：指定类型层级写受保护实例成员或暴露可写 ref/out 时提示；正常读取允许。\n" +
                        "YL0007：Core View 派生声明无参、非 static、非 generic 的 void OnEnable/OnDisable/OnDestroy 时提示，改用正式钩子。"),
                    HelpBlock.Paragraph("作用域与匹配", "唯一 YLFramework.Analyzers.dll 位于 Runtime/Core/Analyzers，即 YLFramework.Runtime 的 asmdef 范围，带 RoslynAnalyzer 标签。" +
                        "主体 Runtime 和实际引用它的 Editor/Test/游戏消费者参与；原 XF/XFAB/XFFSM、未引用主体或预定义程序集的作用域以真实编译图和 csc 输入为准。" +
                        "匹配正式程序集的 marker/View 符号，不按短名或 namespace 冒认。Editor/Test/Core 不整体豁免；生成代码默认不分析。"),
                    HelpBlock.Notice("编译器资产与静态限制", "DLL 的 Any/Editor/普通平台均禁用，关闭自动 Runtime 引用，只作为 -analyzer 输入，不加入 Runtime precompiledReferences。" +
                        "YL0005 只保留 LoadViewArgs.Pool 与 LoadViewRequest<T>.args 两个真实内部 owner 例外；反射、object 擦除、闭包逃逸、任意跨过程副作用和所有控制流不保证。" +
                        "原 XF/Logger 分析器是不同身份，不关闭或替代它们。"),
                    HelpBlock.Paragraph("单 ID 配置", "IDE/SDK 使用 .editorconfig 的 dotnet_diagnostic.YL0002.severity 等设置；Unity 2022.3 Console 不自动遵循 IDE 的 editorconfig。" +
                        "Unity 使用消费者 asmdef 目录的叶子 .ruleset 配置单 ID，Error 阻止编译，None 抑制该 ID；不要为局部需求覆盖根 Default.ruleset。" +
                        "故意契约测试可在声明/语句处精确 pragma warning disable 并立即 restore，不禁用整套诊断。"),
                    HelpBlock.Paragraph("从源码重建", "源码仓库 Tools/YLFramework.Analyzers 使用固定 SDK 9.0.312、Roslyn API 3.8.0、netstandard2.0 产物和 NuGet lock。" +
                        "在仓库根执行 pwsh -NoLogo -NoProfile -NonInteractive -File \"Tools/YLFramework.Analyzers/build.ps1\"，默认 locked restore、Release build/test，不部署；" +
                        "显式追加 -Deploy 才替换唯一 DLL 并保留 meta，随后核对 Unity 导入与实际编译。UnityPackage 携带 DLL/meta/说明，不含 SDK 源码项目、测试、NuGet 或中间产物，重建需取得源码仓库。"),
                    Document("规则示例、配置与重建细节", "Modules/Attributes.md"))
            };
            return new HelpModule
            {
                Guides = guides,
                ApiTypes = guides.SelectMany(p => p.FeaturedTypes).Distinct().Select(t => new HelpApiTypeDefinition(t,
                    t.Namespace == "YLFramework.Core.RoslynAnalyzers" ? "分析器标记" : "Core 精选 API")).ToArray(),
                XmlPaths = { [typeof(Module).Assembly] = Path.GetFullPath("Library/YLFramework.Runtime.xml") }
            };
        }

        private static HelpPage Guide(string id, string title, string summary, Type[] types, params HelpBlock[] blocks) =>
            new HelpPage(id, title, "Core 指南", summary, blocks) { RelatedTypes = types, FeaturedTypes = types };

        private static HelpBlock Document(string title, string path) => HelpBlock.Link(title,
            new Uri(Path.GetFullPath("Assets/YLFramework/Documentation/" + path)).AbsoluteUri);
    }
}
