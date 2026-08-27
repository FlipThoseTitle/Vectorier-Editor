using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

namespace Vectorier.ProjectManager
{
    public class ProjectManager : EditorWindow
    {
        private const string ProjectsFolderPath = "./Projects";
        private const string UndeletableProject = "Vector";

        private List<string> projectList = new List<string>();
        private string selectedProject = null;

        // UI State
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;
        
        // Selected Project Data
        private Texture2D thumbnailTexture;
        private string currentProjectName = "";
        private string currentDescription = "";

        // Track changes to prevent saving on every keystroke
        private bool isDescriptionDirty = false;

        [MenuItem("Vectorier/Manage Project...", false, 58)]
        public static void ShowWindow()
        {
            ProjectManager window = GetWindow<ProjectManager>("Project Manager");
            window.minSize = new Vector2(600, 450);
            window.Show();
        }

        private void OnEnable()
        {
            EnsureDirectories();
            RefreshProjectList();
        }

        private void OnDisable()
        {
            // Ensure we save the description if the user closes the window while typing
            CheckAndSaveDirtyDescription();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(ProjectsFolderPath))
            {
                Directory.CreateDirectory(ProjectsFolderPath);
            }
            
            string undeletablePath = $"{ProjectsFolderPath}/{UndeletableProject}";
            if (!Directory.Exists(undeletablePath))
            {
                Directory.CreateDirectory(undeletablePath);
            }
        }

        private void RefreshProjectList()
        {
            projectList.Clear();
            if (Directory.Exists(ProjectsFolderPath))
            {
                string[] directories = Directory.GetDirectories(ProjectsFolderPath);
                foreach (string dir in directories)
                {
                    projectList.Add(Path.GetFileName(dir));
                }
            }

            if (selectedProject != null && !projectList.Contains(selectedProject))
            {
                selectedProject = null;
            }
        }

        private void LoadProjectData(string projectName)
        {
            // Save any pending changes before switching
            CheckAndSaveDirtyDescription();

            selectedProject = projectName;
            currentProjectName = projectName;

            // Load Thumbnail (png, jpg, jpeg)
            string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };
            thumbnailTexture = null;

            foreach (string ext in possibleExtensions)
            {
                string thumbPath = $"{ProjectsFolderPath}/{projectName}/thumbnail{ext}";
                if (File.Exists(thumbPath))
                {
                    byte[] fileData = File.ReadAllBytes(thumbPath);
                    thumbnailTexture = new Texture2D(2, 2);
                    thumbnailTexture.LoadImage(fileData);
                    break; // Stop looking once we find a valid thumbnail
                }
            }

            // Load Description
            string descPath = $"{ProjectsFolderPath}/{projectName}/description.txt";
            if (File.Exists(descPath))
            {
                currentDescription = File.ReadAllText(descPath);
            }
            else
            {
                currentDescription = "";
            }
            
            // Remove focus from text fields to avoid typing into a newly selected project
            GUI.FocusControl(null); 
        }

        private void CheckAndSaveDirtyDescription()
        {
            if (isDescriptionDirty && !string.IsNullOrEmpty(selectedProject))
            {
                string descPath = $"{ProjectsFolderPath}/{selectedProject}/description.txt";
                File.WriteAllText(descPath, currentDescription);
                isDescriptionDirty = false;
            }
        }

        private void OnGUI()
        {
            // If the user clicks anywhere else in the window, remove focus to trigger saves
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.BeginHorizontal();

            // --- LEFT PANEL ---
            DrawLeftPanel();

            // Divider
            GUILayout.Box("", GUILayout.Width(2), GUILayout.ExpandHeight(true));

            // --- RIGHT PANEL ---
            DrawRightPanel();

            GUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Space(10);

            GUILayout.Label("Project List", EditorStyles.boldLabel);

            // Add/Remove/Duplicate Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Create New Project",
                    "Do you want to create a new project?\n\nThis process may take a few seconds.",
                    "Create",
                    "Cancel"))
                {
                    CreateNewProject();
                }
            }
            if (GUILayout.Button("-", GUILayout.Width(30)))
            {
                DeleteSelectedProject();
            }
            // Duplicate
            if (GUILayout.Button(new GUIContent("D", "Duplicate selected project"), GUILayout.Width(30)))
            {
                DuplicateSelectedProject();
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Project List Scroll View
            leftScrollPosition = GUILayout.BeginScrollView(leftScrollPosition, "box");
            foreach (string proj in projectList)
            {
                // Highlight the selected project
                GUI.backgroundColor = (proj == selectedProject) ? new Color(0.3f, 0.6f, 1f) : Color.white;
                
                if (GUILayout.Button(proj, EditorStyles.toolbarButton, GUILayout.Height(25)))
                {
                    LoadProjectData(proj);
                }
                
                GUI.backgroundColor = Color.white; // Reset color
            }
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private void DrawRightPanel()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Space(10);

            if (string.IsNullOrEmpty(selectedProject))
            {
                GUILayout.Label("Select a project from the list to view its details.", EditorStyles.centeredGreyMiniLabel);
                GUILayout.EndVertical();
                return;
            }

            rightScrollPosition = GUILayout.BeginScrollView(rightScrollPosition);

            // --- Thumbnail (16:9 Aspect Ratio) ---
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Texture displayTex = thumbnailTexture != null ? thumbnailTexture : EditorGUIUtility.whiteTexture;
            
            // Define 16:9 dimensions
            float imgWidth = 320f;
            float imgHeight = imgWidth / (16f / 9f); 
            
            // Reserve space for the image
            Rect imageRect = GUILayoutUtility.GetRect(imgWidth, imgHeight, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
            
            // Draw the texture fitted inside the 16:9 box without stretching
            GUI.DrawTexture(imageRect, displayTex, ScaleMode.ScaleToFit);
            
            // Overlay an invisible button to capture clicks
            if (GUI.Button(imageRect, new GUIContent("", "Click to select a new thumbnail (.png, .jpg, .jpeg)"), GUIStyle.none))
            {
                if (selectedProject == UndeletableProject)
                {
                    EditorUtility.DisplayDialog("Action Denied", "You cannot change this project's thumbnail.", "OK");
                }
                else
                {
                    SelectAndCopyThumbnail();
                }
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Image (Click to change)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);

            // --- Name ---
            GUILayout.Label("Name", EditorStyles.boldLabel);
            
            EditorGUI.BeginDisabledGroup(selectedProject == UndeletableProject);
            
            EditorGUI.BeginChangeCheck();
            
            // DelayedTextField only registers changes when Enter is pressed or focus is lost
            currentProjectName = EditorGUILayout.DelayedTextField(currentProjectName);
            
            if (EditorGUI.EndChangeCheck())
            {
                RenameProject();
            }
            GUILayout.Space(10);

            // --- Description ---
            GUILayout.Label("Description", EditorStyles.boldLabel);
            
            GUI.SetNextControlName("DescriptionTextArea");
            
            // Define a custom GUIStyle that forces word wrapping
            GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
            textAreaStyle.wordWrap = true;
            
            string newDescription = EditorGUILayout.TextArea(currentDescription, textAreaStyle, GUILayout.Height(80));
            
            // Only flag as dirty if the text actually changed
            if (newDescription != currentDescription)
            {
                currentDescription = newDescription;
                isDescriptionDirty = true;
            }
            
            EditorGUI.EndDisabledGroup();

            if (isDescriptionDirty && GUI.GetNameOfFocusedControl() != "DescriptionTextArea")
            {
                CheckAndSaveDirtyDescription();
            }

            GUILayout.Space(20);

            // --- Action Buttons ---
            if (GUILayout.Button("Edit Project", GUILayout.Height(35))) 
            { 
                if (selectedProject == UndeletableProject)
                {
                    EditorUtility.DisplayDialog("Action Denied", $"The project '{UndeletableProject}' isn't editable.", "OK");
                }
                else
                {
                    ProjectManagerSelection.ShowWindow(selectedProject);
                    this.Close();
                }
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Apply to Snail Runner", GUILayout.Height(35))) 
            { 
                ProjectManagerApply.ApplyProject(selectedProject);
            }

            GUILayout.Space(5);
            if (GUILayout.Button("Publish as Mod...", GUILayout.Height(35))) 
            { 
                ProjectManagerPublish.ShowWindow(selectedProject);
                this.Close();
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        // --- Data Management Methods ---

        private void CreateNewProject()
        {
            EditorUtility.DisplayProgressBar("Creating Project", "Initializing...", 0f);
            
            try
            {
                string baseName = "New Project";
                string newName = baseName;
                int counter = 1;

                while (Directory.Exists($"{ProjectsFolderPath}/{newName}"))
                {
                    newName = $"{baseName} ({counter})";
                    counter++;
                }

                string sourcePath = "Assets/Editor/ProjectManager/ProjectTemplate"; 
                string newProjectPath = $"{ProjectsFolderPath}/{newName}";

                EditorUtility.DisplayProgressBar("Creating Project", $"Copying files for '{newName}'...", 0.5f);

                if (AssetDatabase.IsValidFolder(sourcePath))
                {
                    FileUtil.CopyFileOrDirectory(sourcePath, newProjectPath);
                }
                else
                {
                    Debug.LogWarning($"Base project not found at '{sourcePath}'. Creating an empty folder instead.");
                    Directory.CreateDirectory(newProjectPath);
                }

                EditorUtility.DisplayProgressBar("Creating Project", "Refreshing project list...", 0.9f);

                RefreshProjectList();
                LoadProjectData(newName);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void DuplicateSelectedProject()
        {
            if (string.IsNullOrEmpty(selectedProject)) return;

            if (EditorUtility.DisplayDialog("Duplicate Project", $"Are you sure you want to duplicate '{selectedProject}'?", "Duplicate", "Cancel"))
            {
                EditorUtility.DisplayProgressBar("Duplicating Project", "Initializing...", 0f);

                try
                {
                    string originalPath = $"{ProjectsFolderPath}/{selectedProject}";
                    string baseName = selectedProject;
                    string newName = $"{baseName} (Copy)";
                    int counter = 1;

                    while (Directory.Exists($"{ProjectsFolderPath}/{newName}"))
                    {
                        newName = $"{baseName} (Copy {counter})";
                        counter++;
                    }

                    string newPath = $"{ProjectsFolderPath}/{newName}";

                    EditorUtility.DisplayProgressBar("Duplicating Project", $"Copying '{selectedProject}' to '{newName}'...", 0.5f);
                    FileUtil.CopyFileOrDirectory(originalPath, newPath);
                    EditorUtility.DisplayProgressBar("Duplicating Project", "Refreshing project list...", 0.9f);

                    RefreshProjectList();
                    LoadProjectData(newName); 
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Failed to duplicate project '{selectedProject}'. Error: {e.Message}");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        private void DeleteSelectedProject()
        {
            if (string.IsNullOrEmpty(selectedProject)) return;

            if (selectedProject == UndeletableProject)
            {
                EditorUtility.DisplayDialog("Action Denied", $"The project '{UndeletableProject}' cannot be deleted.", "OK");
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Project", $"Are you sure you want to delete '{selectedProject}'? This cannot be undone.", "Delete", "Cancel"))
            {
                string targetDir = $"{ProjectsFolderPath}/{selectedProject}";
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }

                string imagesPath = $"Assets/Resources/Images/{selectedProject}";
                
                if (AssetDatabase.IsValidFolder(imagesPath))
                {
                    AssetDatabase.DeleteAsset(imagesPath);
                }
                else if (Directory.Exists(imagesPath)) 
                {
                    Directory.Delete(imagesPath, true);
                    string metaFile = $"{imagesPath}.meta";
                    if (File.Exists(metaFile))
                    {
                        File.Delete(metaFile);
                    }
                }
                
                selectedProject = null;
                RefreshProjectList();
                
                AssetDatabase.Refresh();
            }
        }

        private void RenameProject()
        {
            string sanitizedName = currentProjectName.Trim();
            
            if (string.IsNullOrEmpty(sanitizedName) || sanitizedName == selectedProject) return;

            if (selectedProject == UndeletableProject)
            {
                currentProjectName = selectedProject; 
                EditorUtility.DisplayDialog("Action Denied", $"The project '{UndeletableProject}' cannot be renamed.", "OK");
                return;
            }

            // Check for protected names
            if (sanitizedName.Equals("Vector", System.StringComparison.OrdinalIgnoreCase) || 
                sanitizedName.Equals("Editor", System.StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Action Denied", $"You cannot name it {sanitizedName}!", "OK");
                currentProjectName = selectedProject; // Revert the UI text field back to the original name
                return;
            }

            string oldPath = $"{ProjectsFolderPath}/{selectedProject}";
            string newPath = $"{ProjectsFolderPath}/{sanitizedName}";

            try
            {
                Directory.Move(oldPath, newPath);
                selectedProject = sanitizedName;
                RefreshProjectList();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to rename project: {e.Message}");
                currentProjectName = selectedProject; 
            }
        }

        private void SelectAndCopyThumbnail()
        {
            string absolutePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Project Thumbnail", 
                "", 
                new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" }
            );
            
            if (!string.IsNullOrEmpty(absolutePath))
            {
                string projPath = $"{ProjectsFolderPath}/{selectedProject}";
                
                string[] possibleExtensions = { ".png", ".jpg", ".jpeg" };
                foreach (string ext in possibleExtensions)
                {
                    string oldThumbPath = $"{projPath}/thumbnail{ext}";
                    if (File.Exists(oldThumbPath))
                    {
                        File.Delete(oldThumbPath);
                    }
                }

                string newExtension = Path.GetExtension(absolutePath).ToLower();
                string destinationPath = $"{projPath}/thumbnail{newExtension}";
                
                File.Copy(absolutePath, destinationPath, true);
                LoadProjectData(selectedProject);
            }
        }
    }
}