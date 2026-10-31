using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace YLFramework.Tests
{
    internal static class TestDependencyGuard
    {
        internal static bool IsApprovedBouncyCastleReference(string path)
        {
            var approved = Path.Combine(Application.dataPath, "YLFramework/Plugins/BouncyCastle/BouncyCastle.Cryptography.dll");
            if (!string.Equals(Path.GetFullPath(path), Path.GetFullPath(approved), StringComparison.OrdinalIgnoreCase) || !File.Exists(approved))
            {
                return false;
            }
            using var stream = File.OpenRead(approved);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() ==
                "d61c1f2ba929a230a58e101ccd850e21f2675fa6b9814ec279633e8a089c3495";
        }

        internal static bool IsAllowedReference(string path, string unityContents, string ui, bool editor, string json = null)
        {
            var absolute = Path.GetFullPath(path);
            if (IsApprovedBouncyCastleReference(absolute) ||
                (json != null && string.Equals(absolute, Path.GetFullPath(json), StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(absolute, Path.GetFullPath(ui), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var name = Path.GetFileName(absolute);
            if ((name == "UnityEngine.dll" || name.StartsWith("UnityEngine.", StringComparison.Ordinal)) && name != "UnityEngine.TestRunner.dll")
            {
                return IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "PlaybackEngines")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(unityContents, "Managed/UnityEngine.dll")), StringComparison.OrdinalIgnoreCase);
            }
            if (editor && (name == "UnityEditor.dll" || name.StartsWith("UnityEditor.", StringComparison.Ordinal)) && name != "UnityEditor.TestRunner.dll")
            {
                return IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEngine")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "Managed/UnityEditor")) ||
                    string.Equals(absolute, Path.GetFullPath(Path.Combine(unityContents, "Managed/UnityEditor.dll")), StringComparison.OrdinalIgnoreCase);
            }
            if (name == "mscorlib.dll" || name == "netstandard.dll" || name == "Microsoft.CSharp.dll" || name == "System.dll" || name.StartsWith("System.", StringComparison.Ordinal))
            {
                return IsInsideFolder(absolute, Path.Combine(unityContents, "NetStandard")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "UnityReferenceAssemblies")) ||
                    IsInsideFolder(absolute, Path.Combine(unityContents, "MonoBleedingEdge/lib/mono"));
            }
            return false;
        }

        internal static bool IsInsideFolder(string path, string folder)
        {
            var prefix = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
