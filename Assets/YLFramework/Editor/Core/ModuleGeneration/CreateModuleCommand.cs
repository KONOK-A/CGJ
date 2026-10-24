using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using YLFramework.Core;

namespace YLFramework.Editor.Core
{
    public class CreateModuleCommand : EditorWindow
    {
        public const string MenuPath = "Assets/Create/YLFramework/Core/Module";
        public const string OutputRoot = "Assets/YLFramework/Examples/Core/Modules";
        public const string ExampleNamespace = "YLFramework.Core.Examples";

        private const string ModuleTemplate = @"
using YLFramework.Core;

namespace YLFramework.Core.Examples
{
    public class {ModuleName} : global::YLFramework.Core.SingletonModule<{ModuleName}>
    {
        // ProjectName 是正式 YL Resource ProjectId，不是 XFAB 项目名或 BundleName。
        // 应用先通过 ResourceConfiguration.Create 和 ResourceStartup 显式启动，选择 bindCore:true，
        // StartAsync 成功后再启动本 Module；Module 不自行初始化资源或覆盖全局 Provider。
        // 完整配置、同一业务的 Simulation/Bundle 和 Core/Audio 可运行例见 Samples/ResourceStartup。
        // Audio 需要时使用同一 startup.AudioProviders[{ProjectName}]，未选择Audio则不装配。
        // 退出先关闭自己的 Module/播放并归还 Lease/Scene 等责任，再调用 startup.StopAsync。
        // Busy 时保持原会话并在真实 owner 归还后重试，不关闭外部会话或强制释放他人责任。
        public override string ProjectName => {ProjectName};
        public override bool IsBaseModule => {BaseModule};
        public override string DefaultStartUpScene => {DefaultStartUpScene};

        public override void OnStart()
        {
            base.OnStart();
            global::UnityEngine.Debug.Log(""OnStart:"" + this.ModuleName);
        }
    }
}
";

        private string _moduleName = "YLExampleModule";
        private string _projectName = "YLExampleModule";
        private bool _isBaseModule;
        private string _defaultStartUpScene = "";

        [MenuItem(MenuPath)]
        private static void CreateModuleItem()
        {
            var window = GetWindowWithRect<CreateModuleCommand>(new Rect(0, 0, 420, 280), true, "YL Core Module");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Output", OutputRoot);
            _moduleName = EditorGUILayout.TextField(new GUIContent("ModuleName", "C# class name"), _moduleName);
            _projectName = EditorGUILayout.TextField(new GUIContent("ProjectName", "YL Resource ProjectId；须由应用显式初始化并绑定 Core Provider"), _projectName);
            _isBaseModule = EditorGUILayout.Toggle(new GUIContent("IsBaseModule", "基础模块不能通过普通 Close 关闭；使用 ModuleManager.Shutdown 统一结束"), _isBaseModule);
            _defaultStartUpScene = EditorGUILayout.TextField("DefaultStartUpScene", _defaultStartUpScene);
            EditorGUILayout.HelpBox("生成独立 YL Module 和 Runtime asmdef。命名加载前由应用初始化 YL Resource 并绑定 Core Provider；生成器不修改资源项目或场景。已有目录不会被覆盖。", MessageType.Info);
            if (!GUILayout.Button("Create YL Module"))
            {
                return;
            }

            try
            {
                CreateModule(_moduleName, _projectName, _isBaseModule, _defaultStartUpScene);
                AssetDatabase.Refresh();
                Close();
            }
            catch (ArgumentException exception)
            {
                ShowNotification(new GUIContent(exception.Message));
            }
            catch (IOException exception)
            {
                ShowNotification(new GUIContent(exception.Message));
            }
        }

        public static void CreateModule(string moduleName, string projectName, bool baseModule, string defaultStartUpScene)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("ProjectName must not be empty.", nameof(projectName));
            }

            if (string.IsNullOrEmpty(moduleName) || !Regex.IsMatch(moduleName, @"\A[A-Za-z_][A-Za-z0-9_]*\z") ||
                Regex.IsMatch(moduleName, @"\A(__arglist|__makeref|__reftype|__refvalue|abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)\z"))
            {
                throw new ArgumentException("ModuleName must be a non-keyword ASCII C# identifier.", nameof(moduleName));
            }

            if (moduleName == "ProjectName" || moduleName == "IsBaseModule" || moduleName == "DefaultStartUpScene" || moduleName == "OnStart")
            {
                throw new ArgumentException("ModuleName conflicts with a generated member.", nameof(moduleName));
            }

            string directory = OutputRoot + "/" + moduleName;
            if (Directory.Exists(directory) || File.Exists(directory) || File.Exists(directory + ".meta"))
            {
                throw new IOException("YL module output already exists: " + directory);
            }

            // Expand once so user strings containing template tokens remain literal data.
            string content = Regex.Replace(ModuleTemplate, @"\{(ModuleName|ProjectName|BaseModule|DefaultStartUpScene)\}", match =>
            {
                switch (match.Groups[1].Value)
                {
                    case "ModuleName":
                        return moduleName;
                    case "ProjectName":
                        return JsonConvert.SerializeObject(projectName);
                    case "BaseModule":
                        return baseModule ? "true" : "false";
                    default:
                        return string.IsNullOrEmpty(defaultStartUpScene) ? "string.Empty" : JsonConvert.SerializeObject(defaultStartUpScene);
                }
            });
            string assembly = JsonConvert.SerializeObject(new
            {
                name = "YLFramework.Core.Examples." + moduleName,
                rootNamespace = ExampleNamespace,
                references = new[] { "YLFramework.Runtime" },
                autoReferenced = false
            }, Formatting.Indented);

            Directory.CreateDirectory(directory);
            using (var stream = new FileStream(directory + "/" + moduleName + ".cs", FileMode.CreateNew))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
            }

            using (var stream = new FileStream(directory + "/" + moduleName + ".asmdef", FileMode.CreateNew))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(assembly);
            }
        }
    }
}
