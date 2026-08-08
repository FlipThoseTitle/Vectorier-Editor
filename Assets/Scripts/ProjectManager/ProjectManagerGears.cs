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
            public string modelXmlPath = ""; 
            public string gearName = "";
            public string displayName = "";
            public string rawPriceInput = "0";
            public int price = 0;
            public string shopImagePath = ""; 
            public Texture2D shopImage;
        }

        private string activeProjectName = "";
        private List<GearItem> gearList = new List<GearItem>();
        private string searchQuery = "";
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

            // --- Total Count ---
            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label($"Total Gears - {gearList.Count}", countStyle, GUILayout.Height(30));
            GUILayout.Space(10);
            
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

            // Clear
            GUI.enabled = gearList.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                ClearAllGears();
            }
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
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
                List<int> visibleIndices = new List<int>();
                for (int i = 0; i < gearList.Count; i++)
                {
                    bool matchName = gearList[i].gearName != null && gearList[i].gearName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchDisplay = gearList[i].displayName != null && gearList[i].displayName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    
                    if (string.IsNullOrEmpty(searchQuery) || matchName || matchDisplay)
                    {
                        visibleIndices.Add(i);
                    }
                }

                if (visibleIndices.Count == 0)
                {
                    GUILayout.Label("No gears match the search.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    for (int i = 0; i < visibleIndices.Count; i++)
                    {
                        DrawGearItem(visibleIndices[i]);
                    }
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
                    if (!string.IsNullOrEmpty(item.modelXmlPath) && File.Exists(item.modelXmlPath))
                    {
                        File.Delete(item.modelXmlPath);
                    }
                    if (!string.IsNullOrEmpty(item.shopImagePath) && File.Exists(item.shopImagePath))
                    {
                        File.Delete(item.shopImagePath);
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

            // Model XML Pointer
            GUILayout.BeginHorizontal();
            GUILayout.Label("Model XML", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            GUI.enabled = false;
            GUILayout.TextField(string.IsNullOrEmpty(item.modelXmlPath) ? "None" : Path.GetFileName(item.modelXmlPath));
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            // Gear Name
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

            // Display Name / Localization
            EditorGUI.BeginChangeCheck();
            string newDisplayName = EditorGUILayout.DelayedTextField("Display Name", item.displayName);
            if (EditorGUI.EndChangeCheck())
            {
                item.displayName = newDisplayName;
                SaveToXml(); // Save to sync the localization file
            }

            // Price
            EditorGUI.BeginChangeCheck();
            item.rawPriceInput = EditorGUILayout.DelayedTextField("Price", item.rawPriceInput);
            if (EditorGUI.EndChangeCheck())
            {
                item.price = ParsePrice(item.rawPriceInput);
                item.rawPriceInput = item.price.ToString(); // update field back to int
                SaveToXml();
            }

            // Shop Image Preview
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shop Image", GUILayout.Width(EditorGUIUtility.labelWidth - 5));
            
            float imgWidth = 256f;
            float imgHeight = 226f;
            Rect imageRect = GUILayoutUtility.GetRect(imgWidth, imgHeight, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
            
            Texture displayTex = item.shopImage != null ? item.shopImage : EditorGUIUtility.whiteTexture;
            GUI.DrawTexture(imageRect, displayTex, ScaleMode.ScaleToFit);
            
            if (item.shopImage == null)
            {
                GUIStyle centeredTextStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.black } // Dark text so it shows up well on the white texture
                };
                
                GUI.Label(imageRect, "Click Here to Change Thumbnail", centeredTextStyle);
            }
            
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
            newItem.displayName = "";
            newItem.price = 0;
            newItem.rawPriceInput = "0";

            if (!string.IsNullOrEmpty(xmlPath))
            {
                string fileName = Path.GetFileNameWithoutExtension(xmlPath);
                newItem.gearName = FormatGearName(fileName);
                newItem.displayName = fileName;

                EnsureDirectories();
                string targetDir = $"./Projects/{activeProjectName}/models";
                string targetPath = $"{targetDir}/{newItem.gearName}.xml";
                
                File.Copy(xmlPath, targetPath, true);
                
                newItem.modelXmlPath = targetPath;
            }

            gearList.Add(newItem);
            selectedIndex = gearList.Count - 1;
            SaveToXml();
        }

        private void RemoveGear()
        {
            if (selectedIndex < 0 || selectedIndex >= gearList.Count) return;

            GearItem item = gearList[selectedIndex];

            if (!string.IsNullOrEmpty(item.modelXmlPath) && File.Exists(item.modelXmlPath))
            {
                File.Delete(item.modelXmlPath);
            }
            if (!string.IsNullOrEmpty(item.shopImagePath) && File.Exists(item.shopImagePath))
            {
                File.Delete(item.shopImagePath);
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
                string targetDir = $"./Projects/{activeProjectName}/icons/shop";
                
                string newImageName = string.IsNullOrEmpty(item.gearName) ? "SHOP_NEW_ITEM" : $"SHOP_{item.gearName}";
                string extension = Path.GetExtension(sourcePath).ToLower();
                string targetPath = $"{targetDir}/{newImageName}{extension}";

                // Delete old image if it exists using File.Delete
                if (!string.IsNullOrEmpty(item.shopImagePath) && File.Exists(item.shopImagePath))
                {
                    File.Delete(item.shopImagePath);
                }

                File.Copy(sourcePath, targetPath, true);
                
                // Manually load image into Texture2D for previewing
                item.shopImagePath = targetPath;
                item.shopImage = new Texture2D(2, 2);
                item.shopImage.LoadImage(File.ReadAllBytes(targetPath));

                SaveToXml();
            }
        }

        private void RenameGearAssets(GearItem item, string newName)
        {
            if (!string.IsNullOrEmpty(item.modelXmlPath) && File.Exists(item.modelXmlPath))
            {
                string dir = Path.GetDirectoryName(item.modelXmlPath);
                string ext = Path.GetExtension(item.modelXmlPath);
                string newPath = Path.Combine(dir, newName + ext).Replace("\\", "/");
                
                if (item.modelXmlPath != newPath)
                {
                    File.Move(item.modelXmlPath, newPath);
                    item.modelXmlPath = newPath;
                }
            }

            if (!string.IsNullOrEmpty(item.shopImagePath) && File.Exists(item.shopImagePath))
            {
                string dir = Path.GetDirectoryName(item.shopImagePath);
                string ext = Path.GetExtension(item.shopImagePath);
                string newPath = Path.Combine(dir, $"SHOP_{newName}{ext}").Replace("\\", "/");
                
                if (item.shopImagePath != newPath)
                {
                    File.Move(item.shopImagePath, newPath);
                    item.shopImagePath = newPath;
                }
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
            CreateFolderRecursive($"./Projects/{activeProjectName}/models");
            CreateFolderRecursive($"./Projects/{activeProjectName}/icons/shop");
            CreateFolderRecursive($"./Projects/{activeProjectName}/commons");
            CreateFolderRecursive($"./Projects/{activeProjectName}/localization");
        }

        private void CreateFolderRecursive(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private void SaveToXml()
        {
            EnsureDirectories();
            string relativePath = $"./Projects/{activeProjectName}/commons/Shop_payed.xml";
            
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

            clothingGroup.RemoveNodes();

            // Rebuild items from list state
            foreach (GearItem item in gearList)
            {
                // Extract the raw file name
                string imgName = !string.IsNullOrEmpty(item.shopImagePath) 
                    ? Path.GetFileNameWithoutExtension(item.shopImagePath) 
                    : (string.IsNullOrEmpty(item.gearName) ? "" : $"SHOP_{item.gearName}");

                XElement itemNode = new XElement("Item",
                    new XAttribute("Price", item.price.ToString()),
                    new XAttribute("Name", item.gearName),
                    new XAttribute("ShopImage", imgName)
                );
                clothingGroup.Add(itemNode);
            }

            doc.Save(relativePath);

            SaveLocalizationToXml();
        }

        private void SaveLocalizationToXml()
        {
            string relativePath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
            
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

            HashSet<string> currentGearTags = new HashSet<string>(gearList.Select(g => "item_" + g.gearName));
            var itemsToRemove = root.Elements().Where(e => e.Name.LocalName.StartsWith("item_GEAR_") && !currentGearTags.Contains(e.Name.LocalName)).ToList();
            
            foreach (var item in itemsToRemove)
            {
                item.Remove();
            }

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

                foreach (string lang in langs)
                {
                    itemNode.SetAttributeValue(lang, gear.displayName);
                }
            }

            doc.Save(relativePath);
        }

        private void LoadGearsFromXml()
        {
            gearList.Clear();
            string relativePath = $"./Projects/{activeProjectName}/commons/Shop_payed.xml";

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

                if (!string.IsNullOrEmpty(item.gearName))
                {
                    string modelsDir = $"./Projects/{activeProjectName}/models";
                    if (Directory.Exists(modelsDir))
                    {
                        string[] xmlFiles = Directory.GetFiles(modelsDir, $"{item.gearName}.xml", SearchOption.TopDirectoryOnly);
                        if (xmlFiles.Length > 0)
                        {
                            item.modelXmlPath = xmlFiles[0].Replace("\\", "/");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(shopImageName))
                {
                    string shopDir = $"./Projects/{activeProjectName}/icons/shop";
                    if (Directory.Exists(shopDir))
                    {
                        string[] possibleExts = { ".png", ".jpg", ".jpeg" };
                        foreach (string ext in possibleExts)
                        {
                            string imgPath = Path.Combine(shopDir, shopImageName + ext).Replace("\\", "/");
                            if (File.Exists(imgPath))
                            {
                                item.shopImagePath = imgPath;
                                item.shopImage = new Texture2D(2, 2);
                                item.shopImage.LoadImage(File.ReadAllBytes(imgPath));
                                break;
                            }
                        }
                    }
                }

                gearList.Add(item);
            }

            LoadLocalizationFromXml();
        }

        private void LoadLocalizationFromXml()
        {
            string relativePath = $"./Projects/{activeProjectName}/localization/localization_all.xml";
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