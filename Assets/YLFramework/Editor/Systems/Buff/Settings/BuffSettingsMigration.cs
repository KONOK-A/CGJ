using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YLFramework.Editor.Buff
{
    internal static class BuffSettingsMigration
    {
        internal const string MenuPath = "YLFramework/Buff/Migrate Legacy Project Settings";

        internal static string Migrate()
        {
            string previousPath = BuffSystemSettings.PreviousFilePath;
            string currentPath = BuffSystemSettings.CurrentFilePath;
            if (!File.Exists(previousPath))
            {
                return null;
            }

            if (File.Exists(currentPath))
            {
                throw new IOException("Refusing conflicting Buff settings. Keep both files for manual review: " +
                    previousPath + " and " + currentPath);
            }

            byte[] original = File.ReadAllBytes(previousPath);
            var objects = InternalEditorUtility.LoadSerializedFileAndForget(previousPath);
            try
            {
                if (objects.Length != 1 || !objects[0] || objects[0].GetType() != typeof(BuffSystemSettings))
                {
                    throw new IOException("Refusing incompatible legacy Buff settings: " + previousPath);
                }

                // Serialize fields only: Buff's collection getters can synchronize assets and save settings.
                string snapshot = EditorJsonUtility.ToJson(objects[0]);
                if (!File.ReadAllBytes(previousPath).SequenceEqual(original))
                {
                    throw new IOException("Legacy Buff settings changed during validation: " + previousPath);
                }

                string backupPath = previousPath + "." + Guid.NewGuid().ToString("N") + ".bak";
                try
                {
                    using (var backup = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        backup.Write(original, 0, original.Length);
                        backup.Flush(true);
                    }

                    if (!File.ReadAllBytes(backupPath).SequenceEqual(original))
                    {
                        throw new IOException("Buff settings backup failed byte verification: " + backupPath);
                    }

                    // Recheck the exact paths after Unity deserialization and before the non-overwriting move.
                    previousPath = BuffSystemSettings.PreviousFilePath;
                    currentPath = BuffSystemSettings.CurrentFilePath;
                    if (!File.ReadAllBytes(previousPath).SequenceEqual(original))
                    {
                        throw new IOException("Legacy Buff settings changed before the move: " + previousPath);
                    }
                    File.Move(previousPath, currentPath);

                    if (!File.ReadAllBytes(currentPath).SequenceEqual(original))
                    {
                        throw new IOException("Migrated Buff settings failed byte verification: " + currentPath);
                    }

                    BuffSettingsProvider.Instance.Reload();
                    var reloaded = BuffSystemSettings.Reload();
                    if (EditorJsonUtility.ToJson(reloaded) != snapshot ||
                        !File.ReadAllBytes(currentPath).SequenceEqual(original) ||
                        !File.ReadAllBytes(backupPath).SequenceEqual(original))
                    {
                        throw new IOException("Migrated Buff settings fields or object references did not reload unchanged.");
                    }

                    return backupPath;
                }
                catch (IOException exception)
                {
                    throw new IOException("Buff settings migration stopped without automatic rollback. Inspect " +
                        previousPath + " and " + currentPath + ". Backup candidate (if created): " + backupPath +
                        ". " + exception.Message, exception);
                }
            }
            finally
            {
                foreach (var loadedObject in objects)
                {
                    if (loadedObject)
                    {
                        UnityEngine.Object.DestroyImmediate(loadedObject);
                    }
                }
            }
        }

        [MenuItem(MenuPath)]
        private static void MigrateFromMenu()
        {
            try
            {
                string backupPath = Migrate();
                EditorUtility.DisplayDialog("Buff Settings Migration", backupPath == null
                    ? "No legacy Buff settings found. No files were changed."
                    : "Buff settings migrated and reloaded. Keep the verified backup:\n" + backupPath, "OK");
            }
            catch (IOException exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Buff Settings Migration Failed", exception.Message, "OK");
            }
        }
    }
}
