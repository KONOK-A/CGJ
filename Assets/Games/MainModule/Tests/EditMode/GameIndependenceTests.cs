using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;

public class GameIndependenceTests
{
    [TestCase(AssembliesType.Editor)]
    [TestCase(AssembliesType.Player)]
    public void GameCompilationAndBinaryReferencesNeverRequireYL(AssembliesType profile)
    {
        var roots = CompilationPipeline.GetAssemblies(profile).Where(a => a.name.StartsWith("Project.Game.MainModule", StringComparison.Ordinal)).ToArray();
        Assert.That(roots, Is.Not.Empty);
        var pending = new Stack<UnityEditor.Compilation.Assembly>(roots);
        var visited = new HashSet<string>();
        while (pending.Count > 0)
        {
            var item = pending.Pop();
            if (!visited.Add(item.name)) continue;
            Assert.IsFalse(item.name.StartsWith("YLFramework", StringComparison.Ordinal), item.name);
            foreach (var reference in item.assemblyReferences) pending.Push(reference);
        }
        foreach (var root in roots)
        {
            var binaries = new Stack<System.Reflection.Assembly>(new[] { System.Reflection.Assembly.Load(root.name) });
            visited.Clear();
            while (binaries.Count > 0)
            {
                var item = binaries.Pop();
                if (!visited.Add(item.FullName)) continue;
                foreach (var reference in item.GetReferencedAssemblies())
                {
                    Assert.IsFalse(reference.Name.StartsWith("YLFramework", StringComparison.Ordinal), item.FullName + " -> " + reference);
                    binaries.Push(System.Reflection.Assembly.Load(reference));
                }
            }
        }
    }

    [Test]
    public void GameResourceClosureNeverReachesYLAssets()
    {
        var roots = AssetDatabase.FindAssets("", new[] { "Assets/Games/MainModule" }).Select(AssetDatabase.GUIDToAssetPath)
            .Concat(new[] { "Assets/Scenes/Main.unity", "Assets/Resources/XFABManagerSettings.asset", "Assets/Resources/XFGameFramework/Canvas.prefab", "Assets/Resources/XFGameFramework/EventSystem.prefab" }).ToArray();
        foreach (var path in AssetDatabase.GetDependencies(roots, true))
            Assert.IsFalse(path.StartsWith("Assets/YLFramework", StringComparison.Ordinal), path);
    }
}
