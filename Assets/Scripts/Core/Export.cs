using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vectorier.XML;
using Vectorier.Handler;
using UnityEngine.SceneManagement;

namespace Vectorier.Core
{
    public class Export : EditorWindow
    {
        private ExportConfig config;

        private Vector2 mainScrollPosition;
        private Vector2 commonModeScrollPosition;
        private Vector2 hunterModeScrollPosition;

        private const string CONFIG_OBJECT_NAME = "[EDITORONLY]ExportConfigHolder";

        // -------------------------
        // Menu
        // -------------------------
        [MenuItem("Vectorier/Export")]
        public static void ShowWindow()
        {
            GetWindow<Export>("Export");
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
        }

        private void OnHierarchyChange()
        {
            if (config == null)
                LoadOrCreateConfig();
        }

        // -------------------------
        // Config
        // -------------------------
        private void LoadOrCreateConfig()
        {
            GameObject configObj = GameObject.Find(CONFIG_OBJECT_NAME);
            if (configObj == null)
            {
                configObj = new GameObject(CONFIG_OBJECT_NAME);
                configObj.hideFlags = HideFlags.HideInHierarchy;
                config = configObj.AddComponent<ExportConfig>();

                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                UnityEngine.Debug.Log("[Export] Created new export config for " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
            else
            {
                if (!configObj.TryGetComponent<ExportConfig>(out config))
                    config = configObj.AddComponent<ExportConfig>();
            }
        }

        // -------------------------
        // UI
        // -------------------------
        private void OnGUI()
        {
            if (config == null)
            {
                LoadOrCreateConfig();
                return;
            }

            mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition, GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("XML Builder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            config.exportType = (ExportConfig.ExportType)EditorGUILayout.EnumPopup("Export Type", config.exportType);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            switch (config.exportType)
            {
                case ExportConfig.ExportType.Level:
                    DrawLevelConfigUI();
                    break;
                case ExportConfig.ExportType.Objects:
                    DrawObjectsConfigUI();
                    break;
                case ExportConfig.ExportType.Buildings:
                    DrawBuildingsConfigUI();
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Build and Export", GUILayout.Height(50)))
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                if (config.exportType == ExportConfig.ExportType.Level)
                    BuildLevel();
                else if (config.exportType == ExportConfig.ExportType.Objects)
                    BuildObjects();
                else
                    BuildBuildings();
            }

            EditorGUILayout.EndScrollView();
        }

        // -------------------------
        // UI Drawers
        // -------------------------
        private void DrawLevelConfigUI()
        {
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.fileName = EditorGUILayout.TextField("Level Name", config.fileName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets);
            DrawSetListUI("Ground", config.groundSets);
            DrawSetListUI("Library", config.librarySets);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Music>", EditorStyles.boldLabel);
            config.musicName = EditorGUILayout.TextField("Music Name", config.musicName);
            config.musicVolume = EditorGUILayout.FloatField("Music Volume", config.musicVolume);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Models>", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Common Mode");
            commonModeScrollPosition = EditorGUILayout.BeginScrollView(commonModeScrollPosition, true, true, GUILayout.Height(100));
            config.commonModeModels = EditorGUILayout.TextArea(config.commonModeModels, new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hunter Mode");
            hunterModeScrollPosition = EditorGUILayout.BeginScrollView(hunterModeScrollPosition, true, true, GUILayout.Height(100));
            config.hunterModeModels = EditorGUILayout.TextArea(config.hunterModeModels, new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            config.coinValue = EditorGUILayout.IntField("Coins Value", config.coinValue);
            config.fastBuild = EditorGUILayout.Toggle("Fast Build", config.fastBuild);
            config.exportAsXML = EditorGUILayout.Toggle("Export as XML", config.exportAsXML);
        }

        private void DrawObjectsConfigUI()
        {
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.fileName = EditorGUILayout.TextField("Objects Name", config.fileName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets);
            DrawSetListUI("Ground", config.groundSets);
            DrawSetListUI("Library", config.librarySets);

            config.fastBuild = EditorGUILayout.Toggle("Fast Build", config.fastBuild);
            config.exportAsXML = EditorGUILayout.Toggle("Export as XML", config.exportAsXML);
        }

        private void DrawBuildingsConfigUI()
        {
            config.filePathDirectory = EditorGUILayout.TextField("File Path Directory", config.filePathDirectory);
            config.fileName = EditorGUILayout.TextField("Buildings Name", config.fileName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets);
            DrawSetListUI("Ground", config.groundSets);
            DrawSetListUI("Library", config.librarySets);

            config.fastBuild = EditorGUILayout.Toggle("Fast Build", config.fastBuild);
            config.exportAsXML = EditorGUILayout.Toggle("Export as XML", config.exportAsXML);
        }

        private void DrawSetListUI(string setName, List<string> setList)
        {
            EditorGUILayout.LabelField(setName + " Sets", EditorStyles.boldLabel);
            int removeIndex = -1;
            for (int i = 0; i < setList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                setList[i] = EditorGUILayout.TextField(setList[i]);
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    removeIndex = i;
                EditorGUILayout.EndHorizontal();
            }
            if (removeIndex >= 0)
                setList.RemoveAt(removeIndex);

            if (GUILayout.Button($"Add {setName} Set"))
                setList.Add("");
        }

        // -------------------------
        // Build operations
        // -------------------------
        private void BuildLevel() => BuildCommon("level-template.xml", "Level", ExportHandler.ExportMode.Level, "UnnamedLevel");
        private void BuildObjects() => BuildCommon("objects-template.xml", "Objects", ExportHandler.ExportMode.Objects, "UnnamedObjectSet");
        private void BuildBuildings() => BuildCommon("buildings-template.xml", "Buildings", ExportHandler.ExportMode.Buildings, "UnnamedBuildingsSet");

        private void BuildCommon(string templateFile, string typeName, ExportHandler.ExportMode mode, string defaultName)
        {
            string xmlFolder = Path.Combine(Application.dataPath, "XML");
            string templatePath = Path.Combine(xmlFolder, templateFile);
            string outputFolder = Path.Combine(xmlFolder, "level_xml");

            EnsureDirectoryExists(outputFolder);

            XmlUtility xmlUtility = new XmlUtility();
            xmlUtility.Create("Root");
            XmlElement root = xmlUtility.RootElement;

            AddSetsToXml(xmlUtility, root);
            if (mode == ExportHandler.ExportMode.Level)
            {
                AddLevelConfigToXml(xmlUtility, root);
            }

            xmlUtility.Save(templatePath);
            ExportHandler.Export(mode, templatePath);

            if (string.IsNullOrEmpty(config.fileName))
            {
                UnityEngine.Debug.LogWarning($"[Export] {typeName} Name is empty. Using '{defaultName}'.");
                config.fileName = defaultName;
            }

            string destXml = Path.Combine(outputFolder, $"{config.fileName}.xml");

            XmlUtility.FormatXML(templatePath, templatePath);
            File.Copy(templatePath, destXml, true);

            CompileXML(templatePath);
        }

        // -------------------------
        // Helpers
        // -------------------------
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private void AddSetsToXml(XmlUtility xmlUtility, XmlElement parentElement)
        {
            XmlElement setsElement = xmlUtility.AddElement(parentElement, "Sets");

            foreach (var citySet in config.citySets)
            {
                if (!string.IsNullOrEmpty(citySet))
                {
                    XmlElement cityElement = xmlUtility.AddElement(setsElement, "City");
                    xmlUtility.SetAttribute(cityElement, "FileName", citySet);
                }
            }

            foreach (var groundSet in config.groundSets)
            {
                if (!string.IsNullOrEmpty(groundSet))
                {
                    XmlElement groundElement = xmlUtility.AddElement(setsElement, "Ground");
                    xmlUtility.SetAttribute(groundElement, "FileName", groundSet);
                }
            }

            foreach (var librarySet in config.librarySets)
            {
                if (!string.IsNullOrEmpty(librarySet))
                {
                    XmlElement libraryElement = xmlUtility.AddElement(setsElement, "Library");
                    xmlUtility.SetAttribute(libraryElement, "FileName", librarySet);
                }
            }
        }

        private void AddLevelConfigToXml(XmlUtility xmlUtility, XmlElement parentElement)
        {
            // Music
            if (!string.IsNullOrEmpty(config.musicName))
            {
                XmlElement musicElement = xmlUtility.AddElement(parentElement, "Music");
                xmlUtility.SetAttribute(musicElement, "Name", config.musicName);
                xmlUtility.SetAttribute(musicElement, "Volume", config.musicVolume);
            }

            // Models
            if (!string.IsNullOrEmpty(config.commonModeModels))
            {
                XmlElement modelsCommon = xmlUtility.AddElement(parentElement, "Models");
                xmlUtility.SetAttribute(modelsCommon, "Choice", "AITriggers");
                xmlUtility.SetAttribute(modelsCommon, "Variant", "CommonMode");
                modelsCommon.InnerXml = config.commonModeModels;
            }
            if (!string.IsNullOrEmpty(config.hunterModeModels))
            {
                XmlElement modelsHunter = xmlUtility.AddElement(parentElement, "Models");
                xmlUtility.SetAttribute(modelsHunter, "Choice", "AITriggers");
                xmlUtility.SetAttribute(modelsHunter, "Variant", "HunterMode");
                modelsHunter.InnerXml = config.hunterModeModels;
            }

            // Coins
            if (config.coinValue > 0)
            {
                XmlElement coins = xmlUtility.AddElement(parentElement, "Coins");
                xmlUtility.SetAttribute(coins, "Value", config.coinValue);
                XmlElement objects = xmlUtility.AddElement(parentElement, "Objects");
                xmlUtility.SetAttribute(objects, "Name", "Money");
            }
        }

        private void CompileXML(string xmlPath)
        {
            // If "Export as XML" is enabled, copy directly to user-specified directory
            if (config.exportAsXML)
            {
                if (string.IsNullOrEmpty(config.filePathDirectory) || string.IsNullOrEmpty(config.fileName))
                {
                    UnityEngine.Debug.LogWarning("[Export] File Path Directory or Name is empty. Cannot export XML.");
                    return;
                }

                string destXml = Path.Combine(config.filePathDirectory, $"{config.fileName}.xml");
                File.Copy(xmlPath, destXml, true);
                UnityEngine.Debug.Log($"[Export] Exported XML copied to: {destXml}");
                return;
            }

            // Run batch builder
            string batchFile = config.fastBuild ? "compile-fast.bat" : "compile.bat";
            string batchPath = Path.Combine(Application.dataPath, "XML", batchFile);

            if (!File.Exists(batchPath))
            {
                UnityEngine.Debug.LogError("[Export] Batch file not found: " + batchPath);
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
                UnityEngine.Debug.Log("[Export] Copied to: " + dest);
            }

            UnityEngine.Debug.Log($"[Export] Compilation finished in {stopwatch.ElapsedMilliseconds / 1000f:F2} seconds.");
        }
    }

    [DisallowMultipleComponent]
    public class ExportConfig : MonoBehaviour
    {
        public enum ExportType { Level, Objects, Buildings }

        public ExportType exportType = ExportType.Level;

        // Common
        public string filePathDirectory = "";
        public string fileName = "";
        public bool fastBuild = false;
        public bool exportAsXML = false;

        // Sets
        public List<string> citySets = new List<string>();
        public List<string> groundSets = new List<string>();
        public List<string> librarySets = new List<string>();

        // Level-only
        public string musicName = "";
        public float musicVolume = 0.3f;
        public string commonModeModels = "";
        public string hunterModeModels = "";
        public int coinValue = 0;
    }
}
