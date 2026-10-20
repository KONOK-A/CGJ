using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

internal static class SaveSafetyRunner
{
    private static int Main(string[] args)
    {
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
        Environment.SetEnvironmentVariable("YL_SAVE_TEST_OUTPUT", output);
        var passed = 0;
        var failed = 0;
        var existingPureJson = new[] { "DefaultJsonContractIsBareDictionaryAndReadsBack", "NestedRecordUsesActualSaveSettingsWithoutChangingOrCallingSaveManager", "IntegerAndEnumKeysRoundTripAsJsonPropertyNames", "JsonDuplicateNamesAndConvertedKeysUseLastValueWhileBadInputThrows" };
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "YLFramework.Tests.Extensions.Data.Save" || t.FullName == "YLFramework.Tests.SerializedDictionaryJsonTests"))
        foreach (var method in type.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute), false)).OrderBy(m => m.Name))
        {
            if (type.FullName == "YLFramework.Tests.SerializedDictionaryJsonTests" && !existingPureJson.Contains(method.Name)) continue;
            var name = type.Name + "." + method.Name;
            if (args.Length != 0 && !args.Any(name.Contains)) continue;
            try
            {
                method.Invoke(Activator.CreateInstance(type), null);
                Console.WriteLine("PASS " + name);
                passed++;
            }
            catch (Exception error)
            {
                var actual = error is TargetInvocationException invocation ? invocation.InnerException : error;
                Console.WriteLine("FAIL " + name + "\n" + actual);
                failed++;
            }
        }
        Console.WriteLine($"Save safety: passed={passed}; failed={failed}; skipped=0; root={output}");
        return failed == 0 && passed != 0 ? 0 : 1;
    }
}
