using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerMusics : EditorWindow
    {
        private string activeProjectName = "";

        // Data for list display
        private List<string> loadedMusicPaths = new List<string>();
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerMusics window = GetWindow<ProjectManagerMusics>("Music");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshMusicList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            // --- Import Music Button ---
            if (GUILayout.Button("Import Music", GUILayout.Height(30)))
            {
                ImportMusicFile();
            }

            GUILayout.Space(10);

            // --- Enclosing Box for List and Actions ---
            GUILayout.BeginVertical("box");
            
            // Action Buttons (- and C)
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle totalLabelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Musics - " + loadedMusicPaths.Count, totalLabelStyle, GUILayout.Height(30));
            
            GUILayout.Space(10); // Small gap between the text and the buttons

            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedMusic();
            }
            
            GUI.enabled = loadedMusicPaths.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllMusic();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Music List ---
            DrawMusicList();
            
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

        private void DrawMusicList()
        {
            if (loadedMusicPaths.Count == 0)
            {
                GUILayout.Label("No music imported yet.\nClick 'Import' or Drag & Drop .mp3 or .wav here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            for (int i = 0; i < loadedMusicPaths.Count; i++)
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
            string path = loadedMusicPaths[index];
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
                                // Get .mp3 and .wav if dragging a folder
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.mp3", SearchOption.AllDirectories));
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.wav", SearchOption.AllDirectories));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".mp3" || ext == ".wav")
                                {
                                    filesToProcess.Add(path);
                                }
                            }
                        }

                        ProcessMusicFiles(filesToProcess.ToArray());
                    }
                    break;
            }
        }

        private void ImportMusicFile()
        {
            // Open a file panel specifically for mp3 and wav files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Music to Import", 
                "", 
                new string[] { "Audio Files", "mp3,wav", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessMusicFiles(new string[] { sourcePath });
        }

        private void ProcessMusicFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string targetDir = $"./Projects/{activeProjectName}/music";
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Music", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);

                progress++;
            }

            EditorUtility.ClearProgressBar();
            
            RefreshMusicList();
        }

        private void DeleteSelectedMusic()
        {
            if (selectedIndices.Count > 0)
            {
                List<string> pathsToDelete = new List<string>();
                bool menuSelected = false;

                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedMusicPaths.Count)
                    {
                        string path = loadedMusicPaths[index];
                        
                        // Check if the file is "menu"
                        if (Path.GetFileNameWithoutExtension(path) == "menu")
                        {
                            menuSelected = true;
                        }
                        else
                        {
                            pathsToDelete.Add(path);
                        }
                    }
                }

                // If ONLY the "menu" music was selected, show the prompt
                if (menuSelected && pathsToDelete.Count == 0)
                {
                    EditorUtility.DisplayDialog("Notice", "The 'menu' music cannot be deleted.", "OK");
                    return; // Stop execution so we don't clear indices or refresh unnecessarily
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
                RefreshMusicList();
            }
        }

        private void ClearAllMusic()
        {
            foreach (string path in loadedMusicPaths)
            {
                // Delete everything EXCEPT the "menu" music
                if (Path.GetFileNameWithoutExtension(path) != "menu")
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
            }

            selectedIndices.Clear();
            RefreshMusicList();
        }

        private void RefreshMusicList()
        {
            loadedMusicPaths.Clear();

            // Refresh Music List
            string dataDir = $"./Projects/{activeProjectName}/music";
            if (Directory.Exists(dataDir))
            {
                List<string> rawFiles = new List<string>();
                rawFiles.AddRange(Directory.GetFiles(dataDir, "*.mp3", SearchOption.TopDirectoryOnly));
                rawFiles.AddRange(Directory.GetFiles(dataDir, "*.wav", SearchOption.TopDirectoryOnly));

                foreach (string file in rawFiles)
                {
                    string assetPath = file.Replace("\\", "/");
                    loadedMusicPaths.Add(assetPath);
                }
            }

            Repaint();
        }
    }
}