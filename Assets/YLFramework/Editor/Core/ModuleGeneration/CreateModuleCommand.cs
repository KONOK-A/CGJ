using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Core.Editor
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
        // 应用启动协程先持有显式 configuration，再完成以下装配，最后启动本 Module：
        // using var initialize = global::YLFramework.Services.Resource.ResourceSystem.InitializeAsync(configuration);
        // yield return initialize;
        // var resources = initialize.TakeResult();
        // resources.BindCoreResourceProvider();
        // yield return global::YLFramework.Core.ModuleManager.StartUpModule<{ModuleName}>();
        // 应用退出先关闭 Module，再按实际所有权清理实例缓存和 Scene，最后 ShutdownAsync。
        public override string ProjectName => {ProjectName};
        public override bool BaseModule => {BaseModule};
        public override string DefaultStartUpScene => {DefaultStartUpScene};

        public override void OnStart()
        {
            base.OnStart();
            global::UnityEngine.Debug.Log(""OnStart:"" + this.ModuleName);
        }
    }
}
";

        private string moduleName = "YLExampleModule";
        private string projectName = "YLExampleModule";
        private bool baseModule;
        private string defaultStartUpScene = "";

        [MenuItem(MenuPath)]
        private static void CreateModuleItem()
        {
            var window = GetWindowWithRect<CreateModuleCommand>(new Rect(0, 0, 420, 280), true, "YL Core Module");
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Output", OutputRoot);
            moduleName = EditorGUILayout.TextField(new GUIContent("ModuleName", "C# class name"), moduleName);
            projectName = EditorGUILayout.TextField(new GUIContent("ProjectName", "YL Resource ProjectId；须由应用显式初始化并绑定 Core Provider"), projectName);
            baseModule = EditorGUILayout.Toggle(new GUIContent("BaseModule", "基础模块不能通过普通 Close 关闭；使用 ModuleManager.Shutdown 统一结束"), baseModule);
            defaultStartUpScene = EditorGUILayout.TextField("DefaultStartUpScene", defaultStartUpScene);
            EditorGUILayout.HelpBox("生成独立 YL Module 和 Runtime asmdef。命名加载前由应用初始化 YL Resource 并绑定 Core Provider；生成器不修改资源项目或场景。已有目录不会被覆盖。", MessageType.Info);
            if (!GUILayout.Button("Create YL Module"))
                return;

            try
            {
                CreateModule(moduleName, projectName, baseModule, defaultStartUpScene);
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
                throw new ArgumentException("ProjectName must not be empty.", nameof(projectName));
            if (string.IsNullOrEmpty(moduleName) || !Regex.IsMatch(moduleName, @"\A[A-Za-z_][A-Za-z0-9_]*\z") ||
                Regex.IsMatch(moduleName, @"\A(__arglist|__makeref|__reftype|__refvalue|abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while)\z"))
                throw new ArgumentException("ModuleName must be a non-keyword ASCII C# identifier.", nameof(moduleName));

            if (moduleName == "ProjectName" || moduleName == "BaseModule" || moduleName == "DefaultStartUpScene" || moduleName == "OnStart")
                throw new ArgumentException("ModuleName conflicts with a generated member.", nameof(moduleName));

            string directory = OutputRoot + "/" + moduleName;
            if (Directory.Exists(directory) || File.Exists(directory) || File.Exists(directory + ".meta"))
                throw new IOException("YL module output already exists: " + directory);

            // Expand once so user strings containing template tokens remain literal data.
            string content = Regex.Replace(ModuleTemplate, @"\{(ModuleName|ProjectName|BaseModule|DefaultStartUpScene)\}", match =>
            {
                switch (match.Groups[1].Value)
                {
                    case "ModuleName": return moduleName;
                    case "ProjectName": return JsonConvert.SerializeObject(projectName);
                    case "BaseModule": return baseModule ? "true" : "false";
                    default: return string.IsNullOrEmpty(defaultStartUpScene) ? "string.Empty" : JsonConvert.SerializeObject(defaultStartUpScene);
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
                writer.Write(content);
            using (var stream = new FileStream(directory + "/" + moduleName + ".asmdef", FileMode.CreateNew))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(assembly);
        }
    }
}
