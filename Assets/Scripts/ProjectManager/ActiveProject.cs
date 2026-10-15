using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vectorier.ProjectManager
{
    // Tracks which project in ./Projects the editor is currently scoped to.
    // Any editor script can read Name or use the Resolve helpers to load files
    public static class ActiveProject
    {
        public const string ProjectsFolderPath = "./Projects";
        public const string DefaultProject = "Vector";

        private const string SettingsFolder = "UserSettings/Vectorier";
        private const string SettingsFile = SettingsFolder + "/ActiveProject.txt";

        // Static fields reset on every domain reload, so this is lazily re-read from disk
        private static string cachedName;

        // Raised after the active project changes. Argument is the new project name.
        public static event Action<string> Changed;

        // Name of the currently active project, e.g. "My Project".
        // Falls back to "Vector" if nothing is set or the stored project no longer exists.
        public static string Name
        {
            get
            {
                string stored = Stored();
                return ProjectExists(stored) ? stored : DefaultProject;
            }
        }

        // Folder of the active project, e.g. "./Projects/My Project"
        public static string FolderPath => GetProjectFolder(Name);

        // True when the active project is the built-in default
        public static bool IsDefault => Name == DefaultProject;

        // Makes the given project the active project
        public static void Set(string projectName)
        {
            if (!ProjectExists(projectName))
            {
                Debug.LogWarning($"[Vectorier] Cannot set active project: '{projectName}' does not exist in {ProjectsFolderPath}.");
                return;
            }

            if (projectName == Name) return;

            Store(projectName);
        }

        // Switches back to the default "Vector" project
        public static void ResetToDefault() => Set(DefaultProject);

        // --- Path helpers ---

        // Path of a file/folder inside the active project (not checked for existence)
        public static string GetPath(string relativePath) => Combine(Name, relativePath);

        // Returns the file from the active project if it exists there, otherwise from
        // the default project. Returns null if neither has it.
        // Example: ActiveProject.ResolveFile("animations/data/run.xml")
        public static string ResolveFile(string relativePath)
        {
            string path = Combine(Name, relativePath);
            if (File.Exists(path)) return path;

            string fallback = Combine(DefaultProject, relativePath);
            return File.Exists(fallback) ? fallback : null;
        }

        // Same as ResolveFile, but for folders
        public static string ResolveDirectory(string relativePath)
        {
            string path = Combine(Name, relativePath);
            if (Directory.Exists(path)) return path;

            string fallback = Combine(DefaultProject, relativePath);
            return Directory.Exists(fallback) ? fallback : null;
        }

        public static string GetProjectFolder(string projectName) => $"{ProjectsFolderPath}/{projectName}";

        public static bool ProjectExists(string projectName) =>
            !string.IsNullOrEmpty(projectName) && Directory.Exists(GetProjectFolder(projectName));

        // --- Called by the Project Manager to keep the selection valid ---

        internal static void HandleRenamed(string oldName, string newName)
        {
            if (Stored() == oldName) Store(newName);
        }

        internal static void HandleDeleted(string projectName)
        {
            if (Stored() == projectName) Store(DefaultProject);
        }

        // --- Persistence ---

        private static string Stored() => cachedName ??= Load();

        private static void Store(string projectName)
        {
            cachedName = projectName;

            try
            {
                Directory.CreateDirectory(SettingsFolder);
                File.WriteAllText(SettingsFile, projectName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Vectorier] Failed to save active project: {e.Message}");
            }

            Changed?.Invoke(projectName);
        }

        private static string Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string name = File.ReadAllText(SettingsFile).Trim();
                    if (name.Length > 0) return name;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Vectorier] Failed to read active project: {e.Message}");
            }

            return DefaultProject;
        }

        private static string Combine(string projectName, string relativePath) =>
            $"{GetProjectFolder(projectName)}/{relativePath.Replace('\\', '/').TrimStart('/')}";
    }
}