using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Globalization;

namespace Vectorier.ProjectManager
{
    public class ProjectManagerTricks : EditorWindow
    {
        private class TrickItem
        {
            public string trickName = "";
            public string displayName = "";
            public string rawPriceInput = "0";
            public int price = 0;
            public Texture2D shopImage;
            public Texture2D trackImage;
        }

        private string activeProjectName = "";
        private List<TrickItem> trickList = new List<TrickItem>();
        private string searchQuery = "";
        private int selectedIndex = -1;
        private Vector2 scrollPosition;

        // Opens this window and accepts the project name
        public static void ShowWindow(string projectName)
        {
            ProjectManagerTricks window = GetWindow<ProjectManagerTricks>("Tricks");
            window.activeProjectName = projectName;
            window.minSize = new Vector2(400, 600);
            window.Show();
            window.Init();
        }

        private void Init()
        {
            LoadTricksFromXml();
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                GUI.FocusControl(null);
                Repaint();
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Tricks", EditorStyles.largeLabel);
            GUILayout.FlexibleSpace();

            GUIStyle countStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };
            GUILayout.Label($"Total Tricks - {trickList.Count}", countStyle, GUILayout.Height(30));
            GUILayout.Space(10);

            if (GUILayout.Button("+", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                AddTrick();
            }

            GUI.enabled = selectedIndex >= 0 && selectedIndex < trickList.Count;
            if (GUILayout.Button("-", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                RemoveTrick();
            }
            GUI.enabled = true;

            GUI.enabled = trickList.Count > 0;
            if (GUILayout.Button("C", GUILayout.Width(30), GUILayout.Height(30)))
            {
                GUI.FocusControl(null);
                ClearAllTricks();
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search:", GUILayout.Width(50));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            if (trickList.Count == 0)
            {
                GUILayout.Label("No tricks added yet. Click '+' to create one.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                List<int> visibleIndices = new List<int>();
                for (int i = 0; i < trickList.Count; i++)
                {
                    bool matchName = trickList[i].trickName != null && trickList[i].trickName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;
                    bool matchDisplay = trickList[i].displayName != null && trickList[i].displayName.IndexOf(searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0;

                    if (string.IsNullOrEmpty(searchQuery) || matchName || matchDisplay)
                    {
                        visibleIndices.Add(i);
                    }
                }

                if (visibleIndices.Count == 0)
                {
                    GUILayout.Label("No tricks match the search.", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    for (int i = 0; i < visibleIndices.Count; i++)
                    {
                        DrawTrickItem(visibleIndices[i]);
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

        private void ClearAllTricks()
        {
            if (EditorUtility.DisplayDialog("Clear All Tricks", "Are you sure you want to delete all tricks?", "Yes", "No"))
            {
                foreach (TrickItem item in trickList)
                {
                    // Delete Shop Image
                    if (item.shopImage != null && !string.IsNullOrEmpty(item.shopImage.name))
                    {
                        DeleteImageFiles($"./Projects/{activeProjectName}/icons/shop", item.shopImage.name);
                    }

                    // Delete Track Image
                    if (item.trackImage != null && !string.IsNullOrEmpty(item.trackImage.name))
                    {
                        DeleteImageFiles($"./Projects/{activeProjectName}/icons/tricks", item.trackImage.name);
                    }
                }

                trickList.Clear();
                selectedIndex = -1;
                SaveToXml();
            }
        }

        private void DrawTrickItem(int index)
        {
            TrickItem item = trickList[index];
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

            // Trick Name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.DelayedTextField("Trick Name", item.trickName);
            if (EditorGUI.EndChangeCheck())
            {
                string formatted = FormatTrickName(newName);
                if (formatted != item.trickName)
                {
                    RenameTrickImages(item, formatted);
                    item.trickName = formatted;
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
                item.rawPriceInput = item.price.ToString(); // Visually update field back to int
                SaveToXml();
            }

            GUILayout.Space(10);

            // --- Shared Text Style for Empty Thumbnails ---
            GUIStyle centeredTextStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true, // wordWrap so it fits inside the smaller Track Image box
                normal = { textColor = Color.black } // Dark text so it shows up well on the white texture
            };

            // Shop Image Preview (256x226)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Shop Image", GUILayout.Width(EditorGUIUtility.labelWidth - 5));

            float shopImgWidth = 256f;
            float shopImgHeight = 226f;
            Rect shopImageRect = GUILayoutUtility.GetRect(shopImgWidth, shopImgHeight, GUILayout.Width(shopImgWidth), GUILayout.Height(shopImgHeight));

            Texture shopDisplayTex = item.shopImage != null ? item.shopImage : EditorGUIUtility.whiteTexture;
            GUI.DrawTexture(shopImageRect, shopDisplayTex, ScaleMode.ScaleToFit);

            // --- ADDED TEXT OVERLAY ---
            if (item.shopImage == null)
            {
                GUI.Label(shopImageRect, "Click Here to Change Thumbnail", centeredTextStyle);
            }

            if (GUI.Button(shopImageRect, new GUIContent("", "Click to assign shop image (256x226)"), GUIStyle.none))
            {
                SelectAndImportImage(item, true);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Track Image Preview (117x117)
            GUILayout.BeginHorizontal();
            GUILayout.Label("Track Image", GUILayout.Width(EditorGUIUtility.labelWidth - 5));

            float trackImgWidth = 117f;
            float trackImgHeight = 117f;
            Rect trackImageRect = GUILayoutUtility.GetRect(trackImgWidth, trackImgHeight, GUILayout.Width(trackImgWidth), GUILayout.Height(trackImgHeight));

            Texture trackDisplayTex = item.trackImage != null ? item.trackImage : EditorGUIUtility.whiteTexture;
            GUI.DrawTexture(trackImageRect, trackDisplayTex, ScaleMode.ScaleToFit);

            // --- TEXT OVERLAY ---
            if (item.trackImage == null)
            {
                GUI.Label(trackImageRect, "Click Here to Change Thumbnail", centeredTextStyle);
            }

            if (GUI.Button(trackImageRect, new GUIContent("", "Click to assign track image (117x117)"), GUIStyle.none))
            {
                SelectAndImportImage(item, false);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);
            GUILayout.EndVertical();
        }

        private void AddTrick()
        {
            TrickItem newItem = new TrickItem();
            newItem.trickName = "";
            newItem.displayName = "";
            newItem.price = 0;
            newItem.rawPriceInput = "0";

            trickList.Add(newItem);
            selectedIndex = trickList.Count - 1;
            SaveToXml();
        }

        private void RemoveTrick()
        {
            if (selectedIndex < 0 || selectedIndex >= trickList.Count) return;

            TrickItem item = trickList[selectedIndex];

            // Delete Shop Image
            if (item.shopImage != null && !string.IsNullOrEmpty(item.shopImage.name))
            {
                DeleteImageFiles($"./Projects/{activeProjectName}/icons/shop", item.shopImage.name);
            }

            // Delete Track Image
            if (item.trackImage != null && !string.IsNullOrEmpty(item.trackImage.name))
            {
                DeleteImageFiles($"./Projects/{activeProjectName}/icons/tricks", item.trackImage.name);
            }

            trickList.RemoveAt(selectedIndex);
            selectedIndex = -1;

            SaveToXml();
        }

        private void SelectAndImportImage(TrickItem item, bool isShopImage)
        {
            string sourcePath = EditorUtility.OpenFilePanelWithFilters(
                isShopImage ? "Select Shop Image (256x226)" : "Select Track Image (117x117)",
                "", new string[] { "Image Files", "png,jpg,jpeg", "All files", "*" });

            if (!string.IsNullOrEmpty(sourcePath))
            {
                EnsureDirectories();

                string targetDir;
                string newImageName;

                if (isShopImage)
                {
                    targetDir = $"./Projects/{activeProjectName}/icons/shop";
                    newImageName = string.IsNullOrEmpty(item.trickName) ? "SHOP_NEW_TRICK" : $"SHOP_{item.trickName}";
                }
                else
                {
                    targetDir = $"./Projects/{activeProjectName}/icons/tricks";
                    newImageName = string.IsNullOrEmpty(item.trickName) ? "TRACK_NEW_TRICK" : $"TRACK_{item.trickName}";
                }

                string extension = Path.GetExtension(sourcePath).ToLower();
                string targetPath = $"{targetDir}/{newImageName}{extension}";

                // Delete old image if it exists
                Texture2D oldTexture = isShopImage ? item.shopImage : item.trackImage;
                if (oldTexture != null && !string.IsNullOrEmpty(oldTexture.name))
                {
                    DeleteImageFiles(targetDir, oldTexture.name);
                }

                File.Copy(sourcePath, targetPath, true);

                // Load Texture outside of Unity's Asset Database
                Texture2D loadedTex = new Texture2D(2, 2);
                loadedTex.LoadImage(File.ReadAllBytes(targetPath));
                loadedTex.name = newImageName; // Assign name for XML saving logic

                if (isShopImage)
                    item.shopImage = loadedTex;
                else
                    item.trackImage = loadedTex;

                SaveToXml();
            }
        }

        private void RenameTrickImages(TrickItem item, string newName)
        {
            // Rename Shop Image
            if (item.shopImage != null && !string.IsNullOrEmpty(item.shopImage.name))
            {
                string dir = $"./Projects/{activeProjectName}/icons/shop";
                RenameImageFile(dir, item.shopImage.name, $"SHOP_{newName}");
                item.shopImage.name = $"SHOP_{newName}";
            }

            // Rename Track Image
            if (item.trackImage != null && !string.IsNullOrEmpty(item.trackImage.name))
            {
                string dir = $"./Projects/{activeProjectName}/icons/tricks";
                RenameImageFile(dir, item.trackImage.name, $"TRACK_{newName}");
                item.trackImage.name = $"TRACK_{newName}";
            }
        }

        // --- Formatting Utility Methods ---

        private string FormatTrickName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            string upper = input.ToUpper().Trim();
            string replaced = upper.Replace(" ", "_");

            if (!replaced.StartsWith("TRICK_"))
                replaced = "TRICK_" + replaced;

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
            Directory.CreateDirectory($"./Projects/{activeProjectName}/icons/shop");
            Directory.CreateDirectory($"./Projects/{activeProjectName}/icons/tricks");
            Directory.CreateDirectory($"./Projects/{activeProjectName}/commons");
            Directory.CreateDirectory($"./Projects/{activeProjectName}/localization");
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

            XElement trickGroup = root.Elements("Group").FirstOrDefault(e => (string)e.Attribute("Name") == "TRICK");
            if (trickGroup == null)
            {
                trickGroup = new XElement("Group", new XAttribute("Name", "TRICK"));
                root.Add(trickGroup);
            }

            trickGroup.RemoveNodes();

            foreach (TrickItem item in trickList)
            {
                string shopImgName = item.shopImage != null ? item.shopImage.name : (string.IsNullOrEmpty(item.trickName) ? "" : $"SHOP_{item.trickName}");
                string trackImgName = item.trackImage != null ? item.trackImage.name : (string.IsNullOrEmpty(item.trickName) ? "" : $"TRACK_{item.trickName}");

                XElement itemNode = new XElement("Item",
                    new XAttribute("Price", item.price.ToString()),
                    new XAttribute("Name", item.trickName),
                    new XAttribute("ShopImage", shopImgName),
                    new XAttribute("TrackImage", trackImgName)
                );

                trickGroup.Add(itemNode);
            }

            doc.Save(relativePath);
            
            // Handle Localization Syncing
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

            HashSet<string> currentTrickTags = new HashSet<string>(trickList.Select(t => "item_" + t.trickName));
            var itemsToRemove = root.Elements().Where(e => e.Name.LocalName.StartsWith("item_TRICK_") && !currentTrickTags.Contains(e.Name.LocalName)).ToList();
            
            foreach (var item in itemsToRemove)
            {
                item.Remove();
            }

            foreach (TrickItem trick in trickList)
            {
                if (string.IsNullOrEmpty(trick.trickName)) continue;

                string tagName = "item_" + trick.trickName;
                XElement itemNode = root.Element(tagName);
                
                if (itemNode == null)
                {
                    itemNode = new XElement(tagName);
                    root.Add(itemNode);
                }

                foreach (string lang in langs)
                {
                    itemNode.SetAttributeValue(lang, trick.displayName);
                }
            }

            doc.Save(relativePath);
        }

        private void LoadTricksFromXml()
        {
            trickList.Clear();
            string relativePath = $"./Projects/{activeProjectName}/commons/Shop_payed.xml";

            if (!File.Exists(relativePath)) return;

            XDocument doc = XDocument.Load(relativePath);
            XElement root = doc.Root;
            if (root == null) return;

            XElement trickGroup = root.Elements("Group").FirstOrDefault(e => (string)e.Attribute("Name") == "TRICK");
            if (trickGroup == null) return;

            foreach (XElement node in trickGroup.Elements("Item"))
            {
                TrickItem item = new TrickItem();

                string parsedPrice = (string)node.Attribute("Price") ?? "0";
                item.price = ParsePrice(parsedPrice);
                item.rawPriceInput = item.price.ToString();

                item.trickName = (string)node.Attribute("Name") ?? "";
                string shopImageName = (string)node.Attribute("ShopImage") ?? "";
                string trackImageName = (string)node.Attribute("TrackImage") ?? "";

                // Link Images dynamically using System.IO
                if (!string.IsNullOrEmpty(shopImageName))
                {
                    Texture2D tex = LoadTextureFromFile($"./Projects/{activeProjectName}/icons/shop", shopImageName);
                    if (tex != null) item.shopImage = tex;
                }

                if (!string.IsNullOrEmpty(trackImageName))
                {
                    Texture2D tex = LoadTextureFromFile($"./Projects/{activeProjectName}/icons/tricks", trackImageName);
                    if (tex != null) item.trackImage = tex;
                }

                trickList.Add(item);
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

            foreach (TrickItem trick in trickList)
            {
                if (string.IsNullOrEmpty(trick.trickName)) continue;
                
                string tagName = "item_" + trick.trickName;
                XElement itemNode = root.Element(tagName);
                if (itemNode != null)
                {
                    XAttribute engAttr = itemNode.Attribute("eng"); 
                    if (engAttr != null)
                    {
                        trick.displayName = engAttr.Value;
                    }
                }
            }
        }

        private Texture2D LoadTextureFromFile(string directory, string fileNameWithoutExt)
        {
            if (!Directory.Exists(directory)) return null;

            string[] files = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*");
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(File.ReadAllBytes(file));
                    tex.name = fileNameWithoutExt; // Reassign the name so XML saving retains it
                    return tex;
                }
            }
            return null;
        }

        private void RenameImageFile(string directory, string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || !Directory.Exists(directory)) return;

            string[] files = Directory.GetFiles(directory, $"{oldName}.*");
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file);
                if (ext.ToLower() == ".png" || ext.ToLower() == ".jpg" || ext.ToLower() == ".jpeg")
                {
                    string newPath = Path.Combine(directory, $"{newName}{ext}");
                    File.Move(file, newPath);
                }
            }
        }

        private void DeleteImageFiles(string directory, string fileNameWithoutExt)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExt) || !Directory.Exists(directory)) return;

            string[] files = Directory.GetFiles(directory, $"{fileNameWithoutExt}.*");
            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    File.Delete(file);
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