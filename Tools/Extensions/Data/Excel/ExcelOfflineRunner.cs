using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

internal static class ExcelOfflineRunner
{
    public static int Main()
    {
        var assembly = Assembly.GetExecutingAssembly();
        string output = Path.GetDirectoryName(assembly.Location);
        string optional = Path.Combine(output, "YLFramework.Excel.Editor.dll");
        if (File.Exists(optional)) Assembly.LoadFrom(optional);
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(assembly, new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0, ["WorkDirectory"] = output });
        var filter = TestFilter.FromXml("<filter><class>YLFramework.Tests.Extensions.Data.ExcelAuthoringTests</class></filter>");
        var result = runner.Run(TestListener.NULL, filter);
        Print(result);
        File.WriteAllText(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml);
        Console.WriteLine($"TOTAL passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
        return result.FailCount == 0 && result.SkipCount == 0 && result.InconclusiveCount == 0 && result.PassCount != 0 ? 0 : 1;
    }
    private static void Print(ITestResult result)
    {
        if (!result.HasChildren) Console.WriteLine(result.ResultState + " " + result.FullName + (result.Message == null ? string.Empty : ": " + result.Message));
        foreach (var child in result.Children) Print(child);
    }
}
