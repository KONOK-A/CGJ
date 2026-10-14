using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.Core.Editor
{ 
    /// <summary>
    /// 项目全局设置(配置文件保存在Assets的同级目录ProjectSettings下面)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProjectSettingsScriptableObject<T> : ScriptableObject where T : ScriptableObject
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (!instance)
                {
                    LoadOrCreate();
                }
                return instance;
            }
        }
        public static void LoadOrCreate()
        {
            string filePath = GetFilePath();

            if (File.Exists(filePath))
            {
                var arr = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
                if (arr.Length != 1 || !arr[0] || arr[0].GetType() != typeof(T))
                {
                    foreach (var loadedObject in arr)
                    {
                        if (loadedObject) DestroyImmediate(loadedObject);
                    }
                    throw new IOException("Refusing to replace incompatible YL settings: " + filePath);
                }
                instance = (T)arr[0];
            }
            else
            {
                instance = CreateInstance<T>();
                Save();
            }

        }

        public static void Save(bool saveAsText = true)
        {
            if (!instance)
                throw new InvalidOperationException("Load YL settings before saving them.");

            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // Only an existing file containing this exact settings type may be updated.
                if (File.Exists(filePath))
                {
                    var existing = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
                    bool compatible = existing.Length == 1 && existing[0] != null && existing[0].GetType() == typeof(T);
                    foreach (var loadedObject in existing)
                    {
                        if (loadedObject) DestroyImmediate(loadedObject);
                    }
                    if (!compatible)
                        throw new IOException("Refusing to overwrite incompatible YL settings: " + filePath);
                }

                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                UnityEngine.Object[] obj = new T[1] { instance };
                InternalEditorUtility.SaveToSerializedFileAndForget(obj, filePath, saveAsText);
            }
        }
        protected static string GetFilePath()
        {
            string fullName = typeof(T).FullName;
            if (string.IsNullOrEmpty(fullName) || fullName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fullName.IndexOf('/') >= 0 || fullName.IndexOf('\\') >= 0)
                throw new InvalidOperationException("A safe, unique FullName is required for YL settings.");

            string filePath = string.Format("ProjectSettings/YLFramework/Core/{0}.asset", fullName);
            for (string parent = Path.GetFullPath(filePath); !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            {
                if ((Directory.Exists(parent) || File.Exists(parent)) &&
                    (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked settings paths are not supported: " + parent);
            }
            return filePath;
        }


        public void OpenProjectSettings(string path)
        {
            ProjectSettingTools.OpenProjectSettings(path);
        }

    } 
}
