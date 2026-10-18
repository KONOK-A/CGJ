using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Samples.Resource.Editor
{
    [CustomEditor(typeof(ResourceStartupExample))]
    public sealed class ResourceStartupExampleEditor : UnityEditor.Editor
    {
        private string developmentKeyEnvironmentVariable = ResourceEditorOperations.ContentKeyEnvironmentVariable;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var example = (ResourceStartupExample)target;
            EditorGUILayout.LabelField("Result", example.Result);
            EditorGUILayout.HelpBox("在生成的 Project.asset 选择 ContentEncryption=None 或 Aes256GcmHkdfSegmentedV1；加密时填写非秘密 ContentKeyId 并显式保存，再构建/发布。" +
                "\n生产环境由应用注入 ContentKeyProvider。下面仅供非生产开发：进程环境变量须为32字节key的Base64；这里只装配provider，不保存或显示key。" +
                "\nBuild选中确切发布并重置UseBuiltIn，请明确切换Mode=Bundles。内置预检/部署不需key；部署后保存自己的Scene。进入Play或域重载后重新注入，再用原 Run Selected Mode。", MessageType.Info);
            developmentKeyEnvironmentVariable = EditorGUILayout.TextField("Development Key Variable", developmentKeyEnvironmentVariable);
            if (GUILayout.Button("Inject Development Environment Key Provider"))
                UseDevelopmentContentKeyFromEnvironment(example, developmentKeyEnvironmentVariable);
            EditorGUILayout.LabelField("Content Key Provider", example.ContentKeyProvider == null ? "Not injected" : "Injected (runtime only)");
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Build And Publish Example Bundle")) Build(example);
                if (GUILayout.Button("Preflight Selected Built-In Release")) Debug.Log(PreflightBuiltIn(example).ToString());
                if (GUILayout.Button("Deploy Selected Release To Built-In"))
                {
                    var deployment = DeployBuiltIn(example);
                    Debug.Log("Built-in=" + deployment.RelativeDirectory + " / repeat=" + deployment.IsRepeat + "; select Bundles and save the example scene explicitly.");
                }
                if (GUILayout.Button("Recover Built-In Deployment"))
                {
                    var recovered = ResourceBuiltInDeployer.Recover(Project(example.ProjectId), Target(example.Platform));
                    Debug.Log(recovered == null ? "No installed deployment to confirm; a pending private stage was aborted if present. Preflight before deploying again." :
                        "Confirmed built-in=" + recovered.RelativeDirectory);
                }
            }
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Run Selected Mode")) example.Begin(CreateConfiguration(example));
                if (GUILayout.Button("Stop / Retry Shutdown")) example.StopExample();
            }
        }

        private static ResourceProject Project(string id)
        {
            var matches = AssetDatabase.FindAssets("t:ResourceProject")
                .Select(x => AssetDatabase.LoadAssetAtPath<ResourceProject>(AssetDatabase.GUIDToAssetPath(x)))
                .Where(x => x && x.ProjectId == id).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Exactly one formal ResourceProject must match the sample ProjectId.");
            return matches[0];
        }

        public static ResourceConfiguration CreateConfiguration(ResourceStartupExample example) =>
            example.Mode == ResourceLoadMode.EditorSimulation
                ? ResourceEditorSimulation.CreateConfiguration(example.Mode, new[] { Project(example.ProjectId) })
                : example.CreateBundleConfiguration();

        /// <summary>仅供非生产开发，显式绑定当前Project/KeyId与进程变量名；Play/域重载后重新调用，不持久化密钥。</summary>
        public static void UseDevelopmentContentKeyFromEnvironment(ResourceStartupExample example, string environmentVariable)
        {
            var project = Project(example.ProjectId);
            example.ContentKeyProvider = ResourceEditorOperations.ContentKeyFromEnvironment(project.ProjectId, project.ContentKeyId, environmentVariable);
        }

        public static ResourceRelease Build(ResourceStartupExample example)
        {
            var report = ResourceEditorOperations.BuildAndPublish(Project(example.ProjectId), example.AppCompatibility, provider: example.ContentKeyProvider);
            var release = report.Release ?? throw new InvalidOperationException(report.Summary, report.BuildError ?? report.PublicationError);
            Undo.RecordObject(example, "Select explicit sample release");
            example.SnapshotDirectory = release.DirectoryPath;
            example.CatalogDigest = release.Catalog.CatalogDigest;
            example.UseBuiltIn = false;
            EditorUtility.SetDirty(example);
            EditorSceneManager.MarkSceneDirty(example.gameObject.scene);
            return release;
        }

        private static BuildTarget Target(string platform) => Enum.TryParse<BuildTarget>(platform, out var target) && target.ToString() == platform
            ? target : throw new InvalidOperationException("Select an explicit supported build platform.");

        public static ResourceBuiltInPlan PreflightBuiltIn(ResourceStartupExample example) => ResourceBuiltInDeployer.Preflight(
            ResourcePublisher.ReadVersion(Project(example.ProjectId), Target(example.Platform), example.AppCompatibility, example.CatalogDigest));

        /// <summary>只部署Inspector已选择的确切发布；不保存用户Scene、不启动或切换运行会话。</summary>
        public static ResourceBuiltInDeployment DeployBuiltIn(ResourceStartupExample example)
        {
            var result = ResourceBuiltInDeployer.Deploy(PreflightBuiltIn(example));
            Undo.RecordObject(example, "Use explicitly deployed built-in resources");
            example.UseBuiltIn = true;
            EditorUtility.SetDirty(example);
            EditorSceneManager.MarkSceneDirty(example.gameObject.scene);
            return result;
        }

        [MenuItem("YLFramework/Samples/Create Resource Startup Example")]
        private static void CreateMenu() => Create();

        /// <summary>用户显式创建独立示例。只新建GUID，不改已有Scene/资源；脏场景拒绝而非自动保存。</summary>
        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Create the example in Edit Mode.");
            for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard dirty scenes before creating the sample.");
            var folder = AssetDatabase.GenerateUniqueAssetPath("Assets/YLResourceStartupExample");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            AssetDatabase.CreateFolder(folder, "Content");
            var content = folder + "/Content";
            File.WriteAllText(content + "/Message.txt", "Hello from ResourceStartup", new UTF8Encoding(false));
            WriteTone(content + "/Tone.wav");
            AssetDatabase.ImportAsset(content + "/Message.txt", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(content + "/Tone.wav", ImportAssetOptions.ForceSynchronousImport);
            var data = CreateInstance<ResourceStartupExampleData>();
            data.Text = "Explicit compiled sample type";
            AssetDatabase.CreateAsset(data, content + "/Data.asset");
            var project = CreateInstance<ResourceProject>();
            project.ProjectId = "startup-example-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            project.DisplayName = "Resource Startup Example";
            project.SourceDirectories.Add(new ResourceSourceDirectory { Guid = AssetDatabase.AssetPathToGUID(content) });
            project.Groups.Add(new ResourceGroup
            {
                Id = "startup-content",
                Packing = ResourcePacking.Together,
                Selections = new List<ResourceSelection>
                {
                    new() { Guid = AssetDatabase.AssetPathToGUID(content + "/Message.txt"), Address = "message" },
                    new() { Guid = AssetDatabase.AssetPathToGUID(content + "/Data.asset"), Address = "data" },
                    new() { Guid = AssetDatabase.AssetPathToGUID(content + "/Tone.wav"), Address = "tone" }
                }
            });
            AssetDatabase.CreateAsset(project, folder + "/Project.asset");
            AssetDatabase.SaveAssetIfDirty(project);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Resource Startup Example", typeof(AudioListener));
            var example = go.AddComponent<ResourceStartupExample>();
            example.ProjectId = project.ProjectId;
            example.Platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            EditorSceneManager.SaveScene(scene, folder + "/Example.unity");
            Selection.activeObject = example;
            return folder;
        }

        private static void WriteTone(string path)
        {
            const int rate = 8000;
            using var output = new BinaryWriter(File.Create(path));
            output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + rate * 2);
            output.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16);
            output.Write((short)1); output.Write((short)1); output.Write(rate); output.Write(rate * 2);
            output.Write((short)2); output.Write((short)16); output.Write(Encoding.ASCII.GetBytes("data")); output.Write(rate * 2);
            for (var i = 0; i < rate; i++) output.Write((short)(Math.Sin(i * 2 * Math.PI * 220 / rate) * 4096));
        }
    }
}
