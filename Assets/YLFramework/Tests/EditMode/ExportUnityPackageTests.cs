using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YLFramework.Actions;
using YLFramework.Utilities.ZipTool.GZip;
using YLFramework.Utilities.ZipTool.Tar;
using Object = UnityEngine.Object;

namespace YLFramework.Tests
{
    public class ExportUnityPackageTests
    {
        private const string FrameworkRoot = "Assets/YLFramework";
        private const string PrefabPath = FrameworkRoot + "/Runtime/Action/Resources/ActionKit/ScreenTransitionCanvas.prefab";
        private const string CanvasPath = FrameworkRoot + "/Runtime/Action/ScreenTransition/ScreenTransitionCanvas.cs";
        private const string PrefabGuid = "2a0bf8e4b9b44ec46a47f771105de1e4";
        private const string CanvasGuid = "256f0a9016a7098469c8afed875a5d50";
        private const string AnalyzerPath = FrameworkRoot + "/Runtime/XFMain/Analyzers/YLFramework.Analyzers.dll";

        [Test]
        public void DefaultExportContainsTheIntegratedSourcesAndPreservesTheScreenPrefab()
        {
            var rootsField = typeof(YLFramework.Editor.ExportUnityPackage).GetField("ExportRoots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(rootsField, Is.Not.Null);
            var configuredRoots = rootsField.GetValue(null) as string[];
            Assert.That(configuredRoots, Is.Not.Null);
            var roots = (string[])configuredRoots.Clone();
            Assert.That(roots, Is.EqualTo(new[]
            {
                FrameworkRoot + "/Editor",
                FrameworkRoot + "/Plugins",
                FrameworkRoot + "/Prefabs",
                FrameworkRoot + "/Runtime",
                FrameworkRoot + "/Samples",
                FrameworkRoot + "/Documentation"
            }));
            foreach (var root in roots) Assert.That(AssetDatabase.IsValidFolder(root), Is.True, root);

            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var sentinels = new[]
            {
                FrameworkRoot + "/Runtime/Action/Core/ActionKit.cs",
                FrameworkRoot + "/Runtime/Audio/AudioKit.cs",
                FrameworkRoot + "/Runtime/Audio/Actions/PlaySoundAction.cs",
                FrameworkRoot + "/Editor/Help/HelpRegistry.cs",
                FrameworkRoot + "/Editor/Help/HelpWindow.cs",
                FrameworkRoot + "/Editor/Help/YLFramework.Editor.Help.asmdef",
                FrameworkRoot + "/Editor/Action/Help/ActionKitHelpProvider.cs",
                FrameworkRoot + "/Editor/Audio/Help/AudioKitHelpProvider.cs",
                FrameworkRoot + "/Editor/XFMain/CoreHelpProvider.cs",
                FrameworkRoot + "/Editor/FoundationToolsHelpProvider.cs",
                FrameworkRoot + "/Editor/XFMain/YLFramework.Core.Editor.asmdef",
                FrameworkRoot + "/Editor/Resource/ResourceBuildOperation.cs",
                FrameworkRoot + "/Editor/Resource/ResReferenceOutput.cs",
                FrameworkRoot + "/Documentation/Modules/Attributes.md",
                AnalyzerPath,
                FrameworkRoot + "/Runtime/csc.rsp",
                FrameworkRoot + "/Documentation/Modules/Action.md",
                FrameworkRoot + "/Documentation/Modules/Audio.md",
                FrameworkRoot + "/Documentation/Modules/Help.md",
                FrameworkRoot + "/Documentation/Modules/UI.md",
                FrameworkRoot + "/Prefabs/YLFramework.prefab",
                FrameworkRoot + "/Prefabs/UITips.prefab",
                FrameworkRoot + "/Prefabs/ColorSelector.prefab",
                FrameworkRoot + "/Samples/ColorSelector/ColorSelectorSample.prefab",
                FrameworkRoot + "/Runtime/XFMain/UI/Tools/ColorWheelGraphic.cs",
                FrameworkRoot + "/Runtime/XFMain/LICENSE.md",
                FrameworkRoot + "/Runtime/THIRD-PARTY-NOTICES.md",
                FrameworkRoot + "/Runtime/Shared/LICENSE.md",
                FrameworkRoot + "/Runtime/Services/Http/HttpManager.cs",
                FrameworkRoot + "/Runtime/Services/Http/SendWebRequest.cs",
                FrameworkRoot + "/Runtime/Shared/YLFramework.Foundation.Runtime.asmdef",
                FrameworkRoot + "/Runtime/YLFramework.Runtime.asmdef",
                FrameworkRoot + "/Runtime/XFMain/YLFramework.Core.Runtime.asmdef",
                FrameworkRoot + "/Runtime/Tools/StringTools.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/GameObjectExtension.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/ObjectExtension.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/AnimatorExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/ColorExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/EventTriggerExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/LayerMaskExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/RigidbodyExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/SceneExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Unity/TimeExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Math/BoundsExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Math/QuaternionExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Math/RayExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Math/Vector2Extensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Math/Vector3Extensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/System/TypeExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/Extensions/Utilities/RandomExtensions.cs",
                FrameworkRoot + "/Runtime/Tools/FSM/Core/FSM.cs",
                FrameworkRoot + "/Runtime/Tools/Grid/NormalGrid.cs",
                FrameworkRoot + "/Runtime/Tools/Grid/DynamicGrid.cs",
                FrameworkRoot + "/Runtime/Tools/Json/JSONObject.cs",
                FrameworkRoot + "/Runtime/Tools/Json/LICENSE.md",
                FrameworkRoot + "/Runtime/Tools/Table/Table.cs",
                FrameworkRoot + "/Runtime/Tools/Table/TableIndex.cs",
                FrameworkRoot + "/Runtime/Tools/ZipTool/Utilities/ZipUtility.cs",
                FrameworkRoot + "/Runtime/Tools/ZipTool/YLFramework.Utilities.ZipTool.asmdef",
                FrameworkRoot + "/Runtime/Tools/ZipTool/ICSharpCode.SharpZipLib/THIRD-PARTY-NOTICES.md",
                FrameworkRoot + "/Documentation/Modules/FSM.md",
                FrameworkRoot + "/Documentation/Modules/Grid.md",
                FrameworkRoot + "/Documentation/Modules/Json.md",
                FrameworkRoot + "/Documentation/Modules/Table.md",
                FrameworkRoot + "/Documentation/Modules/ZipTool.md",
                PrefabPath,
                CanvasPath
            };
            var payloadGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in sentinels)
            {
                Assert.That(File.Exists(Path.Combine(projectRoot, path)), Is.True, "Missing sentinel: " + path);
                var guid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(guid, Does.Match("^[0-9a-f]{32}$"), path);
                Assert.That(payloadGuids.Add(guid), Is.True, "Duplicate sentinel GUID: " + path);
            }

            var requiredPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => roots.Any(root => IsAtOrBelow(path, root))).ToArray();
            var excludedRoots = new[]
            {
                FrameworkRoot + "/Tests", FrameworkRoot + "/Integrations",
                FrameworkRoot + "/Examples", "Assets/YLFrameworkData", "Assets/Games", "Assets/Scenes",
                "Assets/StreamingAssets", "Assets/Plugins/NuGet", "Tools", "Temp", "Library", "Packages",
                FrameworkRoot + "/Runtime/UI",
                FrameworkRoot + "/Runtime/XFMain/Extensions",
                FrameworkRoot + "/Runtime/XFMain/Tools",
                FrameworkRoot + "/Samples/ColorSelector/Prefabs",
                FrameworkRoot + "/Samples/ColorSelector/Shaders",
                FrameworkRoot + "/Samples/ColorSelector/Materials",
                FrameworkRoot + "/Samples/ColorSelector/Textures",
                "Assets/Plugins/Demigiant/DOTween"
            };
            var retiredFiles = new[] { "ActionKitSingleFileCreator.cs", "ActionKitSingleFileExporter.cs", "ActionKitSingleFileCreator.asset", "LogUtil.cs" };
            var tempDirectory = Path.Combine(@"C:\Users\Kano\AppData\Local\Temp\kilo", "YLFramework.ExportTests-" + Guid.NewGuid().ToString("N"));
            var packagePath = Path.Combine(tempDirectory, "framework.unitypackage");
            try
            {
                Directory.CreateDirectory(tempDirectory);
                AssetDatabase.ExportPackage(roots, packagePath, ExportPackageOptions.Recurse);
                Assert.That(File.Exists(packagePath), Is.True, packagePath);
                Assert.That(new FileInfo(packagePath).Length, Is.GreaterThan(0L), packagePath);

                var recordsByGuid = new Dictionary<string, PackageRecord>(StringComparer.Ordinal);
                using (var file = File.OpenRead(packagePath))
                using (var gzip = new GZipInputStream(file) { IsStreamOwner = false })
                using (var tar = new TarInputStream(gzip) { IsStreamOwner = false })
                {
                    TarEntry entry;
                    while ((entry = tar.GetNextEntry()) != null)
                    {
                        if (entry.IsDirectory) continue;
                        var name = entry.Name.StartsWith("./", StringComparison.Ordinal) ? entry.Name.Substring(2) : entry.Name;
                        var parts = name.Split('/');
                        Assert.That(parts.Length, Is.EqualTo(2), "Unexpected package member: " + entry.Name);
                        var guid = parts[0];
                        var member = parts[1];
                        Assert.That(guid, Does.Match("^[0-9a-f]{32}$"), entry.Name);
                        Assert.That(member, Is.Not.Empty, entry.Name);
                        Assert.That(entry.Size, Is.GreaterThanOrEqualTo(0L), entry.Name);
                        if (!recordsByGuid.TryGetValue(guid, out var record))
                        {
                            record = new PackageRecord();
                            recordsByGuid.Add(guid, record);
                        }
                        Assert.That(record.MemberSizes.ContainsKey(member), Is.False, "Duplicate GUID member: " + entry.Name);
                        record.MemberSizes.Add(member, entry.Size);
                        // Select by GUID before pathname arrives; archive order is not a contract.
                        if (member == "pathname") record.Pathname = Encoding.UTF8.GetString(ReadEntry(tar, entry)).TrimEnd('\r', '\n', '\0');
                        else if (member == "asset.meta") record.Meta = ReadEntry(tar, entry);
                        else if (member == "asset")
                        {
                            var payload = ReadEntry(tar, entry);
                            using (var sha = new SHA256Managed()) record.PayloadHash = sha.ComputeHash(payload);
                            if (payloadGuids.Contains(guid)) record.Payload = payload;
                        }
                    }
                }

                Assert.That(recordsByGuid, Is.Not.Empty);
                var recordsByPath = new Dictionary<string, PackageRecord>(StringComparer.Ordinal);
                foreach (var pair in recordsByGuid)
                {
                    var guid = pair.Key;
                    var record = pair.Value;
                    Assert.That(record.Pathname, Is.Not.Null.And.Not.Empty, "Missing pathname: " + guid);
                    Assert.That(record.Meta, Is.Not.Null, "Missing asset.meta: " + record.Pathname);
                    Assert.That(record.Pathname.Contains("\\"), Is.False, record.Pathname);
                    Assert.That(record.Pathname.Split('/').Any(part => part == "." || part == ".."), Is.False, record.Pathname);
                    Assert.That(recordsByPath.ContainsKey(record.Pathname), Is.False, "Duplicate pathname: " + record.Pathname);
                    recordsByPath.Add(record.Pathname, record);
                    var meta = Encoding.UTF8.GetString(record.Meta);
                    var metaGuids = Regex.Matches(meta, @"^guid:[ \t]*([0-9a-f]{32})[ \t]*\r?$", RegexOptions.Multiline);
                    Assert.That(metaGuids.Count, Is.EqualTo(1), record.Pathname);
                    Assert.That(metaGuids[0].Groups[1].Value, Is.EqualTo(guid), record.Pathname);
                    Assert.That(AssetDatabase.AssetPathToGUID(record.Pathname), Is.EqualTo(guid), record.Pathname);
                    Assert.That(record.Meta, Is.EqualTo(File.ReadAllBytes(Path.Combine(projectRoot, record.Pathname + ".meta"))), record.Pathname + ".meta");
                    var isFolder = AssetDatabase.IsValidFolder(record.Pathname);
                    Assert.That(Directory.Exists(Path.Combine(projectRoot, record.Pathname)), Is.EqualTo(isFolder), record.Pathname);
                    // Existing folder metadata can predate the folderAsset marker; do not rewrite it during export.
                    if (Regex.IsMatch(meta, @"^folderAsset:[ \t]*yes[ \t]*\r?$", RegexOptions.Multiline))
                        Assert.That(isFolder, Is.True, record.Pathname);
                    if (isFolder)
                    {
                        if (record.MemberSizes.TryGetValue("asset", out var size)) Assert.That(size, Is.Zero, "Unexpected folder payload: " + record.Pathname);
                    }
                    else
                    {
                        Assert.That(record.MemberSizes.ContainsKey("asset"), Is.True, "Missing asset: " + record.Pathname);
                        var source = File.ReadAllBytes(Path.Combine(projectRoot, record.Pathname));
                        Assert.That(record.MemberSizes["asset"], Is.EqualTo(source.LongLength), record.Pathname);
                        using (var sha = new SHA256Managed())
                            Assert.That(record.PayloadHash, Is.EqualTo(sha.ComputeHash(source)), record.Pathname);
                    }
                    Assert.That(excludedRoots.Any(root => IsAtOrBelow(record.Pathname, root)), Is.False, "Excluded export path: " + record.Pathname);
                    Assert.That(Path.GetFileName(record.Pathname), Is.Not.EqualTo("DOTween.dll").IgnoreCase, "External DOTween must not be exported: " + record.Pathname);
                    Assert.That(retiredFiles.Contains(Path.GetFileName(record.Pathname), StringComparer.Ordinal), Is.False, "Retired export asset: " + record.Pathname);
                    Assert.That(roots.Any(root => IsAtOrBelow(record.Pathname, root)) || (isFolder && roots.Any(root => IsAtOrBelow(root, record.Pathname))),
                        Is.True, "Unexpected path outside export roots: " + record.Pathname);
                }

                foreach (var path in requiredPaths) Assert.That(recordsByPath.ContainsKey(path), Is.True, "Missing recursive asset: " + path);
                // Plugins 全递归，包含二进制、Demo 与辅助资产，不只核验源码扩展名。
                Assert.That(recordsByPath.Keys.Where(p => IsAtOrBelow(p, FrameworkRoot + "/Plugins")),
                    Is.EquivalentTo(requiredPaths.Where(p => IsAtOrBelow(p, FrameworkRoot + "/Plugins"))));
                Assert.That(recordsByPath.Keys.Where(p => Path.GetFileName(p) == "YLFramework.Analyzers.dll"), Is.EqualTo(new[] { AnalyzerPath }));
                Assert.That(recordsByPath.Keys.Any(p => Path.GetFileName(p).StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)), Is.False);
                var analyzerMeta = Encoding.UTF8.GetString(recordsByPath[AnalyzerPath].Meta);
                Assert.That(analyzerMeta, Does.Contain("guid: 471f81c4273ee66429677df44cbd6960"));
                Assert.That(Regex.Matches(analyzerMeta, @"(?m)^- RoslynAnalyzer\s*$").Count, Is.EqualTo(1));
                Assert.That(analyzerMeta, Does.Contain("isExplicitlyReferenced: 1"));
                Assert.That(Regex.Matches(analyzerMeta, @"(?m)^\s+enabled:\s*1\s*$"), Is.Empty);
                var importer = (PluginImporter)AssetImporter.GetAtPath(AnalyzerPath);
                Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
                Assert.That(importer.GetCompatibleWithEditor(), Is.False);
                Assert.That(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), Is.False);
                foreach (var path in sentinels)
                {
                    Assert.That(recordsByPath.TryGetValue(path, out var record), Is.True, "Missing fixed sentinel: " + path);
                    Assert.That(record.Payload, Is.Not.Null, path);
                    Assert.That(record.MemberSizes["asset"], Is.EqualTo((long)record.Payload.Length), path);
                    Assert.That(record.Payload, Is.EqualTo(File.ReadAllBytes(Path.Combine(projectRoot, path))), path);
                    Assert.That(record.Meta, Is.EqualTo(File.ReadAllBytes(Path.Combine(projectRoot, path + ".meta"))), path + ".meta");
                }
                Assert.That(Encoding.UTF8.GetString(recordsByPath[FrameworkRoot + "/Runtime/csc.rsp"].Payload), Does.Contain("-doc:Library/YLFramework.Runtime.xml"));
                Assert.That(recordsByGuid.ContainsKey(PrefabGuid), Is.True);
                Assert.That(recordsByGuid[PrefabGuid].Pathname, Is.EqualTo(PrefabPath));
                Assert.That(recordsByGuid.ContainsKey(CanvasGuid), Is.True);
                Assert.That(recordsByGuid[CanvasGuid].Pathname, Is.EqualTo(CanvasPath));
                foreach (var retiredGuid in new[]
                {
                    "29e4e189755c172469818bfbd3d8a3a4", "18a39294c5bc61646be12faf6a17fe94",
                    "ef050e654d014a7cb539e809f36326a2", "eaa2ff45b0824bb0a88fbda69f891959",
                    "5117b7f5560b4af8a25c8d427cbae882"
                }) Assert.That(recordsByGuid.ContainsKey(retiredGuid), Is.False, "Retired UI identity must not be distributed: " + retiredGuid);
                foreach (var retiredGuid in new[] { "6b5d25785704e1d488a8656928450940", "0b536a55a19ee624189b83d3c308893e", "3b3ac1c2986230d43a7e7cee1e54d130" })
                {
                    Assert.That(recordsByGuid.ContainsKey(retiredGuid), Is.False, "Retired utility identity must not be distributed: " + retiredGuid);
                    // Unity 在本 Editor 会话中保留最近删除的 GUID 路径；检查真实存续资产而非缓存墓碑。
                    var retiredPath = AssetDatabase.GUIDToAssetPath(retiredGuid);
                    if (!string.IsNullOrEmpty(retiredPath))
                    {
                        Assert.That(AssetDatabase.AssetPathToGUID(retiredPath, AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty, retiredGuid);
                        Assert.That(File.Exists(Path.Combine(projectRoot, retiredPath)), Is.False, retiredPath);
                        Assert.That(File.Exists(Path.Combine(projectRoot, retiredPath + ".meta")), Is.False, retiredPath + ".meta");
                    }
                }
                foreach (var oldPath in new[] { "/Runtime/XFMain/Manager/HttpManager.cs", "/Runtime/XFMain/AsyncOperation/SendWebRequest.cs" })
                    Assert.That(recordsByPath.ContainsKey(FrameworkRoot + oldPath), Is.False, oldPath);
                var prefabYaml = Encoding.UTF8.GetString(recordsByPath[PrefabPath].Payload);
                Assert.That(prefabYaml, Does.Match(@"m_Script:[ \t]*\{[^}\r\n]*\bguid:[ \t]*" + CanvasGuid + @"\b"));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Assert.That(prefab, Is.Not.Null, PrefabPath);
                foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                {
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, child.name);
                    foreach (Object component in child.GetComponents<Component>()) Assert.That(component, Is.Not.Null, child.name);
                }
                var canvas = prefab.GetComponent<ScreenTransitionCanvas>();
                Assert.That(canvas, Is.Not.Null);
                var canvasScript = MonoScript.FromMonoBehaviour(canvas);
                Assert.That(canvasScript, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(canvasScript), Is.EqualTo(CanvasPath));
                Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(canvasScript)), Is.EqualTo(CanvasGuid));
                var uiGuids = new[] { "0cd44c1031e13a943bb63640046fad76", "fe87c0e1cc204ed48ad3b37840f39efc" };
                var uiTypes = new[] { typeof(CanvasScaler), typeof(Image) };
                for (var i = 0; i < uiGuids.Length; i++)
                {
                    Assert.That(prefabYaml, Does.Match(@"m_Script:[ \t]*\{[^}\r\n]*\bguid:[ \t]*" + uiGuids[i] + @"\b"));
                    var uiPath = AssetDatabase.GUIDToAssetPath(uiGuids[i]);
                    Assert.That(uiPath, Is.Not.Empty, uiGuids[i]);
                    var uiScript = AssetDatabase.LoadAssetAtPath<MonoScript>(uiPath);
                    Assert.That(uiScript, Is.Not.Null, uiPath);
                    Assert.That(uiScript.GetClass(), Is.EqualTo(uiTypes[i]), uiPath);
                    Assert.That(AssetDatabase.AssetPathToGUID(uiPath), Is.EqualTo(uiGuids[i]), uiPath);
                }
            }
            finally
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
        }

        private static bool IsAtOrBelow(string path, string root)
        {
            return path == root || path.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static byte[] ReadEntry(TarInputStream tar, TarEntry entry)
        {
            using (var memory = new MemoryStream())
            {
                tar.CopyEntryContents(memory);
                Assert.That(memory.Length, Is.EqualTo(entry.Size), "Truncated package member: " + entry.Name);
                return memory.ToArray();
            }
        }

        private sealed class PackageRecord
        {
            public readonly Dictionary<string, long> MemberSizes = new Dictionary<string, long>(StringComparer.Ordinal);
            public string Pathname;
            public byte[] Meta;
            public byte[] Payload;
            public byte[] PayloadHash;
        }
    }
}
