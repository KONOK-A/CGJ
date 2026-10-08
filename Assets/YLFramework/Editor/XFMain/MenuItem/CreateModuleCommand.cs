// Copyright (c) 2021 弦小风. Licensed under the MIT License; see ../LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Editor/MenuItem/CreateModuleCommand.cs
// YL adaptation: isolated menu, namespace, output assembly and non-overwriting generation.
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
            projectName = EditorGUILayout.TextField(new GUIContent("ProjectName", "XFAB resource project name"), projectName);
            baseModule = EditorGUILayout.Toggle(new GUIContent("BaseModule", "基础模块不能通过普通 Close 关闭；使用 ModuleManager.Shutdown 统一结束"), baseModule);
            defaultStartUpScene = EditorGUILayout.TextField("DefaultStartUpScene", defaultStartUpScene);
            EditorGUILayout.HelpBox("Creates a new YL example and its Runtime assembly. Existing directories are never overwritten.", MessageType.Info);
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
                references = new[] { "YLFramework.Core.Runtime" },
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
