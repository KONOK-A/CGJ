using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

// 真实 NUnit 执行 Fixture/TestCase/清理，只选择无需 Unity 原生调用的源码。
internal static class NUnitHost
{
    private static int Main(string[] args)
    {
        try
        {
            var discover = args.Length > 0 && args[0] == "--yl-discover";
            var runSelection = args.Length > 0 && args[0] == "--yl-run";
            var explicitOutput = discover || runSelection;
            if (explicitOutput && args.Length != 3)
            {
                throw new ArgumentException("Expected --yl-discover <category> <output-directory> or --yl-run <filter-xml> <output-directory>.");
            }
            if (!explicitOutput && args.Any(value => value.StartsWith("--", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Unknown NUnit host option.");
            }

            var assembly = Assembly.GetExecutingAssembly();
            var output = explicitOutput ? Path.GetFullPath(args[2]) : Path.GetDirectoryName(assembly.Location);
            if (!Directory.Exists(output))
            {
                throw new DirectoryNotFoundException("The caller must prepare the output directory.");
            }

            var filter = discover
                ? TestFilter.FromXml("<filter><cat>" + SecurityElement.Escape(args[1]) + "</cat></filter>")
                : runSelection ? TestFilter.FromXml(File.ReadAllText(args[1]))
                : args.Length == 0 ? TestFilter.Empty : TestFilter.FromXml("<filter><or>" +
                    string.Join("", args.Select(name => "<class>" + SecurityElement.Escape(name) + "</class>")) + "</or></filter>");
            var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
            runner.Load(assembly, new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0, ["WorkDirectory"] = output });
            if (discover)
            {
                WriteOutput(Path.Combine(output, "discovery.xml"), runner.ExploreTests(filter).ToXml(true).OuterXml, true);
                Console.WriteLine($"NUnit discovery: {runner.CountTestCases(filter)} leaves. Tests were not executed.");
                return runner.CountTestCases(filter) > 0 ? 0 : 1;
            }

            var result = runner.Run(TestListener.NULL, filter);
            Print(result);
            WriteOutput(Path.Combine(output, "results.xml"), result.ToXml(true).OuterXml, explicitOutput);
            Console.WriteLine($"Pure regression: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped.");
            return result.ResultState.Status == TestStatus.Passed && result.FailCount == 0 && result.SkipCount == 0 &&
                result.InconclusiveCount == 0 && result.PassCount != 0 ? 0 : 1;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static void WriteOutput(string path, string content, bool preserveExisting)
    {
        using var stream = new FileStream(path, preserveExisting ? FileMode.CreateNew : FileMode.Create, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
    private static void Print(ITestResult result)
    {
        if (!result.HasChildren)
            Console.WriteLine(result.ResultState + " " + result.FullName + (result.Message == null ? "" : ": " + result.Message));
        foreach (var child in result.Children)
            Print(child);
    }
}
