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

        // Grid layout
        private const float ItemSize = 100f;
        private const float ItemSpacing = 10f;

        // Data for grid display
        private List<Texture2D> loadedTextures = new List<Texture2D>();
        private List<string> loadedTexturePaths = new List<string>();
        private string searchQuery = "";

        // Supports Multi-Selection
        private List<int> selectedIndices = new List<int>();
        private Vector2 scrollPosition;

        // Path helpers
        private string RawTexturesDir => $"Projects/{activeProjectName}/textures";
        private string RawAnimTexturesDir => $"Projects/{activeProjectName}/animatedtextures";
        private string ResourceImagesDir => $"Assets/Resources/Images/{activeProjectName}";
        private string ResourceSpritesDir => $"{ResourceImagesDir}/Sprites";

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerTextures window = GetWindow<ProjectManagerTextures>("Textures");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.SyncResourcesFolder();
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

        // ------------------------------------------------------------------
        // Grid (virtualized: only rows inside the visible scroll area are drawn)
        // ------------------------------------------------------------------

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

            // Reserve the remaining window space for the scroll view
            Rect viewRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            float availableWidth = position.width - 20f; // leave room for the vertical scrollbar
            int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / (ItemSize + ItemSpacing)));
            int rows = Mathf.CeilToInt(visibleIndices.Count / (float)columns);
            float rowHeight = ItemSize + ItemSpacing;

            Rect contentRect = new Rect(0, 0, availableWidth, rows * rowHeight);
            scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / rowHeight));
            int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scrollPosition.y + viewRect.height) / rowHeight));

            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int i = row * columns + col;
                    if (i >= visibleIndices.Count) break;

                    Rect itemRect = new Rect(5f + col * (ItemSize + ItemSpacing), row * rowHeight, ItemSize, ItemSize);
                    DrawTextureItem(visibleIndices[i], itemRect);
                }
            }

            // Clicking empty space inside the grid clears the selection.
            // Item clicks have already called Use(), so they won't reach this.
            Event e = Event.current;
            Rect visibleArea = new Rect(scrollPosition.x, scrollPosition.y, viewRect.width, viewRect.height);
            if (e.type == EventType.MouseDown && e.button == 0 && visibleArea.Contains(e.mousePosition))
            {
                selectedIndices.Clear();
                GUI.FocusControl(null);
                e.Use();
                Repaint();
            }

            GUI.EndScrollView();
        }

        private void DrawTextureItem(int index, Rect boxRect)
        {
            Texture2D tex = loadedTextures[index];
            bool isSelected = selectedIndices.Contains(index);

            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(boxRect, isSelected
                    ? new Color(0.2f, 0.5f, 0.9f, 0.7f)
                    : new Color(0.2f, 0.2f, 0.2f, 0.5f));

                Rect texRect = new Rect(boxRect.x + 5, boxRect.y + 5, boxRect.width - 10, boxRect.height - 30);
                if (tex != null) GUI.DrawTexture(texRect, tex, ScaleMode.ScaleToFit);
            }

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

        // ------------------------------------------------------------------
        // Importing
        // ------------------------------------------------------------------

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
                                filesToProcess.AddRange(Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(IsImageFile));
                            }
                            else if (File.Exists(path) && IsImageFile(path))
                            {
                                filesToProcess.Add(path);
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

            List<string> rawCopies = new List<string>();

            try
            {
                for (int i = 0; i < filePaths.Length; i++)
                {
                    string file = filePaths[i];
                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar("Importing Textures", $"Copying {Path.GetFileName(file)}", (float)i / filePaths.Length);

                    string plistPath = Path.ChangeExtension(file, ".plist");
                    bool hasPlist = File.Exists(plistPath);

                    string targetDir = hasPlist ? RawAnimTexturesDir : RawTexturesDir;
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    // Copy original texture (and plist) to the raw Projects folder outside Assets
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file)).Replace("\\", "/");
                    File.Copy(file, destFile, true);

                    if (hasPlist)
                    {
                        string plistDest = Path.Combine(targetDir, Path.GetFileName(plistPath)).Replace("\\", "/");
                        File.Copy(plistPath, plistDest, true);
                    }

                    rawCopies.Add(destFile);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ImportIntoResources(rawCopies, "Importing Textures");
            RefreshTextureList();
        }

        // Copies raw project textures into Assets/Resources and imports them all in ONE batch.
        // Importer settings are applied by ProjectTextureImportPostprocessor during that single import,
        // so there is no second SaveAndReimport pass. With Parallel Import enabled in Project Settings,
        // the batched Refresh() is spread across worker processes.
        private void ImportIntoResources(IList<string> rawFiles, string progressTitle)
        {
            if (rawFiles.Count == 0) return;

            try
            {
                for (int i = 0; i < rawFiles.Count; i++)
                {
                    string file = rawFiles[i];
                    string fileName = Path.GetFileName(file);

                    if (i % 10 == 0)
                        EditorUtility.DisplayProgressBar(progressTitle, $"Preparing {fileName}", (float)i / rawFiles.Count);

                    string plistPath = Path.ChangeExtension(file, ".plist");

                    if (File.Exists(plistPath))
                    {
                        if (!Directory.Exists(ResourceSpritesDir)) Directory.CreateDirectory(ResourceSpritesDir);

                        // Extract only the middle frame of the animated texture
                        Vectorier.EditorScript.Tools.PlistExtract.ExtractMiddleFrame(file, plistPath, ResourceSpritesDir, fileName);
                    }
                    else
                    {
                        if (!Directory.Exists(ResourceImagesDir)) Directory.CreateDirectory(ResourceImagesDir);

                        string resourceDestFile = Path.Combine(ResourceImagesDir, fileName).Replace("\\", "/");
                        File.Copy(file, resourceDestFile, true);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Single batched import for everything copied above
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------
        // Deleting
        // ------------------------------------------------------------------

        private void DeleteSelectedTexture()
        {
            if (selectedIndices.Count == 0) return;

            List<string> rawPathsToDelete = new List<string>();
            int excludedCount = 0;

            foreach (int index in selectedIndices)
            {
                if (index >= 0 && index < loadedTexturePaths.Count)
                {
                    string rawPath = loadedTexturePaths[index];

                    if (IsFileExcluded(rawPath)) excludedCount++;
                    else rawPathsToDelete.Add(rawPath);
                }
            }

            if (rawPathsToDelete.Count == 0 && excludedCount > 0)
            {
                EditorUtility.DisplayDialog("Cannot Delete", excludedCount + " File isn't deletable!", "OK");
                return;
            }

            DeleteRawAndResourceFiles(rawPathsToDelete);
        }

        private void ClearAllTextures()
        {
            List<string> rawPathsToDelete = loadedTexturePaths.Where(p => !IsFileExcluded(p)).ToList();
            if (rawPathsToDelete.Count == 0) return;

            if (EditorUtility.DisplayDialog("Clear All Textures", "Are you sure you want to delete all imported textures for this project? This cannot be undone.", "Delete All", "Cancel"))
            {
                DeleteRawAndResourceFiles(rawPathsToDelete);
            }
        }

        private void DeleteRawAndResourceFiles(List<string> rawPathsToDelete)
        {
            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string rawPath in rawPathsToDelete)
                {
                    string fileName = Path.GetFileName(rawPath);
                    string plistPath = Path.ChangeExtension(rawPath, ".plist");
                    bool hasPlist = File.Exists(plistPath);

                    // Delete from the raw root folder outside of Assets
                    if (File.Exists(rawPath)) File.Delete(rawPath);
                    if (hasPlist) File.Delete(plistPath);

                    // Delete from Unity's Assets/Resources/Images
                    string resourcePath = hasPlist
                        ? $"{ResourceSpritesDir}/{fileName}"
                        : $"{ResourceImagesDir}/{fileName}";

                    if (File.Exists(resourcePath))
                    {
                        AssetDatabase.DeleteAsset(resourcePath);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            CheckAndDeleteEmptyResourcesFolder();
            selectedIndices.Clear();
            RefreshTextureList();
        }

        // ------------------------------------------------------------------
        // Loading / syncing
        // ------------------------------------------------------------------

        private List<string> GetRawImageFiles()
        {
            List<string> files = new List<string>();
            if (Directory.Exists(RawTexturesDir)) files.AddRange(Directory.GetFiles(RawTexturesDir, "*.*").Where(IsImageFile));
            if (Directory.Exists(RawAnimTexturesDir)) files.AddRange(Directory.GetFiles(RawAnimTexturesDir, "*.*").Where(IsImageFile));
            return files.Select(f => f.Replace("\\", "/")).ToList();
        }

        private string GetExpectedResourcePath(string rawFile)
        {
            string fileName = Path.GetFileName(rawFile);
            bool hasPlist = File.Exists(Path.ChangeExtension(rawFile, ".plist"));
            return hasPlist ? $"{ResourceSpritesDir}/{fileName}" : $"{ResourceImagesDir}/{fileName}";
        }

        private void RefreshTextureList()
        {
            // Clean up RAM-only textures (created via LoadImage) to prevent editor memory leaks.
            // Never destroy actual Unity assets.
            foreach (var tex in loadedTextures)
            {
                if (tex != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex)))
                {
                    DestroyImmediate(tex);
                }
            }

            loadedTextures.Clear();
            loadedTexturePaths.Clear();

            List<string> filesToProcess = GetRawImageFiles();

            try
            {
                for (int i = 0; i < filesToProcess.Count; i++)
                {
                    string file = filesToProcess[i];
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);

                    // Hide excluded textures from the grid (parrots are excluded from deletion but still shown)
                    if (IsFileExcluded(file) &&
                        fileNameWithoutExt != "parrot01" &&
                        fileNameWithoutExt != "parrot02" &&
                        fileNameWithoutExt != "parrot03")
                    {
                        continue;
                    }

                    // Fast path: already imported Unity asset
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GetExpectedResourcePath(file));
                    if (tex != null)
                    {
                        loadedTextures.Add(tex);
                        loadedTexturePaths.Add(file);
                        continue;
                    }

                    // Fallback: manual byte loading (only for files that aren't imported, e.g. the parrots)
                    if (i % 10 == 0)
                    {
                        EditorUtility.DisplayProgressBar("Loading Textures", $"Processing {Path.GetFileName(file)}", (float)i / filesToProcess.Count);
                    }

                    Texture2D manualTex = new Texture2D(2, 2);
                    if (ImageConversion.LoadImage(manualTex, File.ReadAllBytes(file)))
                    {
                        manualTex.name = fileNameWithoutExt;
                        manualTex.hideFlags = HideFlags.HideAndDontSave;
                        loadedTextures.Add(manualTex);
                        loadedTexturePaths.Add(file);
                    }
                    else
                    {
                        DestroyImmediate(manualTex);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Repaint();
        }

        private void SyncResourcesFolder()
        {
            if (string.IsNullOrEmpty(activeProjectName)) return;

            // Find raw files that are missing from the Resources folder
            List<string> missing = GetRawImageFiles()
                .Where(f => !IsFileExcluded(f) && !File.Exists(GetExpectedResourcePath(f)))
                .ToList();

            // Clean up the folder if it exists but contains nothing
            CheckAndDeleteEmptyResourcesFolder();

            // Copy everything missing, then import it all in one batch
            ImportIntoResources(missing, "Syncing Missing Resources");
        }

        private void CheckAndDeleteEmptyResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourceImagesDir)) return;

            bool hasImages = Directory.EnumerateFiles(ResourceImagesDir, "*.*", SearchOption.AllDirectories).Any(IsImageFile);
            if (!hasImages)
            {
                AssetDatabase.DeleteAsset(ResourceImagesDir);
            }
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static bool IsImageFile(string path)
        {
            return path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".jpeg", System.StringComparison.OrdinalIgnoreCase);
        }

        private bool IsFileExcluded(string path)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            // Name of the folder immediately containing the file (textures or animatedtextures)
            string directoryName = Path.GetFileName(Path.GetDirectoryName(path));

            if (directoryName == "textures" && excludedNormalTextures.Contains(fileName))
                return true;

            if (directoryName == "animatedtextures" && excludedAnimatedTextures.Contains(fileName))
                return true;

            return false;
        }
    }

    // Applies Vectorier's sprite settings DURING the first import of any texture placed in
    // Assets/Resources/Images. This replaces the old "Refresh, then SaveAndReimport every asset"
    // pattern, so each texture is imported exactly once.
    public class ProjectTextureImportPostprocessor : AssetPostprocessor
    {
        private const string ResourceImagesRoot = "Assets/Resources/Images/";

        // Bump this if you change the settings below and want Unity to reimport affected textures.
        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ResourceImagesRoot, System.StringComparison.Ordinal)) return;

            TextureImporter importer = (TextureImporter)assetImporter;

            // Only configure brand-new files (no .meta yet), so manual Inspector changes are kept.
            if (!importer.importSettingsMissing) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1;
            importer.mipmapEnabled = false;

            // Optional: compression is the most expensive part of a texture import.
            // Uncompressed imports much faster but uses ~4x more memory / Library space.
            // The game export uses the raw files in Projects/, so this only affects the editor preview.
            // importer.textureCompression = TextureImporterCompression.Uncompressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.TopLeft;
            settings.spriteMeshType = SpriteMeshType.FullRect;          // skip tight-mesh outline generation
            settings.spriteGenerateFallbackPhysicsShape = false;        // skip physics shape generation
            importer.SetTextureSettings(settings);
        }
    }
}