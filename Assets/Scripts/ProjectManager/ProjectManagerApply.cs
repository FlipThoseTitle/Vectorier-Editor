using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Vectorier.ProjectManager
{
    public static class ProjectManagerApply
    {
        private static readonly string[] FoldersToReplace = new string[]
        {
            "animatedtextures",
            "animations",
            "commons",
            "icons",
            "localization",
            "models",
            "music",
            "sounds",
            "textures",
            "xmlroot"
        };

        public static void ApplyProject(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                EditorUtility.DisplayDialog("Error", "No project selected.", "OK");
                return;
            }

            // Prompt window for user confirmation
            bool isConfirmed = EditorUtility.DisplayDialog(
                "Apply to Snail Runner",
                $"Are you sure you want to apply the project '{projectName}' to Snail Runner?\n\nThis will replace assets in the Snail Runner directory.",
                "Yes, Apply",
                "Cancel"
            );

            if (!isConfirmed) return;

            string projectPath = Path.Combine("Assets", "Projects", projectName);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string streamingAssetsPath = Path.Combine(projectRoot, "Snail Runner", "Vector_Data", "StreamingAssets");

            if (!Directory.Exists(streamingAssetsPath))
            {
                EditorUtility.DisplayDialog("Directory Not Found", $"Could not find the StreamingAssets folder at:\n{streamingAssetsPath}\n\nPlease ensure the Snail Runner folder exists in your project's root directory.", "OK");
                return;
            }

            try
            {
                // 1. Process the folders
                foreach (string folderName in FoldersToReplace)
                {
                    string sourceFolder = Path.Combine(projectPath, folderName);
                    string destFolder = Path.Combine(streamingAssetsPath, folderName);

                    // Empty the specific folder's contents instead of deleting it
                    if (Directory.Exists(destFolder))
                    {
                        EmptyDirectory(destFolder);
                    }
                    else
                    {
                        // If it doesn't exist at all yet, create it
                        Directory.CreateDirectory(destFolder);
                    }

                    // Copy the folder from the active project if it exists
                    if (Directory.Exists(sourceFolder))
                    {
                        CopyDirectory(sourceFolder, destFolder);
                    }
                }

                // 2. Process the Videos folder
                string sourceVideosFolder = Path.Combine(projectPath, "videos");

                // Delete all .mp4 files directly under StreamingAssets (excluding subfolders)
                string[] existingMp4s = Directory.GetFiles(streamingAssetsPath, "*.mp4", SearchOption.TopDirectoryOnly);
                foreach (string mp4 in existingMp4s)
                {
                    // Ensure the file isn't read-only before deleting
                    File.SetAttributes(mp4, File.GetAttributes(mp4) & ~FileAttributes.ReadOnly);
                    File.Delete(mp4);
                }

                // Copy all .mp4 files from the active project's 'videos' folder directly into StreamingAssets
                if (Directory.Exists(sourceVideosFolder))
                {
                    string[] sourceMp4s = Directory.GetFiles(sourceVideosFolder, "*.mp4", SearchOption.TopDirectoryOnly);
                    foreach (string mp4Path in sourceMp4s)
                    {
                        string fileName = Path.GetFileName(mp4Path);
                        string destMp4Path = Path.Combine(streamingAssetsPath, fileName);
                        File.Copy(mp4Path, destMp4Path, true);
                    }
                }

                EditorUtility.DisplayDialog("Success", $"'{projectName}' has been successfully applied to Snail Runner!", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error applying project: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"An error occurred while applying the project.\n\nCheck the console for more details.", "OK");
            }
        }

        // Helper method
        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);

            if (!dir.Exists) return;

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            // Copy files in the current directory
            foreach (FileInfo file in dir.GetFiles())
            {
                // Skip Unity .meta files
                if (file.Extension.ToLower() == ".meta") continue;

                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        private static void EmptyDirectory(string directoryPath)
        {
            DirectoryInfo dir = new DirectoryInfo(directoryPath);
            if (!dir.Exists) return;

            // Delete all files in the current folder
            foreach (FileInfo file in dir.GetFiles())
            {
                file.Attributes &= ~FileAttributes.ReadOnly; // Strip read-only flag
                file.Delete();
            }

            // Delete all subdirectories
            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                RemoveReadOnlyAttributes(subDir.FullName); // Strip flags from subdirectories
                Directory.Delete(subDir.FullName, true);
            }
        }

        // Helper method to recursively strip Read Only attributes from a directory tree
        private static void RemoveReadOnlyAttributes(string directoryPath)
        {
            DirectoryInfo dir = new DirectoryInfo(directoryPath);
            if (!dir.Exists) return;

            dir.Attributes &= ~FileAttributes.ReadOnly;

            foreach (FileInfo file in dir.GetFiles())
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                RemoveReadOnlyAttributes(subDir.FullName);
            }
        }
    }
}