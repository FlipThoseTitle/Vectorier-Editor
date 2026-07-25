using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerAnimations : EditorWindow
    {
        private string activeProjectName = "";
        
        // Data for moves XML
        private Object movesXmlAsset;

        // Data for list display
        private List<string> loadedAnimationPaths = new List<string>();
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerAnimations window = GetWindow<ProjectManagerAnimations>("Animations");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshAnimationList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            // --- Import Moves XML ---
            if (GUILayout.Button("Import Moves XML", GUILayout.Height(30)))
            {
                ImportMovesXML();
            }
            
            GUILayout.Space(5);
            
            // --- Moves Reference ---
            movesXmlAsset = EditorGUILayout.ObjectField("Moves:", movesXmlAsset, typeof(Object), false);

            GUILayout.Space(15);

            // --- Import Animations Button ---
            if (GUILayout.Button("Import Animations", GUILayout.Height(30)))
            {
                ImportAnimationFile();
            }

            GUILayout.Space(10);

            // --- Enclosing Box for List and Actions ---
            GUILayout.BeginVertical("box");
            
            // Action Buttons (- and C)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle totalLabelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Animations - " + loadedAnimationPaths.Count, totalLabelStyle, GUILayout.Height(30));
            
            GUILayout.Space(10); // Small gap between the text and the buttons

            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedAnimations();
            }
            
            GUI.enabled = loadedAnimationPaths.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllAnimations();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Animations List ---
            DrawAnimationsList();
            
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

        private void DrawAnimationsList()
        {
            if (loadedAnimationPaths.Count == 0)
            {
                GUILayout.Label("No animations imported yet.\nClick 'Import' or Drag & Drop .bin/.bytes here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < loadedAnimationPaths.Count; i++)
            {
                DrawListItem(i);
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
            string path = loadedAnimationPaths[index];
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

        private void ImportMovesXML()
        {
            string filePath = EditorUtility.OpenFilePanel("Select Moves XML", "", "xml");
            if (string.IsNullOrEmpty(filePath)) return;

            string targetDir = $"Assets/Projects/{activeProjectName}/animations";
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            string fileName = Path.GetFileName(filePath);
            string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
            
            File.Copy(filePath, destFile, true);
            AssetDatabase.Refresh();

            // Assign the newly imported XML to the Object Field
            movesXmlAsset = AssetDatabase.LoadAssetAtPath<Object>(destFile);
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
                                // Get both .bin and .bytes if dragging a folder
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.bin", SearchOption.AllDirectories));
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.bytes", SearchOption.AllDirectories));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".bin" || ext == ".bytes")
                                {
                                    filesToProcess.Add(path);
                                }
                            }
                        }

                        ProcessAnimationFiles(filesToProcess.ToArray());
                    }
                    break;
            }
        }

        private void ImportAnimationFile()
        {
            // Open a file panel specifically for bin and bytes files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Animation to Import", 
                "", 
                new string[] { "Animation Files", "bin,bytes", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessAnimationFiles(new string[] { sourcePath });
        }

        private void ProcessAnimationFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string targetDir = $"Assets/Projects/{activeProjectName}/animations/data";
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Animations", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);

                progress++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            RefreshAnimationList();
        }

        private void DeleteSelectedAnimations()
        {
            if (selectedIndices.Count > 0)
            {
                List<string> pathsToDelete = new List<string>();
                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedAnimationPaths.Count)
                    {
                        pathsToDelete.Add(loadedAnimationPaths[index]);
                    }
                }

                try
                {
                    // Suspend asset imports/updates for bulk optimization
                    AssetDatabase.StartAssetEditing();

                    foreach (string pathToDelete in pathsToDelete)
                    {
                        AssetDatabase.DeleteAsset(pathToDelete);
                    }
                }
                finally
                {
                    // Resume and process all deletions at once
                    AssetDatabase.StopAssetEditing();
                }

                selectedIndices.Clear();
                RefreshAnimationList();
            }
        }

        private void ClearAllAnimations()
        {
            if (EditorUtility.DisplayDialog("Clear All Animations", "Are you sure you want to delete all imported animations (.bin/.bytes) for this project?", "Yes, Delete All", "Cancel"))
            {
                try
                {
                    // Suspend asset imports/updates for bulk optimization
                    AssetDatabase.StartAssetEditing();

                    foreach (string path in loadedAnimationPaths)
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
                finally
                {
                    // Resume and process all deletions at once
                    AssetDatabase.StopAssetEditing();
                }

                selectedIndices.Clear();
                RefreshAnimationList();
            }
        }

        private void RefreshAnimationList()
        {
            loadedAnimationPaths.Clear();

            // Refresh Moves XML Field
            string baseAnimDir = $"Assets/Projects/{activeProjectName}/animations";
            if (movesXmlAsset == null && AssetDatabase.IsValidFolder(baseAnimDir))
            {
                string[] xmlFiles = Directory.GetFiles(baseAnimDir, "*.xml", SearchOption.TopDirectoryOnly);
                if (xmlFiles.Length > 0)
                {
                    movesXmlAsset = AssetDatabase.LoadAssetAtPath<Object>(xmlFiles[0].Replace("\\", "/"));
                }
            }

            // Refresh Animations List
            string dataDir = $"Assets/Projects/{activeProjectName}/animations/data";
            if (AssetDatabase.IsValidFolder(dataDir))
            {
                string[] rawFiles = Directory.GetFiles(dataDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.EndsWith(".bin", System.StringComparison.OrdinalIgnoreCase) || 
                                   file.EndsWith(".bytes", System.StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (string file in rawFiles)
                {
                    string assetPath = file.Replace("\\", "/");
                    loadedAnimationPaths.Add(assetPath);
                }
            }

            Repaint();
        }
    }
}