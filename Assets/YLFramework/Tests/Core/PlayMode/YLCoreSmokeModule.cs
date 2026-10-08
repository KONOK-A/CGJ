using System;
using System.Collections.Generic;

namespace YLFramework.Core.Tests
{
    public sealed class YLCoreSmokeModule : YLFramework.Core.Module
    {
        public const string FixtureRoot = "Assets/YLFramework/Tests/Core/Fixtures/YLCoreSmokeModule";
        public const string Project = nameof(YLCoreSmokeModule);
        public const string Table = "YLCoreSmokeRows";
        public const string SceneName = "YLCoreSmokeScene";
        public const string HarnessName = "YLCoreSmokeHarness";
        public const string ScenePath = FixtureRoot + "/" + SceneName + ".unity";
        public const string HarnessPath = FixtureRoot + "/" + HarnessName + ".unity";
        public const string Json = "[{\"Id\":101,\"Marker\":\"yl-smoke\"}]";

        public bool StartWithScene;
        public int StartCount { get; private set; }
        public bool RowReadyAtStart { get; private set; }
        public bool SceneReadyAtStart { get; private set; }

        // Startup reads the table/scene before it assigns ProjectName = type.Name.
        public YLCoreSmokeModule() => ProjectName = Project;

#pragma warning disable CS0618
        public override Dictionary<string, Type> PreloadConfigTables =>
            new Dictionary<string, Type> { { Table, typeof(YLCoreSmokeRow[]) } };
#pragma warning restore CS0618

        public override string DefaultStartUpScene => StartWithScene ? SceneName : string.Empty;

        public override void OnStart()
        {
            StartCount++;
            var row = GetModel<YLCoreSmokeRow>(101);
            RowReadyAtStart = row != null && row.Marker == "yl-smoke" && ReferenceEquals(row.Module, this);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            SceneReadyAtStart = scene.IsValid() && scene.isLoaded;
        }
    }

    public sealed class YLCoreSmokeRow : YLFramework.Core.Model
    {
        public string Marker { get; set; }
        public int InitCount { get; private set; }
        public int ExitCount { get; private set; }
        public override void OnInit() => InitCount++;
        public override void OnExit() => ExitCount++;
    }
}
