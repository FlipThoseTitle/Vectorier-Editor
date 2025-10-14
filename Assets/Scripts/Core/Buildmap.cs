using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Vectorier.XML;
using Vectorier.Handler;
using Vectorier.Debug;

namespace Vectorier.Core
{
    public class Buildmap : EditorWindow
    {
        private BuildmapConfig config;
        private Vector2 scrollPos;
        private Vector2 commonModeScroll;
        private Vector2 hunterModeScroll;

        // ============================================================
        // MENU ITEM
        // ============================================================
        [MenuItem("Vectorier/Build")]
        public static void ShowWindow()
        {
            GetWindow<Buildmap>("Buildmap");
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
        }

        private void OnDisable()
        {
            SaveConfig();
        }

        private void LoadOrCreateConfig()
        {
            string path = "Assets/Editor/VectorierConfig/BuildmapConfig.asset";
            string folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            config = AssetDatabase.LoadAssetAtPath<BuildmapConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BuildmapConfig>();
                AssetDatabase.CreateAsset(config, path);
                AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log("[Buildmap] Created new configuration asset at " + path);
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

        // ============================================================
        // UI
        // ============================================================
        private void OnGUI()
        {
            if (config == null)
            {
                LoadOrCreateConfig();
                return;
            }

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("Vectorier Level Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            config.exportType = (BuildmapConfig.ExportType)EditorGUILayout.EnumPopup("Export Type", config.exportType);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            switch (config.exportType)
            {
                case BuildmapConfig.ExportType.Level:
                    DrawLevelConfig();
                    break;
                case BuildmapConfig.ExportType.Objects:
                    DrawObjectsConfig();
                    break;
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Build and Export", GUILayout.Height(50)))
            {
                SaveConfig();
                if (config.exportType == BuildmapConfig.ExportType.Level)
                    BuildLevel();
                else
                    BuildObjects();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawLevelConfig()
        {
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.levelName = EditorGUILayout.TextField("Level Name", config.levelName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetList("City", config.citySets);
            DrawSetList("Ground", config.groundSets);
            DrawSetList("Library", config.librarySets);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Music>", EditorStyles.boldLabel);
            config.musicName = EditorGUILayout.TextField("Music Name", config.musicName);
            config.musicVolume = EditorGUILayout.FloatField("Music Volume", config.musicVolume);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Models>", EditorStyles.boldLabel);

            // Common Mode
            EditorGUILayout.LabelField("Common Mode");
            commonModeScroll = EditorGUILayout.BeginScrollView(
                commonModeScroll,
                true,  // horizontal scroll
                true,  // vertical scroll
                GUILayout.Height(100) // visible area height
            );
            config.commonModeModels = EditorGUILayout.TextArea(
                config.commonModeModels,
                new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = false
                },
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            // Hunter Mode
            EditorGUILayout.LabelField("Hunter Mode");
            hunterModeScroll = EditorGUILayout.BeginScrollView(
                hunterModeScroll,
                true,  // horizontal scroll
                true,  // vertical scroll
                GUILayout.Height(100)
            );
            config.hunterModeModels = EditorGUILayout.TextArea(
                config.hunterModeModels,
                new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = false
                },
                GUILayout.ExpandHeight(true),
                GUILayout.ExpandWidth(true)
            );
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            config.coinValue = EditorGUILayout.IntField("Coins Value", config.coinValue);
            config.fastBuild = EditorGUILayout.Toggle("Fast Build", config.fastBuild);
        }

        private void DrawObjectsConfig()
        {
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.levelName = EditorGUILayout.TextField("Level Name", config.levelName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetList("City", config.citySets);
            DrawSetList("Ground", config.groundSets);
            DrawSetList("Library", config.librarySets);
            config.fastBuild = EditorGUILayout.Toggle("Fast Build", config.fastBuild);
        }

        private void DrawSetList(string name, List<string> list)
        {
            EditorGUILayout.LabelField(name + " Sets", EditorStyles.boldLabel);
            int removeIndex = -1;
            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                list[i] = EditorGUILayout.TextField(list[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
                list.RemoveAt(removeIndex);

            if (GUILayout.Button($"Add {name} Set"))
                list.Add("");
        }

        // ============================================================
        // BUILD
        // ============================================================
        private void BuildLevel()
        {
            string xmlFolder = Path.Combine(Application.dataPath, "XML");
            string templatePath = Path.Combine(xmlFolder, "level-template.xml");
            string levelFolder = Path.Combine(xmlFolder, "level_xml");

            // Ensure level_xml directory exists
            if (!Directory.Exists(levelFolder))
                Directory.CreateDirectory(levelFolder);

            // Write base XML template
            XmlUtility xmlUtility = new XmlUtility();
            xmlUtility.Create("Root");

            XmlElement root = xmlUtility.RootElement;

            // --- Sets ---
            XmlElement setsElement = xmlUtility.AddElement(root, "Sets");
            foreach (var c in config.citySets)
            {
                if (!string.IsNullOrEmpty(c))
                {
                    XmlElement city = xmlUtility.AddElement(setsElement, "City");
                    xmlUtility.SetAttribute(city, "FileName", c);
                }
            }
            foreach (var g in config.groundSets)
            {
                if (!string.IsNullOrEmpty(g))
                {
                    XmlElement ground = xmlUtility.AddElement(setsElement, "Ground");
                    xmlUtility.SetAttribute(ground, "FileName", g);
                }
            }
            foreach (var l in config.librarySets)
            {
                if (!string.IsNullOrEmpty(l))
                {
                    XmlElement lib = xmlUtility.AddElement(setsElement, "Library");
                    xmlUtility.SetAttribute(lib, "FileName", l);
                }
            }

            // --- Music ---
            if (!string.IsNullOrEmpty(config.musicName))
            {
                XmlElement musicElement = xmlUtility.AddElement(root, "Music");
                xmlUtility.SetAttribute(musicElement, "Name", config.musicName);
                xmlUtility.SetAttribute(musicElement, "Volume", config.musicVolume);
            }

            // --- Models ---
            if (!string.IsNullOrEmpty(config.commonModeModels))
            {
                XmlElement modelsCommon = xmlUtility.AddElement(root, "Models");
                xmlUtility.SetAttribute(modelsCommon, "Choice", "AITriggers");
                xmlUtility.SetAttribute(modelsCommon, "Variant", "CommonMode");
                modelsCommon.InnerXml = config.commonModeModels;
            }
            if (!string.IsNullOrEmpty(config.hunterModeModels))
            {
                XmlElement modelsHunter = xmlUtility.AddElement(root, "Models");
                xmlUtility.SetAttribute(modelsHunter, "Choice", "AITriggers");
                xmlUtility.SetAttribute(modelsHunter, "Variant", "HunterMode");
                modelsHunter.InnerXml = config.hunterModeModels;
            }

            // --- Coins ---
            if (config.coinValue > 0)
            {
                XmlElement coins = xmlUtility.AddElement(root, "Coins");
                xmlUtility.SetAttribute(coins, "Value", config.coinValue);
                XmlElement objects = xmlUtility.AddElement(root, "Objects");
                xmlUtility.SetAttribute(objects, "Name", "Money");
            }

            // Save the template
            xmlUtility.Save(templatePath);
            DebugLog.Info("[Buildmap] Level template saved: " + templatePath);

            // Run scene export
            ExportHandler.Export(ExportHandler.ExportMode.Level, templatePath);

            // Copy and rename the exported XML using Level Name
            if (string.IsNullOrEmpty(config.levelName))
            {
                DebugLog.Warn("[Buildmap] Level Name is empty. Using 'UnnamedLevel'.");
                config.levelName = "UnnamedLevel";
            }

            string destinationXml = Path.Combine(levelFolder, $"{config.levelName}.xml");

            // Format XML before copying
            XmlUtility.FormatXML(templatePath, templatePath);

            File.Copy(templatePath, destinationXml, true);
            DebugLog.Info($"[Buildmap] Copied and renamed to: {destinationXml}");

            // Compile .dz archive
            CompileXML(templatePath);
        }

        private void BuildObjects()
        {
            string xmlFolder = Path.Combine(Application.dataPath, "XML");
            string templatePath = Path.Combine(xmlFolder, "objects-template.xml");
            string levelFolder = Path.Combine(xmlFolder, "level_xml");

            // Ensure level_xml directory exists
            if (!Directory.Exists(levelFolder))
                Directory.CreateDirectory(levelFolder);

            // Write base XML template
            XmlUtility xmlUtility = new XmlUtility();
            xmlUtility.Create("Root");

            XmlElement root = xmlUtility.RootElement;

            // --- Sets ---
            XmlElement setsElement = xmlUtility.AddElement(root, "Sets");
            foreach (var c in config.citySets)
            {
                if (!string.IsNullOrEmpty(c))
                {
                    XmlElement city = xmlUtility.AddElement(setsElement, "City");
                    xmlUtility.SetAttribute(city, "FileName", c);
                }
            }
            foreach (var g in config.groundSets)
            {
                if (!string.IsNullOrEmpty(g))
                {
                    XmlElement ground = xmlUtility.AddElement(setsElement, "Ground");
                    xmlUtility.SetAttribute(ground, "FileName", g);
                }
            }
            foreach (var l in config.librarySets)
            {
                if (!string.IsNullOrEmpty(l))
                {
                    XmlElement lib = xmlUtility.AddElement(setsElement, "Library");
                    xmlUtility.SetAttribute(lib, "FileName", l);
                }
            }

            // Save template
            xmlUtility.Save(templatePath);
            DebugLog.Info("[Buildmap] Objects template saved: " + templatePath);

            // Run scene export
            ExportHandler.Export(ExportHandler.ExportMode.Objects, templatePath);

            // Copy and rename exported XML
            if (string.IsNullOrEmpty(config.levelName))
            {
                DebugLog.Warn("[Buildmap] Level Name is empty. Using 'UnnamedObjectSet'.");
                config.levelName = "UnnamedObjectSet";
            }

            string destinationXml = Path.Combine(levelFolder, $"{config.levelName}.xml");

            // Format XML before copying
            XmlUtility.FormatXML(templatePath, templatePath);

            File.Copy(templatePath, destinationXml, true);
            DebugLog.Info($"[Buildmap] Copied and renamed to: {destinationXml}");

            // Compile
            CompileXML(templatePath);
        }

        private void CompileXML(string xmlPath)
        {
            string batchFile = config.fastBuild ? "compile-level-fast.bat" : "compile-level.bat";
            string batchPath = Path.Combine(Application.dataPath, "XML", batchFile);

            if (!File.Exists(batchPath))
            {
                DebugLog.Error("[Buildmap] Batch file not found: " + batchPath);
                return;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Process process = new Process();
            process.StartInfo.FileName = batchPath;
            process.StartInfo.WorkingDirectory = Path.Combine(Application.dataPath, "XML");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;

            process.Start();
            process.WaitForExit();

            stopwatch.Stop();

            string sourceFile = Path.Combine(Application.dataPath, "XML", "level_xml.dz");
            if (File.Exists(sourceFile) && !string.IsNullOrEmpty(config.filePathDirectory))
            {
                string dest = Path.Combine(config.filePathDirectory, "level_xml.dz");
                File.Copy(sourceFile, dest, true);
                UnityEngine.Debug.Log("[Buildmap] Copied to: " + dest);
            }

            UnityEngine.Debug.Log($"[Buildmap] Compilation finished in {stopwatch.ElapsedMilliseconds / 1000f:F2} seconds.");
        }
    }
}
