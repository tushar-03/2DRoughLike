using System;
using System.IO;
using UnityEngine;

namespace Roughlike.Core
{
    /// <summary>
    /// Reads and writes save files (JSON text) safely: every write goes to a temp file first, the previous version is kept
    /// as a backup, and reads fall back to the backup if the main file is missing or unreadable. Files live in the
    /// persistent data folder (tests point it at a temp folder).
    /// </summary>
    public static class SaveService
    {
        static string _directoryOverride;

        public static string Directory
        {
            get
            {
                string dir = _directoryOverride ?? Application.persistentDataPath;
                System.IO.Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Redirects saves to another folder (tests). Pass null to restore the default.</summary>
        public static void SetDirectory(string directory) => _directoryOverride = directory;

        static string PathOf(string name, string suffix = ".json") => System.IO.Path.Combine(Directory, name + suffix);

        public static bool Exists(string name) => File.Exists(PathOf(name)) || File.Exists(PathOf(name, ".bak"));

        public static void Write(string name, string json)
        {
            string main = PathOf(name);
            string temp = PathOf(name, ".tmp");
            string backup = PathOf(name, ".bak");

            File.WriteAllText(temp, json);
            if (File.Exists(main))
            {
                File.Copy(main, backup, true);   // keep the last good version
                File.Delete(main);
            }
            File.Move(temp, main);
            Roughlike.Utils.WebBridge.SyncFileSystem();   // browsers keep files in IndexedDB; flush it
        }

        /// <summary>Reads a save. If the main file is missing, empty or fails <paramref name="validate"/>, the backup is tried.</summary>
        public static bool TryRead(string name, out string json, Func<string, bool> validate = null)
        {
            foreach (string suffix in new[] { ".json", ".bak" })
            {
                string path = PathOf(name, suffix);
                if (!File.Exists(path)) continue;
                try
                {
                    string text = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    if (validate != null && !validate(text)) continue;
                    json = text;
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not read {path}: {e.Message}");
                }
            }
            json = null;
            return false;
        }

        public static void Delete(string name)
        {
            foreach (string suffix in new[] { ".json", ".bak", ".tmp" })
            {
                string p = PathOf(name, suffix);
                if (File.Exists(p)) File.Delete(p);
            }
            Roughlike.Utils.WebBridge.SyncFileSystem();
        }
    }
}
