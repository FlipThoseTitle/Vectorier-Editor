using UnityEngine;
using System.Xml;
using System.Collections.Generic;
using Vectorier.XML;
using Vectorier.Element;
using Vectorier.EditorScript;
using Vectorier.Core;

namespace Vectorier.Handler
{
    public static class ImportHandler
    {
        // ================= SPRITE CACHE ================= //

        public static List<string> TextureFolderPaths;
        public static Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();
        public static bool SpriteCacheBuilt = false;

        // ================= SPRITE LAYER COUNTER ================= //

        public static int GlobalOrder_Front = 0;   // depth=0
        public static int GlobalOrder_Middle = 0;  // default
        public static int GlobalOrder_Back = 0;    // depth=1

        // ================= OBJECT DEFINITIONS ================= //

        private static Dictionary<string, XmlElement> ObjectDefinitions = new Dictionary<string, XmlElement>();
        private static HashSet<string> LoadedSetFiles = new HashSet<string>();
        private static HashSet<string> IgnoredTags = new HashSet<string>();

        // ================= IMPORT ================= //

        public static void Import(string directoryPath, string xmlFileName, List<string> textureFolders, bool untagChildren, string selectedNames, bool includeBuildingsMarker, string ignoreTags, bool applyConfig)
        {
            // Parse ignored tags (case-insensitive)
            BuildIgnoreTags(ignoreTags);

            string fullPath = System.IO.Path.Combine(directoryPath, xmlFileName + ".xml");

            if (!System.IO.File.Exists(fullPath))
            {
                Debug.LogError("[ImportHandler] XML file not found: " + fullPath);
                return;
            }

            // Reset caches
            SpriteCache.Clear();
            SpriteCacheBuilt = false;
            ObjectDefinitions.Clear();
            LoadedSetFiles.Clear();
            GlobalOrder_Front = 0;
            GlobalOrder_Middle = 0;
            GlobalOrder_Back = 0;
            TextureFolderPaths = textureFolders;

            // Load XML
            XmlUtility xml = new XmlUtility();
            xml.Load(fullPath);

            if (applyConfig)
                ApplyLevelConfig(xml);

            // Load referenced <Sets>
            LoadSets(directoryPath, xml);

            // Detect file type
            XmlElement track = xml.RootElement.SelectSingleNode("Track") as XmlElement;
            bool isLevelFile = track != null;

            XmlElement mainSection = isLevelFile ? track : xml.RootElement.SelectSingleNode("Objects") as XmlElement;

            if (mainSection == null)
            {
                Debug.LogError("[ImportHandler] XML must have <Track> or <Objects>.");
                return;
            }

            // Object files override referenced definitions
            if (!isLevelFile)
                AddLocalObjectDefinitions(mainSection);

            HashSet<string> allowedNames = BuildAllowedSet(isLevelFile, selectedNames);

            GameObject root = new GameObject(xmlFileName);

            // Import everything under the main section
            ImportObjects(mainSection, root.transform, isLevelFile, allowedNames, includeBuildingsMarker, xml);

            if (untagChildren)
                RemoveTags(root);

            Debug.Log("[ImportHandler] Import Completed: " + xmlFileName);
        }

        // ================= IGNORE TAG PARSER ================= //
        private static void BuildIgnoreTags(string ignoreTags)
        {
            IgnoredTags.Clear();

            if (string.IsNullOrWhiteSpace(ignoreTags))
                return;

            string[] parts = ignoreTags.Split(',');

            foreach (string part in parts)
            {
                string tag = part.Trim();
                if (tag.Length > 0)
                    IgnoredTags.Add(tag.ToLower());
            }
        }

        // ================= LOAD REFERENCE SET FILE ================= //
        private static void LoadSets(string directoryPath, XmlUtility xml)
        {
            XmlElement sets = xml.RootElement.SelectSingleNode("Sets") as XmlElement;
            if (sets == null)
                return;

            foreach (XmlNode node in sets.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                    continue;

                if (element.Name == "Library")
                    continue; // deprecated

                if (element.Name != "Ground" && element.Name != "City")
                    continue;

                string fileName = element.GetAttribute("FileName");
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                string fullPath = System.IO.Path.Combine(directoryPath, fileName);

                // avoid loading the same file multiple times
                if (LoadedSetFiles.Contains(fullPath))
                    continue; // already loaded -> skip

                LoadedSetFiles.Add(fullPath);

                if (!System.IO.File.Exists(fullPath))
                {
                    Debug.LogWarning("[ImportHandler] Missing referenced set: " + fullPath);
                    continue;
                }

                LoadDefinitionsFromFile(fullPath);
            }
        }

        private static void LoadDefinitionsFromFile(string filePath)
        {
            XmlUtility xml = new XmlUtility();
            xml.Load(filePath);

            // Load nested <Sets> inside the referenced file
            LoadSets(System.IO.Path.GetDirectoryName(filePath), xml);

            XmlElement objects = xml.RootElement.SelectSingleNode("Objects") as XmlElement;
            if (objects == null)
                return;

            foreach (XmlNode node in objects.ChildNodes)
            {
                XmlElement objectElement = node as XmlElement;
                if (objectElement == null)
                    continue;

                if (objectElement.Name != "Object")
                    continue;

                string name = objectElement.GetAttribute("Name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                // no overwrite existing definitions
                if (!ObjectDefinitions.ContainsKey(name))
                    ObjectDefinitions[name] = objectElement;
            }
        }

        // ================= LOCAL DEFINITION OVERRIDE ================= //
        private static void AddLocalObjectDefinitions(XmlElement mainSection)
        {
            foreach (XmlNode node in mainSection.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null)
                    continue;

                if (element.Name != "Object")
                    continue;

                string name = element.GetAttribute("Name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                ObjectDefinitions[name] = element;
            }
        }

        // ================= SELECTION LIST (OBJECT ONLY) ================= //

        private static HashSet<string> BuildAllowedSet(bool isLevelFile, string selectedNames)
        {
            if (isLevelFile || string.IsNullOrWhiteSpace(selectedNames))
                return null;

            HashSet<string> set = new HashSet<string>();
            string[] parts = selectedNames.Split(',');

            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    set.Add(trimmed);
            }
            return set;
        }

        // ================= MAIN IMPORT LOOP ================= //

        private static void ImportObjects(XmlElement mainSection, Transform parent, bool isLevelFile, HashSet<string> allowedNames, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            foreach (XmlNode node in mainSection.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element == null || element.Name != "Object")
                    continue;

                if (!isLevelFile && allowedNames != null)
                {
                    string objectName = element.GetAttribute("Name");
                    if (!allowedNames.Contains(objectName))
                        continue;
                }

                if (isLevelFile)
                    ImportLevelLayer(element, parent, includeBuildingsMarker, xmlUtility);
                else
                    ImportSingleObject(element, parent, includeBuildingsMarker, xmlUtility);
            }
        }

        // ================= LEVEL LAYER IMPORT ================= //

        private static void ImportLevelLayer(XmlElement layerElement, Transform parent, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            string factor = layerElement.GetAttribute("Factor");

            GameObject layerObject = new GameObject("Factor_" + factor);
            layerObject.transform.SetParent(parent, false);

            XmlElement content = layerElement.SelectSingleNode("Content") as XmlElement;
            if (content == null)
                return;

            foreach (XmlNode node in content.ChildNodes)
            {
                XmlElement child = node as XmlElement;
                if (child == null)
                    continue;

                WriteByTag(child, layerObject.transform, factor, includeBuildingsMarker, xmlUtility);
            }
        }

        // ================= OBJECT FILE IMPORT ================= //
        private static void ImportSingleObject(XmlElement element, Transform parent, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            XmlElement finalElement = GetFinalObjectElement(element);

            GameObject created = ObjectElement.WriteToScene(finalElement, parent, "1", includeBuildingsMarker, xmlUtility);
            created.tag = "Object";
        }

        private static XmlElement GetFinalObjectElement(XmlElement input)
        {
            // If the input already has <Content>, return it directly
            XmlElement inputContent = input.SelectSingleNode("Content") as XmlElement;
            if (inputContent != null)
                return input;

            // No content -> try to find fallback definition
            string name = input.GetAttribute("Name");
            if (string.IsNullOrWhiteSpace(name))
                return input;

            if (!ObjectDefinitions.TryGetValue(name, out XmlElement definition))
                return input;

            // If the definition also has no content, return input
            XmlElement defContent = definition.SelectSingleNode("Content") as XmlElement;
            if (defContent == null)
                return input;

            // Build new merged object
            XmlDocument owner = input.OwnerDocument;
            XmlElement newObject = owner.CreateElement("Object");

            // Copy all attributes from input (level overrides)
            foreach (XmlAttribute a in input.Attributes)
                newObject.SetAttribute(a.Name, a.Value);

            // Add missing attributes from definition (fallback)
            foreach (XmlAttribute a in definition.Attributes)
            {
                if (!newObject.HasAttribute(a.Name))
                    newObject.SetAttribute(a.Name, a.Value);
            }

            // Copy definition content
            newObject.AppendChild(owner.ImportNode(defContent, true));

            return newObject;
        }

        // ================= TAG ================= //
        public static void WriteByTag(XmlElement xmlElement, Transform parent, string factor, bool includeBuildingsMarker, XmlUtility xmlUtility)
        {
            string tagNameLower = xmlElement.Name.ToLower();

            if (IgnoredTags.Contains(tagNameLower))
                return;

            switch (xmlElement.Name)
            {
                case "Object":
                    XmlElement finalElement = GetFinalObjectElement(xmlElement);
                    GameObject objectInstance = ObjectElement.WriteToScene(finalElement, parent, factor, includeBuildingsMarker, xmlUtility);
                    objectInstance.tag = "Object";
                    break;

                case "Image": ImageElement.WriteToScene(xmlElement, parent, factor); break;
                case "Trigger": TriggerElement.WriteToScene(xmlElement, parent, factor); break;
                case "Area": AreaElement.WriteToScene(xmlElement, parent, factor); break;
                case "Platform": PlatformElement.WriteToScene(xmlElement, parent, factor); break;
                case "Trapezoid": TrapezoidElement.WriteToScene(xmlElement, parent, factor); break;
                case "Spawn": SpawnElement.WriteToScene(xmlElement, parent, factor); break;
                case "Camera": CameraElement.WriteToScene(xmlElement, parent, factor); break;
                case "Model": ModelElement.WriteToScene(xmlElement, parent, factor); break;
                case "Item": ItemElement.WriteToScene(xmlElement, parent, factor); break;
                case "Animation": AnimationElement.WriteToScene(xmlElement, parent, factor); break;
                case "Particle": ParticleElement.WriteToScene(xmlElement, parent, factor); break;
            }
        }

        // -------- UNTAG ALL CHILDREN -------- //
        private static void RemoveTags(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root.transform)
                    continue;

                transform.tag = "Untagged";

                if (!transform.gameObject.GetComponent<AutomaticTagIgnore>())
                    transform.gameObject.AddComponent<AutomaticTagIgnore>();
            }
        }

        // ================= METHOD ================= //

        public static void BuildSpriteCache()
        {
            SpriteCache.Clear();
            SpriteCacheBuilt = true;

            if (TextureFolderPaths == null || TextureFolderPaths.Count == 0)
            {
                Debug.LogError("[ImportHandler] No texture folders provided.");
                return;
            }

            foreach (string folderPath in TextureFolderPaths)
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                    continue;

                string normalizedPath = folderPath.TrimEnd('/');

                Sprite[] loadedSprites = Resources.LoadAll<Sprite>(normalizedPath);

                if (loadedSprites == null || loadedSprites.Length == 0)
                {
                    Debug.LogWarning("[ImportHandler] No sprites found in '" + normalizedPath + "'.");
                    continue;
                }

                foreach (Sprite sprite in loadedSprites)
                {
                    if (!SpriteCache.ContainsKey(sprite.name))
                        SpriteCache[sprite.name] = sprite;
                }
            }
        }

        private static void ApplyLevelConfig(XmlUtility xml)
        {
            GameObject configObj = GameObject.Find("[EDITORONLY]ExportConfigHolder");
            if (configObj == null)
                return;

            ExportConfig config = configObj.GetComponent<ExportConfig>();
            if (config == null)
                return;

            XmlElement root = xml.RootElement;

            // -------- SETS -------- //
            XmlElement sets = root.SelectSingleNode("Sets") as XmlElement;
            if (sets != null)
            {
                config.citySets.Clear();
                config.groundSets.Clear();
                config.librarySets.Clear();

                foreach (XmlNode node in sets.ChildNodes)
                {
                    XmlElement element = node as XmlElement;
                    if (element == null) continue;

                    string file = element.GetAttribute("FileName");
                    if (string.IsNullOrEmpty(file)) continue;

                    switch (element.Name)
                    {
                        case "City": config.citySets.Add(file); break;
                        case "Ground": config.groundSets.Add(file); break;
                        case "Library": config.librarySets.Add(file); break;
                    }
                }
            }

            // -------- MUSIC -------- //
            XmlElement music = root.SelectSingleNode("Music") as XmlElement;
            if (music != null)
            {
                config.musicName = music.GetAttribute("Name");

                if (float.TryParse(music.GetAttribute("Volume"), out float volume))
                    config.musicVolume = volume;
            }

            // -------- MODELS -------- //
            XmlNodeList models = root.SelectNodes("Models");
            foreach (XmlNode node in models)
            {
                XmlElement model = node as XmlElement;
                if (model == null) continue;

                string variant = model.GetAttribute("Variant");
                string formattedModels = FormatModelsXML(model);

                if (variant == "CommonMode")
                    config.commonModeModels = formattedModels;
                else if (variant == "HunterMode")
                    config.hunterModeModels = formattedModels;
            }

            // -------- COINS -------- //
            XmlElement coins = root.SelectSingleNode("Coins") as XmlElement;
            if (coins != null)
            {
                if (int.TryParse(coins.GetAttribute("Value"), out int value))
                    config.coinValue = value;
            }

            UnityEditor.EditorUtility.SetDirty(config);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }

        private static string FormatModelsXML(XmlElement modelsElement)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (XmlNode node in modelsElement.ChildNodes)
            {
                XmlElement model = node as XmlElement;
                if (model == null || model.Name != "Model")
                    continue;

                sb.Append("<Model");

                foreach (XmlAttribute attr in model.Attributes)
                {
                    sb.Append(" ");
                    sb.Append(attr.Name);
                    sb.Append("=\"");
                    sb.Append(attr.Value);
                    sb.Append("\"");
                }

                sb.AppendLine("/>");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
