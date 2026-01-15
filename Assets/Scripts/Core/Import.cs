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
            config.selectedObject = EditorGUILayout.TextField("Selected Objects", config.selectedObject);
            config.ignoreTags = EditorGUILayout.TextField("Ignore Tags", config.ignoreTags);


            if (GUILayout.Button("Open Object List", GUILayout.Height(25)))
            {
                ObjectListWindow.Open(config);
            }

            DrawTextureFoldersUI();
            config.untagChildren = EditorGUILayout.Toggle("Untag Object's Children", config.untagChildren);
            config.includeBuildingsMarker = EditorGUILayout.Toggle("Include Buildings Marker", config.includeBuildingsMarker);
            config.applyConfig = EditorGUILayout.Toggle("Apply Config", config.applyConfig);
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

            if (GUILayout.Button("Add Texture Folder"))
                config.textureFolders.Add("");
        }

        // ================= HELPERS ================= //
        private void DrawDirectoryFieldWithBrowse(string label, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();
            directoryPath = EditorGUILayout.TextField(label, directoryPath);

            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");

                if (!string.IsNullOrEmpty(picked))
                    directoryPath = picked;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawXmlNameFieldWithBrowse(string label, ref string xmlName, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();
            xmlName = EditorGUILayout.TextField(label, xmlName);

            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFilePanel($"Select {label}", startPath, "xml");

                if (!string.IsNullOrEmpty(picked))
                    xmlName = Path.GetFileNameWithoutExtension(picked);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
