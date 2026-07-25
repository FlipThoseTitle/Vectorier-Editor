using System.IO;
using UnityEditor;
using UnityEngine;
using Vectorier.EditorScript;
using Vectorier.Handler;

namespace Vectorier.Core
{
    public class Import : EditorWindow
    {
        private ImportConfig config;
        private Vector2 mainScroll;

        [MenuItem("Vectorier/Import", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<Import>("Import");
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
        }

        private void OnDisable()
        {
            SaveConfig();
        }

        private void OnGUI()
        {
            if (config == null)
            {
                LoadOrCreateConfig();
                return;
            }

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("XML Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            DrawImportUI();
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Import", GUILayout.Height(50)))
            {
                if (!string.IsNullOrEmpty(config.xmlName))
                    config.xmlName = Path.GetFileNameWithoutExtension(config.xmlName);

                SaveConfig();
                ImportHandler.Import(config.filePathDirectory, config.xmlName, config.textureFolders, config.untagChildren, config.selectedObject, config.includeBuildingsMarker, config.ignoreTags, config.applyConfig);
            }

            EditorGUILayout.EndScrollView();
        }

        private void LoadOrCreateConfig()
        {
            string assetPath = "Assets/Editor/Config/ImportConfig.asset";
            string folder = Path.GetDirectoryName(assetPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            config = AssetDatabase.LoadAssetAtPath<ImportConfig>(assetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<ImportConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private void SaveConfig()
        {
            if (config != null)
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawImportUI()
        {
            DrawDirectoryFieldWithBrowse("File Path Directory", ref config.filePathDirectory);
            DrawXmlNameFieldWithBrowse("XML Name", ref config.xmlName, ref config.filePathDirectory);
            config.selectedObject = EditorGUILayout.TextField(new GUIContent("Selected Objects", "Names of the objects to import.\nUse 'Open Object List' button to select your objects."), config.selectedObject);
            config.ignoreTags = EditorGUILayout.TextField(new GUIContent("Ignore Tags", "Enter tags to ignore during import.\nEx: Object,Platform,Image"), config.ignoreTags);


            if (GUILayout.Button(new GUIContent("Open Object List", "Open the object list window to select objects for import."), GUILayout.Height(25)))
            {
                ObjectListWindow.Open(config);
            }

            DrawTextureFoldersUI();
            config.untagChildren = EditorGUILayout.Toggle(new GUIContent("Untag Object's Children", "Untag every single gameObject under the gameObject tagged as 'Object'.\nThis makes it so that the Object tagged gameObject will references from sets XML.\nRecommended to be disabled."), config.untagChildren);
            config.includeBuildingsMarker = EditorGUILayout.Toggle(new GUIContent("Include Buildings Marker", "Include the buildings marker texture during import. (In and Out)"), config.includeBuildingsMarker);
            config.applyConfig = EditorGUILayout.Toggle(new GUIContent("Apply Config", "Apply the level's configuration settings to Export during import."), config.applyConfig);
        }

        private void DrawTextureFoldersUI()
        {
            EditorGUILayout.LabelField("Texture Folders", EditorStyles.boldLabel);
            int removeIndex = -1;
            for (int i = 0; i < config.textureFolders.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                config.textureFolders[i] = EditorGUILayout.TextField(config.textureFolders[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
                config.textureFolders.RemoveAt(removeIndex);

            if (GUILayout.Button(new GUIContent("Add Texture Folder", "Add a texture folder path to the list.\nThis is required for importing textures.")))
                config.textureFolders.Add("");
        }

        // ================= HELPERS ================= //
        private void DrawDirectoryFieldWithBrowse(string label, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();
            directoryPath = EditorGUILayout.TextField(label, directoryPath);

            if (GUILayout.Button(new GUIContent("...", "Browse"), GUILayout.Width(28)))
            {
                string defaultStart = Path.GetDirectoryName(Application.dataPath);
                string startPath = string.IsNullOrEmpty(directoryPath) ? defaultStart : directoryPath;
                
                string picked = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");

                if (!string.IsNullOrEmpty(picked))
                {
                    directoryPath = picked;
                    GUI.FocusControl(null);
                }
            }

            if (GUILayout.Button(new GUIContent("R", "Reset the directory path to default."), GUILayout.Width(28)))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                directoryPath = Path.Combine(projectRoot, "DZIP", "level").Replace("\\", "/");
                
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawXmlNameFieldWithBrowse(string label, ref string xmlName, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();
            xmlName = EditorGUILayout.TextField(label, xmlName);

            if (GUILayout.Button(new GUIContent("...", "Browse"), GUILayout.Width(28)))
            {
                string defaultStart = Path.GetDirectoryName(Application.dataPath);
                string startPath = string.IsNullOrEmpty(directoryPath) ? defaultStart : directoryPath;
                
                string picked = EditorUtility.OpenFilePanel($"Select {label}", startPath, "xml");

                if (!string.IsNullOrEmpty(picked))
                {
                    xmlName = Path.GetFileNameWithoutExtension(picked);
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
