using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vectorier.XML;
using Vectorier.Handler;
using Vectorier.EditorScript;
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

        // ================= MENU ================= //

        [MenuItem("Vectorier/Export", false, 0)]
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

        // ================= CONFIG ================= //

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

        // ================= Parallax ================= //

        private void ExecuteWithParallaxDisabled(Action exportAction)
        {
            var parallaxes = FindObjectsByType<Parallax.Parallax>(FindObjectsSortMode.None);
            var activeParallaxes = new List<Parallax.Parallax>();

            // Find all active parallaxes and turn them off
            foreach (var p in parallaxes)
            {
                if (p.IsActive)
                {
                    activeParallaxes.Add(p);
                    p.ToggleParallax();
                }
            }

            try
            {
                exportAction?.Invoke();
            }
            finally
            {
                foreach (var p in activeParallaxes)
                {
                    if (p != null && !p.IsActive)
                    {
                        p.ToggleParallax();
                    }
                }
            }
        }

        // ================= UI ================= //

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

            config.exportType = (ExportConfig.ExportType)EditorGUILayout.EnumPopup(new GUIContent("Export Type", "Select the type of XML to export.\n- Level: Export the current level\n- Objects: Export all objects in the scene into an objects xml.\n- Buildings: Export all buildings in the scene into a buildings xml."), config.exportType);
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
            
            if (GUILayout.Button(new GUIContent("Export", "Export the level into the file path directory."), GUILayout.Height(50)))
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                ExecuteWithParallaxDisabled(() => 
                {
                    if (config.exportType == ExportConfig.ExportType.Level)
                        BuildLevel();
                    else if (config.exportType == ExportConfig.ExportType.Objects)
                        BuildObjects();
                    else
                        BuildBuildings();
                });
            }

            if (config.exportAsXML)
            {
                if (GUILayout.Button(new GUIContent("Export and Play", "Export the level into the file path directory, and start-up Snail Runner."), GUILayout.Height(50)))
                {
                    EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

                    ExecuteWithParallaxDisabled(() => 
                    {
                        if (config.exportType == ExportConfig.ExportType.Level)
                            BuildLevel();
                        else if (config.exportType == ExportConfig.ExportType.Objects)
                            BuildObjects();
                        else
                            BuildBuildings();
                    });

                    SnailRunner runner = EditorWindow.GetWindow<SnailRunner>("Play Level");
                    runner.SetLevelAndPlay(config.fileName);
                }
            }

            if ((config.exportType == ExportConfig.ExportType.Objects || config.exportType == ExportConfig.ExportType.Buildings) && config.exportAsXML)
            {
                if (GUILayout.Button(new GUIContent("Save to Existing", "Save the exported objects to the selected XML file.\nThe selected XML file is decided from the current file path directory, and the level's name."), GUILayout.Height(40)))
                {
                    if (string.IsNullOrEmpty(config.filePathDirectory) || string.IsNullOrEmpty(config.fileName))
                    {
                        UnityEngine.Debug.LogWarning("[Export] Path or filename missing.");
                    }
                    else
                    {
                        string path = Path.Combine(config.filePathDirectory, $"{config.fileName}.xml");

                        if (!File.Exists(path))
                        {
                            UnityEngine.Debug.LogError("[Export] Target XML does not exist.");
                        }
                        else
                        {
                            ExecuteWithParallaxDisabled(() => 
                            {
                                ExportHandler.ExportToExisting(config.exportType == ExportConfig.ExportType.Objects ? ExportHandler.ExportMode.Objects : ExportHandler.ExportMode.Buildings, path);
                            });
                        }
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("Revert to Default", "Revert all levels xml to the original."), GUILayout.Height(40)))
            {
                RevertToDefault();
            }

            EditorGUILayout.EndScrollView();
        }

        // ================= UI DRAWERS ================= //

        private void DrawLevelConfigUI()
        {
            DrawDirectoryFieldWithBrowse("File Path Directory", ref config.filePathDirectory);
            DrawXmlNameFieldWithBrowse("Level Name", ref config.fileName, ref config.filePathDirectory);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets, "The list of the buildings sets to use for this level.\nThis is for referencing buildings prefabs from the XML.");
            DrawSetListUI("Ground", config.groundSets, "The list of the objects sets to use for this level.\nThis is for referencing object prefabs from the XML.");
            DrawSetListUI("Library", config.librarySets, "(DEPRACATED) May still be used for steam compatibility, but isn't used in Unity.\nThe list of the library sets to use for this level.");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Music>", EditorStyles.boldLabel);
            config.musicName = EditorGUILayout.TextField(new GUIContent("Music Name", "The name of the music track to use for this level."), config.musicName);
            config.musicVolume = EditorGUILayout.FloatField(new GUIContent("Music Volume", "Adjust the volume of the music track.\nDefault: 0.3"), config.musicVolume);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Models>", EditorStyles.boldLabel);

            EditorGUILayout.LabelField("Common Mode");
            commonModeScrollPosition = EditorGUILayout.BeginScrollView(commonModeScrollPosition, true, true, GUILayout.Height(100));
            config.commonModeModels = EditorGUILayout.TextArea(config.commonModeModels, new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(new GUIContent("Edit Common Mode Models", "Edit the model properties for the common mode."), GUILayout.Height(24)))
            {
                ModelEditorWindow.Open(config, true);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hunter Mode");
            hunterModeScrollPosition = EditorGUILayout.BeginScrollView(hunterModeScrollPosition, true, true, GUILayout.Height(100));
            config.hunterModeModels = EditorGUILayout.TextArea(config.hunterModeModels, new GUIStyle(EditorStyles.textArea) { wordWrap = false }, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button(new GUIContent("Edit Hunter Mode Models", "Edit the model properties for the hunter mode."), GUILayout.Height(24)))
            {
                ModelEditorWindow.Open(config, false);
            }

            EditorGUILayout.Space();
            config.coinValue = EditorGUILayout.IntField("Coins Value", config.coinValue);

            config.exportAsXML = EditorGUILayout.Toggle(new GUIContent("Export as XML", "Export the level as an XML file instead of compiling it into .dz\nFor Unity and Snail Runner, enable this.\nFor Steam Version, disable this."), config.exportAsXML);

            if (!config.exportAsXML)
            {
                config.fastBuild = EditorGUILayout.Toggle(new GUIContent("Fast Build", "Will make the compile time faster, but may increase the size of the final build.\nThis is recommended to be enabled."), config.fastBuild);
            }
        }

        private void DrawObjectsConfigUI()
        {
            DrawDirectoryFieldWithBrowse("File Path Directory", ref config.filePathDirectory);
            DrawXmlNameFieldWithBrowse("Objects Name", ref config.fileName, ref config.filePathDirectory);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets, "The list of the buildings sets to use for this level.\nThis is for referencing buildings prefabs from the XML.");
            DrawSetListUI("Ground", config.groundSets, "The list of the objects sets to use for this level.\nThis is for referencing object prefabs from the XML.");
            DrawSetListUI("Library", config.librarySets, "(DEPRACATED) May still be used for steam compatibility, but isn't used in Unity.\nThe list of the library sets to use for this level.");

            config.exportAsXML = EditorGUILayout.Toggle(new GUIContent("Export as XML", "Export the level as an XML file instead of compiling it into .dz\nFor Unity and Snail Runner, enable this.\nFor Steam Version, disable this."), config.exportAsXML);

            if (config.exportAsXML)
            {
                config.fastBuild = EditorGUILayout.Toggle(new GUIContent("Fast Build", "Will make the compile time faster, but may increase the size of the final build.\nThis is recommended to be enabled."), config.fastBuild);
            }
        }

        private void DrawBuildingsConfigUI()
        {
            DrawDirectoryFieldWithBrowse("File Path Directory", ref config.filePathDirectory);
            DrawXmlNameFieldWithBrowse("Buildings Name", ref config.fileName, ref config.filePathDirectory);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("<Sets>", EditorStyles.boldLabel);
            DrawSetListUI("City", config.citySets, "The list of the buildings sets to use for this level.\nThis is for referencing buildings prefabs from the XML.");
            DrawSetListUI("Ground", config.groundSets, "The list of the objects sets to use for this level.\nThis is for referencing object prefabs from the XML.");
            DrawSetListUI("Library", config.librarySets, "(DEPRACATED) May still be used for steam compatibility, but isn't used in Unity.\nThe list of the library sets to use for this level.");

            config.exportAsXML = EditorGUILayout.Toggle(new GUIContent("Export as XML", "Export the level as an XML file instead of compiling it into .dz\nFor Unity and Snail Runner, enable this.\nFor Steam Version, disable this."), config.exportAsXML);

            if (!config.exportAsXML)
            {
                config.fastBuild = EditorGUILayout.Toggle(new GUIContent("Fast Build", "Will make the compile time faster, but may increase the size of the final build.\nThis is recommended to be enabled."), config.fastBuild);
            }
        }

        private void DrawSetListUI(string setName, List<string> setList, string tooltip = "")
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

            if (GUILayout.Button(new GUIContent($"Add {setName} Set", tooltip)))
                setList.Add("");
        }

        // ================= BUILD OPERATIONS ================= //

        private void BuildLevel() => BuildCommon("level-template.xml", "Level", ExportHandler.ExportMode.Level, "UnnamedLevel");
        private void BuildObjects() => BuildCommon("objects-template.xml", "Objects", ExportHandler.ExportMode.Objects, "UnnamedObjectSet");
        private void BuildBuildings() => BuildCommon("buildings-template.xml", "Buildings", ExportHandler.ExportMode.Buildings, "UnnamedBuildingsSet");

        private void BuildCommon(string templateFile, string typeName, ExportHandler.ExportMode mode, string defaultName)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string dzipFolder = Path.Combine(projectRoot, "DZIP");
            string templatePath = Path.Combine(dzipFolder, templateFile);
            string outputFolder = Path.Combine(dzipFolder, "level");

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

        // ================= HELPERS ================= //
        private void DrawDirectoryFieldWithBrowse(string label, ref string directoryPath)
        {
            EditorGUILayout.BeginHorizontal();

            directoryPath = EditorGUILayout.TextField(label, directoryPath);

            // Browse button
            if (GUILayout.Button(new GUIContent("...", "Browse"), GUILayout.Width(28)))
            {
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFolderPanel($"Select {label}", startPath, "");

                if (!string.IsNullOrEmpty(picked))
                {
                    directoryPath = picked;
                    GUI.FocusControl(null);
                }
            }
            
            // Set to default button
            if (GUILayout.Button(new GUIContent("R", "Reset the directory path to Snail Runner's path."), GUILayout.Width(28)))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                directoryPath = Path.Combine(projectRoot, "Snail Runner", "Vector_Data", "StreamingAssets", "xmlroot", "levels").Replace("\\", "/");
                
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
                string startPath = string.IsNullOrEmpty(directoryPath) ? Application.dataPath : directoryPath;
                string picked = EditorUtility.OpenFilePanel($"Select {label}", startPath, "xml");

                if (!string.IsNullOrEmpty(picked))
                    xmlName = Path.GetFileNameWithoutExtension(picked);
            }

            EditorGUILayout.EndHorizontal();
        }

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
            // -------- MUSIC --------
            if (!string.IsNullOrEmpty(config.musicName))
            {
                XmlElement musicElement = xmlUtility.AddElement(parentElement, "Music");
                xmlUtility.SetAttribute(musicElement, "Name", config.musicName);
                xmlUtility.SetAttribute(musicElement, "Volume", config.musicVolume);
            }

            // -------- MODELS --------
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

            // -------- COINS --------
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

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string batchFile = config.fastBuild ? "compile-level-fast.bat" : "compile-level.bat";
            string batchPath = Path.Combine(projectRoot, "DZIP", batchFile);

            if (!File.Exists(batchPath))
            {
                UnityEngine.Debug.LogError("[Export] Batch file not found: " + batchPath);
                return;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            Process process = new Process();
            process.StartInfo.FileName = batchPath;
            
            process.StartInfo.WorkingDirectory = Path.Combine(projectRoot, "DZIP");
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = false;

            process.Start();
            process.WaitForExit();

            stopwatch.Stop();

            string sourceFile = Path.Combine(projectRoot, "DZIP", "level_xml.dz");
            if (File.Exists(sourceFile) && !string.IsNullOrEmpty(config.filePathDirectory))
            {
                string dest = Path.Combine(config.filePathDirectory, "level_xml.dz");
                File.Copy(sourceFile, dest, true);
                UnityEngine.Debug.Log("[Export] Copied to: " + dest);
            }

            UnityEngine.Debug.Log($"[Export] Compilation finished in {stopwatch.ElapsedMilliseconds / 1000f:F2} seconds.");
        }

        private void RevertToDefault()
        {
            if (EditorUtility.DisplayDialog("Revert to Default", 
                "Are you sure you want to revert every levels xml to default?", 
                "Yes", 
                "No"))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string templateDir = Path.Combine(projectRoot, "DZIP", "_TEMPLATE", "level");
                string targetDir = Path.Combine(projectRoot, "DZIP", "level");

                if (Directory.Exists(targetDir))
                {
                    string[] existingFiles = Directory.GetFiles(targetDir);
                    foreach (string file in existingFiles)
                    {
                        File.Delete(file);
                    }
                }
                else
                {
                    Directory.CreateDirectory(targetDir);
                }

                if (Directory.Exists(templateDir))
                {
                    string[] templateFiles = Directory.GetFiles(templateDir);
                    foreach (string file in templateFiles)
                    {
                        string fileName = Path.GetFileName(file);
                        string destFile = Path.Combine(targetDir, fileName);
                        File.Copy(file, destFile, true);
                    }
                    UnityEngine.Debug.Log("[Export] Successfully reverted all level XMLs to default.");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[Export] _TEMPLATE/level directory does not exist!");
                }
            }
        }
    }
}
