using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerGears : EditorWindow
    {
        private class GearItem
        {
            public TextAsset modelXml;
            public string gearName = "";
            public string displayName = "";
            public string rawPriceInput = "0";
            public int price = 0;
            public Texture2D shopImage;
        }

        private string activeProjectName = "";
        private List<GearItem> gearList = new List<GearItem>();
        private int selectedIndex = -1;
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerGears window = GetWindow<ProjectManagerGears>("Gears");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 500);
            window.Show();
            window.Init();
        }

        private void Init()
        {
            LoadGearsFromXml();
        }

        private void OnGUI()
        {
            // Catch background clicks to clear focus and trigger delayed inputs
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(10);

            // --- Header & Action Buttons ---
            GUILayout.BeginHorizontal();
            GUILayout.Label("Gears", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                AddGear();
            }
            
            GUI.enabled = selectedIndex >= 0 && selectedIndex < gearList.Count;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                RemoveGear();
            }
            GUI.enabled = true;

            // Added C button
            GUI.enabled = gearList.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                ClearAllGears();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // --- Gear List ---
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (gearList.Count == 0)
            {
                GUILayout.Label("No gears added yet. Click '+' to create one.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                for (int i = 0; i < gearList.Count; i++)
                {
                    DrawGearItem(i);
                }
            }

            GUILayout.EndScrollView();
        }

        private void OnDestroy()
        {
            // Reopen the selection window with the current project name
            if (!string.IsNullOrEmpty(activeProjectName))
            {
                ProjectManagerSelection.ShowWindow(activeProjectName);
            }
        }

        private void ClearAllGears()
        {
            if (EditorUtility.DisplayDialog("Clear All Gears", "Are you sure you want to delete all gears?", "Yes", "No"))
            {
                foreach (GearItem item in gearList)
                {
                    // Delete Model XML
                    if (item.modelXml != null)
                    {
                        string path = AssetDatabase.GetAssetPath(item.modelXml);
                        if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                    }

                    // Delete Shop Image
                    if (item.shopImage != null)
                    {
                        string path = AssetDatabase.GetAssetPath(item.shopImage);
                        if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
                    }
                }
                
                gearList.Clear();
                selectedIndex = -1;
                SaveToXml();
            }
        }

        private void DrawGearItem(int index)
        {
            GearItem item = gearList[index];
            bool isSelected = (selectedIndex == index);

            // Item Box Background
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            if (isSelected) boxStyle.normal.background = MakeTex(2, 2, new Color(0.2f, 0.5f, 0.9f, 0.5f));

            GUILayout.BeginVertical(boxStyle);
            
            // Header for selection
            GUILayout.BeginHorizontal();
            if (GUILayout.Button((index + 1).ToString(), EditorStyles.toolbarButton))
            {
                selectedIndex = index;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // 1. Model XML Pointer
            EditorGUI.BeginChangeCheck();
            TextAsset newXml = (TextAsset)EditorGUILayout.ObjectField("Model XML", item.modelXml, typeof(TextAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                item.modelXml = newXml;
                SaveToXml();
            }

            // 2. Gear Name (Delayed)
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.DelayedTextField("Gear Name", item.gearName);
            if (EditorGUI.EndChangeCheck())
            {
                string formatted = FormatGearName(newName);
                if (formatted != item.gearName)
                {
                    RenameGearAssets(item, formatted);
                    item.gearName = formatted;
                    SaveToXml();
                }
            }

            // 3. Display Name / Localization (Delayed)
            EditorGUI.BeginChangeCheck();
            string newDisplayName = EditorGUILayout.DelayedTextField("Display Name", item.displayName);
            if (EditorGUI.EndChangeCheck())
            {
                item.displayName = newDisplayName;
                SaveToXml(); // Save to sync the localization file
            }

            // 4. Price (Delayed)
            EditorGUI.BeginChangeCheck();
            item.rawPriceInput = EditorGUILayout.DelayedTextField("Price", item.rawPriceInput);
            if (EditorGUI.EndChangeCheck())
            {
                item.price = ParsePrice(item.rawPriceInput);
                item.rawPriceInput = item.price.ToString(); // Visually update field back to int
                SaveToXml();
            }

            // 5. Shop Image Preview
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shop Image", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            
            float imgWidth = 256f;
            float imgHeight = 226f;
            Rect imageRect = GUILayoutUtility.GetRect(imgWidth, imgHeight, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
            
            Texture displayTex = item.shopImage != null ? item.shopImage : EditorGUIUtility.whiteTexture;
            GUI.DrawTexture(imageRect, displayTex, ScaleMode.ScaleToFit);
            
            if (GUI.Button(imageRect, new GUIContent("", "Click to assign shop image (.png, .jpg)"), GUIStyle.none))
            {
                SelectAndImportShopImage(item);
            }
            
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private void AddGear()
        {
            string xmlPath = EditorUtility.OpenFilePanel("Select Model XML", "", "xml");

            GearItem newItem = new GearItem();
            newItem.gearName = ""; 
            newItem.displayName = ""; // Added
            newItem.price = 0;
            newItem.rawPriceInput = "0";

            if (!string.IsNullOrEmpty(xmlPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(xmlPath);
                newItem.gearName = FormatGearName(fileName);
                newItem.displayName = fileName; // Use the raw filename as the default display name

                EnsureDirectories();
                string targetDir = $"Assets/Projects/{activeProjectName}/models";
                string targetPath = $"{targetDir}/{newItem.gearName}.xml";
                
                File.Copy(xmlPath, targetPath, true);
                AssetDatabase.ImportAsset(targetPath);
                
                newItem.modelXml = AssetDatabase.LoadAssetAtPath<TextAsset>(targetPath);
            }

            gearList.Add(newItem);
            selectedIndex = gearList.Count - 1;
            SaveToXml();
        }

        private void RemoveGear()
        {
            if (selectedIndex < 0 || selectedIndex >= gearList.Count) return;

            GearItem item = gearList[selectedIndex];

            // Delete Model XML
            if (item.modelXml != null)
            {
                string path = AssetDatabase.GetAssetPath(item.modelXml);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            }

            // Delete Shop Image
            if (item.shopImage != null)
            {
                string path = AssetDatabase.GetAssetPath(item.shopImage);
                if (!string.IsNullOrEmpty(path)) AssetDatabase.DeleteAsset(path);
            }

            gearList.RemoveAt(selectedIndex);
            selectedIndex = -1;
            
            SaveToXml();
        }

        private void SelectAndImportShopImage(GearItem item)
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters("Select Shop Image", "", new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" });
            
            if (!string.IsNullOrEmpty(sourcePath))
            {
                EnsureDirectories();
                string targetDir = $"Assets/Projects/{activeProjectName}/icons/shop";
                
                // Construct file name using SHOP_ prefix
                string newImageName = string.IsNullOrEmpty(item.gearName) ? "SHOP_NEW_ITEM" : $"SHOP_{item.gearName}";
                string extension = Path.GetExtension(sourcePath).ToLower();
                string targetPath = $"{targetDir}/{newImageName}{extension}";

                // Delete old image if it exists
                if (item.shopImage != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(item.shopImage));
                }

                File.Copy(sourcePath, targetPath, true);
                AssetDatabase.ImportAsset(targetPath);

                // Configure as Sprite
                TextureImporter importer = AssetImporter.GetAtPath(targetPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }

                item.shopImage = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
                SaveToXml();
            }
        }

        private void RenameGearAssets(GearItem item, string newName)
        {
            // Rename XML
            if (item.modelXml != null)
            {
                string path = AssetDatabase.GetAssetPath(item.modelXml);
                string error = AssetDatabase.RenameAsset(path, newName);
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"Failed to rename Model XML: {error}");
            }

            // Rename Image
            if (item.shopImage != null)
            {
                string path = AssetDatabase.GetAssetPath(item.shopImage);
                string error = AssetDatabase.RenameAsset(path, $"SHOP_{newName}");
                if (!string.IsNullOrEmpty(error)) Debug.LogWarning($"Failed to rename Shop Image: {error}");
            }
        }

        // --- Formatting Utility Methods ---

        private string FormatGearName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            
            string upper = input.ToUpper().Trim();
            string replaced = upper.Replace(" ", "_");
            
            if (!replaced.StartsWith("GEAR_")) 
                replaced = "GEAR_" + replaced;
                
            return replaced;
        }

        private int ParsePrice(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return 0;

            string clean = input.Replace(",", "");
            if (float.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedVal))
            {
                return Mathf.RoundToInt(parsedVal);
            }
            return 0;
        }

        // --- File & XML Management ---

        private void EnsureDirectories()
        {
            CreateFolderRecursive($"Assets/Projects/{activeProjectName}/models");
            CreateFolderRecursive($"Assets/Projects/{activeProjectName}/icons/shop");
            CreateFolderRecursive($"Assets/Projects/{activeProjectName}/commons");
            CreateFolderRecursive($"Assets/Projects/{activeProjectName}/localization");
        }

        private void CreateFolderRecursive(string path)
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                currentPath = nextPath;
            }
        }

        private void SaveToXml()
        {
            EnsureDirectories();
            string relativePath = $"Assets/Projects/{activeProjectName}/commons/Shop_payed.xml";
            
            XDocument doc;
            if (File.Exists(relativePath))
            {
                doc = XDocument.Load(relativePath);
            }
            else
            {
                doc = new XDocument(new XElement("Shop"));
            }

            XElement root = doc.Root;
            if (root == null)
            {
                root = new XElement("Shop");
                doc.Add(root);
            }

            XElement clothingGroup = root.Elements("Group").FirstOrDefault(e => (string)e.Attribute("Name") == "CLOTHING");
            if (clothingGroup == null)
            {
                clothingGroup = new XElement("Group", new XAttribute("Name", "CLOTHING"));
                root.Add(clothingGroup);
            }

            // Clear current items in the CLOTHING group
            clothingGroup.RemoveNodes();

            // Rebuild items from list state
            foreach (GearItem item in gearList)
            {
                string imgName = item.shopImage != null ? item.shopImage.name : (string.IsNullOrEmpty(item.gearName) ? "" : $"SHOP_{item.gearName}");

                XElement itemNode = new XElement("Item",
                    new XAttribute("Price", item.price.ToString()),
                    new XAttribute("Name", item.gearName),
                    new XAttribute("ShopImage", imgName)
                );
                clothingGroup.Add(itemNode);
            }

            doc.Save(relativePath);
            AssetDatabase.ImportAsset(relativePath);

            // --- NEW: Handle Localization Syncing ---
            SaveLocalizationToXml();
        }

        private void SaveLocalizationToXml()
        {
            string relativePath = $"Assets/Projects/{activeProjectName}/localization/localization_all.xml";
            
            XDocument doc;
            if (File.Exists(relativePath))
            {
                doc = XDocument.Load(relativePath);
            }
            else
            {
                doc = new XDocument(new XElement("log"));
            }

            XElement root = doc.Root;
            if (root == null)
            {
                root = new XElement("log");
                doc.Add(root);
            }

            string[] langs = { "eng", "rus", "ger", "ita", "fre", "spa", "tur", "por", "jap", "kor", "chi1", "chi2", "viet", "hin", "arab", "heb", "thai", "pol", "cze", "lat", "dut", "nor", "dan", "finn", "swe", "ukr", "gre" };

            // Clean up items that are no longer in our Gear List (Handles "Removing" gear)
            HashSet<string> currentGearTags = new HashSet<string>(gearList.Select(g => "item_" + g.gearName));
            var itemsToRemove = root.Elements().Where(e => e.Name.LocalName.StartsWith("item_GEAR_") && !currentGearTags.Contains(e.Name.LocalName)).ToList();
            
            foreach (var item in itemsToRemove)
            {
                item.Remove();
            }

            // Add or update current gears
            foreach (GearItem gear in gearList)
            {
                if (string.IsNullOrEmpty(gear.gearName)) continue;

                string tagName = "item_" + gear.gearName;
                XElement itemNode = root.Element(tagName);
                
                if (itemNode == null)
                {
                    itemNode = new XElement(tagName);
                    root.Add(itemNode);
                }

                // Apply all the standard localization attributes with the identical Display Name value
                foreach (string lang in langs)
                {
                    itemNode.SetAttributeValue(lang, gear.displayName);
                }
            }

            doc.Save(relativePath);
            AssetDatabase.ImportAsset(relativePath);
        }

        private void LoadGearsFromXml()
        {
            gearList.Clear();
            string relativePath = $"Assets/Projects/{activeProjectName}/commons/Shop_payed.xml";

            if (!File.Exists(relativePath)) return;

            XDocument doc = XDocument.Load(relativePath);
            XElement root = doc.Root;
            if (root == null) return;

            XElement clothingGroup = root.Elements("Group").FirstOrDefault(e => (string)e.Attribute("Name") == "CLOTHING");
            if (clothingGroup == null) return;

            foreach (XElement node in clothingGroup.Elements("Item"))
            {
                GearItem item = new GearItem();
                
                string parsedPrice = (string)node.Attribute("Price") ?? "0";
                item.price = ParsePrice(parsedPrice);
                item.rawPriceInput = item.price.ToString();
                
                item.gearName = (string)node.Attribute("Name") ?? "";
                string shopImageName = (string)node.Attribute("ShopImage") ?? "";

                // Attempt to link local assets based on standard naming
                if (!string.IsNullOrEmpty(item.gearName))
                {
                    string[] xmlGuids = AssetDatabase.FindAssets(item.gearName + " t:TextAsset", new[] { $"Assets/Projects/{activeProjectName}/models" });
                    if (xmlGuids.Length > 0)
                    {
                        item.modelXml = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(xmlGuids[0]));
                    }
                }

                if (!string.IsNullOrEmpty(shopImageName))
                {
                    string[] imgGuids = AssetDatabase.FindAssets(shopImageName + " t:Texture2D", new[] { $"Assets/Projects/{activeProjectName}/icons/shop" });
                    if (imgGuids.Length > 0)
                    {
                        item.shopImage = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(imgGuids[0]));
                    }
                }

                gearList.Add(item);
            }

            // --- NEW: Load corresponding display names ---
            LoadLocalizationFromXml();
        }

        private void LoadLocalizationFromXml()
        {
            string relativePath = $"Assets/Projects/{activeProjectName}/localization/localization_all.xml";
            if (!File.Exists(relativePath)) return;

            XDocument doc = XDocument.Load(relativePath);
            XElement root = doc.Root;
            if (root == null) return;

            foreach (GearItem gear in gearList)
            {
                if (string.IsNullOrEmpty(gear.gearName)) continue;
                
                string tagName = "item_" + gear.gearName;
                XElement itemNode = root.Element(tagName);
                if (itemNode != null)
                {
                    // We just grab the 'eng' tag since they're all mirrored
                    XAttribute engAttr = itemNode.Attribute("eng"); 
                    if (engAttr != null)
                    {
                        gear.displayName = engAttr.Value;
                    }
                }
            }
        }

        // Helper to generate solid color texture for selection highlighting
        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}