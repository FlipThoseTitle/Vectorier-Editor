using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerSounds : EditorWindow
    {
        private string activeProjectName = "";

        // Data for list display
        private List<string> loadedSoundPaths = new List<string>();
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerSounds window = GetWindow<ProjectManagerSounds>("Sounds");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshSoundList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            // --- Import Sounds Button ---
            if (GUILayout.Button("Import Sounds", GUILayout.Height(30)))
            {
                ImportSoundFile();
            }

            GUILayout.Space(10);

            // --- Enclosing Box for List and Actions ---
            GUILayout.BeginVertical("box");
            
            // Action Buttons (- and C)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            // Note: The '+' button was intentionally omitted based on instructions.

            // Added text to show total sounds, vertically centered to match button heights
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Sounds - " + loadedSoundPaths.Count, labelStyle, GUILayout.Height(30));
            
            GUILayout.Space(5); // Adds a small gap before the buttons

            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedSounds();
            }
            
            GUI.enabled = loadedSoundPaths.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllSounds();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Sounds List ---
            DrawSoundsList();
            
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

        private void DrawSoundsList()
        {
            if (loadedSoundPaths.Count == 0)
            {
                GUILayout.Label("No sounds imported yet.\nClick 'Import' or Drag & Drop .wav here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < loadedSoundPaths.Count; i++)
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
            string path = loadedSoundPaths[index];
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
                                // Get .wav if dragging a folder
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.wav", SearchOption.AllDirectories));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".wav")
                                {
                                    filesToProcess.Add(path);
                                }
                            }
                        }

                        ProcessSoundFiles(filesToProcess.ToArray());
                    }
                    break;
            }
        }

        private void ImportSoundFile()
        {
            // Open a file panel specifically for wav files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Sound to Import", 
                "", 
                new string[] { "Sound Files", "wav", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessSoundFiles(new string[] { sourcePath });
        }

        private void ProcessSoundFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string targetDir = $"Assets/Projects/{activeProjectName}/sounds";
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Sounds", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);

                progress++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            RefreshSoundList();
        }

        private void DeleteSelectedSounds()
        {
            if (selectedIndices.Count > 0)
            {
                List<string> pathsToDelete = new List<string>();
                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedSoundPaths.Count)
                    {
                        pathsToDelete.Add(loadedSoundPaths[index]);
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
                RefreshSoundList();
            }
        }

        private void ClearAllSounds()
        {
            if (EditorUtility.DisplayDialog("Clear All Sounds", "Are you sure you want to delete all imported sounds (.wav) for this project?", "Yes, Delete All", "Cancel"))
            {
                try
                {
                    // Suspend asset imports/updates for bulk optimization
                    AssetDatabase.StartAssetEditing();

                    foreach (string path in loadedSoundPaths)
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
                RefreshSoundList();
            }
        }

        private void RefreshSoundList()
        {
            loadedSoundPaths.Clear();

            // Refresh Sounds List
            string dataDir = $"Assets/Projects/{activeProjectName}/sounds";
            if (AssetDatabase.IsValidFolder(dataDir))
            {
                string[] rawFiles = Directory.GetFiles(dataDir, "*.wav", SearchOption.TopDirectoryOnly);

                foreach (string file in rawFiles)
                {
                    string assetPath = file.Replace("\\", "/");
                    loadedSoundPaths.Add(assetPath);
                }
            }

            Repaint();
        }
    }
}