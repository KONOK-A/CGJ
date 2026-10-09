using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YLFramework.Core.Editor
{

    public static class ScriptableObjectExtensions
    {
#if UNITY_EDITOR

        // Fix编码
        public const string GeneratedAssetRoot = "Assets/YLFramework/Editor/XFMain/GeneratedAssets";

        /// <summary>
        /// 在YL专用生成目录下创建资源文件
        /// </summary>
        /// <param name="scriptableObject"></param> 
        public static void CreateAsset(this ScriptableObject scriptableObject)
        {
            scriptableObject.CreateAsset(GeneratedAssetRoot);
        }


        public static void CreateAsset(this ScriptableObject scriptableObject,string path)
        {
            if (!scriptableObject)
                throw new ArgumentNullException(nameof(scriptableObject));
            if (AssetDatabase.Contains(scriptableObject))
                throw new InvalidOperationException("Create a new ScriptableObject instance; existing assets must not be moved or replaced.");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("An explicit YL generated asset path is required.", nameof(path));

            string projectRoot = Path.GetDirectoryName(Application.dataPath) + Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(Path.Combine(projectRoot, GeneratedAssetRoot));
            string fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Assets may only be created under " + GeneratedAssetRoot, nameof(path));

            // Reject linked ancestors so the dedicated path cannot redirect writes into existing resources.
            for (string parent = fullPath; !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            {
                if ((Directory.Exists(parent) || File.Exists(parent)) &&
                    (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked asset paths are not supported: " + parent);
            }

            path = fullPath.Substring(projectRoot.Length).Replace('\\', '/');
            if (File.Exists(fullPath))
            {
                if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The YL generated asset root must be a folder: " + path);
                path = Path.GetDirectoryName(path).Replace('\\', '/');
            }

            string[] folders = path.Split('/');
            string folder = folders[0];
            for (int i = 1; i < folders.Length; i++)
            {
                string next = folder + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (File.Exists(next) || File.Exists(next + ".meta") || Directory.Exists(next))
                        throw new IOException("Refusing to replace an existing or unimported asset: " + next);
                    if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(folder, folders[i])))
                        throw new IOException("Could not create YL asset folder: " + next);
                }
                folder = next;
            }

            string name = GetName(scriptableObject.GetType().Name, path);
            string assetPath = string.Format("{0}/{1}.asset", path, name);
            if (File.Exists(assetPath) || Directory.Exists(assetPath) || File.Exists(assetPath + ".meta") ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
                throw new IOException("Refusing to overwrite an existing asset: " + assetPath);

            AssetDatabase.CreateAsset(scriptableObject, assetPath);
            Selection.activeObject = scriptableObject;
        }


        private static string GetName(string name, string path)
        {
            string tempName = name;


            string[] matchs = AssetDatabase.FindAssets(string.Format("t:ScriptableObject {0}", tempName), new string[] { path });

            int count = 0;

            while (matchs != null && matchs.Length > 0)
            {
                count++;


                tempName = string.Format("{0} ({1})", name, count);
                matchs = AssetDatabase.FindAssets(string.Format("t:ScriptableObject {0}", tempName), new string[] { path });
            }


            return tempName;
        }

#endif
    }

}
