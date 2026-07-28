using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerTextures : EditorWindow
    {
        private string activeProjectName = "";

        // Exclusion list for normal textures that should not be deleted
        private readonly HashSet<string> excludedNormalTextures = new HashSet<string>
        {
            "bonus_num", "circle", "GStick_arrow", "GStick_circle", 
            "parrot01", "parrot02", "parrot03", "v_back", "tap", "rect"
        };

        private readonly HashSet<string> excludedAnimatedTextures = new HashSet<string>
        {
            "antibot", "bonus_v4", "bonus_v4_off", "credits", "glass_1", "credits_off", 
            "lightning_expl_v2", "lightning_hands", "lightning_paraliz_v2", "paper_v1", 
            "reverse_indicator_left", "reverse_indicator_right", "run_indicator", "stopsign", 
            "trick_active_up", "trick_idle_up", "bird_v0", "bird_v2", "bird_v3"
        };
        
        // Data for grid display
        private List<Texture2D> loadedTextures = new List<Texture2D>();
        private List<string> loadedTexturePaths = new List<string>();
        private string searchQuery = "";
        
        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerTextures window = GetWindow<ProjectManagerTextures>("Textures");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.RefreshTextureList();
        }

        private void OnGUI()
        {
            HandleDragAndDrop();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (GUILayout.Button("Import Textures", GUILayout.Height(40)))
            {
                ImportTextureFile();
            }

            GUILayout.Space(5);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            GUIStyle totalLabelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleRight };
            GUILayout.Label("Total Textures - " + loadedTextures.Count, totalLabelStyle, GUILayout.Height(30));
            
            GUILayout.Space(10); 
            
            GUI.enabled = selectedIndices.Count > 0;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                DeleteSelectedTexture();
            }
            
            GUI.enabled = loadedTextures.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                ClearAllTextures();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            DrawTextureGrid();
        }

        private void OnDestroy()
        {
            // Reopen the selection window with the current project name
            if (!string.IsNullOrEmpty(activeProjectName))
            {
                ProjectManagerSelection.ShowWindow(activeProjectName);
            }
        }

        private void DrawTextureGrid()
        {
            if (loadedTextures.Count == 0)
            {
                GUILayout.Label("No textures imported yet.\nClick 'Import' or Drag & Drop PNG/JPG files here.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            List<int> visibleIndices = new List<int>();
            for (int i = 0; i < loadedTextures.Count; i++)
            {
                if (string.IsNullOrEmpty(searchQuery) || loadedTextures[i].name.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    visibleIndices.Add(i);
                }
            }

            if (visibleIndices.Count == 0)
            {
                GUILayout.Label("No textures match the search.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            float windowWidth = EditorGUIUtility.currentViewWidth - 20; 
            float itemSize = 100f;
            int columns = Mathf.FloorToInt(windowWidth / (itemSize + 10f));
            if (columns < 1) columns = 1;

            int index = 0;
            GUILayout.BeginVertical();
            
            while (index < visibleIndices.Count)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < columns; i++)
                {
                    if (index >= visibleIndices.Count) break;

                    DrawTextureItem(visibleIndices[index], itemSize);
                    index++;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5);
            }
            
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            Rect scrollRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && scrollRect.Contains(Event.current.mousePosition))
            {
                selectedIndices.Clear();
                GUI.FocusControl(null);
                Repaint();
            }
        }

        private void DrawTextureItem(int index, float size)
        {
            Texture2D tex = loadedTextures[index];
            bool isSelected = selectedIndices.Contains(index);

            Rect boxRect = GUILayoutUtility.GetRect(size, size);
            
            if (isSelected)
                EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.5f, 0.9f, 0.7f));
            else
                EditorGUI.DrawRect(boxRect, new Color(0.2f, 0.2f, 0.2f, 0.5f));

            Rect texRect = new Rect(boxRect.x + 5, boxRect.y + 5, boxRect.width - 10, boxRect.height - 30);
            if (tex != null) GUI.DrawTexture(texRect, tex, ScaleMode.ScaleToFit);

            Rect labelRect = new Rect(boxRect.x + 5, boxRect.y + boxRect.height - 20, boxRect.width - 10, 20);
            string labelName = tex != null ? tex.name : "Null";
            GUI.Label(labelRect, labelName, EditorStyles.miniLabel);

            Event e = Event.current;

            if (e.type == EventType.MouseDrag && e.button == 0 && boxRect.Contains(e.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                
                List<Object> draggedObjects = new List<Object>();
                if (isSelected)
                {
                    foreach (int i in selectedIndices)
                    {
                        draggedObjects.Add(loadedTextures[i]);
                    }
                }
                else
                {
                    draggedObjects.Add(tex);
                }

                DragAndDrop.objectReferences = draggedObjects.ToArray();
                DragAndDrop.StartDrag("Dragging Textures");
                e.Use();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && boxRect.Contains(e.mousePosition))
            {
                if (e.control || e.command)
                {
                    if (isSelected) selectedIndices.Remove(index);
                    else selectedIndices.Add(index);
                }
                else if (e.shift && selectedIndices.Count > 0)
                {
                    int lastSelected = selectedIndices[selectedIndices.Count - 1];
                    int start = Mathf.Min(lastSelected, index);
                    int end = Mathf.Max(lastSelected, index);
                    selectedIndices.Clear();
                    for (int i = start; i <= end; i++) selectedIndices.Add(i);
                }
                else
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
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                                    .Where(file => file.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) || 
                                                   file.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                                                   file.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase)));
                            }
                            else if (File.Exists(path))
                            {
                                string ext = Path.GetExtension(path).ToLower();
                                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
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

        private void ImportTextureFile()
        {
            // Open a file panel specifically for png and jpg files
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                "Select Texture to Import", 
                "", 
                new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" }
            );
            
            if (string.IsNullOrEmpty(sourcePath)) return;

            ProcessFiles(new string[] { sourcePath });
        }

        private void ProcessFiles(string[] filePaths)
        {
            if (filePaths.Length == 0) return;

            string baseProjPath = $"Assets/Projects/{activeProjectName}";
            string texturesDir = $"{baseProjPath}/textures";
            string animTexturesDir = $"{baseProjPath}/animatedtextures";

            List<string> newlyImportedAssets = new List<string>();

            int progress = 0;
            foreach (string file in filePaths)
            {
                EditorUtility.DisplayProgressBar("Importing Textures", $"Processing {Path.GetFileName(file)}", (float)progress / filePaths.Length);

                string plistPath = Path.ChangeExtension(file, ".plist");
                bool hasPlist = File.Exists(plistPath);

                string targetDir = hasPlist ? animTexturesDir : texturesDir;

                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                // Copy Texture
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(targetDir, fileName).Replace("\\", "/");
                File.Copy(file, destFile, true);
                newlyImportedAssets.Add(destFile);

                // Copy Plist if exists
                if (hasPlist)
                {
                    string plistDest = Path.Combine(targetDir, Path.GetFileName(plistPath)).Replace("\\", "/");
                    File.Copy(plistPath, plistDest, true);
                }

                progress++;
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            ApplyImporterSettings(newlyImportedAssets);
            RefreshTextureList();
        }

        private void ApplyImporterSettings(List<string> assetPaths)
        {
            foreach (string path in assetPaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 1;

                    TextureImporterSettings settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = (int)SpriteAlignment.TopLeft;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
            }
        }

        private void DeleteSelectedTexture()
        {
            if (selectedIndices.Count > 0)
            {
                List<string> pathsToDelete = new List<string>();
                int excludedCount = 0;

                foreach (int index in selectedIndices)
                {
                    if (index >= 0 && index < loadedTexturePaths.Count)
                    {
                        string path = loadedTexturePaths[index];
                        
                        if (IsFileExcluded(path))
                        {
                            excludedCount++;
                        }
                        else
                        {
                            pathsToDelete.Add(path);
                        }
                    }
                }

                // If ONLY excluded files were selected, show the prompt and stop.
                if (pathsToDelete.Count == 0 && excludedCount > 0)
                {
                    EditorUtility.DisplayDialog("Cannot Delete", excludedCount + " File isn't deletable!", "OK");
                    return;
                }

                // If we reach here, we have at least one deletable file.
                // It will silently ignore the excluded ones (if any) and delete the valid ones.
                try
                {
                    AssetDatabase.StartAssetEditing();

                    foreach (string pathToDelete in pathsToDelete)
                    {
                        string plistPath = Path.ChangeExtension(pathToDelete, ".plist");
                        
                        AssetDatabase.DeleteAsset(pathToDelete);
                        if (File.Exists(plistPath))
                        {
                            AssetDatabase.DeleteAsset(plistPath);
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                selectedIndices.Clear();
                RefreshTextureList();
            }
        }

        private void ClearAllTextures()
        {
            List<string> pathsToDelete = new List<string>();
            
            // Filter out excluded files first
            foreach (string path in loadedTexturePaths)
            {
                if (!IsFileExcluded(path))
                {
                    pathsToDelete.Add(path);
                }
            }

            // If there is nothing to delete (only excluded files exist), do nothing and don't prompt.
            if (pathsToDelete.Count == 0) return;

            if (EditorUtility.DisplayDialog("Clear All Textures", "Are you sure you want to delete all imported textures for this project? This cannot be undone.", "Delete All", "Cancel"))
            {
                try
                {
                    AssetDatabase.StartAssetEditing();

                    foreach (string path in pathsToDelete)
                    {
                        AssetDatabase.DeleteAsset(path);
                        string plistPath = Path.ChangeExtension(path, ".plist");
                        if (File.Exists(plistPath))
                        {
                            AssetDatabase.DeleteAsset(plistPath);
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                selectedIndices.Clear();
                RefreshTextureList();
            }
        }

        private void RefreshTextureList()
        {
            loadedTextures.Clear();
            loadedTexturePaths.Clear();

            string texturesDir = $"Assets/Projects/{activeProjectName}/textures";
            string animTexturesDir = $"Assets/Projects/{activeProjectName}/animatedtextures";

            List<string> searchDirs = new List<string>();
            if (AssetDatabase.IsValidFolder(texturesDir)) searchDirs.Add(texturesDir);
            if (AssetDatabase.IsValidFolder(animTexturesDir)) searchDirs.Add(animTexturesDir);

            if (searchDirs.Count > 0)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", searchDirs.ToArray());

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null)
                    {
                        loadedTextures.Add(tex);
                        loadedTexturePaths.Add(path);
                    }
                }
            }

            Repaint();
        }

        private bool IsFileExcluded(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            // Get the name of the folder immediately containing the file (textures or animatedtextures)
            string directoryName = Path.GetFileName(Path.GetDirectoryName(path));

            if (directoryName == "textures" && excludedNormalTextures.Contains(fileName))
                return true;
            
            if (directoryName == "animatedtextures" && excludedAnimatedTextures.Contains(fileName))
                return true;

            return false;
        }
    }
}