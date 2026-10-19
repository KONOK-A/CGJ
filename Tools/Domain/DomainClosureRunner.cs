using System;
using System.Reflection;

internal static class DomainClosureRunner
{
    private static int Main(string[] paths)
    {
        var failed = false;
        foreach (var path in paths)
        {
            var assembly = Assembly.ReflectionOnlyLoadFrom(path);
            var name = assembly.GetName().Name;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                Console.WriteLine(name + " -> " + reference.Name);
                if (reference.Name.StartsWith("XF", StringComparison.Ordinal) || reference.Name.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                    reference.Name == "MiniExcel" || reference.Name == "YLFramework.Excel.Editor") failed = true;
            }
        }
        Console.WriteLine(failed ? "Forbidden Player reference found." : "Player assembly reference closure has no XF, UnityEditor or optional Excel backend.");
        return failed ? 1 : 0;
    }
}
