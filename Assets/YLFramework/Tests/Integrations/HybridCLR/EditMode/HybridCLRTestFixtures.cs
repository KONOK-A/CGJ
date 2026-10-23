using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using YLFramework.Integrations.HybridCLR;

namespace YLFramework.Tests.Integrations.HybridCLR
{
    public static class HybridCLRTestFixtures
    {
        public const string Root = "Assets/YLFramework/Tests/Integrations/HybridCLR/PlayMode/Resources/YLHybridCLRTests";

        public static void Prepare()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Prepare owned test inputs before PlayMode.");
            }

            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "YLFramework.HybridCLR.Tests.PlayMode");
            var entryBytes = File.ReadAllBytes(assembly.Location);
            var aot = typeof(HybridCLRManifest).Assembly;
            var metadata = File.ReadAllBytes(aot.Location);
            Directory.CreateDirectory(Root);
            File.WriteAllBytes(Root + "/Entry.bytes", entryBytes);
            var conflictBytes = entryBytes.Concat(new byte[] { 1 }).ToArray();
            File.WriteAllBytes(Root + "/ConflictEntry.bytes", conflictBytes);
            if (File.Exists(Root + "/Metadata.bytes"))
            {
                AssetDatabase.DeleteAsset(Root + "/Metadata.bytes");
            }

            File.WriteAllBytes(Root + "/MetadataPayload.bytes", metadata);
            foreach (var variant in new[] { "Good", "BadHash", "BadMvid", "Metadata", "Missing", "Conflict", "External" })
            {
                HybridCLRPayload Payload(System.Reflection.Assembly source, byte[] bytes, bool isAot) => new HybridCLRPayload
                {
                    Name = source.GetName().Name,
                    Identity = source.GetName().FullName,
                    Mvid = source.ManifestModule.ModuleVersionId.ToString("D"),
                    Address = HybridCLRManifest.PayloadAddress(source.GetName().Name, isAot),
                    Length = bytes.Length,
                    Sha256 = HybridCLRManifest.Hash(bytes)
                };
                var entry = Payload(assembly, variant == "Conflict" ? conflictBytes : entryBytes, false);
                if (variant == "BadHash")
                {
                    entry.Sha256 = new string('a', 64);
                }

                if (variant == "BadMvid")
                {
                    entry.Mvid = "74a011fb-8799-4f16-9d21-8fe27d01771d";
                }

                if (variant == "External")
                {
                    entry.Name = "Missing.External";
                    entry.Identity = "Missing.External, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                    entry.Address = HybridCLRManifest.PayloadAddress(entry.Name, false);
                }
                var manifest = new HybridCLRManifest
                {
                    ProjectId = "hybridclr-test",
                    AppContentCompatibilityId = "base-test",
                    UnityVersion = Application.unityVersion,
                    Development = Debug.isDebugBuild,
                    HotUpdateAssemblies = new[] { entry },
                    AotMetadata = variant == "Metadata" ? new[] { Payload(aot, metadata, true) } : Array.Empty<HybridCLRPayload>()
                };
                manifest.Seal();
                File.WriteAllText(Root + "/" + variant + ".json", manifest.ToJson());
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
