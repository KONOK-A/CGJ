using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

// 真实 NUnit 执行 Fixture/TestCase/清理，只选择无需 Unity 原生调用的源码。
internal static class UIPanelOfflineRunner
{
    private static int Main(string[] classes)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var output = Path.GetDirectoryName(assembly.Location);
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(assembly, new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0, ["WorkDirectory"] = output });
        var filter = classes.Length == 0 ? TestFilter.Empty : TestFilter.FromXml("<filter><or>" +
            string.Join("", classes.Select(name => "<class>" + name + "</class>")) + "</or></filter>");
        var result = runner.Run(TestListener.NULL, filter);
        Print(result);
        File.WriteAllText(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml);
        Console.WriteLine($"Pure regression: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
        return result.FailCount == 0 && result.SkipCount == 0 && result.InconclusiveCount == 0 && result.PassCount != 0 ? 0 : 1;
    }
    private static void Print(ITestResult result)
    {
        if (!result.HasChildren) Console.WriteLine(result.ResultState + " " + result.FullName + (result.Message == null ? "" : ": " + result.Message));
        foreach (var child in result.Children) Print(child);
    }
}
