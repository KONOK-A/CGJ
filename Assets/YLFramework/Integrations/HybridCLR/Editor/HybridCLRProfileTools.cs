using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using YLFramework.Editor.Resource;
using YLFramework.Services.Resource;

namespace YLFramework.Integrations.HybridCLR.Editor
{
    internal sealed class HybridCLRProfileInspection
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public string[] Assemblies = Array.Empty<string>();
        public string[] Dependencies = Array.Empty<string>();
        public bool Succeeded => Errors.Count == 0;
    }

    internal static class HybridCLRProfileTools
    {
        internal static HybridCLRBuildProfile Create(string assetPath)
        {
            RequireEditMode();
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                assetPath.Contains('\\') || assetPath.Split('/').Any(p => p.Length == 0 || p == "." || p == "..") ||
                Path.GetExtension(assetPath) != ".asset" || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(assetPath)?.Replace('\\', '/')) ||
                assetPath.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal))
                throw new ArgumentException("请在 Assets 的真实文件夹内创建配置。", nameof(assetPath));
            var stem = assetPath.Substring(0, assetPath.Length - ".asset".Length);
            var projectPath = stem + "-Project.asset";
            var recipePath = stem + "-Recipe.asset";
            var generated = stem + "-Generated";
            foreach (var path in new[] { assetPath, projectPath, recipePath, generated })
            {
                HybridCLRBuildBaseline.RejectLinks(path);
                if (File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta"))
                    throw new IOException("目标已存在，拒绝覆盖：" + path);
            }
            var created = new List<string>();
            try
            {
                var project = ResourceEditorOperations.CreateProject(projectPath); created.Add(projectPath);
                project.DisplayName = Path.GetFileNameWithoutExtension(assetPath);
                var recipe = ScriptableObject.CreateInstance<ResourceBuildRecipe>();
                recipe.Project = project; recipe.Target = BuildTarget.StandaloneWindows64;
                recipe.Action = ResourceBuildAction.BuildAndPublish; recipe.AppContentCompatibilityId = "base-v1";
                AssetDatabase.CreateAsset(recipe, recipePath); created.Add(recipePath);
                var profile = ScriptableObject.CreateInstance<HybridCLRBuildProfile>();
                profile.Recipe = recipe; profile.Development = EditorUserBuildSettings.development; profile.GeneratedAssetDirectory = generated;
                AssetDatabase.CreateAsset(profile, assetPath); created.Add(assetPath);
                profile.BaselineDirectory = Path.GetFullPath("Build/HybridCLR/" + AssetDatabase.AssetPathToGUID(assetPath) + "/base-v1");
                MapGeneratedResources(profile);
                EditorUtility.SetDirty(project); EditorUtility.SetDirty(recipe); EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(project); AssetDatabase.SaveAssetIfDirty(recipe); AssetDatabase.SaveAssetIfDirty(profile);
                return profile;
            }
            catch
            {
                foreach (var path in created.AsEnumerable().Reverse()) AssetDatabase.DeleteAsset(path);
                if (AssetDatabase.IsValidFolder(generated)) AssetDatabase.DeleteAsset(generated);
                throw;
            }
        }

        internal static void MapGeneratedResources(HybridCLRBuildProfile profile)
        {
            RequireEditMode();
            HybridCLRBuildPipeline.ValidateInputPaths(profile);
            if (!profile.Recipe || !profile.Recipe.Project) throw new InvalidDataException("请先选择资源配方及项目。");
            var project = profile.Recipe.Project;
            var root = profile.GeneratedAssetDirectory.TrimEnd('/');
            ValidateCodeMappings(profile);
            foreach (var source in project.SourceDirectories)
            {
                var path = AssetDatabase.GUIDToAssetPath(source.Guid);
                if (path == root && !string.IsNullOrEmpty(source.AddressPrefix) || path != root &&
                    (path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase) || root.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("已有来源目录与代码地址映射冲突，请先在资源项目中明确调整：" + path);
            }
            EnsureFolder(root); EnsureFolder(root + "/hybridclr");
            EnsureFolder(root + "/hybridclr/assemblies"); EnsureFolder(root + "/hybridclr/metadata");
            var rootGuid = AssetDatabase.AssetPathToGUID(root);
            var codeGuid = AssetDatabase.AssetPathToGUID(root + "/hybridclr");
            var sourceExists = project.SourceDirectories.Any(s => s.Guid == rootGuid);
            var selection = ReadCodeSelection(project, codeGuid);
            if (sourceExists && selection != null) return;
            Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("关联 HybridCLR 生成资源");
            var serialized = new SerializedObject(project); serialized.Update();
            if (!sourceExists)
            {
                var sources = serialized.FindProperty("SourceDirectories");
                var source = sources.GetArrayElementAtIndex(sources.arraySize++);
                source.FindPropertyRelative("Guid").stringValue = rootGuid;
                source.FindPropertyRelative("AddressPrefix").stringValue = "";
            }
            if (selection == null)
            {
                var id = "hybridclr-code"; var suffix = 2;
                while (project.Groups.Any(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase))) id = "hybridclr-code-" + suffix++;
                var groups = serialized.FindProperty("Groups");
                var group = groups.GetArrayElementAtIndex(groups.arraySize++);
                group.FindPropertyRelative("Id").stringValue = id;
                group.FindPropertyRelative("Packing").enumValueIndex = (int)ResourcePacking.Together;
                var selections = group.FindPropertyRelative("Selections"); selections.arraySize = 1;
                var item = selections.GetArrayElementAtIndex(0);
                item.FindPropertyRelative("Guid").stringValue = codeGuid;
                item.FindPropertyRelative("Address").stringValue = "";
                item.FindPropertyRelative("Aliases").arraySize = 0;
                var extensions = item.FindPropertyRelative("Extensions"); extensions.arraySize = 2;
                extensions.GetArrayElementAtIndex(0).stringValue = ".bytes"; extensions.GetArrayElementAtIndex(1).stringValue = ".json";
            }
            serialized.ApplyModifiedProperties();
        }

        internal static HybridCLRProfileInspection Inspect(HybridCLRBuildProfile profile)
        {
            var result = new HybridCLRProfileInspection();
            if (!profile) { result.Errors.Add("先新建或选择一个 HybridCLR 配置。"); return result; }
            void Check(Action action)
            {
                try { action(); }
                catch (Exception error) { result.Errors.Add(error.Message); }
            }
            Check(() =>
            {
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(profile))) throw new InvalidDataException("配置必须保存为 Assets 中的资产。");
                HybridCLRBuildPipeline.ValidateInputPaths(profile);
                HybridCLRBuildPipeline.ResolveBaseProfile(profile);
                if (profile.Recipe && profile.Recipe.Project) ValidateCodeMappings(profile);
            });
            if (!profile.Recipe || !profile.Recipe.Project)
            { result.Errors.Add("选择资源配方，并在配方中指定资源项目。"); return result; }
            Check(() =>
            {
                _ = new ResourceAddress(profile.Recipe.Project.ProjectId, "hybridclr/manifest.json");
                _ = new ResourceAddress(profile.Recipe.AppContentCompatibilityId, "compatibility");
                if (profile.Recipe.Target != BuildTarget.StandaloneWindows64 || profile.Recipe.Action != ResourceBuildAction.BuildAndPublish || profile.Recipe.GenerateReferences)
                    throw new InvalidDataException("配方应使用 Windows64、构建并发布，且不生成 C# 地址引用。");
                var root = profile.GeneratedAssetDirectory.TrimEnd('/');
                var code = root + "/hybridclr";
                if (!AssetDatabase.IsValidFolder(root) || !AssetDatabase.IsValidFolder(code))
                    throw new InvalidDataException("代码来源目录已不存在，历史 GUID 不能作为有效文件夹，请显式关联生成资源。");
                var rootGuid = AssetDatabase.AssetPathToGUID(root);
                var codeGuid = AssetDatabase.AssetPathToGUID(code);
                if (string.IsNullOrEmpty(rootGuid) || string.IsNullOrEmpty(codeGuid) ||
                    !profile.Recipe.Project.SourceDirectories.Any(s => s.Guid == rootGuid && string.IsNullOrEmpty(s.AddressPrefix)) ||
                    ReadCodeSelection(profile.Recipe.Project, codeGuid) == null)
                    throw new InvalidDataException("代码来源目录与资源选择未关联，使用“关联生成资源”后保存资源项目。");
            });
            Check(() =>
            {
                var names = HybridCLRBuildPipeline.ConfiguredNames();
                var graph = CompilationPipeline.GetAssemblies(AssembliesType.Player).ToDictionary(a => a.name, StringComparer.Ordinal);
                var external = HybridCLRBuildPipeline.ExternalCandidates(names).ToDictionary(a => a.Name, StringComparer.Ordinal);
                var records = new List<HybridCLRAssemblyRecord>();
                foreach (var name in names)
                {
                    var scripted = graph.TryGetValue(name, out var assembly);
                    if (scripted && external.ContainsKey(name)) throw new InvalidDataException("程序集同时来自脚本和外部 DLL：" + name);
                    if (scripted) records.Add(new HybridCLRAssemblyRecord { Name = name, References = assembly.assemblyReferences.Select(a => a.name)
                        .Concat(assembly.compiledAssemblyReferences.Select(Path.GetFileNameWithoutExtension)).Distinct(StringComparer.Ordinal).ToArray() });
                    else if (external.TryGetValue(name, out var dll)) records.Add(dll);
                    else throw new InvalidDataException("未找到已登记程序集的 Player 脚本或外部 DLL 来源：" + name);
                }
                var selected = HybridCLRBuildPipeline.SelectAssemblies(profile, new HybridCLRAssemblySet { HotUpdate = records, Aot = Array.Empty<HybridCLRAssemblyRecord>() });
                result.Assemblies = selected.HotUpdate.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                result.Dependencies = profile.HotUpdateAssemblies == null || profile.HotUpdateAssemblies.Length == 0 ? Array.Empty<string>() :
                    result.Assemblies.Except(profile.HotUpdateAssemblies, StringComparer.Ordinal).ToArray();
            });
            if (EditorUtility.IsDirty(profile) || EditorUtility.IsDirty(profile.Recipe) || EditorUtility.IsDirty(profile.Recipe.Project))
                result.Warnings.Add("配置存在未保存修改，正式构建前需显式保存。");
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone) != ScriptingImplementation.IL2CPP ||
                PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) != ApiCompatibilityLevel.NET_4_6)
                result.Warnings.Add("当前 Editor 可编辑和预检配置；原生构建需在接收工程显式配置 IL2CPP / .NET Framework。");
            result.Warnings.Add("依赖预览依据当前 Player 脚本图与外部 DLL；正式发布会重新编译并按实际程序集身份复核。");
            return result;
        }

        private static ResourceSelection ReadCodeSelection(ResourceProject project, string guid)
        {
            var matches = project.Groups.SelectMany(g => g.Selections).Where(s => s.Guid == guid).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("代码目录选择必须唯一，请显式调整重复选择，未覆盖用户配置。");
            var selection = matches.SingleOrDefault();
            if (selection != null && (!string.IsNullOrEmpty(selection.Address) || selection.Aliases != null && selection.Aliases.Length != 0 ||
                selection.Extensions == null || !selection.Extensions.OrderBy(e => e, StringComparer.Ordinal).SequenceEqual(new[] { ".bytes", ".json" })))
                throw new InvalidDataException("代码目录选择必须使用标准 .bytes/.json 且不含地址覆盖或文件夹别名，请显式调整，未覆盖用户配置。");
            return selection;
        }

        private static void ValidateCodeMappings(HybridCLRBuildProfile profile)
        {
            var project = profile.Recipe.Project;
            if (project.Groups.Select(g => g.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != project.Groups.Count)
                throw new InvalidDataException("资源组 ID 存在大小写重复，请先在资源项目中明确调整。");
            var expected = profile.GeneratedAssetDirectory.TrimEnd('/');
            foreach (var selection in project.Groups.SelectMany(g => g.Selections))
            {
                var path = AssetDatabase.GUIDToAssetPath(selection.Guid);
                if (string.IsNullOrEmpty(path) || path.Equals(expected, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(expected + "/", StringComparison.OrdinalIgnoreCase) || selection.Extensions != null && selection.Extensions.Length != 0 &&
                    !selection.Extensions.Any(e => string.Equals(e, ".bytes", StringComparison.OrdinalIgnoreCase) || string.Equals(e, ".json", StringComparison.OrdinalIgnoreCase))) continue;
                foreach (var source in project.SourceDirectories)
                {
                    var sourcePath = AssetDatabase.GUIDToAssetPath(source.Guid);
                    if (string.IsNullOrEmpty(sourcePath) || path != sourcePath && !path.StartsWith(sourcePath + "/", StringComparison.OrdinalIgnoreCase)) continue;
                    var relative = path == sourcePath ? "" : path.Substring(sourcePath.Length + 1);
                    var address = !string.IsNullOrEmpty(selection.Address) ? selection.Address :
                        string.IsNullOrEmpty(source.AddressPrefix) ? relative : source.AddressPrefix.TrimEnd('/') + (relative.Length == 0 ? "" : "/" + relative);
                    var code = address.Equals("hybridclr", StringComparison.OrdinalIgnoreCase) ||
                        address.Equals(HybridCLRManifest.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
                        address.Equals("hybridclr/assemblies", StringComparison.OrdinalIgnoreCase) || address.StartsWith("hybridclr/assemblies/", StringComparison.OrdinalIgnoreCase) ||
                        address.Equals("hybridclr/metadata", StringComparison.OrdinalIgnoreCase) || address.StartsWith("hybridclr/metadata/", StringComparison.OrdinalIgnoreCase) ||
                        address.Length == 0 && AssetDatabase.IsValidFolder(path + "/hybridclr");
                    if (code) throw new InvalidDataException("已有 HybridCLR 代码映射来自其他目录，请先明确移除或调整旧选择，未修改用户数据：" + path);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidDataException("Invalid Assets folder: " + path);
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path)))) throw new IOException("Cannot create Assets folder: " + path);
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("请在非 Play、编译或构建的 Editor 中显式编辑配置。");
        }
    }
}
