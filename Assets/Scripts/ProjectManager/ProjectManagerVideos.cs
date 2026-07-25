using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerVideos : EditorWindow
    {
        private string activeProjectName = "";
        
        // Data for grid display
        private List<VideoClip> loadedVideos = new List<VideoClip>();
        private List<string> loadedVideoPaths = new List<string>();
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerVideos window = GetWindow<ProjectManagerVideos>("Videos");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshVideoList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            // --- Import Videos Button ---
            if (GUILayout.Button("Import Videos", GUILayout.Height(40)))
            {
                ImportVideoFile();
            }

            GUILayout.Space(5);

            // --- Action Buttons (- and C) ---
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Videos - " + loadedVideos.Count, labelStyle, GUILayout.Height(30));
            
            GUILayout.Space(5); // Adds a small gap before the buttons

            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedVideos();
            }
            
            GUI.enabled = loadedVideos.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllVideos();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // --- Videos Grid ---
            DrawVideoGrid();
        }

        private void OnDestroy()
        {
            // Reopen the selection window with the current project name
            if (!string.IsNullOrEmpty(activeProjectName))
            {
                ProjectManagerSelection.ShowWindow(activeProjectName);
            }
        }

        private void DrawVideoGrid()
        {
            if (loadedVideos.Count == 0)
            {
                GUILayout.Label("No videos imported yet.\nClick 'Import' or Drag & Drop MP4 files here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            float windowWidth = EditorGUIUtility.currentViewWidth - 20; // Account for scrollbar
            float itemSize = 100f;
            int columns = Mathf.FloorToInt(windowWidth / (itemSize + 10f));
            if (columns < 1) columns = 1;

            int index = 0;
            GUILayout.BeginVertical();
            
            while (index < loadedVideos.Count)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < columns; i++)
                {
                    if (index >= loadedVideos.Count) break;

                    DrawVideoItem(index, itemSize);
                    index++;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            // Catch background clicks specifically inside the scroll area to clear selection.
            Rect scrollRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && scrollRect.Contains(Event.current.mousePosition))
            {
                selectedIndices.Clear();
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawVideoItem(int index, float size)
        {
            VideoClip clip = loadedVideos[index];
            bool isSelected = selectedIndices.Contains(index);

            // Reserve space for the item
            Rect boxRect = GUILayoutUtility.GetRect(size, size);
            
            // Draw Background Color
            if (isSelected)
                EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.5f, 0.9f, 0.7f)); // Blue highlight
            else
                EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.2f, 0.2f, 0.5f)); // Dark grey

            // Get thumbnail from the video clip
            Texture2D tex = null;
            if (clip != null)
            {
                // GetAssetPreview is async. If it's not ready, it returns null.
                tex = AssetPreview.GetAssetPreview(clip);
                
                if (tex == null)
                {
                    // If it's still loading the preview in the background, force the window to repaint 
                    // so it instantly pops in once ready. Fallback to a mini icon in the meantime.
                    if (AssetPreview.IsLoadingAssetPreview(clip.GetInstanceID()))
                    {
                        Repaint(); 
                    }
                    tex = AssetPreview.GetMiniThumbnail(clip);
                }
            }

            // Draw Video Thumbnail
            Rect texRect = new Rect(boxRect.x + 5, boxRect.y + 5, boxRect.width - 10, boxRect.height - 30);
            if (tex != null) GUI.DrawTexture(texRect, tex, ScaleMode.ScaleToFit);

            // Draw Label
            Rect labelRect = new Rect(boxRect.x + 5, boxRect.y + boxRect.height - 20, boxRect.width - 10, 20);
            string labelName = clip != null ? clip.name : "Null";
            GUI.Label(labelRect, labelName, EditorStyles.miniLabel);

            // --- Multi-Select Logic ---
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && boxRect.Contains(e.mousePosition))
            {
                if (e.control || e.command) // Ctrl/Cmd Click
                {
                    if (isSelected) selectedIndices.Remove(index);
                    else selectedIndices.Add(index);
                }
                else if (e.shift && selectedIndices.Count > 0) // Shift Click
                {
                    int lastSelected = selectedIndices[selectedIndices.Count - 1];
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
                
                // Consume the event so the background doesn't unselect it
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
                    // Show a copy icon on the mouse
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        List<string> filesToProcess = new List<string>();

                        // Parse dropped items (can be a mix of folders and files)
                        foreach (string path in DragAndDrop.paths)
                        {
                            if (Directory.Exists(path))
                            {
                                // Handle folders
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.mp4", SearchOption.AllDirectories));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".mp4")
                                {
                                    filesToProcess.Add(path);
                                }
                            }
                        }

                        ProcessFiles(filesToProcess.ToArray());
                    }
                    break;
            }
        }

        private void ImportVideoFile()
        {
            // Open a file panel specifically for mp4 files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Video to Import", 
                "", 
                new string[] { "Video Files", "mp4", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessFiles(new string[] { sourcePath });
        }

        private void ProcessFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string targetDir = $"Assets/Projects/{activeProjectName}/videos";

            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Videos", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                // Copy Video
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);

                progress++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
            
            RefreshVideoList();
        }

        private void DeleteSelectedVideos()
        {
            if (selectedIndices.Count > 0)
            {
                // Gather paths first so indices don't shift as we delete them
                List<string> pathsToDelete = new List<string>();
                bool introSelected = false;

                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedVideoPaths.Count)
                    {
                        string path = loadedVideoPaths[index];
                        
                        // Check if the file is "intro"
                        if (Path.GetFileNameWithoutExtension(path) == "intro")
                        {
                            introSelected = true;
                        }
                        else
                        {
                            pathsToDelete.Add(path);
                        }
                    }
                }

                // If ONLY the "intro" video was selected, show the prompt
                if (introSelected && pathsToDelete.Count == 0)
                {
                    EditorUtility.DisplayDialog("Notice", "The 'intro' video cannot be deleted.", "OK");
                    return; // Stop execution so we don't clear indices or refresh unnecessarily
                }

                if (pathsToDelete.Count > 0)
                {
                    try
                    {
                        // Suspend asset imports/updates
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
                }

                selectedIndices.Clear();
                RefreshVideoList();
            }
        }

        private void ClearAllVideos()
        {
            try
            {
                // Suspend asset imports/updates to vastly speed up bulk operations
                AssetDatabase.StartAssetEditing();

                foreach (string path in loadedVideoPaths)
                {
                    // Delete everything EXCEPT the "intro" video
                    if (Path.GetFileNameWithoutExtension(path) != "intro")
                    {
                        AssetDatabase.DeleteAsset(path);
                    }
                }
            }
            finally
            {
                // Resume and process all deletions at once
                AssetDatabase.StopAssetEditing();
            }

            selectedIndices.Clear();
            RefreshVideoList();
        }

        private void RefreshVideoList()
        {
            loadedVideos.Clear();
            loadedVideoPaths.Clear();

            string videosDir = $"Assets/Projects/{activeProjectName}/videos";

            if (AssetDatabase.IsValidFolder(videosDir))
            {
                string[] guids = AssetDatabase.FindAssets("t:VideoClip", new string[] { videosDir });

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    VideoClip clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                    if (clip != null)
                    {
                        loadedVideos.Add(clip);
                        loadedVideoPaths.Add(path);
                    }
                }
            }

            Repaint();
        }
    }
}