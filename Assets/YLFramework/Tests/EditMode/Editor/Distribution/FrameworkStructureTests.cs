using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Tests
{
    public class FrameworkStructureTests
    {
        [Serializable]
        private sealed class Move
        {
            public string From;
            public string To;
            public string Guid;
        }

        [Serializable]
        private sealed class MoveList
        {
            public Move[] Moves;
        }

        [Test]
        public void ProductLayoutPreservesEveryRecordedAssetIdentity()
        {
            const string Root = "Assets/YLFramework/";
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Tests/Fixtures/NamespaceMigration/ProductMoves.json");
            Assert.That(manifest, Is.Not.Null);
            var moves = JsonUtility.FromJson<MoveList>(manifest.text).Moves;
            Assert.That(moves.Length, Is.EqualTo(28));
            foreach (var move in moves)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(Root + move.To), Is.EqualTo(move.Guid), move.To);
                Assert.That(AssetDatabase.GUIDToAssetPath(move.Guid), Is.EqualTo(Root + move.To), move.Guid);
                Assert.That(File.Exists(Root + move.From) || Directory.Exists(Root + move.From), Is.False, move.From);
            }
        }

        [Test]
        public void BridgeAndSharedAuthoringTypesHaveSeparateOwnedFiles()
        {
            foreach (var path in new[]
            {
                "Runtime/Systems/Resource/Integration/Audio/ResourceSystem.Audio.cs",
                "Runtime/Systems/Resource/Integration/Audio/ResourceAudioClipProvider.cs",
                "Editor/Systems/Shared/DomainSessionLifecycle.cs",
                "Editor/Systems/Shared/DomainResourceIconGui.cs"
            })
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/YLFramework/" + path), Is.Not.Null, path);
            }
            Assert.That(File.ReadAllText("Assets/YLFramework/Runtime/Systems/Resource/Integration/Audio/ResourceAudioClipProvider.cs"),
                Does.Not.Contain("partial class ResourceSystem"));
            Assert.That(File.ReadAllText("Assets/YLFramework/Editor/Systems/Skill/SkillAuthoring.cs"),
                Does.Not.Contain("class DomainResourceIconGui"));
        }
    }
}
