using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerModels : EditorWindow
    {
        private string activeProjectName = "";

        // Data for list display
        private List<string> loadedModelPaths = new List<string>();

        private string searchQuery = "";
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerModels window = GetWindow<ProjectManagerModels>("Models");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshModelList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            // --- Import Models Button ---
            if (GUILayout.Button("Import Models", GUILayout.Height(30)))
            {
                ImportModelFile();
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Enclosing Box for List and Actions ---
            GUILayout.BeginVertical("box");
            
            // Action Buttons (- and C)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Total Models Text
            GUIStyle totalLabelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Models - " + loadedModelPaths.Count, totalLabelStyle, GUILayout.Height(30));
            
            GUILayout.Space(10); // Small gap between the text and the buttons

            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedModels();
            }
            
            GUI.enabled = loadedModelPaths.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllModels();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Models List ---
            DrawModelsList();
            
            GUILayout.EndVertical();
        }

        private void OnDestroy()
        {
            // Reopen the selection window with the current project name
            if (!string.IsNullOrEmpty(activeProjectName))
            {
                ProjectManagerSelection.ShowWindow(activeProjectName);
            }
        }

        private void DrawModelsList()
        {
            if (loadedModelPaths.Count == 0)
            {
                GUILayout.Label("No models imported yet.\nClick 'Import' or Drag & Drop .xml here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            List<int> visibleIndices = new List<int>();
            for (int i = 0; i < loadedModelPaths.Count; i++)
            {
                string fileName = Path.GetFileNameWithoutExtension(loadedModelPaths[i]);
                if (string.IsNullOrEmpty(searchQuery) || fileName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visibleIndices.Add(i);
                }
            }

            if (visibleIndices.Count == 0)
            {
                GUILayout.Label("No models match the search.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < visibleIndices.Count; i++)
            {
                DrawListItem(visibleIndices[i]);
                GUILayout.Space(2); // Small gap between items
            }
            
            GUILayout.EndScrollView();

            // Catch background clicks inside the scroll area to clear selection
            Rect scrollRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && scrollRect.Contains(Event.current.mousePosition))
            {
                selectedIndices.Clear();
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawListItem(int index)
        {
            string path = loadedModelPaths[index];
            string fileName = Path.GetFileNameWithoutExtension(path);
            bool isSelected = selectedIndices.Contains(index);

            // Reserve space for the list item
            Rect itemRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 40, 25);
            
            // Draw Background Color based on selection
            if (isSelected)
                EditorGUI.DrawRect(itemRect, new Color(0.2f, 0.5f, 0.9f, 0.7f)); // Blue highlight
            else
                EditorGUI.DrawRect(itemRect, new Color(0.25f, 0.25f, 0.25f, 0.5f)); // Dark grey

            // Draw Outline Border manually using DrawRect
            Color borderThicknessColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            float borderThickness = 1f;
            EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, itemRect.width, borderThickness), borderThicknessColor); // Top
            EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y + itemRect.height - borderThickness, itemRect.width, borderThickness), borderThicknessColor); // Bottom
            EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, borderThickness, itemRect.height), borderThicknessColor); // Left
            EditorGUI.DrawRect(new Rect(itemRect.x + itemRect.width - borderThickness, itemRect.y, borderThickness, itemRect.height), borderThicknessColor); // Right

            // Draw Label
            Rect labelRect = new Rect(itemRect.x + 10, itemRect.y + 2, itemRect.width - 20, 20);
            GUI.Label(labelRect, fileName, EditorStyles.label);

            // --- Multi-Select Logic ---
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && itemRect.Contains(e.mousePosition))
            {
                if (e.control || e.command) // Ctrl/Cmd Click
                {
                    if (isSelected) selectedIndices.Remove(index);
                    else selectedIndices.Add(index);
                }
                else if (e.shift && selectedIndices.Count > 0) // Shift Click
                {
                    int lastSelected = selectedIndices.Last();
                    int start = Mathf.Min(lastSelected, index);
                    int end = Mathf.Max(lastSelected, index);
                    selectedIndices.Clear();
                    for (int i = start; i <= end; i++) selectedIndices.Add(i);
                }
                else // Normal Click
                {
                    selectedIndices.Clear();
                    selectedIndices.Add(index);
                }
                
                e.Use();
                GUI.FocusControl(null);
            }
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        List<string> filesToProcess = new List<string>();

                        foreach (string path in DragAndDrop.paths)
                        {
                            if (Directory.Exists(path))
                            {
                                // Get .xml if dragging a folder
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.xml", SearchOption.AllDirectories));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".xml")
                                {
                                    filesToProcess.Add(path);
                                }
                            }
                        }

                        ProcessModelFiles(filesToProcess.ToArray());
                    }
                    break;
            }
        }

        private void ImportModelFile()
        {
            // Open a file panel specifically for xml files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Model to Import", 
                "", 
                new string[] { "XML Files", "xml", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessModelFiles(new string[] { sourcePath });
        }

        private void ProcessModelFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string targetDir = $"./Projects/{activeProjectName}/models";
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Models", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);

                progress++;
            }

            EditorUtility.ClearProgressBar();
            
            RefreshModelList();
        }

        private void DeleteSelectedModels()
        {
            if (selectedIndices.Count > 0)
            {
                List<string> pathsToDelete = new List<string>();
                int undeletableCount = 0;

                // Define the protected models list
                HashSet<string> protectedModels = new HashSet<string> { "0", "1" };

                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedModelPaths.Count)
                    {
                        string path = loadedModelPaths[index];
                        string fileName = Path.GetFileNameWithoutExtension(path);
                        
                        // Check if the file is in the protected list
                        if (protectedModels.Contains(fileName))
                        {
                            undeletableCount++;
                        }
                        else
                        {
                            pathsToDelete.Add(path);
                        }
                    }
                }

                // Trigger a prompt showing how many undeletable files were selected
                if (undeletableCount > 0)
                {
                    EditorUtility.DisplayDialog("Notice", $"{undeletableCount} selected file(s) cannot be deleted because they are protected.", "OK");
                    
                    // If ONLY undeletable files were selected, stop here
                    if (pathsToDelete.Count == 0) return;
                }

                if (pathsToDelete.Count > 0)
                {
                    foreach (string pathToDelete in pathsToDelete)
                    {
                        if (File.Exists(pathToDelete))
                        {
                            File.Delete(pathToDelete);
                        }
                    }
                }

                selectedIndices.Clear();
                RefreshModelList();
            }
        }

        private void ClearAllModels()
        {
            // Define the protected models list
            HashSet<string> protectedModels = new HashSet<string> { "0", "1" };

            foreach (string path in loadedModelPaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                
                // Delete everything EXCEPT the protected models
                if (!protectedModels.Contains(fileName))
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }

            selectedIndices.Clear();
            RefreshModelList();
        }

        private void RefreshModelList()
        {
            loadedModelPaths.Clear();

            // Refresh Models List
            string dataDir = $"./Projects/{activeProjectName}/models";
            
            if (Directory.Exists(dataDir))
            {
                string[] rawFiles = Directory.GetFiles(dataDir, "*.xml", SearchOption.TopDirectoryOnly);

                foreach (string file in rawFiles)
                {
                    string assetPath = file.Replace("\\", "/");
                    loadedModelPaths.Add(assetPath);
                }
            }

            Repaint();
        }
    }
}