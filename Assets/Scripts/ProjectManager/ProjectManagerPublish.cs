using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerPublish : EditorWindow
    {
        // State variables
        private string activeProjectName = "";
        private string publishLocation = "";

        private bool fastBuild = true;
        
        // Dropdown options
        private readonly string[] gameVersions = new string[] { "Steam", "Unity", "Steam and Unity" };
        private int selectedVersionIndex = 0;

        // Static method to initialize and open the window while passing the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerPublish window = GetWindow<ProjectManagerPublish>("Publish Project");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(450, 200);
            window.Show();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(15);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Publishing: {activeProjectName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            DrawDirectoryFieldWithBrowse("Location", ref publishLocation, "Select the destination directory where your project will be published.");

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Game Version:", "Select which platforms to compile and publish this project for."), GUILayout.Width(100));
            selectedVersionIndex = EditorGUILayout.Popup(selectedVersionIndex, gameVersions);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            fastBuild = EditorGUILayout.Toggle(
                new GUIContent("Fast Build", "If using Steam Version, will make the compile time faster, but may increase the size of the final build.\nThis is recommended to be enabled."),
                fastBuild
            );

            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Publish", "Start compiling and publishing the project."), GUILayout.Height(60)))
            {
                if (string.IsNullOrEmpty(publishLocation))
                {
                    EditorUtility.DisplayDialog("Error", "Please select a valid directory location first.", "OK");
                    return;
                }

                if (EditorUtility.DisplayDialog("Confirm Publish", 
                    $"Are you sure you want to publish '{activeProjectName}' to the selected location?", 
                    "Publish", "Cancel"))
                {
                    string publishBaseFolder = Path.Combine(publishLocation, activeProjectName);
                    string sourcePath = Path.Combine(Application.dataPath, "Projects", activeProjectName);

                    if (!Directory.Exists(sourcePath))
                    {
                        EditorUtility.DisplayDialog("Error", $"Could not find the project folder at:\n{sourcePath}", "OK");
                        return;
                    }

                    if (Directory.Exists(publishBaseFolder))
                    {
                        EditorUtility.DisplayDialog("Error", 
                            $"A folder named '{activeProjectName}' already exists at the target location.\n\nPlease choose a different location or delete the existing folder before publishing.", 
                            "OK");
                        return;
                    }

                    if (selectedVersionIndex == 0) 
                    {
                        PublishForSteam();
                        ProcessBaseFiles(sourcePath, publishBaseFolder);
                        EditorUtility.DisplayDialog("Success", $"Successfully published '{activeProjectName}' for Steam!", "OK");
                    }
                    else if (selectedVersionIndex == 1) 
                    {
                        PublishForUnity();
                        ProcessBaseFiles(sourcePath, publishBaseFolder);
                        EditorUtility.DisplayDialog("Success", $"Successfully published '{activeProjectName}' for Unity!", "OK");
                    }
                    else if (selectedVersionIndex == 2) 
                    {
                        PublishForUnity();
                        PublishForSteam();
                        ProcessBaseFiles(sourcePath, publishBaseFolder);
                        EditorUtility.DisplayDialog("Success", $"Successfully published '{activeProjectName}' for Steam & Unity!", "OK");
                    }
                }
            }
        }

        // --- Publishing Logic ---

        private void PublishForUnity()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Publishing", "Initializing Unity Build...", 0f);

                string sourcePath = Path.Combine(Application.dataPath, "Projects", activeProjectName);
                string publishBaseFolder = Path.Combine(publishLocation, activeProjectName);
                string unityTargetDir = Path.Combine(publishBaseFolder, "Unity", "Vector_Data", "StreamingAssets");

                Directory.CreateDirectory(unityTargetDir);

                string[] allFiles = Directory
                    .GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                int totalFiles = allFiles.Length;

                for (int i = 0; i < totalFiles; i++)
                {
                    string filePath = allFiles[i];

                    EditorUtility.DisplayProgressBar(
                        "Publishing for Unity",
                        $"Copying file {i + 1} of {totalFiles}: {Path.GetFileName(filePath)}",
                        (float)i / totalFiles);

                    string relativePath = filePath.Substring(sourcePath.Length + 1).Replace('\\', '/');
                    string destinationPath = "";

                    if (relativePath.Equals("description.txt", StringComparison.OrdinalIgnoreCase) ||
                        (relativePath.StartsWith("thumbnail.", StringComparison.OrdinalIgnoreCase) && !relativePath.Contains("/")))
                    {
                        continue;
                    }
                    else if (relativePath.StartsWith("videos/"))
                    {
                        if (filePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
                        {
                            destinationPath = Path.Combine(unityTargetDir, Path.GetFileName(filePath));
                        }
                        else
                        {
                            continue;
                        }
                    }
                    else
                    {
                        destinationPath = Path.Combine(unityTargetDir, relativePath);
                    }

                    if (!string.IsNullOrEmpty(destinationPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                        File.Copy(filePath, destinationPath, true);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void PublishForSteam()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Publishing for Steam", "Preparing directories...", 0.0f);

                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string dzipDir = Path.Combine(projectRoot, "DZIP");
                string templateDir = Path.Combine(dzipDir, "_TEMPLATE");
                string sourcePath = Path.Combine(Application.dataPath, "Projects", activeProjectName);
                string publishBaseFolder = Path.Combine(publishLocation, activeProjectName);
                string steamTargetDir = Path.Combine(publishBaseFolder, "Steam", "Vector");

                Directory.CreateDirectory(steamTargetDir);

                CopyFolderContents(Path.Combine(templateDir, "common"), Path.Combine(dzipDir, "common"));
                CopyFolderContents(Path.Combine(templateDir, "gui"), Path.Combine(dzipDir, "gui"));
                CopyFolderContents(Path.Combine(templateDir, "texture"), Path.Combine(dzipDir, "texture"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling Textures...", 0.1f);
                CopyFolderContents(Path.Combine(sourcePath, "animatedtextures"), Path.Combine(dzipDir, "texture"));
                CopyFolderContents(Path.Combine(sourcePath, "textures"), Path.Combine(dzipDir, "texture"));

                string tricksDir = Path.Combine(sourcePath, "icons", "tricks");
                if (Directory.Exists(tricksDir))
                {
                    foreach (string file in Directory.GetFiles(tricksDir))
                    {
                        if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                        if (Path.GetFileNameWithoutExtension(file).Equals("lock", StringComparison.OrdinalIgnoreCase)) continue;
                        File.Copy(file, Path.Combine(dzipDir, "texture", Path.GetFileName(file)), true);
                    }
                }

                RunBatchFile(dzipDir, fastBuild ? "compile-texture-fast.bat" : "compile-texture.bat");
                MoveFile(Path.Combine(dzipDir, "track_content_universal.dz"), Path.Combine(steamTargetDir, "track_content_universal.dz"));

                RunBatchFile(dzipDir, "compile-empty-track-content-2048.bat");
                RunBatchFile(dzipDir, "compile-empty-track-techno-2048.bat");
                MoveFile(Path.Combine(dzipDir, "track_content_2048.dz"), Path.Combine(steamTargetDir, "track_content_2048.dz"));
                MoveFile(Path.Combine(dzipDir, "track_techno_2048.dz"), Path.Combine(steamTargetDir, "track_techno_2048.dz"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling Animations...", 0.25f);
                CopyFolderContents(Path.Combine(sourcePath, "animations", "data"), Path.Combine(dzipDir, "animation"));
                RunBatchFile(dzipDir, fastBuild ? "compile-animation-fast.bat" : "compile-animation.bat");
                MoveFile(Path.Combine(dzipDir, "animations.dz"), Path.Combine(steamTargetDir, "animations.dz"));

                string movesXmlSource = Path.Combine(sourcePath, "animations", "moves.xml");
                if (File.Exists(movesXmlSource)) File.Copy(movesXmlSource, Path.Combine(steamTargetDir, "Moves_new.xml"), true);

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling Commons & Localization...", 0.4f);
                CopyFolderContents(Path.Combine(sourcePath, "commons"), Path.Combine(dzipDir, "common"));
                CopyFolderContents(Path.Combine(sourcePath, "localization"), Path.Combine(dzipDir, "common"));
                RunBatchFile(dzipDir, fastBuild ? "compile-common-fast.bat" : "compile-common.bat");
                MoveFile(Path.Combine(dzipDir, "common_xml.dz"), Path.Combine(steamTargetDir, "common_xml.dz"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling Sounds...", 0.55f);
                CopyFolderContents(Path.Combine(sourcePath, "sounds"), Path.Combine(dzipDir, "sound"));
                RunBatchFile(dzipDir, fastBuild ? "compile-sound-fast.bat" : "compile-sound.bat");
                MoveFile(Path.Combine(dzipDir, "sound.dz"), Path.Combine(steamTargetDir, "sound.dz"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Processing Music...", 0.65f);
                CopyFolderContents(Path.Combine(sourcePath, "music"), steamTargetDir);

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling GUI...", 0.7f);
                CopyFolderContents(Path.Combine(sourcePath, "icons", "locations"), Path.Combine(dzipDir, "gui"));
                CopyFolderContents(Path.Combine(sourcePath, "icons", "shop"), Path.Combine(dzipDir, "gui"));
                CopyFolderContents(Path.Combine(sourcePath, "icons", "stories"), Path.Combine(dzipDir, "gui"));
                RunBatchFile(dzipDir, fastBuild ? "compile-gui-fast.bat" : "compile-gui.bat");
                MoveFile(Path.Combine(dzipDir, "GUI_2048_1536.dz"), Path.Combine(steamTargetDir, "GUI_2048_1536.dz"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Processing Video...", 0.8f);
                string videoTarget = Path.Combine(steamTargetDir, "Video");
                Directory.CreateDirectory(videoTarget);
                CopyFolderContents(Path.Combine(sourcePath, "videos"), videoTarget);

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Compiling Levels...", 0.85f);
                string levelBackupDir = Path.Combine(templateDir, "level_backup");
                Directory.CreateDirectory(levelBackupDir);
                MoveFolderContents(Path.Combine(dzipDir, "level"), levelBackupDir);

                CopyFolderContents(Path.Combine(sourcePath, "models"), Path.Combine(dzipDir, "level"));
                
                string xmlrootDir = Path.Combine(sourcePath, "xmlroot");
                if (Directory.Exists(xmlrootDir))
                {
                    foreach (string file in Directory.GetFiles(xmlrootDir, "*.xml"))
                    {
                        if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                        File.Copy(file, Path.Combine(dzipDir, "level", Path.GetFileName(file)), true);
                    }
                }
                CopyFolderContents(Path.Combine(sourcePath, "xmlroot", "levels"), Path.Combine(dzipDir, "level"));

                RunBatchFile(dzipDir, fastBuild ? "compile-level-fast.bat" : "compile-level.bat");
                MoveFile(Path.Combine(dzipDir, "level_xml.dz"), Path.Combine(steamTargetDir, "level_xml.dz"));

                EditorUtility.DisplayProgressBar("Publishing for Steam", "Cleaning up...", 0.95f);
                ClearFolder(Path.Combine(dzipDir, "texture"));
                ClearFolder(Path.Combine(dzipDir, "sound"));
                ClearFolder(Path.Combine(dzipDir, "level"));
                ClearFolder(Path.Combine(dzipDir, "gui"));
                ClearFolder(Path.Combine(dzipDir, "common"));
                ClearFolder(Path.Combine(dzipDir, "animation"));

                MoveFolderContents(levelBackupDir, Path.Combine(dzipDir, "level"));
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // --- Helper Methods ---

        private void ProcessBaseFiles(string sourcePath, string publishBaseFolder)
        {
            string[] files = Directory.GetFiles(sourcePath);

            foreach (string file in files)
            {
                string fileName = Path.GetFileName(file);

                if (Path.GetExtension(file).Equals(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (fileName.Equals("description.txt", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("thumbnail.", StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(file, Path.Combine(publishBaseFolder, fileName), true);
                }
            }

            string readmeSource = Path.Combine(Application.dataPath, "Editor", "ProjectManager", "README.txt");
            if (File.Exists(readmeSource))
            {
                File.Copy(readmeSource, Path.Combine(publishBaseFolder, "README.txt"), true);
            }
}

        private void CopyFolderContents(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                CopyFolderContents(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
            }
        }

        private void MoveFolderContents(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    continue;

                string targetPath = Path.Combine(targetDir, Path.GetFileName(file));

                if (File.Exists(targetPath))
                    File.Delete(targetPath);

                File.Move(file, targetPath);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string targetPath = Path.Combine(targetDir, Path.GetFileName(dir));
                MoveFolderContents(dir, targetPath);

                if (Directory.Exists(dir) &&
                    Directory.GetFiles(dir).Length == 0 &&
                    Directory.GetDirectories(dir).Length == 0)
                {
                    Directory.Delete(dir, false);
                }
            }
        }

        private void MoveFile(string source, string target)
        {
            if (File.Exists(source))
            {
                if (File.Exists(target)) File.Delete(target);
                File.Move(source, target);
            }
        }

        private void ClearFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            foreach (string file in Directory.GetFiles(folderPath))
            {
                File.Delete(file);
            }

            foreach (string dir in Directory.GetDirectories(folderPath))
            {
                Directory.Delete(dir, true);
            }
        }

        private void RunBatchFile(string workingDirectory, string batchFileName)
        {
            string batchPath = Path.Combine(workingDirectory, batchFileName);
            if (!File.Exists(batchPath)) return;

            Process process = new Process();
            process.StartInfo.FileName = batchPath;
            process.StartInfo.WorkingDirectory = workingDirectory;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;

            process.Start();
            process.WaitForExit();
        }

        private void DrawDirectoryFieldWithBrowse(string label, ref string directoryPath, string tooltip = "")
        {
            EditorGUILayout.BeginHorizontal();

            GUIContent labelContent = new GUIContent(label, tooltip);

            // The standard Unity text field natively clips text that gets too long
            directoryPath = EditorGUILayout.TextField(labelContent, directoryPath);

            // Browse button ("...")
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");

                if (!string.IsNullOrEmpty(picked))
                {
                    directoryPath = picked;
                    
                    // Unselect the text field to force the UI to visually update
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}