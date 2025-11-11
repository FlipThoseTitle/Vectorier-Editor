using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Vectorier.Handler;

namespace Vectorier.Core
{
    public class Import : EditorWindow
    {
        private ImportConfig config;
        private Vector2 mainScroll;

        [MenuItem("Vectorier/Import")]
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
                SaveConfig();
                ImportHandler.Import(config.filePathDirectory, config.xmlName, config.textureFolders, config.untagChildren);
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
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.xmlName = EditorGUILayout.TextField("XML Name", config.xmlName);
            config.xmlName = EditorGUILayout.TextField("Selected Objects", config.xmlName);
            DrawTextureFoldersUI();
            config.untagChildren = EditorGUILayout.Toggle("Untag Object's Children", config.untagChildren);
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
    }
}
