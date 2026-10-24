using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.Editor.Utilities
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class PreviousProjectSettingsNameAttribute : Attribute
    {
        internal string FullName
        {
            get;
        }

        public PreviousProjectSettingsNameAttribute(string fullName)
        {
            FullName = fullName;
        }
    }

    /// <summary>
    /// 项目全局设置(配置文件保存在Assets的同级目录ProjectSettings下面)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ProjectSettingsScriptableObject<T> : ScriptableObject where T : ScriptableObject
    {
        private static readonly PreviousProjectSettingsNameAttribute _previousName =
            typeof(T).GetCustomAttribute<PreviousProjectSettingsNameAttribute>(false);
        private static T _instance;

        internal static string CurrentFilePath => GetFilePath();
        internal static string PreviousFilePath => _previousName == null ? null : BuildFilePath(_previousName.FullName);

        public static T Instance
        {
            get
            {
                RejectPreviousSettings();
                if (!_instance)
                {
                    LoadOrCreate();
                }
                return _instance;
            }
        }
        public static void LoadOrCreate()
        {
            string filePath = GetFilePath();
            RejectPreviousSettings();

            if (File.Exists(filePath))
            {
                _instance = LoadExisting(filePath);
            }
            else
            {
                _instance = CreateInstance<T>();
                Save();
            }

        }

        public static void Save(bool saveAsText = true)
        {
            RejectPreviousSettings();
            if (!_instance)
            {
                throw new InvalidOperationException("Load YL settings before saving them.");
            }

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
                        if (loadedObject)
                        {
                            DestroyImmediate(loadedObject);
                        }
                    }
                    if (!compatible)
                    {
                        throw new IOException("Refusing to overwrite incompatible YL settings: " + filePath);
                    }
                }

                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                UnityEngine.Object[] obj = new T[1] { _instance };
                RejectPreviousSettings();
                filePath = GetFilePath();
                InternalEditorUtility.SaveToSerializedFileAndForget(obj, filePath, saveAsText);
            }
        }

        internal static T Reload()
        {
            string filePath = GetFilePath();
            RejectPreviousSettings();
            T previous = _instance;
            _instance = null;
            try
            {
                _instance = LoadExisting(filePath);
                return _instance;
            }
            finally
            {
                if (previous)
                {
                    DestroyImmediate(previous);
                }
            }
        }

        private static T LoadExisting(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new IOException("Cannot reload missing YL settings: " + filePath);
            }

            var objects = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            if (objects.Length == 1 && objects[0] && objects[0].GetType() == typeof(T))
            {
                return (T)objects[0];
            }

            foreach (var loadedObject in objects)
            {
                if (loadedObject)
                {
                    DestroyImmediate(loadedObject);
                }
            }
            throw new IOException("Refusing to replace incompatible YL settings: " + filePath);
        }

        private static void RejectPreviousSettings()
        {
            string previous = PreviousFilePath;
            if (previous != null && File.Exists(previous))
            {
                throw new IOException("Previous YL settings require explicit migration before loading or saving: " + previous);
            }
        }

        protected static string GetFilePath()
        {
            return BuildFilePath(typeof(T).FullName);
        }

        private static string BuildFilePath(string fullName)
        {
            if (string.IsNullOrEmpty(fullName) || fullName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                fullName.IndexOf('/') >= 0 || fullName.IndexOf('\\') >= 0)
            {
                throw new InvalidOperationException("A safe, unique FullName is required for YL settings.");
            }

            string filePath = string.Format("ProjectSettings/YLFramework/Core/{0}.asset", fullName);
            string fullPath = Path.GetFullPath(filePath);
            for (string parent = fullPath; !string.IsNullOrEmpty(parent); parent = Path.GetDirectoryName(parent))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(parent);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked settings paths are not supported: " + parent);
                }

                bool directory = (attributes & FileAttributes.Directory) != 0;
                if (parent == fullPath ? directory : !directory)
                {
                    throw new IOException("Expected a settings file under real directories: " + parent);
                }
            }
            return filePath;
        }


        public void OpenProjectSettings(string path)
        {
            ProjectSettingTools.OpenProjectSettings(path);
        }

    }
}
