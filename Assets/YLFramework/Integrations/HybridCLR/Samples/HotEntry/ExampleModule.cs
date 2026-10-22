using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using YLFramework.Core;

namespace YLFramework.Samples.HybridCLR.HotUpdate
{
    [Preserve]
    public sealed class ExampleMessage : Model
    {
        public string Text { get; set; }
    }

    [Preserve]
    public sealed class ExampleModule : Module
    {
        public override string ProjectName { get => HybridCLRExampleState.ProjectId; set { } }
#pragma warning disable CS0672, CS0618
        public override Dictionary<string, Type> PreloadConfigTables => new Dictionary<string, Type>
        {
            { "sample/message.json", typeof(ExampleMessage[]) }
        };
#pragma warning restore CS0672, CS0618
        public override void OnStart()
        {
            var message = GetModel<ExampleMessage>(1);
            if (message == null) throw new InvalidOperationException("The YL Core configuration did not create the sample Model.");
            HybridCLRExampleState.Message = VersionInfo.Version + ": " + message.Text + "; dependency-count=" + VersionInfo.Count(new List<int> { 1, 2 });
            HybridCLRExampleState.Async = VersionInfo.DescribeAsync();
        }
    }
}
